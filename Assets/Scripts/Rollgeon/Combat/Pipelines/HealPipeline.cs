using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using UnityEngine;

namespace Rollgeon.Combat.Pipelines
{
    /// <summary>
    /// Central pipeline that resolves all healing between entities.
    /// <para>
    /// <b>Foundation scope.</b> This implementation covers the stages that the current
    /// foundation supports: percent resolution, max HP clamping, Health write via
    /// <see cref="AttributesManager"/>, and event firing. Outgoing/Incoming heal multipliers
    /// are placeholder stages that will be wired in when the corresponding stats land,
    /// without changing the public API.
    /// </para>
    /// </summary>
    public class HealPipeline : IHealPipeline
    {
        private readonly AttributesManager _attributes;
        private readonly Func<Guid, int> _maxHpResolver;

        /// <summary>
        /// Creates the pipeline with explicit dependencies (test-friendly).
        /// </summary>
        /// <param name="attributes">Required. The attribute manager for reading/writing Health.</param>
        /// <param name="maxHpResolver">Optional. Resolves max HP for a given entity. If null, no max HP clamp is applied.</param>
        public HealPipeline(AttributesManager attributes, Func<Guid, int> maxHpResolver = null)
        {
            _attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
            _maxHpResolver = maxHpResolver ?? (_ => int.MaxValue);
        }

        /// <summary>
        /// Convenience ctor that pulls dependencies from <see cref="ServiceLocator"/>.
        /// Used by bootstrap registration.
        /// </summary>
        public HealPipeline()
        {
            _attributes = ServiceLocator.GetService<AttributesManager>();
            _maxHpResolver = _ => int.MaxValue;
        }

        /// <inheritdoc />
        public HealContext Resolve(HealContext ctx)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            int heal = ctx.BaseHeal;

            // -- 0. Null / zero guard ------------------------------------------------
            if (heal <= 0)
            {
                ctx.FinalHeal = 0;
                ctx.WasClamped = false;
                return ctx;
            }

            // -- 1. Percent resolution ------------------------------------------------
            if (ctx.IsPercentOfMax)
            {
                int maxHp = _maxHpResolver(ctx.TargetId);
                heal = Mathf.RoundToInt(maxHp * (heal / 100f));
            }

            // -- 2. Outgoing multiplier (placeholder -- stat not yet defined) ---------
            // When OutgoingHealMultiplier lands, wire it here:
            // float outMult = _attributes.GetAttributeModifiedValue<OutgoingHealMultiplier, float>(ctx.SourceId);
            // heal = Mathf.RoundToInt(heal * outMult);

            // -- 3. Incoming multiplier (placeholder -- stat not yet defined) ---------
            // float inMult = _attributes.GetAttributeModifiedValue<IncomingHealMultiplier, float>(ctx.TargetId);
            // heal = Mathf.RoundToInt(heal * inMult);

            // -- 4. Clamp: don't heal beyond max HP -----------------------------------
            int maxHpForClamp = _maxHpResolver(ctx.TargetId);
            var health = _attributes.GetAttribute<Health>(ctx.TargetId);

            if (health == null)
            {
                Debug.LogWarning(
                    $"[HealPipeline] Target '{ctx.TargetId}' has no Health attribute — heal discarded.");
                ctx.FinalHeal = 0;
                ctx.WasClamped = false;
                return ctx;
            }

            int currentHp = health.Value;
            int headroom = maxHpForClamp - currentHp;
            if (headroom < 0) headroom = 0;

            bool wasClamped = heal > headroom;
            int finalHeal = Mathf.Min(heal, headroom);

            ctx.FinalHeal = finalHeal;
            ctx.WasClamped = wasClamped;

            // -- 5. Apply: commit to Health -------------------------------------------
            if (finalHeal > 0)
            {
                int newHp = currentHp + finalHeal;
                _attributes.SetAttributeValue<Health, int>(ctx.TargetId, newHp);
            }

            // -- 6. Fire resolved event (TypedEvent channel) --------------------------
            TypedEvent<HealResolvedPayload>.Raise(new HealResolvedPayload
            {
                SourceGuid = ctx.SourceId,
                TargetGuid = ctx.TargetId,
                FinalHeal = ctx.FinalHeal,
                WasPercentBased = ctx.IsPercentOfMax,
            });

            return ctx;
        }
    }
}
