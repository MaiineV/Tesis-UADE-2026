using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
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
        /// <summary>
        /// HP con los que queda un target salvado por <see cref="ILethalDamageOverride"/>
        /// (tutorial). 10% del pool base de 100 — el mismo ratio que tenía 1 HP sobre
        /// el pool viejo de 10.
        /// </summary>
        public const int LethalOverrideRemainingHp = 10;

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

            // ── 1b. Outgoing flat bonus (Fortaleza) ───────────────────────────
            // Solo golpes ofensivos del atacante: DoT/ambiental/reacciones quedan afuera
            // por diseño, y el gate por Kind acá evita que cada provider lo repita.
            if ((ctx.Kind == AttackKind.ComboAttack || ctx.Kind == AttackKind.BasicAttack)
                && ServiceLocator.TryGetService<IOutgoingFlatDamageBonusProvider>(out var flatBonus)
                && flatBonus != null)
            {
                int bonus = flatBonus.GetFlatBonus(ctx);
                if (bonus != 0) damage += bonus;
            }

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

            // ── 3. Incoming multiplier ────────────────────────────────────────
            damage = ApplyIncomingMultiplier(ctx, damage);

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

                    // Piso de HP por (target, source) — Mimic (Feature#0046): daño
                    // no-jugador nunca lo baja de 1. Clampeado a la vida actual para
                    // no curar a un target que ya estaba por debajo del piso.
                    if (ServiceLocator.TryGetService<IMinHpClampProvider>(out var clampProvider)
                        && clampProvider != null
                        && clampProvider.TryGetMinHp(ctx.TargetId, ctx.SourceId, out int minHp)
                        && newHp < minHp)
                    {
                        newHp = currentHp < minHp ? currentHp : minHp;
                        ctx.FinalDamage = currentHp - newHp;
                    }

                    // Override de letalidad (tutorial): el golpe letal deja al target
                    // con un resto de vida y WasLethal queda false — el DeathWatcher
                    // nunca lo ve. Clampeado a la vida actual para no curar a un target
                    // que ya estaba por debajo del resto.
                    if (newHp <= 0
                        && ServiceLocator.TryGetService<ILethalDamageOverride>(out var lethalOverride)
                        && lethalOverride != null
                        && lethalOverride.ShouldPreventLethal(ctx.TargetId))
                    {
                        newHp = currentHp < LethalOverrideRemainingHp
                            ? currentHp
                            : LethalOverrideRemainingHp;
                        ctx.FinalDamage = currentHp - newHp;

                        // BUG-062 (hardening): un golpe letal anulado en silencio es
                        // exactamente la firma del bug reportado ("inmortal permanente") —
                        // logueamos SIEMPRE que esto dispara, no solo la primera vez, para
                        // que un caso fuera del tutorial (override mal scoped/leakeado) sea
                        // visible en el log del piso donde pasó.
                        Debug.LogWarning(
                            $"[DamagePipeline] Golpe letal anulado por ILethalDamageOverride " +
                            $"(target={ctx.TargetId}, source={ctx.SourceId}): {currentHp}→{newHp} HP " +
                            $"(daño anulado: {finalDamage - ctx.FinalDamage}).");
                    }

                    hpBefore = currentHp;
                    hpAfter = newHp;

                    _attributes.SetAttributeValue<Health, int>(ctx.TargetId, newHp);
                    ctx.WasLethal = newHp <= 0;
                }
                else
                {
                    // BUG-062 (hardening): daño descartado en silencio es otra firma posible
                    // de "inmortalidad permanente" — un target sin Health nunca puede morir
                    // ni bajar de vida. Elevado de Warning a Error: esto nunca debería pasar
                    // para una entidad viva en combate (registro incompleto/roto).
                    Debug.LogError(
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
                IncomingMultiplier = ctx.IncomingMultiplier,
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

            // Stage 2 — weakness (read-only: PeekMultiplier NO dispara OnWeaknessHit).
            float weakMult = 1f;
            if (ctx.IsWeaknessHit && _weaknessChecker != null)
            {
                weakMult = _weaknessChecker.PeekMultiplier(ctx.SourceId, ctx.TargetId, ctx.ComboId);
                if (weakMult > 1f) damage = Mathf.RoundToInt(damage * weakMult);
            }
            ctx.WeaknessMultiplier = weakMult;

            // Stage 3 — incoming multiplier. Va también acá o el preview miente: el jugador vería
            // "30" en el desglose y la barra del jefe bajaría 9.
            damage = ApplyIncomingMultiplier(ctx, damage);

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

        /// <summary>
        /// <b>Regla huérfana, a propósito.</b> "Combo repetido = 0 daño": nadie la llama hoy.
        /// Vivía detrás del pasivo global anti-repetición (A/B), que se eliminó porque su presión
        /// la ejerce ahora el propio boss desde su árbol (<c>AINode_RotateBlock</c>) y porque
        /// anular daño sin aviso en pantalla se sentía como un bug. Se conserva el cálculo —no el
        /// wiring— para cuando se quiera reintroducir la mecánica con UI que la comunique.
        /// <para>
        /// Contrato original: <c>Resolve()</c> corría DESPUÉS de que <c>Record()</c> empujara el
        /// combo del golpe al frente del historial (el "anterior real" queda en el índice 1);
        /// <c>Preview()</c> corría ANTES de confirmarlo (el anterior es directamente
        /// <c>LastCombo</c>). ComboId vacío (ataques sin combo / no-jugador) nunca la activa.
        /// </para>
        /// </summary>
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

        /// <summary>
        /// Stage 3: aplica el multiplicador entrante de <see cref="IIncomingDamageMultiplierProvider"/>
        /// y deja el factor usado en <see cref="DamageContext.IncomingMultiplier"/>. Sin provider
        /// registrado devuelve <paramref name="damage"/> tal cual.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Piso de 1.</b> Con una reducción del 70% un golpe de 3 daría 1 al redondear, pero uno de
        /// 1 daría 0 — y un golpe que muestra 0 se lee como un bug, no como una armadura. El piso sólo
        /// aplica si el daño que entró al stage era positivo: no inventa daño donde no había.
        /// </para>
        /// <para>
        /// Compartido entre <see cref="Resolve"/> y <see cref="Preview"/>: es el único lugar donde vive
        /// la cuenta, así que el desglose que el jugador ve y el número que le baja al jefe no pueden
        /// desfasarse.
        /// </para>
        /// </remarks>
        private static int ApplyIncomingMultiplier(DamageContext ctx, int damage)
        {
            ctx.IncomingMultiplier = 1f;
            if (damage <= 0) return damage;

            if (!ServiceLocator.TryGetService<IIncomingDamageMultiplierProvider>(out var provider)
                || provider == null)
            {
                return damage;
            }

            if (!provider.TryGetMultiplier(ctx.TargetId, out float multiplier)) return damage;
            if (multiplier < 0f) multiplier = 0f;
            if (Mathf.Approximately(multiplier, 1f)) return damage;

            ctx.IncomingMultiplier = multiplier;

            int reduced = Mathf.RoundToInt(damage * multiplier);
            return reduced < 1 ? 1 : reduced;
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
