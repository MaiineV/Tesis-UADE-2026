using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Triggers.Concretes
{
    /// <summary>
    /// Este dado no se puede holdear entre rolls. Hook: <c>OnEnchantmentApplied</c>.
    /// Encantamiento "Lento" (negativo).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>TODO Phase 4:</b> integrar con el roll service para prevenir el hold
    /// efectivamente. El trigger existe para que el SO pueda referenciarlo; la
    /// prevención real de hold se wirea en Phase 4 cuando el roll service consulte
    /// los triggers del dado antes de permitir lock.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class PreventHolding : IOnEnchantmentAppliedTrigger
    {
        public void OnEnchantmentApplied(EnchantmentTriggerContext ctx)
        {
            // Marker: el roll service de Phase 4 chequea si el dado tiene un
            // PreventHolding trigger antes de permitir lock.
            Debug.Log(
                $"[PreventHolding] Encantamiento aplicado en slot {ctx.Slot.BagSlotIndex} " +
                $"— hold prevention pendiente de integración con roll service.");
        }
    }
}