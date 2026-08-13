using System;
using Patterns;
using Rollgeon.Effects;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Effects
{
    /// <summary>Operación de <see cref="EffSlotCounter"/>.</summary>
    public enum SlotCounterOperation
    {
        Reset,
        Increment,
    }

    /// <summary>
    /// Resetea o incrementa un counter per-slot de <see cref="IDiceEnchantmentRuntime"/>
    /// (keyed por <c>(slot del carrier, Key)</c>). El slot llega por
    /// <see cref="ScratchTriggerContext"/> — solo tiene sentido dentro de un dispatch
    /// del canal dados. Reemplaza el bookkeeping hardcodeado de triggers stateful
    /// (ExplodeIfUnusedForTurns).
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public class EffSlotCounter : BaseEffect,
        IRequiresTriggerContext<ScratchTriggerContext>
    {
        [Title("Counter")]
        public SlotCounterOperation Operation = SlotCounterOperation.Increment;

        [Tooltip("Clave del counter per-slot. Los grupos que cooperan (increment + check) usan la misma clave.")]
        public string Key = "counter";

        [ShowIf(nameof(Operation), SlotCounterOperation.Increment)]
        public int Delta = 1;

        protected override bool ShowSelection => false;

        public override string GetEffectName() => "Slot Counter";

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null
                || !context.TryGetTriggerContext<ScratchTriggerContext>(out var trig)
                || trig.Slot == null)
            {
                Debug.LogWarning("[EffSlotCounter] sin ScratchTriggerContext con Slot — este efecto " +
                                 "solo funciona dentro de un dispatch de encantamiento de dados.");
                return false;
            }

            if (!ServiceLocator.TryGetService<IDiceEnchantmentRuntime>(out var runtime) || runtime == null)
                return false;

            var slot = trig.Slot.Value;
            if (Operation == SlotCounterOperation.Reset) runtime.ResetCounter(slot, Key);
            else runtime.IncrementCounter(slot, Key, Delta);
            return true;
        }
    }
}
