using System;
using Patterns;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Entities.Behaviors;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>
    /// Resuelve y aplica daño al target vía <see cref="IDamagePipeline"/>. Escribe el
    /// daño final bajo <see cref="BehaviorValueKey.FloatingDamage"/> para que un
    /// <c>EffPlayFeedback</c> downstream pueda mostrar el número flotante.
    /// TECHNICAL.md §8.7, §9.5, §12.2.
    /// </summary>
    /// <remarks>
    /// Atómico: resuelve el target desde <see cref="EffectContext.SelectionResult"/>
    /// (si hay) o <see cref="EffectContext.TargetGuid"/> como fallback. Si <see cref="IDamagePipeline"/>
    /// no está registrado en el <c>ServiceLocator</c>, loguea warning y devuelve <c>false</c>
    /// — cortocircuita la cadena (§8.8).
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public class EffDealDamage : BaseEffect<DamageArgs, int>,
        IUsesSelection, IUsesValue, ICanBeConstantValue, IShouldStoreValuesOnBehavior
    {
        [Title("Damage")]
        [SerializeField, MinValue(0), MaxValue(999)]
        [Tooltip("Daño base antes de pipeline (mitigación, críticos, weakness).")]
        private int _baseAmount = 10;

        [SerializeField]
        [Tooltip("Tipo de ataque usado al construir el DamageContext.")]
        private AttackKind _attackKind = AttackKind.BasicAttack;

        public override string GetEffectName() => "Deal Damage";

        protected override DamageArgs ResolveArgs(EffectContext context) =>
            new DamageArgs { BaseAmount = _baseAmount };

        protected override int ResolveValue(EffectContext context) => _baseAmount;

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null) return false;

            var amount = ResolveArgs(context).BaseAmount;
            if (amount <= 0) return true;

            var targetGuid = ResolveTargetGuid(context);

            int resolvedDamage = amount;
            if (ServiceLocator.TryGetService<IDamagePipeline>(out var pipeline) && pipeline != null)
            {
                var dmgCtx = new DamageContext
                {
                    SourceId = context.SourceEntity != null ? context.SourceEntity.Guid : context.SourceGuid,
                    TargetId = targetGuid,
                    BaseDamage = amount,
                    Kind = _attackKind,
                };
                pipeline.Resolve(dmgCtx);
                resolvedDamage = dmgCtx.FinalDamage;
            }
            else
            {
                Debug.LogWarning("[EffDealDamage] IDamagePipeline no registrado — usando BaseAmount crudo.");
            }

            if (context.SourceBehavior != null)
            {
                context.SourceBehavior.SetBehaviorValue(
                    BehaviorValueKey.FloatingDamage,
                    new FloatingNumberBehaviorValue
                    {
                        Value = resolvedDamage,
                        TargetEntityGuid = targetGuid,
                    });
            }

            return true;
        }

        private static Guid ResolveTargetGuid(EffectContext context)
        {
            if (context.SelectionResult?.FirstSelectedCoord is Grid.GridCoord coord
                && ServiceLocator.TryGetService<Grid.IGridManager>(out var grid)
                && grid.TryGetOccupant(coord, out var occupant)
                && occupant != Guid.Empty)
                return occupant;
            return context.TargetGuid;
        }
    }
}
