using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.FirstRoll;
using Rollgeon.PreConditions.Concretes;

namespace Rollgeon.PreConditions.Tests
{
    [TestFixture]
    public class FirstRollTrackerServiceTests
    {
        private FirstRollTrackerService _tracker;
        private Guid _entityA;
        private Guid _entityB;
        private Guid _roomId;

        [SetUp]
        public void Setup()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _tracker = new FirstRollTrackerService();
            _tracker.Register();

            _entityA = Guid.NewGuid();
            _entityB = Guid.NewGuid();
            _roomId = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            _tracker.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        [Test]
        public void Register_RegistersUnderInterface()
        {
            Assert.IsTrue(ServiceLocator.TryGetService<IFirstRollTracker>(out var resolved));
            Assert.AreSame(_tracker, resolved);
        }

        [Test]
        public void IsFirstRoll_OutsideCombat_ReturnsFalse()
        {
            Assert.IsFalse(_tracker.IsFirstRoll(_entityA),
                "Sin OnCombatStart, IsFirstRoll debe retornar false.");
        }

        [Test]
        public void IsFirstRoll_AfterCombatStart_ReturnsTrue()
        {
            EventManager.Trigger(EventName.OnCombatStart, _roomId);
            Assert.IsTrue(_tracker.IsFirstRoll(_entityA));
            Assert.IsTrue(_tracker.IsFirstRoll(_entityB),
                "Cada entidad arranca con su flag intacta.");
        }

        [Test]
        public void IsFirstRoll_AfterRollResolved_ReturnsFalseForThatEntity()
        {
            EventManager.Trigger(EventName.OnCombatStart, _roomId);
            EventManager.Trigger(EventName.OnRollResolved, _entityA);

            Assert.IsFalse(_tracker.IsFirstRoll(_entityA),
                "Tras el primer roll, A queda consumido.");
            Assert.IsTrue(_tracker.IsFirstRoll(_entityB),
                "B no resolvio todavia — sigue siendo primer roll.");
        }

        [Test]
        public void OnCombatStart_ResetsConsumedSet()
        {
            EventManager.Trigger(EventName.OnCombatStart, _roomId);
            EventManager.Trigger(EventName.OnRollResolved, _entityA);
            Assert.IsFalse(_tracker.IsFirstRoll(_entityA));

            // Nuevo combate.
            EventManager.Trigger(EventName.OnCombatStart, Guid.NewGuid());
            Assert.IsTrue(_tracker.IsFirstRoll(_entityA),
                "OnCombatStart limpia el set — A vuelve a ser primer roll.");
        }

        [Test]
        public void OnCombatEnd_DeactivatesTracker()
        {
            EventManager.Trigger(EventName.OnCombatStart, _roomId);
            EventManager.Trigger(EventName.OnCombatEnd, _roomId);

            Assert.IsFalse(_tracker.IsFirstRoll(_entityA),
                "Tras OnCombatEnd, IsFirstRoll degrada a false (fuera de combate).");
        }

        [Test]
        public void IsFirstRoll_EmptyGuid_ReturnsFalse()
        {
            EventManager.Trigger(EventName.OnCombatStart, _roomId);
            Assert.IsFalse(_tracker.IsFirstRoll(Guid.Empty));
        }

        [Test]
        public void OnRollResolved_OutsideCombat_DoesNotConsume()
        {
            // Roll fuera de combate — el tracker lo ignora.
            EventManager.Trigger(EventName.OnRollResolved, _entityA);

            EventManager.Trigger(EventName.OnCombatStart, _roomId);
            Assert.IsTrue(_tracker.IsFirstRoll(_entityA),
                "El roll fuera de combate no debe haber consumido a A.");
        }

        [Test]
        public void OnRollResolved_BadPayload_NoThrow()
        {
            EventManager.Trigger(EventName.OnCombatStart, _roomId);
            Assert.DoesNotThrow(() => EventManager.Trigger(EventName.OnRollResolved));
            Assert.DoesNotThrow(() => EventManager.Trigger(EventName.OnRollResolved, "not-a-guid"));
            Assert.IsTrue(_tracker.IsFirstRoll(_entityA),
                "Payloads invalidos no deben tocar el set de consumidos.");
        }

        // ====================================================================
        // PCFirstRollOfCombat — integracion con el tracker
        // ====================================================================

        [Test]
        public void PC_FirstRollOfCombat_PassesOnFirstRoll()
        {
            EventManager.Trigger(EventName.OnCombatStart, _roomId);
            var pc = new PCFirstRollOfCombat();
            Assert.IsTrue(pc.Evaluate(new PreConditionContext { OwnerGuid = _entityA }));
        }

        [Test]
        public void PC_FirstRollOfCombat_FailsAfterRoll()
        {
            EventManager.Trigger(EventName.OnCombatStart, _roomId);
            EventManager.Trigger(EventName.OnRollResolved, _entityA);

            var pc = new PCFirstRollOfCombat();
            Assert.IsFalse(pc.Evaluate(new PreConditionContext { OwnerGuid = _entityA }));
        }

        [Test]
        public void PC_FirstRollOfCombat_FailsOutsideCombat()
        {
            var pc = new PCFirstRollOfCombat();
            Assert.IsFalse(pc.Evaluate(new PreConditionContext { OwnerGuid = _entityA }));
        }

        [Test]
        public void PC_FirstRollOfCombat_FailsWithEmptyOwnerGuid()
        {
            EventManager.Trigger(EventName.OnCombatStart, _roomId);
            var pc = new PCFirstRollOfCombat();
            Assert.IsFalse(pc.Evaluate(new PreConditionContext { OwnerGuid = Guid.Empty }));
        }

        [Test]
        public void PC_FirstRollOfCombat_FailsWhenTrackerNotRegistered()
        {
            ServiceLocator.Clear();
            var pc = new PCFirstRollOfCombat();
            Assert.IsFalse(pc.Evaluate(new PreConditionContext { OwnerGuid = _entityA }));
        }
    }
}
