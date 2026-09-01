using NUnit.Framework;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;

namespace Rollgeon.Combat.Tests
{
    /// <summary>CrossAroundSelf: brazos ortogonales de largo size, sin la celda central.</summary>
    [TestFixture]
    public class ThreatAreaShapeCrossTests
    {
        private static GridManager MakeGrid()
        {
            var grid = new GridManager();
            grid.LoadRoom(NavGraph.Rect(7, 7));
            return grid;
        }

        [Test]
        public void Cross_Size2_HasEightCells_WithoutCenter()
        {
            var tiles = ThreatAreaShape.Compute(MakeGrid(), new GridCoord(3, 3),
                ThreatShape.CrossAroundSelf, 2, default);

            Assert.AreEqual(8, tiles.Count);
            Assert.IsFalse(tiles.Contains(new GridCoord(3, 3)), "sin la celda del boss");
            Assert.IsTrue(tiles.Contains(new GridCoord(5, 3)));
            Assert.IsTrue(tiles.Contains(new GridCoord(1, 3)));
            Assert.IsTrue(tiles.Contains(new GridCoord(3, 5)));
            Assert.IsTrue(tiles.Contains(new GridCoord(3, 1)));
            Assert.IsFalse(tiles.Contains(new GridCoord(4, 4)), "sin diagonales");
        }

        [Test]
        public void Cross_ClampsToRoomBounds()
        {
            var tiles = ThreatAreaShape.Compute(MakeGrid(), new GridCoord(0, 0),
                ThreatShape.CrossAroundSelf, 2, default);

            Assert.AreEqual(4, tiles.Count, "solo los brazos Este y Norte caben");
        }

        [Test]
        public void Cross_AnchorsOnSelf()
        {
            Assert.IsTrue(ThreatAreaShape.AnchorsOnSelf(ThreatShape.CrossAroundSelf));
        }
    }
}
