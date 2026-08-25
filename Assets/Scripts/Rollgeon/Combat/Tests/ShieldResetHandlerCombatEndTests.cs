using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.FSM;

namespace Rollgeon.Combat.Tests
{
    /// <summary>
    /// BUG-062 (hardening): <see cref="ShieldResetHandler"/> solo reseteaba escudo en
    /// <c>OnTurnStarted</c> del dueño — un escudo residual que sobrevive al cierre del
    /// combate (ronda cortada por Aborted/Defeat/Victory antes de que ese turno volviera a
    /// empezar) podía quedar pegado como buff permanente. Este archivo cubre el reset de
    /// seguridad nuevo en <c>OnCombatEnd</c>, separado de <see cref="ShieldResetHandlerTests"/>
    /// (que cubre <c>OnTurnStarted</c>) para no tocar ese archivo existente.
    /// </summary>
    [TestFixture]
    public class ShieldResetHandlerCombatEndTests
    {
        private AttributesManager _attrManager;
        private Guid _playerId;
        private Guid _enemyId;
        private ShieldResetHandler _handler;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();

            _attrManager = new AttributesManager();
            _playerId = Guid.NewGuid();
            _enemyId = Guid.NewGuid();

            RegisterEntity(_playerId);
            RegisterEntity(_enemyId);

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

        private void RegisterEntity(Guid id)
        {
            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(100));
            attrs.SetAttribute<Shield>(new Shield(0));
            _attrManager.Register(id, attrs);
        }

        [Test]
        public void OnCombatEnd_ResetsShieldToZero_ForEveryRegisteredEntity()
        {
            // Arrange
            _attrManager.SetAttributeValue<Shield, int>(_playerId, 12);
            _attrManager.SetAttributeValue<Shield, int>(_enemyId, 8);

            // Act
            EventManager.Trigger(EventName.OnCombatEnd, Guid.NewGuid(), CombatOutcome.Aborted);

            // Assert
            Assert.AreEqual(0, _attrManager.GetAttribute<Shield>(_playerId).Value,
                "un escudo residual del player no debe sobrevivir al cierre del combate.");
            Assert.AreEqual(0, _attrManager.GetAttribute<Shield>(_enemyId).Value,
                "el barrido de OnCombatEnd cubre a todas las entidades registradas, no solo al player.");
        }

        [Test]
        public void OnCombatEnd_FiresOnShieldChanged_OnlyForEntitiesThatHadShield()
        {
            // Arrange — solo el player tiene escudo residual.
            _attrManager.SetAttributeValue<Shield, int>(_playerId, 5);

            int changedCount = 0;
            EventManager.Subscribe(EventName.OnShieldChanged, args => changedCount++);

            // Act
            EventManager.Trigger(EventName.OnCombatEnd, Guid.NewGuid(), CombatOutcome.Victory);

            // Assert
            Assert.AreEqual(1, changedCount,
                "resetear un escudo ya en 0 (el enemigo) no debe emitir OnShieldChanged.");
        }

        [Test]
        public void OnCombatEnd_DoesNothing_WhenNoShieldsArePositive()
        {
            bool shieldChangedFired = false;
            EventManager.Subscribe(EventName.OnShieldChanged, args => shieldChangedFired = true);

            EventManager.Trigger(EventName.OnCombatEnd, Guid.NewGuid(), CombatOutcome.Defeat);

            Assert.IsFalse(shieldChangedFired);
        }

        [Test]
        public void Dispose_Unsubscribes_NoResetOnCombatEndAfterDispose()
        {
            _attrManager.SetAttributeValue<Shield, int>(_playerId, 15);
            _handler.Dispose();

            EventManager.Trigger(EventName.OnCombatEnd, Guid.NewGuid(), CombatOutcome.Aborted);

            Assert.AreEqual(15, _attrManager.GetAttribute<Shield>(_playerId).Value,
                "Shield should remain after handler is disposed.");
        }
    }
}
