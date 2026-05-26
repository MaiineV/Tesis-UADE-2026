using System;
using Sirenix.OdinInspector;

namespace Rollgeon.Upgrades.Dice.Triggers.Concretes
{
    /// <summary>
    /// Multiplica el daño del combo según la paridad de la cara que sacó el dado
    /// carrier. Hook: <c>OnComboMatched</c>. Cubre el ejemplo "malvado" del GDD:
    /// "si le sale impar te da un x3 en multi pero si sale par x0".
    /// </summary>
    /// <remarks>
    /// El multiplicador se compone con los de otros triggers via
    /// <c>scratch.ComboDamageMultiplier *= factor</c>. Múltiples triggers de
    /// este tipo en distintos dados se multiplican entre sí — atender el balance.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class ParityScoreMultiplier : IOnComboMatchedTrigger
    {
        [Title("Multipliers")]
        [InfoBox("Factor aplicado al daño del combo según la cara del dado carrier. " +
                 "Default replica el ejemplo del GDD: x3 si impar, x0 si par.")]

        [HorizontalGroup("mults"), MinValue(0f), LabelText("On Odd")]
        public float MultiplierOdd = 3f;

        [HorizontalGroup("mults"), MinValue(0f), LabelText("On Even")]
        public float MultiplierEven = 0f;

        public void OnComboMatched(EnchantmentTriggerContext ctx)
        {
            if (ctx?.Scratch == null || ctx.Effect?.DiceResult == null) return;
            int idx = ctx.Slot.BagSlotIndex;
            if (idx < 0 || idx >= ctx.Effect.DiceResult.Count) return;

            int face = ctx.Effect.DiceResult[idx];
            float factor = (face % 2) == 0 ? MultiplierEven : MultiplierOdd;
            ctx.Scratch.ComboDamageMultiplier *= factor;
        }
    }
}
