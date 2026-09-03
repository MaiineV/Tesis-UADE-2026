using NUnit.Framework;
using Rollgeon.Combos;
using Rollgeon.Effects.Readers;

namespace Rollgeon.Effects.Tests
{
    /// <summary>
    /// Tests de los readers que leen dados del <see cref="EffectContext"/>:
    /// <see cref="ReadHighestContributingDie"/> (Fuente Mágica),
    /// <see cref="ReadUnusedDiceCount"/> (Dados en Reserva) y
    /// <see cref="ReadDiceCountByParity"/> (Bolsa del Impar).
    /// </summary>
    [TestFixture]
    public class DiceContextReadersTests
    {
        // ---- ReadHighestContributingDie -------------------------------------------

        [Test]
        public void HighestContributingDie_UsesOnlyComboDice()
        {
            // Holdeó [4, 4, 10, 12]; el par lo forman los índices 0 y 1 → 4, no el 12.
            var ctx = new EffectContext
            {
                DiceResult = new[] { 4, 4, 10, 12, 1 },
                KeptDice = new[] { 4, 4, 10, 12 },
                ComboResult = ComboDetectionResult.Match("combo.pair", 8, 2, new[] { 0, 1 }),
            };

            Assert.AreEqual(4, new ReadHighestContributingDie().Read(ctx));
        }

        [Test]
        public void HighestContributingDie_GddExample_TrioWith10()
        {
            var ctx = new EffectContext
            {
                DiceResult = new[] { 4, 4, 10 },
                KeptDice = new[] { 4, 4, 10 },
                ComboResult = ComboDetectionResult.Match("combo.higher_number", 0, 3, new[] { 0, 1, 2 }),
            };

            Assert.AreEqual(10, new ReadHighestContributingDie().Read(ctx));
        }

        [Test]
        public void HighestContributingDie_NoCombo_FallsBackToKeptMax()
        {
            var ctx = new EffectContext { DiceResult = new[] { 6, 2, 9 }, KeptDice = new[] { 6, 2 } };

            Assert.AreEqual(6, new ReadHighestContributingDie().Read(ctx));
        }

        [Test]
        public void HighestContributingDie_NoDice_ReturnsZero()
        {
            Assert.AreEqual(0, new ReadHighestContributingDie().Read(new EffectContext()));
            Assert.AreEqual(0, new ReadHighestContributingDie().Read(null));
        }

        // ---- ReadUnusedDiceCount --------------------------------------------------

        [Test]
        public void UnusedDiceCount_IsRolledMinusKept()
        {
            var ctx = new EffectContext { DiceResult = new[] { 1, 2, 3, 4, 5, 6 }, KeptDice = new[] { 3, 3, 3, 4 } };

            Assert.AreEqual(2, new ReadUnusedDiceCount().Read(ctx));
        }

        [Test]
        public void UnusedDiceCount_AllKept_ReturnsZero()
        {
            var ctx = new EffectContext { DiceResult = new[] { 1, 2 }, KeptDice = new[] { 1, 2 } };

            Assert.AreEqual(0, new ReadUnusedDiceCount().Read(ctx));
        }

        [Test]
        public void UnusedDiceCount_WithoutKeptDice_ReturnsZero()
        {
            var ctx = new EffectContext { DiceResult = new[] { 1, 2, 3 } };

            Assert.AreEqual(0, new ReadUnusedDiceCount().Read(ctx));
            Assert.AreEqual(0, new ReadUnusedDiceCount().Read(null));
        }

        // ---- ReadDiceCountByParity ------------------------------------------------

        [Test]
        public void DiceCountByParity_Odd_GddExample_PaysPerOddDie()
        {
            // GDD: 1, 4, 7, 8 → dos impares → +6
            var ctx = new EffectContext { DiceResult = new[] { 1, 4, 7, 8 } };

            Assert.AreEqual(6, new ReadDiceCountByParity { Parity = DiceParity.Odd, PerDieAmount = 3 }.Read(ctx));
        }

        [Test]
        public void DiceCountByParity_ReadsTheWholeRoll_NotOnlyKept()
        {
            var ctx = new EffectContext { DiceResult = new[] { 1, 3, 5 }, KeptDice = new[] { 1 } };

            Assert.AreEqual(3, new ReadDiceCountByParity { Parity = DiceParity.Odd, PerDieAmount = 1 }.Read(ctx));
        }

        [Test]
        public void DiceCountByParity_Even_CountsEvens()
        {
            var ctx = new EffectContext { DiceResult = new[] { 2, 3, 4, 6 } };

            Assert.AreEqual(3, new ReadDiceCountByParity { Parity = DiceParity.Even, PerDieAmount = 1 }.Read(ctx));
        }

        [Test]
        public void DiceCountByParity_NoDice_ReturnsZero()
        {
            Assert.AreEqual(0, new ReadDiceCountByParity().Read(new EffectContext()));
            Assert.AreEqual(0, new ReadDiceCountByParity().Read(null));
        }
    }
}
