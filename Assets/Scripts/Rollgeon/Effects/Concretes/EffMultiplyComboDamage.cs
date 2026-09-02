using System;
using Rollgeon.Effects.Readers;
using Rollgeon.Upgrades;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
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

        [Tooltip("Opcional: reader que resuelve el factor en cada dispatch (vía ReadFloat) y PISA la " +
                 "constante. Eco Menguante: ReadAttackDecayMultiplier. Null = usar la constante.")]
        [OdinSerialize, SerializeReference]
        private EffectIntReader _multiplierReader;

        protected override bool ShowSelection => false;

        public float Multiplier
        {
            get => _multiplier;
            set => _multiplier = value;
        }

        /// <summary>Factor dinámico; si no es null gana sobre <see cref="Multiplier"/>.</summary>
        public EffectIntReader MultiplierReader
        {
            get => _multiplierReader;
            set => _multiplierReader = value;
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

            // ReadFloat: la fracción (Eco 4.9, 4.8…) viaja entera al scratch y se redondea
            // una sola vez al final de la fórmula N×M.
            float factor = _multiplierReader != null ? _multiplierReader.ReadFloat(context) : _multiplier;
            trig.Scratch.ComboDamageMultiplier *= factor;
            return true;
        }
    }
}
