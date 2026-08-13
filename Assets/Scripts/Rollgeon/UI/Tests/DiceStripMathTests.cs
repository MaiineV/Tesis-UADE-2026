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
    }
}
