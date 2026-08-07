using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.AntiRepeat;
using Rollgeon.Combat.ComboLog;
using Rollgeon.Combat.Damage;
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

            // ── 0b. Repeat-combo guard (no repetir el mismo combo 2 veces seguidas) ──
            // Gated: solo aplica cuando el pasivo anti-repetición está en Mode Combo (A/B).
            // Record() ya corrió para este golpe (CombatHandoffService, antes de llegar
            // acá) — comparamos contra el anterior al que acaba de empujar al frente.
            if (ComboRepeatRuleActive() && IsRepeatOfPreviousCombo(ctx.ComboId, alreadyRecordedThisAttack: true))
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
            int shieldBefore = ReadShield(ctx.TargetId);
            int absorbed = ComputeShieldAbsorbed(shieldBefore, damage);
            if (absorbed > 0)
            {
                damage -= absorbed;
                int newShield = shieldBefore - absorbed;
                _attributes.SetAttributeValue<Shield, int>(ctx.TargetId, newShield);
                ctx.ShieldAbsorbed = absorbed;

                // Shield "broken" = estaba arriba (>0) y quedó en 0 tras absorber. Lo
                // exponemos en el payload para que la UI pueda spawnear un "Broken Shield"
                // junto con el número de daño residual (si hay).
                shieldBroken = newShield == 0;

                EventManager.Trigger(EventName.OnShieldChanged, ctx.TargetId, newShield);
            }

            ctx.BlockedByShield = damage == 0 && ctx.ShieldAbsorbed > 0;

            // ── 5. Apply: commit to Health ────────────────────────────────────
            int finalDamage = damage;
            ctx.FinalDamage = finalDamage;

            int hpBefore = -1;
            int hpAfter = -1;

            if (finalDamage > 0)
            {
                var health = _attributes.GetAttribute<Health>(ctx.TargetId);
                if (health != null)
                {
                    int currentHp = health.Value;
                    int newHp = currentHp - finalDamage;
                    if (newHp < 0) newHp = 0;

                    // Override de letalidad (tutorial): el golpe letal deja al target
                    // en 1 HP y WasLethal queda false — el DeathWatcher nunca lo ve.
                    if (newHp <= 0
                        && ServiceLocator.TryGetService<ILethalDamageOverride>(out var lethalOverride)
                        && lethalOverride != null
                        && lethalOverride.ShouldPreventLethal(ctx.TargetId))
                    {
                        newHp = 1;
                        ctx.FinalDamage = currentHp - newHp;
                    }

                    hpBefore = currentHp;
                    hpAfter = newHp;

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

            DamageDebugLogger.LogApplication(ctx, shieldBefore, hpBefore, hpAfter);

            return ctx;
        }

        /// <inheritdoc />
        public DamageContext Preview(DamageContext ctx)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            int damage = ctx.BaseDamage;
            if (damage <= 0)
            {
                ctx.FinalDamage = 0;
                ctx.WeaknessMultiplier = 1f;
                ctx.ShieldAbsorbed = 0;
                ctx.BlockedByShield = false;
                return ctx;
            }

            // Repeat-combo guard (Mode Combo): a diferencia de Resolve, acá el jugador todavía
            // está eligiendo dados — Record() para ESTE intento todavía no corrió, así que
            // comparamos directo contra el último combo ya confirmado.
            if (ComboRepeatRuleActive() && IsRepeatOfPreviousCombo(ctx.ComboId, alreadyRecordedThisAttack: false))
            {
                ctx.FinalDamage = 0;
                ctx.WeaknessMultiplier = 1f;
                ctx.ShieldAbsorbed = 0;
                ctx.BlockedByShield = false;
                return ctx;
            }

            // Stage 2 — weakness (read-only: PeekMultiplier NO dispara OnWeaknessHit).
            float weakMult = 1f;
            if (ctx.IsWeaknessHit && _weaknessChecker != null)
            {
                weakMult = _weaknessChecker.PeekMultiplier(ctx.SourceId, ctx.TargetId, ctx.ComboId);
                if (weakMult > 1f) damage = Mathf.RoundToInt(damage * weakMult);
            }
            ctx.WeaknessMultiplier = weakMult;

            // Stage 4 — shield absorption (computar, NO escribir Shield ni disparar eventos).
            int absorbed = ComputeShieldAbsorbed(ReadShield(ctx.TargetId), damage);
            if (absorbed > 0)
            {
                damage -= absorbed;
                ctx.ShieldAbsorbed = absorbed;
            }
            ctx.BlockedByShield = damage == 0 && ctx.ShieldAbsorbed > 0;
            ctx.FinalDamage = damage;
            return ctx;
        }

        // Gate del pasivo anti-repetición (A/B). El zeroing por combo repetido SOLO aplica en
        // Mode Combo. Si el servicio no está registrado (tests unitarios del pipeline, bootstrap
        // parcial), tratamos la regla como APAGADA (legacy) para no anular daño inesperadamente.
        private static bool ComboRepeatRuleActive()
        {
            return ServiceLocator.TryGetService<IAntiRepeatModeService>(out var svc)
                   && svc != null
                   && svc.Mode == AntiRepeatMode.Combo;
        }

        // "Combo repetido = 0 daño" — memoria de un solo paso contra IComboLogService
        // (ya existe, poblado por CombatHandoffService en cada ataque primario con tirada).
        // Resolve() corre DESPUÉS de que Record() ya empujó el combo de este golpe al
        // frente del historial — el "anterior real" queda en el índice 1. Preview() corre
        // ANTES de que Record() confirme el intento — el "anterior real" es directamente
        // LastCombo (índice 0). ComboId vacío (ataques sin combo / no-jugador) nunca activa
        // la regla.
        private static bool IsRepeatOfPreviousCombo(string comboId, bool alreadyRecordedThisAttack)
        {
            if (string.IsNullOrEmpty(comboId)) return false;
            if (!ServiceLocator.TryGetService<IComboLogService>(out var log) || log == null) return false;

            if (alreadyRecordedThisAttack)
            {
                var lastTwo = log.Last(2);
                return lastTwo.Count >= 2 && lastTwo[1] == comboId;
            }

            return log.LastCombo == comboId;
        }

        private int ReadShield(Guid targetId)
        {
            var shieldAttr = _attributes.GetAttribute<Shield>(targetId);
            return shieldAttr != null ? shieldAttr.Value : 0;
        }

        // Cuánto absorbe el escudo actual de un golpe — aritmética compartida entre Resolve
        // (que además escribe y emite el evento) y Preview (solo computa). Único source of
        // truth para que preview y golpe real nunca driftéen.
        private static int ComputeShieldAbsorbed(int shieldValue, int damage)
        {
            if (shieldValue <= 0 || damage <= 0) return 0;
            return Mathf.Min(shieldValue, damage);
        }
    }
}
