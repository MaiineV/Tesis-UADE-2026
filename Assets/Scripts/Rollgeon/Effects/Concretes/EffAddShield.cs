using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Effects.Readers;
using Rollgeon.Entities.Behaviors;
using Rollgeon.Grid;
using Rollgeon.UI.Tooltips;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    [Serializable, HideReferenceObjectPicker]
    public class EffAddShield : BaseEffect<ShieldArgs, int>,
        IUsesValue, ICanBeConstantValue, IShouldStoreValuesOnBehavior, IHasTooltipInfo
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

        [OdinSerialize, SerializeReference]
        [ShowIf("_shieldSource", DamageSource.FromReader)]
        [Tooltip("Reader polimórfico que resuelve el shield desde stats de entidad en runtime.")]
        private EffectIntReader _reader;

        [SerializeField, ShowIf("_shieldSource", DamageSource.FromReader)]
        [MinValue(0.01f)]
        [Tooltip("Multiplicador aplicado al resultado del reader.")]
        private float _readerMultiplier = 1f;

        public DamageSource ShieldSource => _shieldSource;
        public int BaseAmount => _baseAmount;
        public float ComboMultiplier => _comboMultiplier;

        public override string GetEffectName() => "Add Shield";

        // IHasTooltipInfo — mismo criterio que EffDealDamage: valores dinámicos cuando
        // la fuente lo permite (FromReader lee stats del owner en hover-time).
        public string BuildTooltip()
            => TooltipContext.TryForCurrentHero(Rollgeon.Phase.GamePhase.Combat, out var ctx)
                ? BuildTooltip(ctx)
                : BuildTooltip(default(TooltipContext));

        public string BuildTooltip(in TooltipContext context)
        {
            switch (_shieldSource)
            {
                case DamageSource.ComboValue:
                    return Mathf.Approximately(_comboMultiplier, 1f)
                        ? "Escudo: puntaje del combo"
                        : "Escudo: puntaje del combo × " + _comboMultiplier.ToString("0.##");
                case DamageSource.FromReader when _reader != null:
                    return "Escudo: +" + Mathf.RoundToInt(
                        _reader.Read(context.ToReaderContext()) * _readerMultiplier);
                case DamageSource.FromReader:
                    return null;
                default:
                    return "Escudo: +" + _baseAmount;
            }
        }

        protected override ShieldArgs ResolveArgs(EffectContext context)
        {
            int amount = _shieldSource switch
            {
                DamageSource.ComboValue when context?.ComboResult is { IsMatch: true } combo
                    => Mathf.RoundToInt(combo.BaseDamage * _comboMultiplier),
                DamageSource.ComboValue => 0,
                DamageSource.FromReader when _reader != null
                    => Mathf.RoundToInt(_reader.Read(context) * _readerMultiplier),
                DamageSource.FromReader => 0,
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
