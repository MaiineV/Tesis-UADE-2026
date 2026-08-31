using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Dice;
using Rollgeon.UI.Screens;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Lógica pura de la tira de la bolsa: orden ascendente por caras y diff de
    /// edición única entre estados consecutivos.
    /// </summary>
    [TestFixture]
    public class DiceStripMathTests
    {
        // ---------------- SortAscending ----------------

        [Test]
        public void SortAscending_OrdersByMaxFace()
        {
            var bag = new List<DiceType> { DiceType.D20, DiceType.D4, DiceType.D12 };

            var sorted = DiceStripMath.SortAscending(bag);

            CollectionAssert.AreEqual(
                new[] { DiceType.D4, DiceType.D12, DiceType.D20 }, sorted);
        }

        [Test]
        public void SortAscending_D3BeforeD4_ByFacesNotEnumOrder()
        {
            // D3 = 6 en el enum pero 3 caras — el orden es por MaxFace.
            var bag = new List<DiceType> { DiceType.D4, DiceType.D3 };

            var sorted = DiceStripMath.SortAscending(bag);

            CollectionAssert.AreEqual(new[] { DiceType.D3, DiceType.D4 }, sorted);
        }

        [Test]
        public void SortAscending_KeepsDuplicatesTogether()
        {
            var bag = new List<DiceType> { DiceType.D6, DiceType.D4, DiceType.D6, DiceType.D4 };

            var sorted = DiceStripMath.SortAscending(bag);

            CollectionAssert.AreEqual(
                new[] { DiceType.D4, DiceType.D4, DiceType.D6, DiceType.D6 }, sorted);
        }

        // ---------------- ComputeDiff ----------------

        [Test]
        public void ComputeDiff_EqualLists_ReturnsNone()
        {
            var a = new List<DiceType> { DiceType.D4, DiceType.D6 };
            var b = new List<DiceType> { DiceType.D4, DiceType.D6 };

            var diff = DiceStripMath.ComputeDiff(a, b);

            Assert.AreEqual(DiceStripMath.StripChange.None, diff.Change);
        }

        [Test]
        public void ComputeDiff_MiddleInsertion_ReturnsInsertAtIndex()
        {
            var oldBag = new List<DiceType> { DiceType.D4, DiceType.D12 };
            var newBag = new List<DiceType> { DiceType.D4, DiceType.D6, DiceType.D12 };

            var diff = DiceStripMath.ComputeDiff(oldBag, newBag);

            Assert.AreEqual(DiceStripMath.StripChange.Insert, diff.Change);
            Assert.AreEqual(1, diff.Index);
        }

        [Test]
        public void ComputeDiff_TailInsertion_ReturnsInsertAtEnd()
        {
            var oldBag = new List<DiceType> { DiceType.D4 };
            var newBag = new List<DiceType> { DiceType.D4, DiceType.D20 };

            var diff = DiceStripMath.ComputeDiff(oldBag, newBag);

            Assert.AreEqual(DiceStripMath.StripChange.Insert, diff.Change);
            Assert.AreEqual(1, diff.Index);
        }

        [Test]
        public void ComputeDiff_MiddleRemoval_ReturnsRemoveAtIndex()
        {
            var oldBag = new List<DiceType> { DiceType.D4, DiceType.D6, DiceType.D12 };
            var newBag = new List<DiceType> { DiceType.D4, DiceType.D12 };

            var diff = DiceStripMath.ComputeDiff(oldBag, newBag);

            Assert.AreEqual(DiceStripMath.StripChange.Remove, diff.Change);
            Assert.AreEqual(1, diff.Index);
        }

        [Test]
        public void ComputeDiff_ClearToEmpty_IsSingleRemovalOrRebuild()
        {
            // Vaciar de 3 a 0 no es edición única → Rebuild.
            var oldBag = new List<DiceType> { DiceType.D4, DiceType.D6, DiceType.D12 };
            var newBag = new List<DiceType>();

            var diff = DiceStripMath.ComputeDiff(oldBag, newBag);

            Assert.AreEqual(DiceStripMath.StripChange.Rebuild, diff.Change);
        }

        [Test]
        public void ComputeDiff_SameCountDifferentContent_ReturnsRebuild()
        {
            var a = new List<DiceType> { DiceType.D4, DiceType.D6 };
            var b = new List<DiceType> { DiceType.D4, DiceType.D8 };

            var diff = DiceStripMath.ComputeDiff(a, b);

            Assert.AreEqual(DiceStripMath.StripChange.Rebuild, diff.Change);
        }

        // ---------------- SlotX ----------------

        [TestCase(0, 1, 0f)]
        [TestCase(0, 2, -59f)]
        [TestCase(1, 2, 59f)]
        [TestCase(2, 5, 0f)]
        public void SlotX_CentersStrip(int index, int count, float expected)
        {
            Assert.AreEqual(expected, DiceStripMath.SlotX(index, count, spacing: 118f), 0.001f);
        }

        // ---------------- FitSpacing ----------------

        [TestCase(0)]
        [TestCase(1)]
        public void FitSpacing_ZeroOrOneDie_ReturnsBaseSpacing(int count)
        {
            Assert.AreEqual(118f,
                DiceStripMath.FitSpacing(count, baseSpacing: 118f, dieSize: 96f, availableWidth: 900f));
        }

        [Test]
        public void FitSpacing_SmallBagFits_KeepsAuthoredSpacing()
        {
            // 5 dados a 118px de spacing ocupan 4*118 + 96 = 568 < 900: sin clamp.
            Assert.AreEqual(118f,
                DiceStripMath.FitSpacing(5, baseSpacing: 118f, dieSize: 96f, availableWidth: 900f));
        }

        [Test]
        public void FitSpacing_LargeBag_CompressesToExactFit()
        {
            // 12 dados: (900 - 96) / 11 = 73.09… — la tira entra justa en el ancho.
            Assert.AreEqual((900f - 96f) / 11f,
                DiceStripMath.FitSpacing(12, baseSpacing: 118f, dieSize: 96f, availableWidth: 900f),
                0.001f);
        }

        [Test]
        public void FitSpacing_ExactBoundary_KeepsAuthoredSpacing()
        {
            // El fit da exactamente el base: no debe comprimir de más.
            // width = die + (count-1)*base = 96 + 4*118 = 568.
            Assert.AreEqual(118f,
                DiceStripMath.FitSpacing(5, baseSpacing: 118f, dieSize: 96f, availableWidth: 568f));
        }

        [Test]
        public void FitSpacing_AbsurdlyNarrowWidth_ClampsAtZero()
        {
            // Contenedor más angosto que un dado: solape total, nunca negativo.
            Assert.AreEqual(0f,
                DiceStripMath.FitSpacing(10, baseSpacing: 118f, dieSize: 96f, availableWidth: 50f));
        }
    }
}
