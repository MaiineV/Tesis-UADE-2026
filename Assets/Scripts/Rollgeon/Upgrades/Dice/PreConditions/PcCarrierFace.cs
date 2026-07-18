using System;
using Rollgeon.Dice;
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
    public sealed class PcCarrierFace : BasePreCondition
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
                    for (int i = 0; i < eff.DiceResult.Count; i++)
                    {
                        if (i != idx && eff.DiceResult[i] == face) return true;
                    }
                    return false;
                default:
                    return false;
            }
        }
    }
}
