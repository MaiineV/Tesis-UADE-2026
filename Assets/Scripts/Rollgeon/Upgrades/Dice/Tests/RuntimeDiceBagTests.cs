using NUnit.Framework;
using Rollgeon.Dice;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Tests
{
    /// <summary>
    /// Cobertura de <see cref="RuntimeDiceBag"/> — state per-bag-slot + counters.
    /// No requiere ServiceLocator: testea la clase plain C# directamente.
    /// </summary>
    [TestFixture]
    public class RuntimeDiceBagTests
    {
        // ---- Setup -----------------------------------------------------------

        private static RuntimeDiceBag MakeBag(params DiceType[] dice)
            => new RuntimeDiceBag(dice);

        // ---- Slot count -----------------------------------------------------

        [Test]
        public void GetEnchantmentSlotCount_MatchesMaxEnchantmentSlots()
        {
            var bag = MakeBag(DiceType.D3, DiceType.D6, DiceType.D10, DiceType.D20);

            Assert.AreEqual(1, bag.GetEnchantmentSlotCount(0)); // D3
            Assert.AreEqual(2, bag.GetEnchantmentSlotCount(1)); // D6
            Assert.AreEqual(3, bag.GetEnchantmentSlotCount(2)); // D10
            Assert.AreEqual(4, bag.GetEnchantmentSlotCount(3)); // D20
        }

        [Test]
        public void GetEnchantmentSlotCount_OutOfRange_ReturnsZero()
        {
            var bag = MakeBag(DiceType.D6);
            Assert.AreEqual(0, bag.GetEnchantmentSlotCount(99));
            Assert.AreEqual(0, bag.GetEnchantmentSlotCount(-1));
        }

        // ---- Enchantment slot ops -------------------------------------------

        [Test]
        public void GetEnchantmentAt_Default_ReturnsNull()
        {
            var bag = MakeBag(DiceType.D6);
            Assert.IsNull(bag.GetEnchantmentAt(0, 0));
            Assert.IsNull(bag.GetEnchantmentAt(0, 1));
        }

        [Test]
        public void SetEnchantmentAt_ValidIndices_PersistsValue()
        {
            var bag = MakeBag(DiceType.D6);
            var ench = ScriptableObject.CreateInstance<EnchantmentSO>();
            try
            {
                bool ok = bag.SetEnchantmentAt(0, 0, ench);
                Assert.IsTrue(ok);
                Assert.AreSame(ench, bag.GetEnchantmentAt(0, 0));
            }
            finally
            {
                ScriptableObject.DestroyImmediate(ench);
            }
        }

        [Test]
        public void SetEnchantmentAt_OutOfRange_ReturnsFalse()
        {
            var bag = MakeBag(DiceType.D6);
            var ench = ScriptableObject.CreateInstance<EnchantmentSO>();
            try
            {
                Assert.IsFalse(bag.SetEnchantmentAt(99, 0, ench));
                Assert.IsFalse(bag.SetEnchantmentAt(0, 99, ench));
            }
            finally
            {
                ScriptableObject.DestroyImmediate(ench);
            }
        }

        [Test]
        public void SetEnchantmentAt_Null_ClearsSlot()
        {
            var bag = MakeBag(DiceType.D6);
            var ench = ScriptableObject.CreateInstance<EnchantmentSO>();
            try
            {
                bag.SetEnchantmentAt(0, 0, ench);
                Assert.IsNotNull(bag.GetEnchantmentAt(0, 0));

                bag.SetEnchantmentAt(0, 0, null);
                Assert.IsNull(bag.GetEnchantmentAt(0, 0));
            }
            finally
            {
                ScriptableObject.DestroyImmediate(ench);
            }
        }

        // ---- Counters --------------------------------------------------------

        [Test]
        public void IncrementCounter_FreshKey_StartsAtOne()
        {
            var bag = MakeBag(DiceType.D6);
            var slot = new EnchantmentSlotRef(DiceType.D6, 0, 0);

            int result = bag.IncrementCounter(slot, "test_key");

            Assert.AreEqual(1, result);
            Assert.AreEqual(1, bag.GetCounter(slot, "test_key"));
        }

        [Test]
        public void IncrementCounter_RepeatedCalls_Accumulate()
        {
            var bag = MakeBag(DiceType.D6);
            var slot = new EnchantmentSlotRef(DiceType.D6, 0, 0);

            bag.IncrementCounter(slot, "k");
            bag.IncrementCounter(slot, "k");
            int third = bag.IncrementCounter(slot, "k");

            Assert.AreEqual(3, third);
        }

        [Test]
        public void ResetCounter_AfterIncrement_ReturnsZero()
        {
            var bag = MakeBag(DiceType.D6);
            var slot = new EnchantmentSlotRef(DiceType.D6, 0, 0);
            bag.IncrementCounter(slot, "k", delta: 5);

            bag.ResetCounter(slot, "k");

            Assert.AreEqual(0, bag.GetCounter(slot, "k"));
        }

        [Test]
        public void Counters_DifferentSlots_IsolatedFromEachOther()
        {
            var bag = MakeBag(DiceType.D6, DiceType.D20);
            var slotA = new EnchantmentSlotRef(DiceType.D6, 0, 0);
            var slotB = new EnchantmentSlotRef(DiceType.D20, 1, 0);

            bag.IncrementCounter(slotA, "k");
            bag.IncrementCounter(slotA, "k");

            Assert.AreEqual(2, bag.GetCounter(slotA, "k"));
            Assert.AreEqual(0, bag.GetCounter(slotB, "k"));
        }

        [Test]
        public void ClearCountersForSlot_RemovesOnlyMatchingSlot()
        {
            var bag = MakeBag(DiceType.D6, DiceType.D20);
            var slotA = new EnchantmentSlotRef(DiceType.D6, 0, 0);
            var slotB = new EnchantmentSlotRef(DiceType.D20, 1, 0);

            bag.IncrementCounter(slotA, "k1");
            bag.IncrementCounter(slotA, "k2");
            bag.IncrementCounter(slotB, "k1");

            bag.ClearCountersForSlot(slotA);

            Assert.AreEqual(0, bag.GetCounter(slotA, "k1"));
            Assert.AreEqual(0, bag.GetCounter(slotA, "k2"));
            Assert.AreEqual(1, bag.GetCounter(slotB, "k1"));
        }
    }
}
