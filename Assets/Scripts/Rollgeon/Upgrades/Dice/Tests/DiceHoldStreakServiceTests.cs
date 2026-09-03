using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Patterns.Save;
using Rollgeon.Combat.Handoff;
using Rollgeon.Dice;
using Rollgeon.Effects;
using Rollgeon.Upgrades.Dice.Readers;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Tests
{
    /// <summary>
    /// Feature#0073 — mecánica de "guardado" para Ancla (<see cref="DiceHoldStreakService"/> +
    /// <see cref="ReadCarrierHoldStreak"/>) y gate de reroll para Lento
    /// (<see cref="CombatHandoffService.ApplyKeepConstraints"/>).
    /// </summary>
    [TestFixture]
    public class DiceHoldStreakServiceTests
    {
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();
        private DiceHoldStreakService _streaks;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            SaveSystem.ResetForTests();
            _streaks = new DiceHoldStreakService();
            _streaks.SubscribeEventsForTests();
            ServiceLocator.AddService<IDiceHoldStreakService>(_streaks, ServiceScope.Global);
        }

        [TearDown]
        public void TearDown()
        {
            _streaks?.UnsubscribeEventsForTests();
            _streaks = null;
            foreach (var obj in _created)
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            _created.Clear();
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            SaveSystem.ResetForTests();
        }

        // ================================================================
        // Service
        // ================================================================

        [Test]
        public void KeptDie_AccumulatesOnePerReroll_RerolledDieResetsToZero()
        {
            _streaks.OnFreshRoll();
            _streaks.OnReroll(new[] { true, false, true });
            _streaks.OnReroll(new[] { true, true, false });

            Assert.AreEqual(2, _streaks.GetStreak(0));
            Assert.AreEqual(1, _streaks.GetStreak(1));
            Assert.AreEqual(0, _streaks.GetStreak(2), "el slot 2 voló en el segundo reroll");
        }

        [Test]
        public void FreshRoll_ClearsEverything()
        {
            _streaks.OnReroll(new[] { true, true });
            _streaks.OnFreshRoll();

            Assert.AreEqual(0, _streaks.GetStreak(0));
            Assert.AreEqual(0, _streaks.GetStreak(1));
        }

        [Test]
        public void CombatStartAndEnd_ClearStreaks()
        {
            _streaks.OnReroll(new[] { true });
            EventManager.Trigger(EventName.OnCombatStart, Guid.NewGuid());
            Assert.AreEqual(0, _streaks.GetStreak(0));

            _streaks.OnReroll(new[] { true });
            EventManager.Trigger(EventName.OnCombatEnd, Guid.NewGuid(), 0);
            Assert.AreEqual(0, _streaks.GetStreak(0));
        }

        [Test]
        public void OutOfRangeSlot_ReturnsZero()
        {
            _streaks.OnReroll(new[] { true });
            Assert.AreEqual(0, _streaks.GetStreak(-1));
            Assert.AreEqual(0, _streaks.GetStreak(7));
        }

        // ================================================================
        // Reader (Ancla)
        // ================================================================

        private static EffectContext CarrierCtx(int carrier)
        {
            return new EffectContext
            {
                SourceGuid = Guid.NewGuid(),
                DiceResult = new[] { 3, 3, 3 },
                TriggerContext = new ScratchTriggerContext
                {
                    Scratch = new EnchantmentScratch(),
                    Slot = new EnchantmentSlotRef(DiceType.D6, carrier, 0),
                    Channel = ScratchChannel.DiceEnchantment,
                },
            };
        }

        [Test]
        public void ReadCarrierHoldStreak_ScalesPerRoll_AndCapsAtMaxRolls()
        {
            var reader = new ReadCarrierHoldStreak { PerRoll = 5, MaxRolls = 3 };
            var keep = new[] { true, false };

            Assert.AreEqual(0, reader.Read(CarrierCtx(0)), "sin tiradas guardadas no hay bono");
            _streaks.OnReroll(keep);
            Assert.AreEqual(5, reader.Read(CarrierCtx(0)));
            _streaks.OnReroll(keep);
            Assert.AreEqual(10, reader.Read(CarrierCtx(0)));
            _streaks.OnReroll(keep);
            Assert.AreEqual(15, reader.Read(CarrierCtx(0)));
            _streaks.OnReroll(keep);
            Assert.AreEqual(15, reader.Read(CarrierCtx(0)), "tope 3 tiradas (+15)");
            Assert.AreEqual(0, reader.Read(CarrierCtx(1)), "el dado que vuela no acumula");
        }

        [Test]
        public void ReadCarrierHoldStreak_WithoutService_ReturnsZero()
        {
            ServiceLocator.Clear();
            _streaks.OnReroll(new[] { true });
            Assert.AreEqual(0, new ReadCarrierHoldStreak().Read(CarrierCtx(0)));
        }

        // ================================================================
        // Lento: ApplyKeepConstraints fuerza keep=false
        // ================================================================

        private DiceEnchantmentService BagWithLentoAt(int slot, int diceCount)
        {
            var svc = new DiceEnchantmentService(config: null);
            var bag = ScriptableObject.CreateInstance<DiceBagSO>();
            bag.Dice = new List<DiceType>();
            for (int i = 0; i < diceCount; i++) bag.Dice.Add(DiceType.D6);
            _created.Add(bag);
            svc.InitializeFromBag(bag);

            var lento = ScriptableObject.CreateInstance<EnchantmentSO>();
            lento.EditorSetCapabilities(new List<IEnchantmentCapability> { new CapPreventHolding() });
            _created.Add(lento);
            svc.Bag.AddEnchantment(slot, lento);

            ServiceLocator.AddService<IDiceEnchantmentService>(svc, ServiceScope.Global);
            return svc;
        }

        [Test]
        public void ApplyKeepConstraints_LentoDie_IsForcedToReroll_EvenIfHeld()
        {
            BagWithLentoAt(slot: 1, diceCount: 3);

            var keep = CombatHandoffService.ApplyKeepConstraints(new[] { true, true, true }, 3);

            Assert.IsTrue(keep[0]);
            Assert.IsFalse(keep[1], "Lento no se puede guardar: vuela aunque el HUD lo marque");
            Assert.IsTrue(keep[2]);
        }

        [Test]
        public void ApplyKeepConstraints_WithoutBag_LeavesKeepUntouched()
        {
            var keep = CombatHandoffService.ApplyKeepConstraints(new[] { true, false }, 2);
            CollectionAssert.AreEqual(new[] { true, false }, keep);
        }

        [Test]
        public void SlotHasCapability_IsFalseForOtherSlotsAndOutOfRange()
        {
            var svc = BagWithLentoAt(slot: 0, diceCount: 2);

            Assert.IsTrue(svc.Bag.SlotHasCapability<CapPreventHolding>(0));
            Assert.IsFalse(svc.Bag.SlotHasCapability<CapPreventHolding>(1));
            Assert.IsFalse(svc.Bag.SlotHasCapability<CapPreventHolding>(9));
            Assert.IsTrue(EnchantmentCapabilityQueries.PlayerSlotHasCapability<CapPreventHolding>(0));
        }
    }
}
