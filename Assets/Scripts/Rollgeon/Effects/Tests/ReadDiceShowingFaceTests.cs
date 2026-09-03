using NUnit.Framework;
using Rollgeon.Effects.Readers;

namespace Rollgeon.Effects.Tests
{
    /// <summary>
    /// El reader de Jackpot: +PerDieAmount por cada dado de la jugada que muestre Face.
    /// </summary>
    [TestFixture]
    public class ReadDiceShowingFaceTests
    {
        private static ReadDiceShowingFace Jackpot()
            => new ReadDiceShowingFace { Face = 7, PerDieAmount = 5 };

        [Test]
        public void Read_TwoSevensAmongKept_ReturnsTen()
        {
            var ctx = new EffectContext { KeptDice = new[] { 7, 7, 3, 4, 5 } };
            Assert.AreEqual(10, Jackpot().Read(ctx));
        }

        [Test]
        public void Read_NoSevens_ReturnsZero()
        {
            var ctx = new EffectContext { KeptDice = new[] { 1, 2, 3, 4, 6 } };
            Assert.AreEqual(0, Jackpot().Read(ctx));
        }

        [Test]
        public void Read_CountsOnlyKeptDice_NotTheWholeRoll()
        {
            // El 7 descartado no participa del ataque.
            var ctx = new EffectContext
            {
                DiceResult = new[] { 7, 7, 7, 2, 2 },
                KeptDice = new[] { 7, 2, 2 },
            };
            Assert.AreEqual(5, Jackpot().Read(ctx));
        }

        [Test]
        public void Read_WithoutExplicitKeep_FallsBackToRoll()
        {
            var ctx = new EffectContext { DiceResult = new[] { 7, 1, 7 } };
            Assert.AreEqual(10, Jackpot().Read(ctx));
        }

        [Test]
        public void Read_WithoutDice_ReturnsZero()
        {
            Assert.AreEqual(0, Jackpot().Read(new EffectContext()));
            Assert.AreEqual(0, Jackpot().Read(null));
        }
    }
}
