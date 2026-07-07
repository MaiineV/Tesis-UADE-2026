using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Heroes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Tutorial.Tests
{
    [TestFixture]
    public class TutorialActionGateServiceTests
    {
        private ClassHeroSO _hero;
        private int _unlockedEvents;
        private EventManager.EventReceiver _onUnlocked;

        [SetUp]
        public void SetUp()
        {
            _hero = ScriptableObject.CreateInstance<ClassHeroSO>();
            _hero.PhaseBehaviors = new List<HeroActionBehavior>
            {
                new HeroActionBehavior { ActionName = "Movement", Slot = HeroBehaviorSlot.Movement },
                new HeroActionBehavior { ActionName = "Attack", Slot = HeroBehaviorSlot.BaseAttack },
                new HeroActionBehavior { ActionName = "Heal", Slot = HeroBehaviorSlot.Healing },
                new HeroActionBehavior { ActionName = "Force Door", Slot = HeroBehaviorSlot.ForceDoor },
            };

            _unlockedEvents = 0;
            _onUnlocked = args => _unlockedEvents++;
            EventManager.Subscribe(EventName.OnTutorialActionUnlocked, _onUnlocked);
        }

        [TearDown]
        public void TearDown()
        {
            EventManager.UnSubscribe(EventName.OnTutorialActionUnlocked, _onUnlocked);
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            Object.DestroyImmediate(_hero);
        }

        private TutorialActionGateService CreateGate(params HeroBehaviorSlot[] locked) =>
            new TutorialActionGateService(_hero, locked);

        [Test]
        public void IsSlotLocked_InitialLock_LocksOnlyGivenSlots()
        {
            var gate = CreateGate(HeroBehaviorSlot.Healing, HeroBehaviorSlot.ForceDoor);

            Assert.IsFalse(gate.IsSlotLocked(HeroBehaviorSlot.Movement));
            Assert.IsFalse(gate.IsSlotLocked(HeroBehaviorSlot.BaseAttack));
            Assert.IsTrue(gate.IsSlotLocked(HeroBehaviorSlot.Healing));
            Assert.IsTrue(gate.IsSlotLocked(HeroBehaviorSlot.ForceDoor));
        }

        [Test]
        public void IsActionLocked_ActionNameOfLockedSlot_ReturnsTrue()
        {
            var gate = CreateGate(HeroBehaviorSlot.Healing);

            Assert.IsTrue(gate.IsActionLocked("Heal"));
            Assert.IsFalse(gate.IsActionLocked("Attack"));
        }

        [Test]
        public void IsActionLocked_UnknownActionId_NeverLocks()
        {
            var gate = CreateGate(HeroBehaviorSlot.Healing, HeroBehaviorSlot.ForceDoor);

            Assert.IsFalse(gate.IsActionLocked("Enemy Bite"));
            Assert.IsFalse(gate.IsActionLocked(null));
            Assert.IsFalse(gate.IsActionLocked(string.Empty));
        }

        [Test]
        public void Unlock_LockedSlot_UnlocksAndFiresEvent()
        {
            var gate = CreateGate(HeroBehaviorSlot.Healing);

            gate.Unlock(HeroBehaviorSlot.Healing);

            Assert.IsFalse(gate.IsSlotLocked(HeroBehaviorSlot.Healing));
            Assert.IsFalse(gate.IsActionLocked("Heal"));
            Assert.AreEqual(1, _unlockedEvents);
        }

        [Test]
        public void Unlock_AlreadyUnlockedSlot_IsIdempotentAndSilent()
        {
            var gate = CreateGate(HeroBehaviorSlot.Healing);
            gate.Unlock(HeroBehaviorSlot.Healing);

            gate.Unlock(HeroBehaviorSlot.Healing);

            Assert.AreEqual(1, _unlockedEvents, "El segundo Unlock no debe re-disparar el evento.");
        }

        [Test]
        public void CreateAndRegister_RegistersRunScopedWithTutorialDefaults()
        {
            var gate = TutorialActionGateService.CreateAndRegister(_hero);

            Assert.IsTrue(ServiceLocator.TryGetService<ITutorialActionGateService>(out var resolved));
            Assert.AreSame(gate, resolved);
            Assert.IsFalse(gate.IsSlotLocked(HeroBehaviorSlot.Movement));
            Assert.IsTrue(gate.IsSlotLocked(HeroBehaviorSlot.BaseAttack),
                "ATACAR arranca bloqueado — se desbloquea en su paso de enseñanza.");
            Assert.IsTrue(gate.IsSlotLocked(HeroBehaviorSlot.SpecialAttack));
            Assert.IsTrue(gate.IsSlotLocked(HeroBehaviorSlot.Healing));
            Assert.IsTrue(gate.IsSlotLocked(HeroBehaviorSlot.ForceDoor));
        }
    }
}
