using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Modifiers;
using Rollgeon.PreConditions.Concretes;

namespace Rollgeon.PreConditions.Tests
{
    [TestFixture]
    public class PCHasIntAttributeTests
    {
        private AttributesManager _manager;
        private Guid _ownerId;

        [SetUp]
        public void Setup()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            OperationResolver.ClearCache();

            _manager = new AttributesManager();
            ServiceLocator.AddService<AttributesManager>(_manager);
            _ownerId = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            _manager.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private void RegisterEnergy(int value)
        {
            var attrs = new ModifiableAttributes();
            attrs.SetAttribute<TestEnergy>(new TestEnergy(value));
            _manager.Register(_ownerId, attrs);
        }

        private static PreConditionContext Ctx(Guid owner) => new PreConditionContext { OwnerGuid = owner };

        [Test]
        public void Evaluate_GreaterOrEqual_PassesWhenCurrentMeetsThreshold()
        {
            RegisterEnergy(5);
            var pc = new PCHasIntAttribute
            {
                AttributeType = typeof(TestEnergy),
                Comparison = IntComparison.GreaterOrEqual,
                Value = 3,
            };
            Assert.IsTrue(pc.Evaluate(Ctx(_ownerId)));
        }

        [Test]
        public void Evaluate_GreaterOrEqual_FailsWhenBelowThreshold()
        {
            RegisterEnergy(2);
            var pc = new PCHasIntAttribute
            {
                AttributeType = typeof(TestEnergy),
                Comparison = IntComparison.GreaterOrEqual,
                Value = 3,
            };
            Assert.IsFalse(pc.Evaluate(Ctx(_ownerId)));
        }

        [Test]
        public void Evaluate_AllOperators_BehaveConsistently()
        {
            RegisterEnergy(5);
            var ctx = Ctx(_ownerId);

            Assert.IsTrue(new PCHasIntAttribute  { AttributeType = typeof(TestEnergy), Comparison = IntComparison.Equal,           Value = 5 }.Evaluate(ctx));
            Assert.IsFalse(new PCHasIntAttribute { AttributeType = typeof(TestEnergy), Comparison = IntComparison.Equal,           Value = 6 }.Evaluate(ctx));
            Assert.IsTrue(new PCHasIntAttribute  { AttributeType = typeof(TestEnergy), Comparison = IntComparison.NotEqual,        Value = 6 }.Evaluate(ctx));
            Assert.IsTrue(new PCHasIntAttribute  { AttributeType = typeof(TestEnergy), Comparison = IntComparison.Less,            Value = 6 }.Evaluate(ctx));
            Assert.IsFalse(new PCHasIntAttribute { AttributeType = typeof(TestEnergy), Comparison = IntComparison.Less,            Value = 5 }.Evaluate(ctx));
            Assert.IsTrue(new PCHasIntAttribute  { AttributeType = typeof(TestEnergy), Comparison = IntComparison.LessOrEqual,     Value = 5 }.Evaluate(ctx));
            Assert.IsTrue(new PCHasIntAttribute  { AttributeType = typeof(TestEnergy), Comparison = IntComparison.Greater,         Value = 4 }.Evaluate(ctx));
            Assert.IsFalse(new PCHasIntAttribute { AttributeType = typeof(TestEnergy), Comparison = IntComparison.Greater,         Value = 5 }.Evaluate(ctx));
            Assert.IsTrue(new PCHasIntAttribute  { AttributeType = typeof(TestEnergy), Comparison = IntComparison.GreaterOrEqual,  Value = 5 }.Evaluate(ctx));
        }

        [Test]
        public void Evaluate_UseModifiedValue_AppliesIntrinsicModifiers()
        {
            // Stat raw=5, mod intrinsico +10 → modified=15.
            var attrs = new ModifiableAttributes();
            var energy = new TestEnergy(5);
            attrs.SetAttribute<TestEnergy>(energy);
            _manager.Register(_ownerId, attrs);

            var mod = new Modifier<int>(
                amount: 10,
                op: ModifierOperation.Add,
                duration: 0,
                carrierId: _ownerId,
                sourceId: Guid.Empty,
                dir: ModifierDirection.Intrinsic,
                lifetime: ModifierLifetime.Permanent,
                tickEvent: EventName.OnTurnStarted);
            energy.AddModifier<int>(mod);

            var rawCheck = new PCHasIntAttribute
            {
                AttributeType = typeof(TestEnergy),
                Comparison = IntComparison.GreaterOrEqual,
                Value = 10,
                UseModifiedValue = false,
            };
            Assert.IsFalse(rawCheck.Evaluate(Ctx(_ownerId)),
                "Raw value (5) no debe pasar el chequeo >=10.");

            var modCheck = new PCHasIntAttribute
            {
                AttributeType = typeof(TestEnergy),
                Comparison = IntComparison.GreaterOrEqual,
                Value = 10,
                UseModifiedValue = true,
            };
            Assert.IsTrue(modCheck.Evaluate(Ctx(_ownerId)),
                "Modified value (5+10) debe pasar el chequeo >=10.");
        }

        [Test]
        public void Evaluate_NullContext_ReturnsFalse()
        {
            var pc = new PCHasIntAttribute { AttributeType = typeof(TestEnergy), Value = 0 };
            Assert.IsFalse(pc.Evaluate(null));
        }

        [Test]
        public void Evaluate_NullAttributeType_ReturnsFalse()
        {
            RegisterEnergy(10);
            var pc = new PCHasIntAttribute { AttributeType = null, Value = 0 };
            Assert.IsFalse(pc.Evaluate(Ctx(_ownerId)));
        }

        [Test]
        public void Evaluate_OwnerNotRegistered_ReturnsFalse()
        {
            var pc = new PCHasIntAttribute
            {
                AttributeType = typeof(TestEnergy),
                Comparison = IntComparison.GreaterOrEqual,
                Value = 0,
            };
            Assert.IsFalse(pc.Evaluate(Ctx(Guid.NewGuid())));
        }

        [Test]
        public void Evaluate_StatNotPresentOnEntity_ReturnsFalse()
        {
            // Owner registrado pero sin TestEnergy.
            var attrs = new ModifiableAttributes();
            attrs.SetAttribute<TestHealth>(new TestHealth(100));
            _manager.Register(_ownerId, attrs);

            var pc = new PCHasIntAttribute
            {
                AttributeType = typeof(TestEnergy),
                Comparison = IntComparison.GreaterOrEqual,
                Value = 0,
            };
            Assert.IsFalse(pc.Evaluate(Ctx(_ownerId)));
        }
    }
}
