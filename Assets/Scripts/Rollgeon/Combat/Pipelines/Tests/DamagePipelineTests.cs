using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.AntiRepeat;
using Rollgeon.Combat.ComboLog;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Weakness;

namespace Rollgeon.Combat.Pipelines.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="DamagePipeline"/> (Foundation#0008).
    /// </summary>
    [TestFixture]
    public class DamagePipelineTests
    {
        private AttributesManager _attrManager;
        private Guid _sourceId;
        private Guid _targetId;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            TypedEvent<DamageResolvedPayload>.Clear();
            ServiceLocator.Clear();

            _attrManager = new AttributesManager();
            _sourceId = Guid.NewGuid();
            _targetId = Guid.NewGuid();

            // Register source (no Health needed for the attacker in these tests).
            var sourceAttrs = new ModifiableAttributes();
            sourceAttrs.EnsureInitialized();
            _attrManager.Register(_sourceId, sourceAttrs);

            // Register target with 100 HP.
            var targetAttrs = new ModifiableAttributes();
            targetAttrs.EnsureInitialized();
            targetAttrs.SetAttribute<Health>(new Health(100));
            _attrManager.Register(_targetId, targetAttrs);

            // Suppress warnings for missing entities in log.
            AttributesManager.LogMissingEntityAsWarning = true;
        }

        [TearDown]
        public void TearDown()
        {
            _attrManager.Dispose();
            EventManager.ResetEventDictionary();
            TypedEvent<DamageResolvedPayload>.Clear();
            ServiceLocator.Clear();
        }

        // ── Repeat-combo guard, gated por Mode (pasivo anti-repetición A/B) ──
        //
        // El zeroing por combo repetido SOLO aplica en Mode Combo. En Mode Dice (o sin
        // servicio registrado) la regla está apagada y el daño pega completo.

        private static void RegisterMode(AntiRepeatMode mode)
        {
            ServiceLocator.AddService<IAntiRepeatModeService>(new FakeAntiRepeatModeService(mode), ServiceScope.Global);
        }

        [Test]
        public void Resolve_ModeCombo_SameComboAsLastTurn_ZeroesDamage()
        {
            RegisterMode(AntiRepeatMode.Combo);
            var comboLog = new ComboLogService();
            comboLog.Register();
            comboLog.Record("combo.par");   // turno anterior
            comboLog.Record("combo.par");   // CombatHandoffService ya registró ESTE golpe antes de Resolve

            var pipeline = new DamagePipeline(_attrManager);
            var ctx = new DamageContext
            {
                SourceId = _sourceId, TargetId = _targetId, BaseDamage = 30,
                Kind = AttackKind.BasicAttack, ComboId = "combo.par",
            };

            pipeline.Resolve(ctx);

            Assert.AreEqual(0, ctx.FinalDamage, "Mode Combo: mismo combo 2 veces seguidas debe anular el daño.");
        }

        [Test]
        public void Resolve_ModeCombo_DifferentComboThanLastTurn_FullDamage()
        {
            RegisterMode(AntiRepeatMode.Combo);
            var comboLog = new ComboLogService();
            comboLog.Register();
            comboLog.Record("combo.doblepar"); // turno anterior
            comboLog.Record("combo.par");      // este golpe (ya registrado)

            var pipeline = new DamagePipeline(_attrManager);
            var ctx = new DamageContext
            {
                SourceId = _sourceId, TargetId = _targetId, BaseDamage = 30,
                Kind = AttackKind.BasicAttack, ComboId = "combo.par",
            };

            pipeline.Resolve(ctx);

            Assert.AreEqual(30, ctx.FinalDamage, "Combo distinto al anterior debe pegar completo.");
        }

        [Test]
        public void Resolve_ModeCombo_SameComboWithDifferentComboInBetween_FullDamage()
        {
            RegisterMode(AntiRepeatMode.Combo);
            var comboLog = new ComboLogService();
            comboLog.Register();
            comboLog.Record("combo.par");       // T1
            comboLog.Record("combo.doblepar");  // T2
            comboLog.Record("combo.par");       // T3 — este golpe

            var pipeline = new DamagePipeline(_attrManager);
            var ctx = new DamageContext
            {
                SourceId = _sourceId, TargetId = _targetId, BaseDamage = 30,
                Kind = AttackKind.BasicAttack, ComboId = "combo.par",
            };

            pipeline.Resolve(ctx);

            Assert.AreEqual(30, ctx.FinalDamage,
                "Par->DoblePar->Par: el anterior inmediato es DoblePar, no Par — debe pegar completo.");
        }

        [Test]
        public void Resolve_ModeCombo_NoComboFallback_NeverZeroed()
        {
            RegisterMode(AntiRepeatMode.Combo);
            var comboLog = new ComboLogService();
            comboLog.Register();
            comboLog.Record(null); // fallback "sin combo"
            comboLog.Record(null); // otro fallback seguido

            var pipeline = new DamagePipeline(_attrManager);
            var ctx = new DamageContext
            {
                SourceId = _sourceId, TargetId = _targetId, BaseDamage = 30,
                Kind = AttackKind.BasicAttack, ComboId = null,
            };

            pipeline.Resolve(ctx);

            Assert.AreEqual(30, ctx.FinalDamage,
                "Ataques sin combo (ComboId null) nunca deben anularse por esta regla.");
        }

        [Test]
        public void Preview_ModeCombo_PredictsRepeatZero_MatchesResolve()
        {
            RegisterMode(AntiRepeatMode.Combo);
            var comboLog = new ComboLogService();
            comboLog.Register();
            comboLog.Record("combo.par"); // último combo confirmado

            var pipeline = new DamagePipeline(_attrManager);

            // Preview: el intento todavía no se confirmó (Record no corrió para él).
            var previewCtx = new DamageContext
            {
                SourceId = _sourceId, TargetId = _targetId, BaseDamage = 30,
                Kind = AttackKind.BasicAttack, ComboId = "combo.par",
            };
            pipeline.Preview(previewCtx);

            // Ahora se confirma de verdad: Record corre, después Resolve.
            comboLog.Record("combo.par");
            var resolveCtx = new DamageContext
            {
                SourceId = _sourceId, TargetId = _targetId, BaseDamage = 30,
                Kind = AttackKind.BasicAttack, ComboId = "combo.par",
            };
            pipeline.Resolve(resolveCtx);

            Assert.AreEqual(0, previewCtx.FinalDamage);
            Assert.AreEqual(0, resolveCtx.FinalDamage);
            Assert.AreEqual(resolveCtx.FinalDamage, previewCtx.FinalDamage,
                "Preview debe predecir exactamente lo que Resolve termina aplicando.");
        }

        [Test]
        public void Resolve_ModeDice_SameComboAsLastTurn_FullDamage()
        {
            RegisterMode(AntiRepeatMode.Dice); // regla de combo repetido APAGADA en Mode Dice
            var comboLog = new ComboLogService();
            comboLog.Register();
            comboLog.Record("combo.par");
            comboLog.Record("combo.par");

            var pipeline = new DamagePipeline(_attrManager);
            var ctx = new DamageContext
            {
                SourceId = _sourceId, TargetId = _targetId, BaseDamage = 30,
                Kind = AttackKind.BasicAttack, ComboId = "combo.par",
            };

            pipeline.Resolve(ctx);

            Assert.AreEqual(30, ctx.FinalDamage,
                "Mode Dice: repetir combo NO debe anular el daño (la regla es exclusiva de Mode Combo).");
        }

        [Test]
        public void Preview_ModeDice_SameComboAsLastTurn_FullDamage()
        {
            RegisterMode(AntiRepeatMode.Dice);
            var comboLog = new ComboLogService();
            comboLog.Register();
            comboLog.Record("combo.par");

            var pipeline = new DamagePipeline(_attrManager);
            var ctx = new DamageContext
            {
                SourceId = _sourceId, TargetId = _targetId, BaseDamage = 30,
                Kind = AttackKind.BasicAttack, ComboId = "combo.par",
            };

            pipeline.Preview(ctx);

            Assert.AreEqual(30, ctx.FinalDamage, "Mode Dice: Preview no debe anular el combo repetido.");
        }

        [Test]
        public void Resolve_NoModeServiceRegistered_SameComboAsLastTurn_FullDamage()
        {
            // Sin IAntiRepeatModeService la regla queda apagada (legacy) — protege los tests
            // del pipeline que no wirean el pasivo.
            var comboLog = new ComboLogService();
            comboLog.Register();
            comboLog.Record("combo.par");
            comboLog.Record("combo.par");

            var pipeline = new DamagePipeline(_attrManager);
            var ctx = new DamageContext
            {
                SourceId = _sourceId, TargetId = _targetId, BaseDamage = 30,
                Kind = AttackKind.BasicAttack, ComboId = "combo.par",
            };

            pipeline.Resolve(ctx);

            Assert.AreEqual(30, ctx.FinalDamage,
                "Sin servicio de modo registrado, la regla de combo repetido debe estar apagada.");
        }

        // ── 1. Apply_ReducesTargetHealth ─────────────────────────────────

        [Test]
        public void Apply_ReducesTargetHealth()
        {
            var pipeline = new DamagePipeline(_attrManager);

            var ctx = new DamageContext
            {
                SourceId = _sourceId,
                TargetId = _targetId,
                BaseDamage = 30,
                Kind = AttackKind.BasicAttack,
            };

            pipeline.Resolve(ctx);

            int hp = _attrManager.GetAttribute<Health>(_targetId).Value;
            Assert.AreEqual(70, hp, "Health should be reduced by the damage amount.");
            Assert.AreEqual(30, ctx.FinalDamage);
        }

        // ── 2. Apply_WithWeakness_MultipliesDamage ───────────────────────

        [Test]
        public void Apply_WithWeakness_MultipliesDamage()
        {
            var weakChecker = new FakeWeaknessChecker(2.0f);
            var pipeline = new DamagePipeline(_attrManager, weakChecker);

            var ctx = new DamageContext
            {
                SourceId = _sourceId,
                TargetId = _targetId,
                BaseDamage = 20,
                IsWeaknessHit = true,
                ComboId = "combo.par",
                Kind = AttackKind.ComboAttack,
            };

            pipeline.Resolve(ctx);

            // 20 * 2.0 = 40
            Assert.AreEqual(40, ctx.FinalDamage, "Damage should be multiplied by weakness.");
            Assert.AreEqual(2.0f, ctx.WeaknessMultiplier);
            int hp = _attrManager.GetAttribute<Health>(_targetId).Value;
            Assert.AreEqual(60, hp);
        }

        // ── 3. Apply_ZeroDamage_NoModifier ───────────────────────────────

        [Test]
        public void Apply_ZeroDamage_NoModifier()
        {
            var pipeline = new DamagePipeline(_attrManager);

            var ctx = new DamageContext
            {
                SourceId = _sourceId,
                TargetId = _targetId,
                BaseDamage = 0,
                Kind = AttackKind.BasicAttack,
            };

            pipeline.Resolve(ctx);

            Assert.AreEqual(0, ctx.FinalDamage, "Zero damage should result in zero final damage.");
            int hp = _attrManager.GetAttribute<Health>(_targetId).Value;
            Assert.AreEqual(100, hp, "Health should be unchanged.");
        }

        // ── 4. Apply_FiresDamageEvent ────────────────────────────────────

        [Test]
        public void Apply_FiresDamageEvent()
        {
            var pipeline = new DamagePipeline(_attrManager);

            DamageResolvedPayload? captured = null;
            TypedEvent<DamageResolvedPayload>.Subscribe(p => captured = p);

            var ctx = new DamageContext
            {
                SourceId = _sourceId,
                TargetId = _targetId,
                BaseDamage = 15,
                Kind = AttackKind.BasicAttack,
            };

            pipeline.Resolve(ctx);

            Assert.IsTrue(captured.HasValue, "DamageResolvedPayload should have been raised.");
            Assert.AreEqual(_sourceId, captured.Value.SourceGuid);
            Assert.AreEqual(_targetId, captured.Value.TargetGuid);
            Assert.AreEqual(15, captured.Value.FinalDamage);
            Assert.IsFalse(captured.Value.WeaknessHit);
        }

        // ── 5. Apply_ReturnsDamageContext ────────────────────────────────

        [Test]
        public void Apply_ReturnsDamageContext()
        {
            var pipeline = new DamagePipeline(_attrManager);

            var ctx = new DamageContext
            {
                SourceId = _sourceId,
                TargetId = _targetId,
                BaseDamage = 50,
                Kind = AttackKind.ComboAttack,
            };

            var result = pipeline.Resolve(ctx);

            Assert.AreSame(ctx, result, "Resolve should return the same DamageContext instance.");
            Assert.AreEqual(50, result.FinalDamage);
            Assert.AreEqual(1f, result.WeaknessMultiplier);
            Assert.IsFalse(result.WasLethal);
        }

        // ── Extra: lethal damage ─────────────────────────────────────────

        [Test]
        public void Apply_LethalDamage_SetsWasLethalAndClampsToZero()
        {
            var pipeline = new DamagePipeline(_attrManager);

            var ctx = new DamageContext
            {
                SourceId = _sourceId,
                TargetId = _targetId,
                BaseDamage = 150,
                Kind = AttackKind.BasicAttack,
            };

            pipeline.Resolve(ctx);

            int hp = _attrManager.GetAttribute<Health>(_targetId).Value;
            Assert.AreEqual(0, hp, "Health should be clamped to 0.");
            Assert.IsTrue(ctx.WasLethal, "WasLethal should be true when HP reaches 0.");
            Assert.AreEqual(150, ctx.FinalDamage);
        }

        // ── Extra: fires legacy OnDamageOutgoing/Incoming events ─────────

        [Test]
        public void Apply_FiresLegacyDamageEvents()
        {
            var pipeline = new DamagePipeline(_attrManager);

            bool outgoingFired = false;
            bool incomingFired = false;
            EventManager.Subscribe(EventName.OnDamageOutgoing, (args) => outgoingFired = true);
            EventManager.Subscribe(EventName.OnDamageIncoming, (args) => incomingFired = true);

            var ctx = new DamageContext
            {
                SourceId = _sourceId,
                TargetId = _targetId,
                BaseDamage = 10,
                Kind = AttackKind.BasicAttack,
            };

            pipeline.Resolve(ctx);

            Assert.IsTrue(outgoingFired, "OnDamageOutgoing should have been triggered.");
            Assert.IsTrue(incomingFired, "OnDamageIncoming should have been triggered.");
        }

        // ── Shield absorption ────────────────────────────────────────────

        [Test]
        public void Shield_PartialAbsorption_ReducesDamageAndShield()
        {
            _attrManager.GetAttributes(_targetId).SetAttribute<Shield>(new Shield(20));
            var pipeline = new DamagePipeline(_attrManager);

            var ctx = new DamageContext
            {
                SourceId = _sourceId,
                TargetId = _targetId,
                BaseDamage = 50,
                Kind = AttackKind.BasicAttack,
            };

            pipeline.Resolve(ctx);

            Assert.AreEqual(30, ctx.FinalDamage);
            Assert.AreEqual(20, ctx.ShieldAbsorbed);
            Assert.IsFalse(ctx.BlockedByShield);
            Assert.AreEqual(0, _attrManager.GetAttribute<Shield>(_targetId).Value);
            Assert.AreEqual(70, _attrManager.GetAttribute<Health>(_targetId).Value);
        }

        [Test]
        public void Shield_FullAbsorption_ZeroDamageToHealth()
        {
            _attrManager.GetAttributes(_targetId).SetAttribute<Shield>(new Shield(50));
            var pipeline = new DamagePipeline(_attrManager);

            var ctx = new DamageContext
            {
                SourceId = _sourceId,
                TargetId = _targetId,
                BaseDamage = 30,
                Kind = AttackKind.BasicAttack,
            };

            pipeline.Resolve(ctx);

            Assert.AreEqual(0, ctx.FinalDamage);
            Assert.AreEqual(30, ctx.ShieldAbsorbed);
            Assert.IsTrue(ctx.BlockedByShield);
            Assert.AreEqual(20, _attrManager.GetAttribute<Shield>(_targetId).Value);
            Assert.AreEqual(100, _attrManager.GetAttribute<Health>(_targetId).Value);
        }

        [Test]
        public void Shield_Zero_NothingAbsorbed()
        {
            _attrManager.GetAttributes(_targetId).SetAttribute<Shield>(new Shield(0));
            var pipeline = new DamagePipeline(_attrManager);

            var ctx = new DamageContext
            {
                SourceId = _sourceId,
                TargetId = _targetId,
                BaseDamage = 25,
                Kind = AttackKind.BasicAttack,
            };

            pipeline.Resolve(ctx);

            Assert.AreEqual(25, ctx.FinalDamage);
            Assert.AreEqual(0, ctx.ShieldAbsorbed);
            Assert.IsFalse(ctx.BlockedByShield);
            Assert.AreEqual(75, _attrManager.GetAttribute<Health>(_targetId).Value);
        }

        [Test]
        public void Shield_FiresOnShieldChangedEvent()
        {
            _attrManager.GetAttributes(_targetId).SetAttribute<Shield>(new Shield(10));
            var pipeline = new DamagePipeline(_attrManager);

            int capturedShield = -1;
            EventManager.Subscribe(EventName.OnShieldChanged, args =>
            {
                capturedShield = (int)args[1];
            });

            var ctx = new DamageContext
            {
                SourceId = _sourceId,
                TargetId = _targetId,
                BaseDamage = 7,
                Kind = AttackKind.BasicAttack,
            };

            pipeline.Resolve(ctx);

            Assert.AreEqual(3, capturedShield, "OnShieldChanged should fire with remaining shield.");
        }

        [Test]
        public void Shield_PayloadCarriesShieldAbsorbed()
        {
            _attrManager.GetAttributes(_targetId).SetAttribute<Shield>(new Shield(15));
            var pipeline = new DamagePipeline(_attrManager);

            DamageResolvedPayload? captured = null;
            TypedEvent<DamageResolvedPayload>.Subscribe(p => captured = p);

            var ctx = new DamageContext
            {
                SourceId = _sourceId,
                TargetId = _targetId,
                BaseDamage = 10,
                Kind = AttackKind.BasicAttack,
            };

            pipeline.Resolve(ctx);

            Assert.IsTrue(captured.HasValue);
            Assert.AreEqual(10, captured.Value.ShieldAbsorbed);
            Assert.AreEqual(0, captured.Value.FinalDamage);
        }

        // ── Preview (read-only) ──────────────────────────────────────────

        [Test]
        public void Preview_WithShield_ComputesFinalDamage_WithoutMutatingShieldOrHealth()
        {
            _attrManager.GetAttributes(_targetId).SetAttribute<Shield>(new Shield(20));
            var pipeline = new DamagePipeline(_attrManager);

            var ctx = new DamageContext
            {
                SourceId = _sourceId,
                TargetId = _targetId,
                BaseDamage = 50,
                Kind = AttackKind.BasicAttack,
            };

            pipeline.Preview(ctx);

            // Mismo resultado que Resolve (50 - 20 escudo = 30) pero SIN escribir estado.
            Assert.AreEqual(30, ctx.FinalDamage);
            Assert.AreEqual(20, ctx.ShieldAbsorbed);
            Assert.AreEqual(20, _attrManager.GetAttribute<Shield>(_targetId).Value,
                "Preview no debe consumir el escudo del target.");
            Assert.AreEqual(100, _attrManager.GetAttribute<Health>(_targetId).Value,
                "Preview no debe tocar el Health del target.");
        }

        [Test]
        public void Preview_WithWeakness_UsesPeekMultiplier_WithoutFiringOnWeaknessHit()
        {
            var weakChecker = new FakeWeaknessChecker(2.0f);
            var pipeline = new DamagePipeline(_attrManager, weakChecker);

            bool weaknessHitFired = false;
            EventManager.Subscribe(EventName.OnWeaknessHit, _ => weaknessHitFired = true);

            var ctx = new DamageContext
            {
                SourceId = _sourceId,
                TargetId = _targetId,
                BaseDamage = 20,
                IsWeaknessHit = true,
                ComboId = "combo.par",
                Kind = AttackKind.ComboAttack,
            };

            pipeline.Preview(ctx);

            Assert.AreEqual(40, ctx.FinalDamage, "Preview aplica el multiplicador de weakness.");
            Assert.AreEqual(2.0f, ctx.WeaknessMultiplier);
            Assert.IsFalse(weaknessHitFired, "Preview NO debe disparar OnWeaknessHit.");
            Assert.AreEqual(100, _attrManager.GetAttribute<Health>(_targetId).Value);
        }

        [Test]
        public void Preview_MatchesResolveFinalDamage_ForSameInput()
        {
            _attrManager.GetAttributes(_targetId).SetAttribute<Shield>(new Shield(12));
            var pipeline = new DamagePipeline(_attrManager);

            var previewCtx = new DamageContext
            {
                SourceId = _sourceId, TargetId = _targetId, BaseDamage = 40,
                Kind = AttackKind.BasicAttack,
            };
            pipeline.Preview(previewCtx);

            var resolveCtx = new DamageContext
            {
                SourceId = _sourceId, TargetId = _targetId, BaseDamage = 40,
                Kind = AttackKind.BasicAttack,
            };
            pipeline.Resolve(resolveCtx);

            Assert.AreEqual(resolveCtx.FinalDamage, previewCtx.FinalDamage,
                "Preview y Resolve deben coincidir en FinalDamage para el mismo input.");
        }

        [Test]
        public void Preview_DoesNotFireDamageResolvedEvent()
        {
            var pipeline = new DamagePipeline(_attrManager);

            bool anyPayload = false;
            TypedEvent<DamageResolvedPayload>.Subscribe(_ => anyPayload = true);

            var ctx = new DamageContext
            {
                SourceId = _sourceId, TargetId = _targetId, BaseDamage = 30,
                Kind = AttackKind.BasicAttack,
            };
            pipeline.Preview(ctx);

            Assert.IsFalse(anyPayload, "Preview no debe emitir DamageResolvedPayload.");
        }

        // ── Fake implementations ─────────────────────────────────────────

        private sealed class FakeAntiRepeatModeService : IAntiRepeatModeService
        {
            public FakeAntiRepeatModeService(AntiRepeatMode mode) => Mode = mode;
            public AntiRepeatMode Mode { get; private set; }
            public void SetMode(AntiRepeatMode mode) => Mode = mode;
        }

        private class FakeWeaknessChecker : IWeaknessChecker
        {
            private readonly float _multiplier;

            public FakeWeaknessChecker(float multiplier)
            {
                _multiplier = multiplier;
            }

            public float GetMultiplier(Guid attacker, Guid target, string matchedComboId)
            {
                return _multiplier;
            }

            public float PeekMultiplier(Guid attacker, Guid target, string matchedComboId)
            {
                return _multiplier;
            }
        }
    }
}
