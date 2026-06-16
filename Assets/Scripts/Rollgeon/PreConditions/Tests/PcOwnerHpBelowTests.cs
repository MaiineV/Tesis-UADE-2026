using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.PreConditions.Concretes;

namespace Rollgeon.PreConditions.Tests
{
    [TestFixture]
    public class PcOwnerHpBelowTests
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
            attrs.SetAttribute<Health>(new Health(100));
            _manager.Register(_ownerId, attrs);
        }

        [TearDown]
        public void TearDown()
        {
            _manager.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private static PreConditionContext Ctx(Guid owner, int? maxHp) =>
            new PreConditionContext { OwnerGuid = owner, OwnerMaxHp = maxHp };

        [Test]
        public void Evaluate_FullHp_ReturnsFalse()
        {
            Assert.IsFalse(new PcOwnerHpBelow { Percent = 0.5f }.Evaluate(Ctx(_ownerId, 100)));
        }

        [Test]
        public void Evaluate_BelowThreshold_ReturnsTrue()
        {
            _manager.Modify<Health, int>(_ownerId, _ => 40);
            Assert.IsTrue(new PcOwnerHpBelow { Percent = 0.5f }.Evaluate(Ctx(_ownerId, 100)));
        }

        [Test]
        public void Evaluate_AtThresholdExactly_ReturnsFalse()
        {
            _manager.Modify<Health, int>(_ownerId, _ => 50);
            Assert.IsFalse(new PcOwnerHpBelow { Percent = 0.5f }.Evaluate(Ctx(_ownerId, 100)));
        }

        [Test]
        public void Evaluate_NoMaxHp_ReturnsFalse()
        {
            _manager.Modify<Health, int>(_ownerId, _ => 10);
            Assert.IsFalse(new PcOwnerHpBelow { Percent = 0.99f }.Evaluate(Ctx(_ownerId, null)));
        }

        [Test]
        public void Evaluate_NoAttributesManager_ReturnsFalse()
        {
            ServiceLocator.Clear();
            Assert.IsFalse(new PcOwnerHpBelow { Percent = 0.99f }.Evaluate(Ctx(_ownerId, 100)));
        }

        [Test]
        public void Evaluate_OwnerWithoutHealth_ReturnsFalse()
        {
            var stranger = Guid.NewGuid();
            Assert.IsFalse(new PcOwnerHpBelow { Percent = 0.5f }.Evaluate(Ctx(stranger, 100)));
        }
    }
}
