using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
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

        // ── Fake implementations ─────────────────────────────────────────

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
        }
    }
}
