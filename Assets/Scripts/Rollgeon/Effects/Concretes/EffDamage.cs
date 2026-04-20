using System;
using Patterns;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Effects.Stubs;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>
    /// <b>EXAMPLE — subject to refinement by downstream combat task (T100b / T103).</b>
    /// <para>
    /// Valida el pipeline de <see cref="EffectData"/>: armado <c>[SerializeField]</c>,
    /// selection settings, escritura en <c>SourceBehavior.SetBehaviorValue</c> vía
    /// <see cref="IShouldStoreValuesOnBehavior"/>, llamada a <see cref="DamagePipelineStub"/>.
    /// El combat real va a reemplazar el stub con <c>DamagePipeline</c> (§12) que resuelve
    /// mitigación, críticos, elementales, etc.
    /// </para>
    /// <para>
    /// Markers usadas: <see cref="IUsesSelection"/>, <see cref="IUsesValue"/>,
    /// <see cref="ICanBeConstantValue"/>, <see cref="IShouldStoreValuesOnBehavior"/>.
    /// </para>
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public class EffDamage : BaseEffect<DamageArgs, int>,
        IUsesSelection, IUsesValue, ICanBeConstantValue, IShouldStoreValuesOnBehavior
    {
        [Title("Damage")]
        [SerializeField, MinValue(0), MaxValue(999)]
        [Tooltip("Daño base antes de mitigación y críticos. Rango defensivo 0..999.")]
        private int _baseAmount = 10;

        public override string GetEffectName() => "Deal Damage (example)";

        protected override DamageArgs ResolveArgs(EffectContext context)
        {
            return new DamageArgs { BaseAmount = _baseAmount };
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
            if (amount <= 0) return true; // no-op, but not an error

            // Resolver target — primero SelectionResult, si no hay, TargetGuid del contexto.
            Entity target = null;
            var targetGuid = Guid.Empty;
            if (context.SelectionResult != null)
            {
                targetGuid = context.SelectionResult.FirstSelectedGuid;
            }
            if (targetGuid == Guid.Empty) targetGuid = context.TargetGuid;
            // La foundation no resuelve Guid → Entity (no hay EntityRegistry todavía) — el
            // stub de DamagePipeline tolera Entity == null y sólo loggea.

            // Prefer the real DamagePipeline when registered; fall back to stub otherwise.
            int resolvedDamage = amount;
            if (ServiceLocator.TryGetService<IDamagePipeline>(out var pipeline))
            {
                var sourceGuid = context.SourceEntity != null
                    ? context.SourceEntity.Guid
                    : Guid.Empty;

                var ctx = new DamageContext
                {
                    SourceId = sourceGuid,
                    TargetId = targetGuid,
                    BaseDamage = amount,
                    Kind = AttackKind.BasicAttack,
                };

                pipeline.Resolve(ctx);
                resolvedDamage = ctx.FinalDamage;
            }
            else
            {
                DamagePipelineStub.Apply(context.SourceEntity, target, amount);
            }

            // IShouldStoreValuesOnBehavior — escribe el número flotante para el feedback downstream.
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
    }
}
