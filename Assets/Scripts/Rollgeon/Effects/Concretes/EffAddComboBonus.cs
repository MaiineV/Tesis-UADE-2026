using System;
using Rollgeon.Effects.Readers;
using Rollgeon.Upgrades;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>
    /// Suma un valor a <c>bono_combo</c> (el término aditivo final de la fórmula de daño)
    /// escribiendo <c>BonusComboDamage</c> en el scratch del dispatch en curso. El scratch
    /// llega por <see cref="ScratchTriggerContext"/>: en un hook at-match alimenta el
    /// <c>LastComboScratch</c> del canal; en la ventana de combo jugado (ComboPlayed)
    /// alimenta el play scratch que <c>PlayerComboDamage.Resolve</c> lee antes de resolver.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public class EffAddComboBonus : BaseEffect,
        IUsesValue, ICanBeConstantValue, ICanBeGenericValue,
        IComboScratchWriter, IRequiresTriggerContext<ScratchTriggerContext>
    {
        [Title("Combo Bonus")]
        [Tooltip("Cuánto bono sumar. Admite readers (constante — puede ser negativa —, oro actual, contador de combo, cara del dado…).")]
        [OdinSerialize, SerializeReference]
        private EffectIntReader _amount = new ReadConstantInt();

        protected override bool ShowSelection => false;

        public EffectIntReader Amount
        {
            get => _amount;
            set => _amount = value;
        }

        public override string GetEffectName() => "Add Combo Bonus";

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null
                || !context.TryGetTriggerContext<ScratchTriggerContext>(out var trig)
                || trig.Scratch == null)
            {
                Debug.LogWarning("[EffAddComboBonus] sin ScratchTriggerContext — este efecto " +
                                 "solo funciona dentro de un dispatch de trigger de combo.");
                return false;
            }

            trig.Scratch.BonusComboDamage += _amount?.Read(context) ?? 0;
            return true;
        }
    }
}
