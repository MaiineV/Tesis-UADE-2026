using System;
using Patterns;
using Rollgeon.Effects;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Effects
{
    /// <summary>
    /// Auto-remueve el encantamiento carrier del slot (self-destruct). El slot llega
    /// por <see cref="ScratchTriggerContext"/>. Componer con PreConditions para el
    /// gating ("explota si el counter llegó a N"). El service tolera el remove durante
    /// el dispatch (set-null en la lista, mismo patrón que el trigger legacy).
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public class EffRemoveEnchantment : BaseEffect,
        IRequiresTriggerContext<ScratchTriggerContext>
    {
        protected override bool ShowSelection => false;

        public override string GetEffectName() => "Remove Enchantment (self)";

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null
                || !context.TryGetTriggerContext<ScratchTriggerContext>(out var trig)
                || trig.Slot == null)
            {
                Debug.LogWarning("[EffRemoveEnchantment] sin ScratchTriggerContext con Slot — este efecto " +
                                 "solo funciona dentro de un dispatch de encantamiento de dados.");
                return false;
            }

            if (!ServiceLocator.TryGetService<IDiceEnchantmentRuntime>(out var runtime) || runtime == null)
                return false;

            runtime.RemoveEnchantment(trig.Slot.Value);
            return true;
        }
    }
}
