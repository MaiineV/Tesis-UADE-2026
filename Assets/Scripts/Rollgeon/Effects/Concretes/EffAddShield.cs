using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Entities.Behaviors;
using Rollgeon.Grid;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    [Serializable, HideReferenceObjectPicker]
    public class EffAddShield : BaseEffect<ShieldArgs, int>,
        IUsesValue, ICanBeConstantValue, IShouldStoreValuesOnBehavior
    {
        [Title("Shield")]
        [SerializeField]
        [Tooltip("Source: Constant uses _baseAmount, ComboValue uses the resolved combo's BaseDamage.")]
        private DamageSource _shieldSource = DamageSource.Constant;

        [SerializeField, ShowIf("_shieldSource", DamageSource.Constant)]
        [MinValue(0), MaxValue(999)]
        private int _baseAmount = 5;

        [SerializeField, ShowIf("_shieldSource", DamageSource.ComboValue)]
        [MinValue(0.01f)]
        [Tooltip("Multiplier applied to the combo's BaseDamage.")]
        private float _comboMultiplier = 1f;

        public DamageSource ShieldSource => _shieldSource;
        public int BaseAmount => _baseAmount;
        public float ComboMultiplier => _comboMultiplier;

        public override string GetEffectName() => "Add Shield";

        protected override ShieldArgs ResolveArgs(EffectContext context)
        {
            int amount = _shieldSource switch
            {
                DamageSource.ComboValue when context?.ComboResult is { IsMatch: true } combo
                    => Mathf.RoundToInt(combo.BaseDamage * _comboMultiplier),
                _ => _baseAmount,
            };
            return new ShieldArgs { BaseAmount = amount };
        }

        protected override int ResolveValue(EffectContext context) => ResolveArgs(context).BaseAmount;

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null) return false;

            var amount = ResolveArgs(context).BaseAmount;
            if (amount <= 0) return true;

            var targetGuid = ResolveTargetGuid(context);

            if (targetGuid == Guid.Empty)
            {
                Debug.LogWarning("[EffAddShield] No target resolved — aborting chain.");
                return false;
            }

            if (!ServiceLocator.TryGetService<AttributesManager>(out var attributes)
                || attributes == null)
            {
                Debug.LogWarning("[EffAddShield] AttributesManager not registered.");
                return false;
            }

            var shieldAttr = attributes.GetAttribute<Shield>(targetGuid);
            int current = shieldAttr?.Value ?? 0;
            int newShield = current + amount;

            attributes.SetAttributeValue<Shield, int>(targetGuid, newShield);
            EventManager.Trigger(EventName.OnShieldChanged, targetGuid, newShield);

            if (context.SourceBehavior != null)
            {
                context.SourceBehavior.SetBehaviorValue(
                    BehaviorValueKey.FloatingShield,
                    new FloatingNumberBehaviorValue
                    {
                        Value = amount,
                        TargetEntityGuid = targetGuid,
                    });
            }

            return true;
        }

        private static Guid ResolveTargetGuid(EffectContext context)
        {
            if (context.SelectionResult?.FirstSelectedCoord is GridCoord coord
                && ServiceLocator.TryGetService<IGridManager>(out var grid)
                && grid.TryGetOccupant(coord, out var occupant)
                && occupant != Guid.Empty)
                return occupant;
            return context.SourceGuid;
        }
    }
}
