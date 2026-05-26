using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.PreConditions.Concretes;

namespace Rollgeon.PreConditions.Tests
{
    [TestFixture]
    public class PcOwnerStatCompareTests
    {
        private AttributesManager _manager;
        private Guid _ownerId;

        [SetUp]
        public void Setup()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _manager = new AttributesManager();
            ServiceLocator.AddService<AttributesManager>(_manager);
            _ownerId = Guid.NewGuid();

            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Energy>(new Energy(5));
            attrs.SetAttribute<Health>(new Health(50));
            _manager.Register(_ownerId, attrs);
        }

        [TearDown]
        public void TearDown()
        {
            _manager.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private PreConditionContext Ctx() => new PreConditionContext
        {
            OwnerGuid = _ownerId,
            Attributes = _manager,
        };

        [Test]
        public void Evaluate_AllOperators_BehaveConsistently()
        {
            // Arrange — Energy = 5
            var ctx = Ctx();

            // Act + Assert
            Assert.IsTrue(new PcOwnerStatCompare  { Stat = StatType.Energy, Comparison = IntComparison.Equal,          Value = 5 }.Evaluate(ctx));
            Assert.IsFalse(new PcOwnerStatCompare { Stat = StatType.Energy, Comparison = IntComparison.Equal,          Value = 6 }.Evaluate(ctx));
            Assert.IsTrue(new PcOwnerStatCompare  { Stat = StatType.Energy, Comparison = IntComparison.NotEqual,       Value = 6 }.Evaluate(ctx));
            Assert.IsFalse(new PcOwnerStatCompare { Stat = StatType.Energy, Comparison = IntComparison.NotEqual,       Value = 5 }.Evaluate(ctx));
            Assert.IsTrue(new PcOwnerStatCompare  { Stat = StatType.Energy, Comparison = IntComparison.Less,           Value = 6 }.Evaluate(ctx));
            Assert.IsFalse(new PcOwnerStatCompare { Stat = StatType.Energy, Comparison = IntComparison.Less,           Value = 5 }.Evaluate(ctx));
            Assert.IsTrue(new PcOwnerStatCompare  { Stat = StatType.Energy, Comparison = IntComparison.LessOrEqual,    Value = 5 }.Evaluate(ctx));
            Assert.IsFalse(new PcOwnerStatCompare { Stat = StatType.Energy, Comparison = IntComparison.LessOrEqual,    Value = 4 }.Evaluate(ctx));
            Assert.IsTrue(new PcOwnerStatCompare  { Stat = StatType.Energy, Comparison = IntComparison.Greater,        Value = 4 }.Evaluate(ctx));
            Assert.IsFalse(new PcOwnerStatCompare { Stat = StatType.Energy, Comparison = IntComparison.Greater,        Value = 5 }.Evaluate(ctx));
            Assert.IsTrue(new PcOwnerStatCompare  { Stat = StatType.Energy, Comparison = IntComparison.GreaterOrEqual, Value = 5 }.Evaluate(ctx));
            Assert.IsFalse(new PcOwnerStatCompare { Stat = StatType.Energy, Comparison = IntComparison.GreaterOrEqual, Value = 6 }.Evaluate(ctx));
        }

        [Test]
        public void Evaluate_DifferentStat_ReadsCorrectAttribute()
        {
            // Arrange — Energy = 5, Health = 50
            var ctx = Ctx();

            // Act + Assert — chequea que el switch routea al stat correcto
            Assert.IsTrue(
                new PcOwnerStatCompare { Stat = StatType.Health, Comparison = IntComparison.Equal, Value = 50 }.Evaluate(ctx));
            Assert.IsFalse(
                new PcOwnerStatCompare { Stat = StatType.Health, Comparison = IntComparison.Equal, Value = 5 }.Evaluate(ctx));
        }

        [Test]
        public void Evaluate_NullContext_ReturnsTruePermissively()
        {
            // Arrange
            var pc = new PcOwnerStatCompare
            {
                Stat = StatType.Energy,
                Comparison = IntComparison.Greater,
                Value = 100,
            };

            // Act + Assert — sin contexto no podemos evaluar, no vetamos (ver doc-comment)
            Assert.IsTrue(pc.Evaluate(null));
        }

        [Test]
        public void Evaluate_NullAttributes_ReturnsTruePermissively()
        {
            // Arrange — context sin Attributes (caller no AI: hero UI / effects pipeline)
            var ctx = new PreConditionContext { OwnerGuid = _ownerId, Attributes = null };
            var pc = new PcOwnerStatCompare
            {
                Stat = StatType.Energy,
                Comparison = IntComparison.Greater,
                Value = 100,
            };

            // Act + Assert
            Assert.IsTrue(pc.Evaluate(ctx));
        }

        [Test]
        public void Evaluate_EmptyOwnerGuid_ReturnsTruePermissively()
        {
            // Arrange
            var ctx = new PreConditionContext { OwnerGuid = Guid.Empty, Attributes = _manager };
            var pc = new PcOwnerStatCompare
            {
                Stat = StatType.Energy,
                Comparison = IntComparison.Greater,
                Value = 100,
            };

            // Act + Assert
            Assert.IsTrue(pc.Evaluate(ctx));
        }
    }
}
