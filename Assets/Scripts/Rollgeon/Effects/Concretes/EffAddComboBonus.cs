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

        // bool y no "scale = 1f": Odin no corre field initializers al deserializar, así que un
        // float nuevo quedaría en 0 en todos los EffAddComboBonus ya autorados. false (0) preserva
        // byte a byte el comportamiento previo.
        [Tooltip("Resta el valor en vez de sumarlo. Fuente Mágica: saca el dado más alto de N " +
                 "(ReadHighestContributingDie) para que solo cuente en M.")]
        [SerializeField]
        private bool _subtract;

        protected override bool ShowSelection => false;

        public EffectIntReader Amount
        {
            get => _amount;
            set => _amount = value;
        }

        public bool Subtract
        {
            get => _subtract;
            set => _subtract = value;
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

            int amount = _amount?.Read(context) ?? 0;
            trig.Scratch.BonusComboDamage += _subtract ? -amount : amount;
            return true;
        }
    }
}
