using System;
using System.Collections.Generic;
using Rollgeon.Combat.Damage;
using Rollgeon.Dice;
using Rollgeon.Effects;
using Rollgeon.PreConditions;
using Sirenix.OdinInspector;

namespace Rollgeon.Upgrades.Dice.PreConditions
{
    /// <summary>Predicado sobre la cara del dado carrier en <see cref="PcCarrierFace"/>.</summary>
    public enum CarrierFaceMode
    {
        /// <summary>El carrier muestra su cara máxima (Ench_Fortaleza legacy).</summary>
        OnMaxFace,

        /// <summary>Cara par (ParityScoreMultiplier legacy).</summary>
        Even,

        /// <summary>Cara impar.</summary>
        Odd,

        /// <summary>Otro dado de la tirada comparte la cara del carrier (Gemelo / Resonante legacy).</summary>
        HasDuplicate,
    }

    /// <summary>
    /// Gating por la cara del dado carrier del encantamiento. Lee el slot desde el
    /// <see cref="ScratchTriggerContext"/> y las caras desde
    /// <c>PreConditionContext.Effect.DiceResult</c>.
    /// </summary>
    /// <remarks>
    /// Sin contexto de carrier o sin caras devuelve <c>false</c> — NO permisivo:
    /// los triggers legacy hacían early-return sin aplicar el bonus cuando faltaban
    /// datos, y un gate de cara que no se puede evaluar no debe habilitar el efecto.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class PcCarrierFace : BasePreCondition, IReadsTriggerEffect
    {
        public CarrierFaceMode Mode = CarrierFaceMode.HasDuplicate;

        public override string ConditionName => $"Cara del carrier: {Mode}";

        public override bool Evaluate(PreConditionContext context)
        {
            var eff = context?.Effect;
            if (eff?.DiceResult == null) return false;
            if (!eff.TryGetTriggerContext<ScratchTriggerContext>(out var trig) || trig.Slot == null)
                return false;

            var slot = trig.Slot.Value;
            int idx = slot.BagSlotIndex;
            if (idx < 0 || idx >= eff.DiceResult.Count) return false;

            int face = eff.DiceResult[idx];
            switch (Mode)
            {
                case CarrierFaceMode.OnMaxFace:
                    return face == slot.Type.MaxFace();
                case CarrierFaceMode.Even:
                    return (face % 2) == 0;
                case CarrierFaceMode.Odd:
                    return (face % 2) != 0;
                case CarrierFaceMode.HasDuplicate:
                    return HasDuplicate(eff, idx, face);
                default:
                    return false;
            }
        }

        /// <summary>
        /// BUG carrier-scope: "gemelo/resonante" comparaba contra TODA la tirada — un
        /// carrier afuera del combo podía igual encontrar un duplicado entre otros dados
        /// también afuera del combo y disparar el bonus condicional. Con datos de
        /// contribución (hook ComboMatched/ComboPlayed vía <see cref="EffectContext.ComboResult"/>)
        /// restringimos la búsqueda a los dados que efectivamente formaron el combo, y el
        /// carrier debe ser uno de ellos. Sin esos datos (RollResolved/DiceRolled/TurnFinished,
        /// que no tienen noción de combo) preservamos el comportamiento legado: toda la tirada.
        /// </summary>
        private static bool HasDuplicate(EffectContext eff, int carrierBagSlot, int face)
        {
            var contributingSlots = ResolveContributingBagSlots(eff);
            if (contributingSlots != null)
            {
                if (!contributingSlots.Contains(carrierBagSlot)) return false;
                for (int i = 0; i < contributingSlots.Count; i++)
                {
                    int slot = contributingSlots[i];
                    if (slot == carrierBagSlot) continue;
                    if (slot < 0 || slot >= eff.DiceResult.Count) continue;
                    if (eff.DiceResult[slot] == face) return true;
                }
                return false;
            }

            for (int i = 0; i < eff.DiceResult.Count; i++)
            {
                if (i != carrierBagSlot && eff.DiceResult[i] == face) return true;
            }
            return false;
        }

        /// <summary>
        /// Bag slots de los dados contribuyentes del combo, o <c>null</c> si no hay contexto
        /// de combo (match nulo/false o sin índices) — el caller cae al comportamiento legado
        /// en ese caso. Reusa el mismo mapeo local→bag slot que <c>CarrierParticipates</c>.
        /// </summary>
        private static List<int> ResolveContributingBagSlots(EffectContext eff)
        {
            var combo = eff.ComboResult;
            if (combo == null || !combo.Value.IsMatch) return null;

            var contributing = combo.Value.ContributingIndices;
            if (contributing == null || contributing.Count == 0) return null;

            var map = eff.KeptDiceOriginalIndices;
            var slots = new List<int>(contributing.Count);
            for (int i = 0; i < contributing.Count; i++)
                slots.Add(ContributingDiceResolver.ResolveBagSlot(contributing[i], map));
            return slots;
        }
    }
}
