using System;
using Rollgeon.Upgrades;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>
    /// Compone un multiplicador sobre el término de combo de la fórmula de daño
    /// (<c>ComboDamageMultiplier</c> del scratch del dispatch en curso). Multiplicativo
    /// entre triggers — el orden de dispatch no altera el resultado.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public class EffMultiplyComboDamage : BaseEffect,
        IUsesValue, ICanBeConstantValue,
        IComboScratchWriter, IRequiresTriggerContext<ScratchTriggerContext>
    {
        [Title("Combo Multiplier")]
        [Tooltip("Factor aplicado al término (daño_combo_base × multi). Ej: 1.5 = +50%, 0 = anula el término.")]
        [SerializeField]
        private float _multiplier = 1f;

        protected override bool ShowSelection => false;

        public float Multiplier
        {
            get => _multiplier;
            set => _multiplier = value;
        }

        public override string GetEffectName() => "Multiply Combo Damage";

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null
                || !context.TryGetTriggerContext<ScratchTriggerContext>(out var trig)
                || trig.Scratch == null)
            {
                Debug.LogWarning("[EffMultiplyComboDamage] sin ScratchTriggerContext — este efecto " +
                                 "solo funciona dentro de un dispatch de trigger de combo.");
                return false;
            }

            trig.Scratch.ComboDamageMultiplier *= _multiplier;
            return true;
        }
    }
}
