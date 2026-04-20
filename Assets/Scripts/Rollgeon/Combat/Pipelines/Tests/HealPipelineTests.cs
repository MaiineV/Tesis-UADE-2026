using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.Pipelines;

namespace Rollgeon.Combat.Pipelines.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="HealPipeline"/> (Foundation#0009).
    /// </summary>
    [TestFixture]
    public class HealPipelineTests
    {
        private AttributesManager _attrManager;
        private Guid _sourceId;
        private Guid _targetId;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            TypedEvent<HealResolvedPayload>.Clear();

            _attrManager = new AttributesManager();
            _sourceId = Guid.NewGuid();
            _targetId = Guid.NewGuid();

            var sourceAttrs = new ModifiableAttributes();
            sourceAttrs.EnsureInitialized();
            _attrManager.Register(_sourceId, sourceAttrs);

            var targetAttrs = new ModifiableAttributes();
            targetAttrs.EnsureInitialized();
            targetAttrs.SetAttribute<Health>(new Health(100));
            _attrManager.Register(_targetId, targetAttrs);

            AttributesManager.LogMissingEntityAsWarning = true;
        }

        [TearDown]
        public void TearDown()
        {
            _attrManager.Dispose();
            EventManager.ResetEventDictionary();
            TypedEvent<HealResolvedPayload>.Clear();
        }

        // -- 1. Resolve_ReducesNoHealth_WhenZeroHeal ------------------------------

        [Test]
        public void Resolve_ReducesNoHealth_WhenZeroHeal()
        {
            var pipeline = new HealPipeline(_attrManager);

            var ctx = new HealContext
            {
                SourceId = _sourceId,
                TargetId = _targetId,
                BaseHeal = 0,
            };

            pipeline.Resolve(ctx);

            Assert.AreEqual(0, ctx.FinalHeal, "Zero heal should result in zero final heal.");
            int hp = _attrManager.GetAttribute<Health>(_targetId).Value;
            Assert.AreEqual(100, hp, "Health should be unchanged.");
        }

        // -- 2. Resolve_IncreasesTargetHealth -------------------------------------

        [Test]
        public void Resolve_IncreasesTargetHealth()
        {
            _attrManager.SetAttributeValue<Health, int>(_targetId, 50);
            Func<Guid, int> maxHp = _ => 100;
            var pipeline = new HealPipeline(_attrManager, maxHp);

            var ctx = new HealContext
            {
                SourceId = _sourceId,
                TargetId = _targetId,
                BaseHeal = 30,
            };

            pipeline.Resolve(ctx);

            int hp = _attrManager.GetAttribute<Health>(_targetId).Value;
            Assert.AreEqual(80, hp, "Health should increase by the heal amount.");
            Assert.AreEqual(30, ctx.FinalHeal);
        }

        // -- 3. Resolve_PercentOfMax_ConvertsToAbsolute ---------------------------

        [Test]
        public void Resolve_PercentOfMax_ConvertsToAbsolute()
        {
            _attrManager.SetAttributeValue<Health, int>(_targetId, 40);
            Func<Guid, int> maxHp = _ => 100;
            var pipeline = new HealPipeline(_attrManager, maxHp);

            var ctx = new HealContext
            {
                SourceId = _sourceId,
                TargetId = _targetId,
                BaseHeal = 50,
                IsPercentOfMax = true,
            };

            pipeline.Resolve(ctx);

            int hp = _attrManager.GetAttribute<Health>(_targetId).Value;
            Assert.AreEqual(90, hp, "50% of 100 max HP = 50 heal, 40 + 50 = 90.");
            Assert.AreEqual(50, ctx.FinalHeal);
        }

        // -- 4. Resolve_ClampsToMaxHP ---------------------------------------------

        [Test]
        public void Resolve_ClampsToMaxHP()
        {
            _attrManager.SetAttributeValue<Health, int>(_targetId, 80);
            Func<Guid, int> maxHp = _ => 100;
            var pipeline = new HealPipeline(_attrManager, maxHp);

            var ctx = new HealContext
            {
                SourceId = _sourceId,
                TargetId = _targetId,
                BaseHeal = 60,
            };

            pipeline.Resolve(ctx);

            int hp = _attrManager.GetAttribute<Health>(_targetId).Value;
            Assert.AreEqual(100, hp, "Health should be clamped to max HP.");
            Assert.AreEqual(20, ctx.FinalHeal, "Only 20 HP of headroom should be healed.");
            Assert.IsTrue(ctx.WasClamped, "WasClamped should be true when heal exceeds headroom.");
        }

        // -- 5. Resolve_AlreadyAtMax_HealsZero ------------------------------------

        [Test]
        public void Resolve_AlreadyAtMax_HealsZero()
        {
            Func<Guid, int> maxHp = _ => 100;
            var pipeline = new HealPipeline(_attrManager, maxHp);

            var ctx = new HealContext
            {
                SourceId = _sourceId,
                TargetId = _targetId,
                BaseHeal = 30,
            };

            pipeline.Resolve(ctx);

            int hp = _attrManager.GetAttribute<Health>(_targetId).Value;
            Assert.AreEqual(100, hp, "Health should remain at max.");
            Assert.AreEqual(0, ctx.FinalHeal, "No healing when already at max.");
            Assert.IsTrue(ctx.WasClamped);
        }

        // -- 6. Resolve_FiresHealResolvedPayload ----------------------------------

        [Test]
        public void Resolve_FiresHealResolvedPayload()
        {
            _attrManager.SetAttributeValue<Health, int>(_targetId, 50);
            Func<Guid, int> maxHp = _ => 100;
            var pipeline = new HealPipeline(_attrManager, maxHp);

            HealResolvedPayload? captured = null;
            TypedEvent<HealResolvedPayload>.Subscribe(p => captured = p);

            var ctx = new HealContext
            {
                SourceId = _sourceId,
                TargetId = _targetId,
                BaseHeal = 25,
            };

            pipeline.Resolve(ctx);

            Assert.IsTrue(captured.HasValue, "HealResolvedPayload should have been raised.");
            Assert.AreEqual(_sourceId, captured.Value.SourceGuid);
            Assert.AreEqual(_targetId, captured.Value.TargetGuid);
            Assert.AreEqual(25, captured.Value.FinalHeal);
            Assert.IsFalse(captured.Value.WasPercentBased);
        }

        // -- 7. Resolve_NullContext_Throws ----------------------------------------

        [Test]
        public void Resolve_NullContext_Throws()
        {
            var pipeline = new HealPipeline(_attrManager);

            Assert.Throws<ArgumentNullException>(() => pipeline.Resolve(null));
        }

        // -- 8. Resolve_TargetWithoutHealth_SetsZero ------------------------------

        [Test]
        public void Resolve_TargetWithoutHealth_SetsZero()
        {
            var noHealthId = Guid.NewGuid();
            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            _attrManager.Register(noHealthId, attrs);

            var pipeline = new HealPipeline(_attrManager);

            var ctx = new HealContext
            {
                SourceId = _sourceId,
                TargetId = noHealthId,
                BaseHeal = 30,
            };

            pipeline.Resolve(ctx);

            Assert.AreEqual(0, ctx.FinalHeal, "FinalHeal should be 0 when target has no Health.");
        }

        // -- 9. Resolve_ReturnsContextWithOutputFields ----------------------------

        [Test]
        public void Resolve_ReturnsContextWithOutputFields()
        {
            _attrManager.SetAttributeValue<Health, int>(_targetId, 60);
            Func<Guid, int> maxHp = _ => 100;
            var pipeline = new HealPipeline(_attrManager, maxHp);

            var ctx = new HealContext
            {
                SourceId = _sourceId,
                TargetId = _targetId,
                BaseHeal = 20,
            };

            var result = pipeline.Resolve(ctx);

            Assert.AreSame(ctx, result, "Resolve should return the same HealContext instance.");
            Assert.AreEqual(20, result.FinalHeal);
            Assert.IsFalse(result.WasClamped);
        }

        // -- 10. Resolve_NegativeHeal_TreatedAsZero -------------------------------

        [Test]
        public void Resolve_NegativeHeal_TreatedAsZero()
        {
            var pipeline = new HealPipeline(_attrManager);

            var ctx = new HealContext
            {
                SourceId = _sourceId,
                TargetId = _targetId,
                BaseHeal = -5,
            };

            pipeline.Resolve(ctx);

            Assert.AreEqual(0, ctx.FinalHeal, "Negative heal should be treated as zero.");
            int hp = _attrManager.GetAttribute<Health>(_targetId).Value;
            Assert.AreEqual(100, hp, "Health should be unchanged.");
        }
    }
}
