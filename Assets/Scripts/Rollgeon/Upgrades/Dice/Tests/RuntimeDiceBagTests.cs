using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rollgeon.Dice;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Tests
{
    /// <summary>
    /// Cobertura de <see cref="RuntimeDiceBag"/> — lista de encantamientos sin
    /// techo por dado + counters per-slot + counters per-dado.
    /// No requiere ServiceLocator: testea la clase plain C# directamente.
    /// </summary>
    [TestFixture]
    public class RuntimeDiceBagTests
    {
        // ---- Setup -----------------------------------------------------------

        private static RuntimeDiceBag MakeBag(params DiceType[] dice)
            => new RuntimeDiceBag(dice);

        // ---- Enchantment count -------------------------------------------------

        [Test]
        public void GetEnchantmentCount_FreshBag_StartsAtZero()
        {
            var bag = MakeBag(DiceType.D3, DiceType.D6, DiceType.D10, DiceType.D20);

            Assert.AreEqual(0, bag.GetEnchantmentCount(0));
            Assert.AreEqual(0, bag.GetEnchantmentCount(1));
            Assert.AreEqual(0, bag.GetEnchantmentCount(2));
            Assert.AreEqual(0, bag.GetEnchantmentCount(3));
        }

        [Test]
        public void GetEnchantmentCount_OutOfRange_ReturnsZero()
        {
            var bag = MakeBag(DiceType.D6);
            Assert.AreEqual(0, bag.GetEnchantmentCount(99));
            Assert.AreEqual(0, bag.GetEnchantmentCount(-1));
        }

        [Test]
        public void GetEnchantmentCount_GrowsWithEachAddEnchantment()
        {
            var bag = MakeBag(DiceType.D6);
            var first = ScriptableObject.CreateInstance<EnchantmentSO>();
            var second = ScriptableObject.CreateInstance<EnchantmentSO>();
            try
            {
                bag.AddEnchantment(0, first);
                Assert.AreEqual(1, bag.GetEnchantmentCount(0));

                bag.AddEnchantment(0, second);
                Assert.AreEqual(2, bag.GetEnchantmentCount(0));
            }
            finally
            {
                ScriptableObject.DestroyImmediate(first);
                ScriptableObject.DestroyImmediate(second);
            }
        }

        // ---- AddEnchantment ---------------------------------------------------

        [Test]
        public void GetEnchantmentAt_Default_ReturnsNull()
        {
            var bag = MakeBag(DiceType.D6);
            Assert.IsNull(bag.GetEnchantmentAt(0, 0));
            Assert.IsNull(bag.GetEnchantmentAt(0, 1));
        }

        [Test]
        public void AddEnchantment_SequentialCalls_ReturnsIncrementingIndices()
        {
            var bag = MakeBag(DiceType.D6);
            var first = ScriptableObject.CreateInstance<EnchantmentSO>();
            var second = ScriptableObject.CreateInstance<EnchantmentSO>();
            var third = ScriptableObject.CreateInstance<EnchantmentSO>();
            try
            {
                Assert.AreEqual(0, bag.AddEnchantment(0, first));
                Assert.AreEqual(1, bag.AddEnchantment(0, second));
                Assert.AreEqual(2, bag.AddEnchantment(0, third));
                Assert.AreEqual(3, bag.GetEnchantmentCount(0));
                Assert.AreSame(first, bag.GetEnchantmentAt(0, 0));
                Assert.AreSame(second, bag.GetEnchantmentAt(0, 1));
                Assert.AreSame(third, bag.GetEnchantmentAt(0, 2));
            }
            finally
            {
                ScriptableObject.DestroyImmediate(first);
                ScriptableObject.DestroyImmediate(second);
                ScriptableObject.DestroyImmediate(third);
            }
        }

        [Test]
        public void AddEnchantment_NullEnchantment_ReturnsMinusOneAndDoesNotGrow()
        {
            var bag = MakeBag(DiceType.D6);

            int index = bag.AddEnchantment(0, null);

            Assert.AreEqual(-1, index);
            Assert.AreEqual(0, bag.GetEnchantmentCount(0));
        }

        [Test]
        public void AddEnchantment_OutOfRangeBagIndex_ReturnsMinusOne()
        {
            var bag = MakeBag(DiceType.D6);
            var ench = ScriptableObject.CreateInstance<EnchantmentSO>();
            try
            {
                Assert.AreEqual(-1, bag.AddEnchantment(99, ench));
            }
            finally
            {
                ScriptableObject.DestroyImmediate(ench);
            }
        }

        // ---- SetEnchantmentAt ---------------------------------------------------

        [Test]
        public void SetEnchantmentAt_WithinExistingRange_PersistsValue()
        {
            var bag = MakeBag(DiceType.D6);
            var original = ScriptableObject.CreateInstance<EnchantmentSO>();
            var replacement = ScriptableObject.CreateInstance<EnchantmentSO>();
            try
            {
                bag.AddEnchantment(0, original);

                bool ok = bag.SetEnchantmentAt(0, 0, replacement);

                Assert.IsTrue(ok);
                Assert.AreSame(replacement, bag.GetEnchantmentAt(0, 0));
            }
            finally
            {
                ScriptableObject.DestroyImmediate(original);
                ScriptableObject.DestroyImmediate(replacement);
            }
        }

        [Test]
        public void SetEnchantmentAt_OutOfRangeBagIndex_ReturnsFalse()
        {
            var bag = MakeBag(DiceType.D6);
            var ench = ScriptableObject.CreateInstance<EnchantmentSO>();
            try
            {
                Assert.IsFalse(bag.SetEnchantmentAt(99, 0, ench));
            }
            finally
            {
                ScriptableObject.DestroyImmediate(ench);
            }
        }

        [Test]
        public void SetEnchantmentAt_SlotIndexBeyondCurrentCount_ReturnsFalse()
        {
            var bag = MakeBag(DiceType.D6);
            var ench = ScriptableObject.CreateInstance<EnchantmentSO>();
            try
            {
                bag.AddEnchantment(0, ench); // count = 1, único índice válido = 0

                Assert.IsFalse(bag.SetEnchantmentAt(0, 1, ench),
                    "SetEnchantmentAt no puede crecer la lista — para eso está AddEnchantment");
            }
            finally
            {
                ScriptableObject.DestroyImmediate(ench);
            }
        }

        [Test]
        public void SetEnchantmentAt_Null_TombstonesSlotWithoutShrinkingCount()
        {
            var bag = MakeBag(DiceType.D6);
            var ench = ScriptableObject.CreateInstance<EnchantmentSO>();
            try
            {
                bag.AddEnchantment(0, ench);
                Assert.IsNotNull(bag.GetEnchantmentAt(0, 0));

                bag.SetEnchantmentAt(0, 0, null);

                Assert.IsNull(bag.GetEnchantmentAt(0, 0));
                Assert.AreEqual(1, bag.GetEnchantmentCount(0),
                    "tombstone no compacta la lista — el count no baja");
            }
            finally
            {
                ScriptableObject.DestroyImmediate(ench);
            }
        }

        // ---- Counters (per enchantment slot) ------------------------------------

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

        // ---- Die counters (per dado, no per slot) -------------------------------

        [Test]
        public void GetDieCounter_FreshKey_ReturnsZero()
        {
            var bag = MakeBag(DiceType.D6);

            Assert.AreEqual(0, bag.GetDieCounter(0, "altar_roll_count"));
        }

        [Test]
        public void IncrementDieCounter_RepeatedCalls_Accumulate()
        {
            var bag = MakeBag(DiceType.D6);

            bag.IncrementDieCounter(0, "altar_roll_count");
            bag.IncrementDieCounter(0, "altar_roll_count");
            int third = bag.IncrementDieCounter(0, "altar_roll_count");

            Assert.AreEqual(3, third);
        }

        [Test]
        public void DieCounters_DifferentBagsAndKeys_AreIndependent()
        {
            var bag = MakeBag(DiceType.D6, DiceType.D20);

            bag.IncrementDieCounter(0, "altar_roll_count", 2);
            bag.IncrementDieCounter(0, "other_key", 9);

            Assert.AreEqual(2, bag.GetDieCounter(0, "altar_roll_count"));
            Assert.AreEqual(9, bag.GetDieCounter(0, "other_key"));
            Assert.AreEqual(0, bag.GetDieCounter(1, "altar_roll_count"),
                "el counter del dado 0 no debe filtrar al dado 1");
        }

        // ---- Capture / Restore round-trip ---------------------------------------

        [Test]
        public void CaptureRestore_TombstoneAtIndexZeroWithEnchantmentAtIndexOne_RestoresPaddingCountersAndDieCounters()
        {
            // Arrange — tombstone en índice 0 (se sacó un encantamiento) + encantamiento
            // vivo en índice 1, para forzar que RestoreState paddee hasta el SlotIndex
            // del snapshot en vez de compactar.
            var dummy = ScriptableObject.CreateInstance<EnchantmentSO>();
            var ench = ScriptableObject.CreateInstance<EnchantmentSO>();
            typeof(UpgradeSO).GetField("_upgradeId", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(ench, "upg.ench_test");
            try
            {
                var byId = new Dictionary<string, EnchantmentSO> { { "upg.ench_test", ench } };
                var dice = new[] { DiceType.D6 };
                var bag = new RuntimeDiceBag(dice, id => byId.TryGetValue(id, out var e) ? e : null);

                bag.AddEnchantment(0, dummy);       // índice 0
                bag.SetEnchantmentAt(0, 0, null);   // tombstone en índice 0
                bag.AddEnchantment(0, ench);        // índice 1

                var slotRef = new EnchantmentSlotRef(DiceType.D6, 0, 1);
                bag.IncrementCounter(slotRef, "k", 5);
                bag.IncrementDieCounter(0, "altar_roll_count", 2);

                var captured = bag.CaptureState();

                // Act
                var reborn = new RuntimeDiceBag(dice, id => byId.TryGetValue(id, out var e) ? e : null);
                reborn.RestoreState(captured);

                // Assert
                Assert.AreEqual(2, reborn.GetEnchantmentCount(0),
                    "el padding del tombstone en 0 se restaura — el índice de append 1 debe seguir siendo 1");
                Assert.IsNull(reborn.GetEnchantmentAt(0, 0));
                Assert.AreSame(ench, reborn.GetEnchantmentAt(0, 1));
                Assert.AreEqual(5, reborn.GetCounter(slotRef, "k"));
                Assert.AreEqual(2, reborn.GetDieCounter(0, "altar_roll_count"));
            }
            finally
            {
                ScriptableObject.DestroyImmediate(dummy);
                ScriptableObject.DestroyImmediate(ench);
            }
        }
    }
}
