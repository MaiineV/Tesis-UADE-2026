using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;

namespace Rollgeon.Combat.Tests
{
    /// <summary>
    /// Tests de <see cref="ShieldPersistenceService"/> + el hook en
    /// <see cref="ShieldResetHandler"/> (Feature#0084, Coin Shield cara par): un escudo
    /// marcado sobrevive EXACTAMENTE un <c>OnTurnStarted</c>, y el siguiente ya resetea normal.
    /// </summary>
    [TestFixture]
    public class ShieldPersistenceTests
    {
        private AttributesManager _attrManager;
        private Guid _entityId;
        private ShieldPersistenceService _persistence;
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

            _persistence = new ShieldPersistenceService();
            _handler = new ShieldResetHandler(_attrManager, _persistence);
        }

        [TearDown]
        public void TearDown()
        {
            _handler.Dispose();
            _persistence.Dispose();
            _attrManager.Dispose();
            EventManager.ResetEventDictionary();
        }

        [Test]
        public void PersistedShield_SurvivesExactlyOneTurnStarted_ThenResetsNext()
        {
            _attrManager.SetAttributeValue<Shield, int>(_entityId, 15);
            _persistence.PersistThroughNextReset(_entityId);

            EventManager.Trigger(EventName.OnTurnStarted, _entityId);
            Assert.AreEqual(15, _attrManager.GetAttribute<Shield>(_entityId).Value, "primer turno: marca consumida, sin reset.");
            Assert.IsFalse(_persistence.IsPersisted(_entityId), "la marca se consume, no se reusa.");

            EventManager.Trigger(EventName.OnTurnStarted, _entityId);
            Assert.AreEqual(0, _attrManager.GetAttribute<Shield>(_entityId).Value, "segundo turno: ya resetea normal.");
        }

        [Test]
        public void NonPersistedShield_ResetsAsBefore()
        {
            _attrManager.SetAttributeValue<Shield, int>(_entityId, 10);

            EventManager.Trigger(EventName.OnTurnStarted, _entityId);

            Assert.AreEqual(0, _attrManager.GetAttribute<Shield>(_entityId).Value);
        }

        [Test]
        public void CombatEnd_ClearsPersistenceMarks()
        {
            _persistence.PersistThroughNextReset(_entityId);

            EventManager.Trigger(EventName.OnCombatEnd);

            Assert.IsFalse(_persistence.IsPersisted(_entityId));
        }

        [Test]
        public void PersistThroughNextReset_FiresOnShieldPersisted()
        {
            bool fired = false;
            Guid captured = Guid.Empty;
            EventManager.Subscribe(EventName.OnShieldPersisted, args =>
            {
                fired = true;
                captured = (Guid)args[0];
            });

            _persistence.PersistThroughNextReset(_entityId);

            Assert.IsTrue(fired);
            Assert.AreEqual(_entityId, captured);
        }
    }
}
