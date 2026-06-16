using System;
using Rollgeon.Dice;
using Sirenix.OdinInspector;

namespace Rollgeon.Upgrades.Dice.Triggers.Concretes
{
    /// <summary>
    /// "Volatil": si el dado muestra la cara MAX, el resultado se duplica; si
    /// muestra 1 (MIN), el resultado se anula a 0. Hook: <c>OnRollResolved</c>.
    /// </summary>
    /// <remarks>
    /// <b>Aproximacion MVP.</b> El scratch no soporta modificacion per-die —
    /// se aplica como delta al daño del combo. Phase 4 agrega per-die result
    /// modification y este trigger se actualiza para modificar
    /// <c>DiceResult[idx]</c> directamente.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class DoubleMaxZeroMin : IOnRollResolvedTrigger
    {
        public void OnRollResolved(EnchantmentTriggerContext ctx)
        {
            if (ctx?.Scratch == null || ctx.Effect?.DiceResult == null) return;

            int idx = ctx.Slot.BagSlotIndex;
            if (idx < 0 || idx >= ctx.Effect.DiceResult.Count) return;

            int face = ctx.Effect.DiceResult[idx];
            int maxFace = ctx.Slot.Type.MaxFace();

            if (face == maxFace)
            {
                // Duplica: original + bonus = 2x
                ctx.Scratch.BonusComboDamage += face;
            }
            else if (face == 1)
            {
                // Anula: original 1 - 1 = 0
                ctx.Scratch.BonusComboDamage -= 1;
            }
        }
    }
}