using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.PreConditions.Concretes;

namespace Rollgeon.PreConditions.Tests
{
    /// <summary>
    /// <see cref="PcOwnerShieldAtLeast"/> (Feature#0085, Coin Shield): "no puede usarse con
    /// 0 de escudo".
    /// </summary>
    [TestFixture]
    public class PcOwnerShieldAtLeastTests
    {
        private AttributesManager _attrs;
        private Guid _owner;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _attrs = new AttributesManager();
            ServiceLocator.AddService<AttributesManager>(_attrs, ServiceScope.Global);

            _owner = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            _attrs?.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private void RegisterShield(int amount)
        {
            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Shield>(new Shield(amount));
            _attrs.Register(_owner, attrs);
        }

        [Test]
        public void test_evaluate_shieldZero_defaultMinOne_returnsFalse()
        {
            RegisterShield(0);
            var pc = new PcOwnerShieldAtLeast { Min = 1 };

            var result = pc.Evaluate(new PreConditionContext { OwnerGuid = _owner });

            Assert.IsFalse(result);
        }

        [Test]
        public void test_evaluate_shieldAtMin_returnsTrue()
        {
            RegisterShield(1);
            var pc = new PcOwnerShieldAtLeast { Min = 1 };

            var result = pc.Evaluate(new PreConditionContext { OwnerGuid = _owner });

            Assert.IsTrue(result);
        }

        [Test]
        public void test_evaluate_shieldAboveMin_returnsTrue()
        {
            RegisterShield(10);
            var pc = new PcOwnerShieldAtLeast { Min = 1 };

            var result = pc.Evaluate(new PreConditionContext { OwnerGuid = _owner });

            Assert.IsTrue(result);
        }

        [Test]
        public void test_evaluate_ownerNotRegistered_returnsFalse()
        {
            var pc = new PcOwnerShieldAtLeast { Min = 1 };

            var result = pc.Evaluate(new PreConditionContext { OwnerGuid = Guid.NewGuid() });

            Assert.IsFalse(result);
        }

        [Test]
        public void test_evaluate_ownerGuidEmpty_returnsFalse()
        {
            var pc = new PcOwnerShieldAtLeast { Min = 1 };

            var result = pc.Evaluate(new PreConditionContext { OwnerGuid = Guid.Empty });

            Assert.IsFalse(result);
        }
    }
}
