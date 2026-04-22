using NUnit.Framework;

namespace Rollgeon.Grid.Tests
{
    [TestFixture]
    public class GridCoordTests
    {
        [Test]
        public void Equals_MatchesSameXY()
        {
            var a = new GridCoord(3, 4);
            var b = new GridCoord(3, 4);
            Assert.IsTrue(a.Equals(b));
            Assert.IsTrue(a == b);
            Assert.IsFalse(a != b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void NotEquals_DifferentXY()
        {
            Assert.IsFalse(new GridCoord(1, 2).Equals(new GridCoord(2, 1)));
            Assert.IsTrue(new GridCoord(1, 2) != new GridCoord(2, 1));
        }

        [Test]
        public void Neighbors4_ReturnsFourCardinalTiles()
        {
            var origin = new GridCoord(5, 5);
            var expected = new[]
            {
                new GridCoord(5, 6),
                new GridCoord(6, 5),
                new GridCoord(5, 4),
                new GridCoord(4, 5),
            };
            CollectionAssert.AreEquivalent(expected, origin.Neighbors4());
        }

        [Test]
        public void Manhattan_IsAbsXPlusAbsY()
        {
            var a = new GridCoord(0, 0);
            var b = new GridCoord(3, -4);
            Assert.AreEqual(7, a.Manhattan(b));
        }

        [Test]
        public void Chebyshev_IsMaxOfAbsXAbsY()
        {
            var a = new GridCoord(0, 0);
            var b = new GridCoord(3, -4);
            Assert.AreEqual(4, a.Chebyshev(b));
        }

        [Test]
        public void Operators_AddSubtractWorkComponentwise()
        {
            Assert.AreEqual(new GridCoord(4, 6), new GridCoord(1, 2) + new GridCoord(3, 4));
            Assert.AreEqual(new GridCoord(-2, -2), new GridCoord(1, 2) - new GridCoord(3, 4));
        }
    }
}
