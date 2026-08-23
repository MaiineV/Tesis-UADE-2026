using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.ComboLog;
using Rollgeon.Combat.ContractMod;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// <see cref="IContractModifierService"/> no tiene expiración por modificador (sólo
    /// <c>ClearAll</c>), así que "dura 1 turno" se implementa limpiando todo al empezar el turno y
    /// volviendo a promulgar; en fase 2 no limpia y los corrimientos se acumulan.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_ShiftComboToNeighbor : AIActionNode
    {
        public enum ShiftDirection
        {
            /// <summary>Al inmediatamente superior por daño base: el combo pobre paga más.</summary>
            Up = 0,

            /// <summary>Al inmediatamente inferior: la Escalera paga como Doble Par.</summary>
            Down = 1,

            RandomNeighbor = 2,
        }

        [Tooltip("Hacia qué vecino corre el combo. RandomNeighbor sortea por corrimiento — es lo " +
                 "que hace que haya corrimientos aprovechables y no solo castigos.")]
        public ShiftDirection Direction = ShiftDirection.RandomNeighbor;

        [Tooltip("Cuántos ataques atrás mira para decidir 'el más jugado'. Empates: gana el más reciente.")]
        [MinValue(1)]
        public int ComboLogWindow = 5;

        [Tooltip("Corrimientos por turno en fase 1.")]
        [MinValue(1)]
        public int ShiftsPerTurnPhase1 = 1;

        [Tooltip("Corrimientos por turno en fase 2 ('muestra la manga').")]
        [MinValue(1)]
        public int ShiftsPerTurnPhase2 = 2;

        [Tooltip("Ratio de HP (0..1) al que entra en fase 2. 0 = sin fase 2.")]
        [Range(0f, 1f)]
        public float Phase2HpThreshold = 0.35f;

        [Tooltip("Fase 1: limpia los corrimientos anteriores antes de promulgar (dura 1 turno).")]
        public bool RevertPreviousShifts = true;

        [Tooltip("Fase 2: NO limpia — los corrimientos se acumulan hasta el final del combate.")]
        public bool Phase2ShiftsArePermanent = true;

        [Tooltip("Combos que el corrimiento nunca toca. Generala es la debilidad del jefe: la " +
                 "única mano que no depende de la tabla.")]
        public List<string> ImmuneComboIds = new List<string> { "combo.generala" };

        public override string NodeName => "Shift Combo To Neighbor (Anotador)";

        public override AIResult Tick(AIContext context)
        {
            if (context == null) return AIResult.Failed;

            if (!ServiceLocator.TryGetService<IContractModifierService>(out var mods) || mods == null)
            {
                Debug.LogError("[AINode_ShiftComboToNeighbor] IContractModifierService no registrado. " +
                               "Agregá ContractModifierServiceBootstrap a ServiceBootstrap.ExtraServices.");
                return AIResult.Failed;
            }

            bool phase2 = IsInPhase2(context);
            bool permanent = phase2 && Phase2ShiftsArePermanent;
            if (!permanent && RevertPreviousShifts) mods.ClearAll();

            int shifts = phase2 ? ShiftsPerTurnPhase2 : ShiftsPerTurnPhase1;
            var targets = PickMostPlayed(shifts);

            // Log vacío (el jugador todavía no atacó, o solo saca daño mínimo): nada que corregir.
            // Succeeded igual — un Failed acá abortaría el turno del boss.
            if (targets.Count == 0) return AIResult.Succeeded;

            foreach (var comboId in targets)
                mods.SetComboToNeighbor(comboId, ResolveDirection(context));

            return AIResult.Succeeded;
        }

        /// <summary>Del más frecuente al menos; empate por frecuencia ⇒ gana el más reciente.</summary>
        private List<string> PickMostPlayed(int count)
        {
            var result = new List<string>();
            if (count < 1) return result;
            if (!ServiceLocator.TryGetService<IComboLogService>(out var log) || log == null) return result;

            int window = ComboLogWindow < 1 ? 1 : ComboLogWindow;
            var recent = log.Last(window);
            if (recent == null || recent.Count == 0) return result;

            var order = new List<string>();
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var comboId in recent)
            {
                if (string.IsNullOrEmpty(comboId)) continue;
                if (string.Equals(comboId, log.NoComboMarker, StringComparison.Ordinal)) continue;
                if (IsImmune(comboId)) continue;

                if (counts.TryGetValue(comboId, out var seen)) counts[comboId] = seen + 1;
                else { counts[comboId] = 1; order.Add(comboId); }
            }

            for (int picked = 0; picked < count; picked++)
            {
                string best = null;
                int bestCount = 0;
                foreach (var comboId in order)
                {
                    if (result.Contains(comboId)) continue;
                    if (counts[comboId] > bestCount)
                    {
                        bestCount = counts[comboId];
                        best = comboId;
                    }
                }

                if (best == null) break;
                result.Add(best);
            }

            return result;
        }

        private bool IsImmune(string comboId)
        {
            if (ImmuneComboIds == null) return false;
            foreach (var immune in ImmuneComboIds)
            {
                if (string.Equals(immune, comboId, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private int ResolveDirection(AIContext context)
        {
            switch (Direction)
            {
                case ShiftDirection.Up: return +1;
                case ShiftDirection.Down: return -1;
                default: return NextInt(context, 2) == 0 ? +1 : -1;
            }
        }

        private bool IsInPhase2(AIContext context)
        {
            if (Phase2HpThreshold <= 0f) return false;

            var attrs = context.Attributes;
            if (attrs == null) ServiceLocator.TryGetService<AttributesManager>(out attrs);
            if (attrs == null || context.SelfMaxHp <= 0) return false;

            var hp = attrs.GetAttribute<Health>(context.SelfGuid);
            if (hp == null) return false;
            return (float)hp.ModifiedValue / context.SelfMaxHp <= Phase2HpThreshold;
        }

        private static int NextInt(AIContext context, int exclusiveUpperBound)
        {
            if (exclusiveUpperBound <= 1) return 0;
            return context.Rng != null
                ? context.Rng.Next(exclusiveUpperBound)
                : UnityEngine.Random.Range(0, exclusiveUpperBound);
        }
    }
}
