using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.ComboLog;
using Rollgeon.Combat.Weakness;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Le reasigna la debilidad al propio boss al combo que el jugador <b>más viene usando</b>,
    /// leyendo el <see cref="IComboLogService"/> y escribiendo en <see cref="IWeaknessRegistry"/>.
    /// Fase 2 de La Generala: "se cambia la debilidad — adopta el combo que más venís usando".
    /// </summary>
    /// <remarks>
    /// <para>
    /// Empates: gana el más reciente (el log viene del más nuevo al más viejo). El marcador de
    /// "sin combo" del log se ignora — el daño mínimo no es una mano que el jugador esté eligiendo.
    /// </para>
    /// <para>
    /// Pensado para ir dentro de <c>If(PcOwnerHpBelow) → Once(...)</c>: es un cambio de una sola vez.
    /// Devuelve <see cref="AIResult.Succeeded"/> también cuando el log está vacío (nada que adoptar,
    /// pero tampoco es un fallo que deba abortar el turno) salvo que
    /// <see cref="FailWhenLogEmpty"/> esté en true.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_AdoptWeakness : AIActionNode
    {
        [Tooltip("Cuántas entradas del historial de combos del jugador se miran para elegir la más frecuente.")]
        [MinValue(1)]
        public int LogWindow = 8;

        [Tooltip("Multiplicador de weakness que queda registrado. 0 = usar el default global del RulesetSO.")]
        [MinValue(0f)]
        public float MultiplierOverride = 1.5f;

        [Tooltip("Si true, no poder elegir combo (log vacío) devuelve Failed en vez de Succeeded. " +
                 "Dejalo en false si el nodo va suelto dentro de un Sequence.")]
        public bool FailWhenLogEmpty = false;

        public override string NodeName => $"Adopt Weakness (player's most used, x{MultiplierOverride})";

        public override AIResult Tick(AIContext context)
        {
            if (context == null || context.SelfGuid == Guid.Empty) return AIResult.Failed;

            if (!ServiceLocator.TryGetService<IComboLogService>(out var log) || log == null)
            {
                Debug.LogWarning("[AINode_AdoptWeakness] IComboLogService no registrado — " +
                                 "la debilidad queda como estaba.");
                return FailWhenLogEmpty ? AIResult.Failed : AIResult.Succeeded;
            }

            if (!ServiceLocator.TryGetService<IWeaknessRegistry>(out var weakness) || weakness == null)
            {
                Debug.LogWarning("[AINode_AdoptWeakness] IWeaknessRegistry no registrado — " +
                                 "no se puede reasignar la debilidad.");
                return FailWhenLogEmpty ? AIResult.Failed : AIResult.Succeeded;
            }

            var picked = MostFrequent(log.Last(LogWindow), log.NoComboMarker);
            if (string.IsNullOrEmpty(picked))
                return FailWhenLogEmpty ? AIResult.Failed : AIResult.Succeeded;

            weakness.SetWeakness(context.SelfGuid, picked, MultiplierOverride);
            return AIResult.Succeeded;
        }

        /// <summary>
        /// Combo más repetido de <paramref name="history"/> (índice 0 = más reciente), ignorando
        /// <paramref name="noComboMarker"/>. Empate ⇒ el más reciente. <c>null</c> si no hay nada.
        /// </summary>
        internal static string MostFrequent(IReadOnlyList<string> history, string noComboMarker)
        {
            if (history == null || history.Count == 0) return null;

            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            var firstSeen = new Dictionary<string, int>(StringComparer.Ordinal);

            for (int i = 0; i < history.Count; i++)
            {
                var id = history[i];
                if (string.IsNullOrEmpty(id)) continue;
                if (!string.IsNullOrEmpty(noComboMarker) &&
                    string.Equals(id, noComboMarker, StringComparison.Ordinal)) continue;

                counts.TryGetValue(id, out int count);
                counts[id] = count + 1;
                if (!firstSeen.ContainsKey(id)) firstSeen[id] = i;
            }

            string best = null;
            int bestCount = 0;
            int bestIndex = int.MaxValue;
            foreach (var pair in counts)
            {
                int index = firstSeen[pair.Key];
                bool wins = pair.Value > bestCount || (pair.Value == bestCount && index < bestIndex);
                if (!wins) continue;

                best = pair.Key;
                bestCount = pair.Value;
                bestIndex = index;
            }

            return best;
        }
    }
}
