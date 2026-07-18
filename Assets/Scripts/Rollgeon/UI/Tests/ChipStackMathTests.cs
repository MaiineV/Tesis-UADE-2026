using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.UI.HUD;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Lógica pura del HUD de pilas de fichas: diff por prefijo, composición
    /// vida+escudo, regla visual del oro y formatos de label.
    /// </summary>
    [TestFixture]
    public class ChipStackMathTests
    {
        // ---------------- ComputeDiff ----------------

        [Test]
        public void ComputeDiff_EqualLists_ReturnsEmpty()
        {
            var displayed = new List<int> { 0, 0, 1 };
            var target = new List<int> { 0, 0, 1 };

            var diff = ChipStackMath.ComputeDiff(displayed, target);

            Assert.IsTrue(diff.IsEmpty);
            Assert.AreEqual(3, diff.KeepCount);
        }

        [Test]
        public void ComputeDiff_PureGain_AddsOnTop()
        {
            var diff = ChipStackMath.ComputeDiff(new List<int> { 0, 0 }, new List<int> { 0, 0, 0, 0 });

            Assert.AreEqual(2, diff.KeepCount);
            Assert.AreEqual(0, diff.RemoveCount);
            Assert.AreEqual(2, diff.AddCount);
        }

        [Test]
        public void ComputeDiff_PureLoss_RemovesFromTop()
        {
            var diff = ChipStackMath.ComputeDiff(new List<int> { 0, 0, 0, 0 }, new List<int> { 0 });

            Assert.AreEqual(1, diff.KeepCount);
            Assert.AreEqual(3, diff.RemoveCount);
            Assert.AreEqual(0, diff.AddCount);
        }

        [Test]
        public void ComputeDiff_MidListSpriteChange_RemovesToPrefixAndReadds()
        {
            // Vida baja y escudo sube en el mismo frame: [hp,hp,hp] → [hp,hp,shield].
            var diff = ChipStackMath.ComputeDiff(new List<int> { 0, 0, 0 }, new List<int> { 0, 0, 1 });

            Assert.AreEqual(2, diff.KeepCount);
            Assert.AreEqual(1, diff.RemoveCount);
            Assert.AreEqual(1, diff.AddCount);
        }

        // ---------------- BuildHealthChipIds ----------------

        [Test]
        public void BuildHealthChipIds_HpAndShield_ShieldOnTop()
        {
            var buffer = new List<int>();

            ChipStackMath.BuildHealthChipIds(3, 2, buffer);

            CollectionAssert.AreEqual(new[] { 0, 0, 0, 1, 1 }, buffer);
        }

        [Test]
        public void BuildHealthChipIds_NegativeValues_ClampToEmpty()
        {
            var buffer = new List<int> { 9, 9 };

            ChipStackMath.BuildHealthChipIds(-3, -1, buffer);

            Assert.AreEqual(0, buffer.Count);
        }

        // ---------------- Labels ----------------

        [Test]
        public void FormatHealthLabel_NoShield_PlainFraction()
        {
            Assert.AreEqual("10/10", ChipStackMath.FormatHealthLabel(10, 0, 10, "A3B3B1"));
        }

        [Test]
        public void FormatHealthLabel_WithShield_ColoredTotalOverMax()
        {
            Assert.AreEqual("<color=#A3B3B1>12</color>/10",
                ChipStackMath.FormatHealthLabel(10, 2, 10, "A3B3B1"));
        }

        [Test]
        public void FormatEnergyLabel_ReturnsFraction()
        {
            Assert.AreEqual("4/4", ChipStackMath.FormatEnergyLabel(4, 4));
        }

        [Test]
        public void FormatGoldLabel_ReturnsPlainNumber()
        {
            Assert.AreEqual("17", ChipStackMath.FormatGoldLabel(17));
        }

        // ---------------- Gold visual rule ----------------

        [TestCase(0, 0, false)]
        [TestCase(1, 1, false)]
        [TestCase(4, 4, false)]
        [TestCase(5, 4, true)]
        [TestCase(6, 4, true)]
        [TestCase(99, 4, true)]
        public void ComputeGoldVisual_MapsGoldToFlatChipsAndTilted(int gold, int expectedFlat, bool expectedTilted)
        {
            var visual = ChipStackMath.ComputeGoldVisual(gold);

            Assert.AreEqual(expectedFlat, visual.FlatChips);
            Assert.AreEqual(expectedTilted, visual.ShowTilted);
        }

        [TestCase(4, 5, ChipStackMath.GoldTransition.ChipsChanged)]
        [TestCase(5, 9, ChipStackMath.GoldTransition.ShakeOnly)]
        [TestCase(9, 5, ChipStackMath.GoldTransition.ShakeOnly)]
        [TestCase(5, 4, ChipStackMath.GoldTransition.ChipsChanged)]
        [TestCase(7, 7, ChipStackMath.GoldTransition.None)]
        [TestCase(0, 3, ChipStackMath.GoldTransition.ChipsChanged)]
        public void ClassifyGoldTransition_ClassifiesRegimes(int oldGold, int newGold,
            ChipStackMath.GoldTransition expected)
        {
            Assert.AreEqual(expected, ChipStackMath.ClassifyGoldTransition(oldGold, newGold));
        }
    }
}
