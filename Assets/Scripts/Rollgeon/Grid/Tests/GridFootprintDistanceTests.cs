using NUnit.Framework;
using UnityEngine;

namespace Rollgeon.Grid.Tests
{
    /// <summary>Distancia rect-a-rect: celda más cercana de A a celda más cercana de B.</summary>
    [TestFixture]
    public class GridFootprintDistanceTests
    {
        static readonly Vector2Int Two = new Vector2Int(2, 2);

        [Test]
        public void Manhattan_UnitVsUnit_MatchesGridCoord()
        {
            var a = new GridCoord(1, 2);
            var b = new GridCoord(4, 7);
            Assert.AreEqual(a.Manhattan(b),
                GridFootprint.ManhattanDistance(a, Vector2Int.one, b, Vector2Int.one));
            Assert.AreEqual(a.Chebyshev(b),
                GridFootprint.ChebyshevDistance(a, Vector2Int.one, b, Vector2Int.one));
        }

        [Test]
        public void Manhattan_AdjacentByNonAnchorCell_IsOne()
        {
            // 2×2 en (1,1) cubre (1,1)-(2,2); la celda (3,2) está pegada a (2,2), no al ancla.
            Assert.AreEqual(1, GridFootprint.ManhattanDistance(new GridCoord(1, 1), Two, new GridCoord(3, 2)));
            // Desde el ancla sería 3 — la métrica vieja fallaba acá.
            Assert.AreEqual(3, new GridCoord(1, 1).Manhattan(new GridCoord(3, 2)));
        }

        [Test]
        public void Manhattan_Overlapping_IsZero()
        {
            Assert.AreEqual(0, GridFootprint.ManhattanDistance(new GridCoord(1, 1), Two, new GridCoord(2, 2), Two));
        }

        [Test]
        public void Manhattan_Diagonal_SumsBothAxes()
        {
            // 2×2 en (0,0) cubre hasta (1,1); (3,3) queda a (2,2) de la esquina más cercana.
            Assert.AreEqual(4, GridFootprint.ManhattanDistance(new GridCoord(0, 0), Two, new GridCoord(3, 3)));
            Assert.AreEqual(2, GridFootprint.ChebyshevDistance(new GridCoord(0, 0), Two, new GridCoord(3, 3)));
        }

        [Test]
        public void Manhattan_RectVsRect_UsesNearestEdges()
        {
            // 2×1 en (0,0) cubre (0,0)-(1,0); 2×2 en (4,0) cubre (4,0)-(5,1): las celdas más
            // cercanas son (1,0) y (4,0) → 3 pasos.
            Assert.AreEqual(3, GridFootprint.ManhattanDistance(
                new GridCoord(0, 0), new Vector2Int(2, 1), new GridCoord(4, 0), Two));
        }

        [Test]
        public void Distance_NormalizesNonPositiveFootprints()
        {
            Assert.AreEqual(3, GridFootprint.ManhattanDistance(
                new GridCoord(0, 0), new Vector2Int(0, -2), new GridCoord(3, 0)));
        }
    }
}
