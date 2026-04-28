using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;

namespace Rollgeon.Combat.Tests
{
    [TestFixture]
    public class ShieldResetHandlerTests
    {
        private AttributesManager _attrManager;
        private Guid _entityId;
        private ShieldResetHandler _handler;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();

            _attrManager = new AttributesManager();
            _entityId = Guid.NewGuid();

            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(100));
            attrs.SetAttribute<Shield>(new Shield(0));
            _attrManager.Register(_entityId, attrs);

            _handler = new ShieldResetHandler(_attrManager);
            AttributesManager.LogMissingEntityAsWarning = true;
        }

        [TearDown]
        public void TearDown()
        {
            _handler.Dispose();
            _attrManager.Dispose();
            EventManager.ResetEventDictionary();
        }

        [Test]
        public void OnTurnStarted_ResetsShieldToZero()
        {
            _attrManager.SetAttributeValue<Shield, int>(_entityId, 15);

            EventManager.Trigger(EventName.OnTurnStarted, _entityId);

            Assert.AreEqual(0, _attrManager.GetAttribute<Shield>(_entityId).Value);
        }

        [Test]
        public void OnTurnStarted_FiresOnShieldChanged_WhenShieldWasPositive()
        {
            _attrManager.SetAttributeValue<Shield, int>(_entityId, 10);

            int capturedShield = -1;
            EventManager.Subscribe(EventName.OnShieldChanged, args =>
            {
                capturedShield = (int)args[1];
            });

            EventManager.Trigger(EventName.OnTurnStarted, _entityId);

            Assert.AreEqual(0, capturedShield);
        }

        [Test]
        public void OnTurnStarted_DoesNothing_WhenShieldAlreadyZero()
        {
            bool shieldChangedFired = false;
            EventManager.Subscribe(EventName.OnShieldChanged, args =>
            {
                shieldChangedFired = true;
            });

            EventManager.Trigger(EventName.OnTurnStarted, _entityId);

            Assert.IsFalse(shieldChangedFired);
            Assert.AreEqual(0, _attrManager.GetAttribute<Shield>(_entityId).Value);
        }

        [Test]
        public void Dispose_Unsubscribes_NoResetAfterDispose()
        {
            _attrManager.SetAttributeValue<Shield, int>(_entityId, 20);
            _handler.Dispose();

            EventManager.Trigger(EventName.OnTurnStarted, _entityId);

            Assert.AreEqual(20, _attrManager.GetAttribute<Shield>(_entityId).Value,
                "Shield should remain after handler is disposed.");
        }
    }
}
