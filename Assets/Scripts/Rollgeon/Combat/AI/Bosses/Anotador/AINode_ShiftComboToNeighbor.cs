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
    /// "Tacha" del Anotador (piso 2): corre el combo que el jugador más viene usando al vecino de la
    /// hoja, así su Escalera paga como Doble Par (o al revés). Efecto de inicio de turno — va como
    /// hijo del Sequence raíz, no consume la acción del boss.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Elige el más jugado, no uno al azar.</b> Lee <see cref="IComboLogService"/> en una ventana
    /// de <see cref="ComboLogWindow"/> ataques y corre el combo más frecuente. Es lo que hace que la
    /// pelea castigue la memoria: el jugador que tiene la tabla aprendida es el que la usa siempre
    /// igual, y eso es exactamente lo que el Anotador corrompe.
    /// </para>
    /// <para>
    /// <b>Fases sin ramificar el árbol</b> (mismo criterio que <see cref="AINode_PromulgateRule"/>,
    /// que resuelve su intervalo leyendo su propia vida). Bajo
    /// <see cref="Phase2HpThreshold"/> pasa a <see cref="ShiftsPerTurnPhase2"/> corrimientos por
    /// turno y deja de devolverlos (<see cref="Phase2ShiftsArePermanent"/>): se acumulan hasta el
    /// final del combate. Un único nodo = un único lugar donde vive ese estado; partirlo en
    /// "SetShiftCount"/"SetShiftPermanent" bajo el gate de fase obligaría a coordinar dos nodos
    /// distintos del mismo árbol.
    /// </para>
    /// <para>
    /// <b>Duración = 1 turno en fase 1.</b> <see cref="IContractModifierService"/> no tiene
    /// expiración por modificador (solo <c>ClearAll</c>), así que "dura 1 turno" se implementa como
    /// "limpio todo al empezar mi turno y vuelvo a promulgar" — idéntico a lo que ya hace
    /// <see cref="AINode_PromulgateRule"/>. En fase 2 no limpia, y ahí está el acumulado.
    /// </para>
    /// <para>
    /// <b>Generala es inmune</b> (<see cref="ImmuneComboIds"/>): cinco iguales son cinco iguales, se
    /// corra la hoja o no. Es la debilidad del jefe y la única mano que no depende de la tabla; si el
    /// corrimiento pudiera caerle encima, la salida de diseño de la pelea desaparece.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_ShiftComboToNeighbor : AIActionNode
    {
        /// <summary>Hacia qué vecino de la hoja se corre el combo elegido.</summary>
        public enum ShiftDirection
        {
            /// <summary>Al inmediatamente superior por daño base: el combo pobre paga más.</summary>
            Up = 0,

            /// <summary>Al inmediatamente inferior: la Escalera paga como Doble Par.</summary>
            Down = 1,

            /// <summary>Uno de los dos vecinos, al azar, por corrimiento.</summary>
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

        // ======================================================================
        // Elección del combo
        // ======================================================================

        /// <summary>
        /// Los <paramref name="count"/> combos más jugados de la ventana, del más frecuente al
        /// menos. Empate por frecuencia ⇒ gana el más reciente (recorremos el log de nuevo a viejo
        /// y comparamos con <c>&gt;</c> estricto), que es la lectura de "lo que más venís usando".
        /// </summary>
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

            // `order` ya viene de más reciente a más viejo, así que la selección estable por
            // frecuencia desempata a favor del más reciente sin ordenar nada.
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

                // Menos combos distintos que corrimientos disponibles: se corren los que hay.
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

        // ======================================================================
        // Helpers
        // ======================================================================

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
