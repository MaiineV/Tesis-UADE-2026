using System;
using Patterns;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Entities.Behaviors;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>
    /// Resuelve y aplica curación al target vía <see cref="IHealPipeline"/>. Escribe el
    /// valor curado bajo <see cref="BehaviorValueKey.FloatingHeal"/> para consumo del
    /// feedback downstream. TECHNICAL.md §8.7, §9.5, §17.M.
    /// </summary>
    /// <remarks>
    /// Atómico: resuelve el target desde <see cref="EffectContext.SelectionResult"/>
    /// o <see cref="EffectContext.SourceGuid"/> como fallback (heal self). Si
    /// <see cref="IHealPipeline"/> no está registrado, aborta la cadena (§8.8).
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public class EffHeal : BaseEffect<HealArgs, int>,
        IUsesSelection, IUsesValue, ICanBeConstantValue, IShouldStoreValuesOnBehavior
    {
        [Title("Heal")]
        [SerializeField, MinValue(0), MaxValue(999)]
        [Tooltip("Curación base antes de pipeline (overheal, shields).")]
        private int _baseAmount = 10;

        [SerializeField]
        [Tooltip("Si true, BaseAmount es porcentaje del max HP del target.")]
        private bool _isPercentOfMax;

        [SerializeField]
        [Tooltip("Tag libre para logging/telemetría — ej. 'potion', 'support.heal'.")]
        private string _sourceTag = "eff.heal";

        public override string GetEffectName() => "Heal";

        protected override HealArgs ResolveArgs(EffectContext context) =>
            new HealArgs { BaseAmount = _baseAmount };

        protected override int ResolveValue(EffectContext context) => _baseAmount;

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null) return false;

            var amount = ResolveArgs(context).BaseAmount;
            if (amount <= 0) return true;

            var targetGuid = ResolveTargetGuid(context);
            if (targetGuid == Guid.Empty)
            {
                Debug.LogWarning("[EffHeal] No target resuelto — aborta cadena.");
                return false;
            }

            int resolvedHeal = amount;
            if (ServiceLocator.TryGetService<IHealPipeline>(out var pipeline) && pipeline != null)
            {
                var healCtx = new HealContext
                {
                    SourceId = context.SourceEntity != null ? context.SourceEntity.Guid : context.SourceGuid,
                    TargetId = targetGuid,
                    BaseHeal = amount,
                    IsPercentOfMax = _isPercentOfMax,
                    SourceTag = _sourceTag,
                };
                pipeline.Resolve(healCtx);
                resolvedHeal = healCtx.FinalHeal;
            }
            else
            {
                Debug.LogWarning("[EffHeal] IHealPipeline no registrado — usando amount crudo.");
            }

            if (context.SourceBehavior != null)
            {
                context.SourceBehavior.SetBehaviorValue(
                    BehaviorValueKey.FloatingHeal,
                    new FloatingNumberBehaviorValue
                    {
                        Value = resolvedHeal,
                        TargetEntityGuid = targetGuid,
                    });
            }

            return true;
        }

        private static Guid ResolveTargetGuid(EffectContext context)
        {
            if (context.SelectionResult != null && context.SelectionResult.FirstSelectedGuid != Guid.Empty)
                return context.SelectionResult.FirstSelectedGuid;
            return context.SourceGuid;
        }
    }
}
