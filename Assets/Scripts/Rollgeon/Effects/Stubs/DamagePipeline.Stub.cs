using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Entities;
using UnityEngine;

namespace Rollgeon.Effects.Stubs
{
    /// <summary>
    /// [RETIRED STUB] — replaced by <see cref="Rollgeon.Combat.Pipelines.DamagePipeline"/>
    /// (Foundation#0008). This static facade is kept for backward compatibility with callers
    /// that have not yet migrated. It delegates to the real pipeline via
    /// <see cref="ServiceLocator"/>.
    /// </summary>
    public static class DamagePipelineStub
    {
        /// <summary>
        /// Legacy entry point. Builds a <see cref="DamageContext"/> and delegates to
        /// the real <see cref="IDamagePipeline"/>. If the pipeline is not registered
        /// yet, falls back to a Debug.Log (same as the original stub).
        /// </summary>
        public static void Apply(Entity source, Entity target, int amount)
        {
            if (!ServiceLocator.TryGetService<IDamagePipeline>(out var pipeline))
            {
                var srcGuid = source != null ? source.Guid.ToString() : "null";
                var tgtGuid = target != null ? target.Guid.ToString() : "null";
                Debug.Log($"[STUB DamagePipeline] {srcGuid} → {tgtGuid}: {amount} damage (pipeline not registered)");
                return;
            }

            var ctx = new DamageContext
            {
                SourceId = source != null ? source.Guid : System.Guid.Empty,
                TargetId = target != null ? target.Guid : System.Guid.Empty,
                BaseDamage = amount,
                Kind = AttackKind.BasicAttack,
            };

            pipeline.Resolve(ctx);
        }
    }
}
