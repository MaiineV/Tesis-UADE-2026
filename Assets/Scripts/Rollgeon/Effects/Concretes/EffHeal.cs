using System;
using Patterns;
using Rollgeon.Entities;
using Rollgeon.Entities.Behaviors;
using Rollgeon.Grid;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>
    /// <b>EXAMPLE — subject to refinement by downstream combat task (T100b / T103).</b>
    /// <para>
    /// Contraparte de <see cref="EffDamage"/>. Escribe un <c>FloatingNumberBehaviorValue</c>
    /// bajo la key <see cref="BehaviorValueKey.FloatingHeal"/>. La lógica real de sumar HP
    /// via <c>IModifiable&lt;HealthAttribute&gt;</c> se implementa cuando Foundation#0003
    /// exponga su API completa de readers — hasta entonces, este stub loggea intención.
    /// </para>
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public class EffHeal : BaseEffect<HealArgs, int>,
        IUsesSelection, IUsesValue, ICanBeConstantValue, IShouldStoreValuesOnBehavior
    {
        [Title("Heal")]
        [SerializeField, MinValue(0), MaxValue(999)]
        [Tooltip("Curación base antes de modificadores. Rango defensivo 0..999.")]
        private int _baseAmount = 10;

        public override string GetEffectName() => "Heal (example)";

        protected override HealArgs ResolveArgs(EffectContext context)
        {
            return new HealArgs { BaseAmount = _baseAmount };
        }

        protected override int ResolveValue(EffectContext context)
        {
            return _baseAmount;
        }

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null) return false;

            var args = ResolveArgs(context);
            var amount = args.BaseAmount;
            if (amount <= 0) return true; // no-op

            var targetGuid = Guid.Empty;
            if (context.SelectionResult?.FirstSelectedCoord is GridCoord coord)
            {
                if (ServiceLocator.TryGetService<IGridManager>(out var grid))
                    grid.TryGetOccupant(coord, out targetGuid);
            }
            if (targetGuid == Guid.Empty) targetGuid = context.SourceGuid;

            Debug.Log($"[EffHeal example] source {context.SourceGuid} heals {targetGuid} for {amount}");

            if (context.SourceBehavior != null)
            {
                context.SourceBehavior.SetBehaviorValue(
                    BehaviorValueKey.FloatingHeal,
                    new FloatingNumberBehaviorValue
                    {
                        Value = amount,
                        TargetEntityGuid = targetGuid,
                    });
            }

            return true;
        }
    }
}
