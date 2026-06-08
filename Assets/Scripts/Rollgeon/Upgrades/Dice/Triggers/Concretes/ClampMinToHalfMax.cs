using System;
using Rollgeon.Dice;
using Sirenix.OdinInspector;

namespace Rollgeon.Upgrades.Dice.Triggers.Concretes
{
    /// <summary>
    /// "Afilado": el resultado minimo del dado se clampea a ceil(maxFace/2).
    /// Un d6 nunca baja de 3, un d4 de 2, un d12 de 6. Hook: <c>OnRollResolved</c>.
    /// </summary>
    /// <remarks>
    /// <b>Aproximacion MVP.</b> El scratch no soporta modificacion per-die del
    /// resultado — se aplica como delta al daño del combo. Phase 4 agrega
    /// per-die result clamping y este trigger se actualiza para modificar
    /// <c>DiceResult[idx]</c> directamente.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class ClampMinToHalfMax : IOnRollResolvedTrigger
    {
        public void OnRollResolved(EnchantmentTriggerContext ctx)
        {
            if (ctx?.Scratch == null || ctx.Effect?.DiceResult == null) return;

            int idx = ctx.Slot.BagSlotIndex;
            if (idx < 0 || idx >= ctx.Effect.DiceResult.Count) return;

            int face = ctx.Effect.DiceResult[idx];
            int maxFace = ctx.Slot.Type.MaxFace();
            int minAllowed = (maxFace + 1) / 2; // ceil(maxFace / 2)

            if (face < minAllowed)
            {
                // Aproximacion via BonusComboDamage delta; true per-die clamping
                // requiere roll service integration (Phase 4).
                ctx.Scratch.BonusComboDamage += minAllowed - face;
            }
        }
    }
}