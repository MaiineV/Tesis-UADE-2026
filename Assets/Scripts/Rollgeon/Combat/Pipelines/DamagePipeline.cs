using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.Weakness;
using UnityEngine;

namespace Rollgeon.Combat.Pipelines
{
    /// <summary>
    /// Central pipeline that resolves all damage between entities (TECHNICAL.md §12.2).
    /// <para>
    /// <b>Foundation scope.</b> This implementation covers the stages that the current
    /// foundation supports: weakness multiplier, Health write via <see cref="AttributesManager"/>,
    /// and event firing. Outgoing/Incoming modifier multipliers and Shield absorption are
    /// specced in §12.2 but depend on stats (<c>OutgoingDamageMultiplier</c>,
    /// <c>IncomingDamageMultiplier</c>, <c>Shield</c>) that are not yet defined. When those
    /// stats land, the corresponding stages will be wired in without changing the public API.
    /// </para>
    /// </summary>
    public class DamagePipeline : IDamagePipeline
    {
        private readonly AttributesManager _attributes;
        private readonly IWeaknessChecker _weaknessChecker;

        /// <summary>
        /// Creates the pipeline with explicit dependencies (test-friendly).
        /// </summary>
        /// <param name="attributes">Required. The attribute manager for reading/writing Health.</param>
        /// <param name="weaknessChecker">Optional. If null, weakness multiplier is always 1.0.</param>
        public DamagePipeline(AttributesManager attributes, IWeaknessChecker weaknessChecker = null)
        {
            _attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
            _weaknessChecker = weaknessChecker;
        }

        /// <summary>
        /// Convenience ctor that pulls dependencies from <see cref="ServiceLocator"/>.
        /// Used by bootstrap registration.
        /// </summary>
        public DamagePipeline()
        {
            _attributes = ServiceLocator.GetService<AttributesManager>();
            ServiceLocator.TryGetService<IWeaknessChecker>(out var wc);
            _weaknessChecker = wc;
        }

        /// <inheritdoc />
        public DamageContext Resolve(DamageContext ctx)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            int damage = ctx.BaseDamage;

            // ── 0. Zero / negative guard ──────────────────────────────────────
            if (damage <= 0)
            {
                ctx.FinalDamage = 0;
                ctx.WeaknessMultiplier = 1f;
                ctx.WasLethal = false;
                return ctx;
            }

            // ── 1. Outgoing multiplier (placeholder — stat not yet defined) ───
            // When OutgoingDamageMultiplier lands, wire it here:
            // float outMult = _attributes.GetAttributeModifiedValue<OutgoingDamageMultiplier, float>(ctx.SourceId);
            // damage = Mathf.RoundToInt(damage * outMult);

            EventManager.Trigger(EventName.OnDamageOutgoing,
                ctx.SourceId, ctx.TargetId, damage);

            // ── 2. Weakness multiplier ────────────────────────────────────────
            float weakMult = 1f;
            if (ctx.IsWeaknessHit && _weaknessChecker != null)
            {
                weakMult = _weaknessChecker.GetMultiplier(ctx.SourceId, ctx.TargetId, ctx.ComboId);
                if (weakMult > 1f)
                {
                    damage = Mathf.RoundToInt(damage * weakMult);
                }
            }
            ctx.WeaknessMultiplier = weakMult;

            // ── 3. Incoming multiplier (placeholder — stat not yet defined) ───
            // float inMult = _attributes.GetAttributeModifiedValue<IncomingDamageMultiplier, float>(ctx.TargetId);
            // damage = Mathf.RoundToInt(damage * inMult);

            EventManager.Trigger(EventName.OnDamageIncoming,
                ctx.SourceId, ctx.TargetId, damage);

            // ── 4. Shield absorption ─────────────────────────────────────
            bool shieldBroken = false;
            var shieldAttr = _attributes.GetAttribute<Shield>(ctx.TargetId);
            if (shieldAttr != null && shieldAttr.Value > 0)
            {
                int shield = shieldAttr.Value;
                int absorbed = Mathf.Min(shield, damage);
                damage -= absorbed;
                int newShield = shield - absorbed;
                _attributes.SetAttributeValue<Shield, int>(ctx.TargetId, newShield);
                ctx.ShieldAbsorbed = absorbed;

                // Shield "broken" = estaba arriba (>0) y quedó en 0 tras absorber. Lo
                // exponemos en el payload para que la UI pueda spawnear un "Broken Shield"
                // junto con el número de daño residual (si hay).
                shieldBroken = absorbed > 0 && newShield == 0;

                if (absorbed > 0)
                    EventManager.Trigger(EventName.OnShieldChanged, ctx.TargetId, newShield);
            }

            ctx.BlockedByShield = damage == 0 && ctx.ShieldAbsorbed > 0;

            // ── 5. Apply: commit to Health ────────────────────────────────────
            int finalDamage = damage;
            ctx.FinalDamage = finalDamage;

            if (finalDamage > 0)
            {
                var health = _attributes.GetAttribute<Health>(ctx.TargetId);
                if (health != null)
                {
                    int currentHp = health.Value;
                    int newHp = currentHp - finalDamage;
                    if (newHp < 0) newHp = 0;

                    _attributes.SetAttributeValue<Health, int>(ctx.TargetId, newHp);
                    ctx.WasLethal = newHp <= 0;
                }
                else
                {
                    Debug.LogWarning(
                        $"[DamagePipeline] Target '{ctx.TargetId}' has no Health attribute — damage discarded.");
                    ctx.FinalDamage = 0;
                    ctx.WasLethal = false;
                }
            }
            else
            {
                ctx.WasLethal = false;
            }

            // ── 6. Fire resolved event (TypedEvent channel) ──────────────────
            TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                SourceGuid = ctx.SourceId,
                TargetGuid = ctx.TargetId,
                FinalDamage = ctx.FinalDamage,
                WeaknessHit = ctx.WeaknessMultiplier > 1f,
                WasLethal = ctx.WasLethal,
                ShieldAbsorbed = ctx.ShieldAbsorbed,
                BlockedByShield = ctx.BlockedByShield,
                ShieldBroken = shieldBroken,
            });

            return ctx;
        }
    }
}
