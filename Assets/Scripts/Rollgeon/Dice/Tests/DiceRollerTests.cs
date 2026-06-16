using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Rollgeon.Dice.Tests
{
    /// <summary>
    /// TECHNICAL.md §6.3. Usamos seed fijo para chequear determinismo y la
    /// preservación de holds en el reroll.
    /// </summary>
    [TestFixture]
    public class DiceRollerTests
    {
        private const int Seed = 42;

        private DiceBagSO _bag;
        private List<DiceBagSO> _created;

        [SetUp]
        public void SetUp()
        {
            _created = new List<DiceBagSO>();
            _bag = ScriptableObject.CreateInstance<DiceBagSO>();
            _bag.name = "TestBag";
            _bag.Dice = new List<DiceType>
            {
                DiceType.D6, DiceType.D6, DiceType.D6, DiceType.D6, DiceType.D6,
            };
            _created.Add(_bag);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var bag in _created)
            {
                if (bag != null) Object.DestroyImmediate(bag);
            }
            _created = null;
            _bag = null;
        }

        [Test]
        public void RollAll_ReturnsArrayMatchingBagSize()
        {
            var roller = new DiceRoller(Seed);
            var result = roller.RollAll(_bag);
            Assert.AreEqual(5, result.Length);
        }

        [Test]
        public void RollAll_AllValuesWithinFaceRange()
        {
            // Bag mixta: cada slot tiene MaxFace distinto.
            _bag.Dice = new List<DiceType>
            {
                DiceType.D4, DiceType.D6, DiceType.D8, DiceType.D10, DiceType.D20,
            };

            var roller = new DiceRoller(Seed);
            var result = roller.RollAll(_bag);

            for (int i = 0; i < result.Length; i++)
            {
                int max = _bag.Dice[i].MaxFace();
                Assert.GreaterOrEqual(result[i], 1, $"slot {i} ({_bag.Dice[i]}) bajo el mínimo");
                Assert.LessOrEqual(result[i], max, $"slot {i} ({_bag.Dice[i]}) sobre el máximo {max}");
            }
        }

        [Test]
        public void RollAll_SameSeed_ProducesSameSequence()
        {
            var rollerA = new DiceRoller(Seed);
            var rollerB = new DiceRoller(Seed);
            CollectionAssert.AreEqual(rollerA.RollAll(_bag), rollerB.RollAll(_bag));
        }

        [Test]
        public void RollAll_DifferentSeeds_ProduceDifferentSequence()
        {
            // Para 5 D6 con seeds distintos esperamos al menos un slot distinto.
            var rollerA = new DiceRoller(Seed);
            var rollerB = new DiceRoller(Seed + 1);
            var a = rollerA.RollAll(_bag);
            var b = rollerB.RollAll(_bag);

            bool anyDifferent = false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) { anyDifferent = true; break; }
            }
            Assert.IsTrue(anyDifferent, "Esperado al menos un slot distinto entre dos seeds.");
        }

        [Test]
        public void Reroll_PreservesHeldSlots()
        {
            var roller = new DiceRoller(Seed);
            var previous = new[] { 1, 2, 3, 4, 5 };
            var keep = new[] { true, true, false, false, false };

            var result = roller.Reroll(_bag, previous, keep);

            Assert.AreEqual(1, result[0], "slot 0 holdeado");
            Assert.AreEqual(2, result[1], "slot 1 holdeado");
            // 2..4 son rerolls — sólo verificamos que están en rango.
            for (int i = 2; i < 5; i++)
            {
                Assert.GreaterOrEqual(result[i], 1);
                Assert.LessOrEqual(result[i], 6);
            }
        }

        [Test]
        public void Reroll_NullKeep_RerollsEverything()
        {
            var roller = new DiceRoller(Seed);
            var previous = new[] { 1, 1, 1, 1, 1 };

            var result = roller.Reroll(_bag, previous, keep: null);

            Assert.AreEqual(5, result.Length);
            for (int i = 0; i < 5; i++)
            {
                Assert.GreaterOrEqual(result[i], 1);
                Assert.LessOrEqual(result[i], 6);
            }
        }

        [Test]
        public void Reroll_AllKeepFalse_RerollsEverything()
        {
            var roller = new DiceRoller(Seed);
            var previous = new[] { 1, 1, 1, 1, 1 };
            var keep = new[] { false, false, false, false, false };

            var result = roller.Reroll(_bag, previous, keep);

            // Probabilidad de que System.Random(42) devuelva exactamente {1,1,1,1,1}
            // sobre 5 D6 es (1/6)^5 ≈ 0.013%; aceptable como test smoke.
            bool atLeastOneChanged = false;
            for (int i = 0; i < 5; i++)
            {
                if (result[i] != previous[i]) { atLeastOneChanged = true; break; }
            }
            Assert.IsTrue(atLeastOneChanged, "Esperado al menos un cambio cuando keep es todo false.");
        }

        [Test]
        public void Reroll_TwoConsecutiveCallsAdvanceRng()
        {
            // Con el mismo previous y mismo keep=null, dos rerolls del mismo roller
            // NO deben devolver el mismo array (RNG avanza).
            var roller = new DiceRoller(Seed);
            var previous = new[] { 1, 1, 1, 1, 1 };

            var first = roller.Reroll(_bag, previous, keep: null);
            var second = roller.Reroll(_bag, previous, keep: null);

            bool anyDifferent = false;
            for (int i = 0; i < 5; i++)
            {
                if (first[i] != second[i]) { anyDifferent = true; break; }
            }
            Assert.IsTrue(anyDifferent, "Dos rerolls consecutivos deberían diferir (RNG avanza).");
        }

        [Test]
        public void Reroll_KeepLongerThanBag_IgnoresExtra()
        {
            var roller = new DiceRoller(Seed);
            var previous = new[] { 6, 6, 6, 6, 6 };
            var keep = new[] { true, true, true, true, true, true, true }; // 7 entries.

            var result = roller.Reroll(_bag, previous, keep);

            CollectionAssert.AreEqual(previous, result);
        }
    }
}
