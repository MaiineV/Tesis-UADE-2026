using NUnit.Framework;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;

namespace Rollgeon.Combat.Tests
{
    /// <summary>
    /// Tests de <see cref="ThreatAreaShape.ComputeDirectionalBand"/>: la banda direccional
    /// que sale del boss (piso 1) hacia el jugador, en las 4 direcciones cardinales, más
    /// recorte contra el borde de la grilla.
    /// </summary>
    [TestFixture]
    public class ThreatAreaShapeTests
    {
        private GridManager _grid;

        [SetUp]
        public void SetUp()
        {
            _grid = new GridManager();
        }

        [Test]
        public void ComputeDirectionalBand_PlayerNorth_MarksBandNorthOfSelf()
        {
            // Arrange
            _grid.LoadRoom(NavGraph.Rect(9, 9));
            var self = new GridCoord(4, 4);
            var player = new GridCoord(4, 7);

            // Act
            var tiles = ThreatAreaShape.ComputeDirectionalBand(_grid, self, player, halfWidth: 1, depth: 2);

            // Assert
            Assert.AreEqual(6, tiles.Count);
            Assert.IsTrue(tiles.Contains(new GridCoord(3, 5)));
            Assert.IsTrue(tiles.Contains(new GridCoord(4, 5)));
            Assert.IsTrue(tiles.Contains(new GridCoord(5, 5)));
            Assert.IsTrue(tiles.Contains(new GridCoord(3, 6)));
            Assert.IsTrue(tiles.Contains(new GridCoord(4, 6)));
            Assert.IsTrue(tiles.Contains(new GridCoord(5, 6)));
            Assert.IsFalse(tiles.Contains(self), "La banda nunca incluye la propia fila/columna del boss.");
        }

        [Test]
        public void ComputeDirectionalBand_PlayerSouth_MarksBandSouthOfSelf()
        {
            // Arrange
            _grid.LoadRoom(NavGraph.Rect(9, 9));
            var self = new GridCoord(4, 4);
            var player = new GridCoord(4, 1);

            // Act
            var tiles = ThreatAreaShape.ComputeDirectionalBand(_grid, self, player, halfWidth: 1, depth: 2);

            // Assert
            Assert.AreEqual(6, tiles.Count);
            Assert.IsTrue(tiles.Contains(new GridCoord(3, 3)));
            Assert.IsTrue(tiles.Contains(new GridCoord(4, 3)));
            Assert.IsTrue(tiles.Contains(new GridCoord(5, 3)));
            Assert.IsTrue(tiles.Contains(new GridCoord(3, 2)));
            Assert.IsTrue(tiles.Contains(new GridCoord(4, 2)));
            Assert.IsTrue(tiles.Contains(new GridCoord(5, 2)));
        }

        [Test]
        public void ComputeDirectionalBand_PlayerEast_MarksBandEastOfSelf()
        {
            // Arrange
            _grid.LoadRoom(NavGraph.Rect(9, 9));
            var self = new GridCoord(4, 4);
            var player = new GridCoord(7, 4);

            // Act
            var tiles = ThreatAreaShape.ComputeDirectionalBand(_grid, self, player, halfWidth: 1, depth: 2);

            // Assert
            Assert.AreEqual(6, tiles.Count);
            Assert.IsTrue(tiles.Contains(new GridCoord(5, 3)));
            Assert.IsTrue(tiles.Contains(new GridCoord(5, 4)));
            Assert.IsTrue(tiles.Contains(new GridCoord(5, 5)));
            Assert.IsTrue(tiles.Contains(new GridCoord(6, 3)));
            Assert.IsTrue(tiles.Contains(new GridCoord(6, 4)));
            Assert.IsTrue(tiles.Contains(new GridCoord(6, 5)));
        }

        [Test]
        public void ComputeDirectionalBand_PlayerWest_MarksBandWestOfSelf()
        {
            // Arrange
            _grid.LoadRoom(NavGraph.Rect(9, 9));
            var self = new GridCoord(4, 4);
            var player = new GridCoord(1, 4);

            // Act
            var tiles = ThreatAreaShape.ComputeDirectionalBand(_grid, self, player, halfWidth: 1, depth: 2);

            // Assert
            Assert.AreEqual(6, tiles.Count);
            Assert.IsTrue(tiles.Contains(new GridCoord(3, 3)));
            Assert.IsTrue(tiles.Contains(new GridCoord(3, 4)));
            Assert.IsTrue(tiles.Contains(new GridCoord(3, 5)));
            Assert.IsTrue(tiles.Contains(new GridCoord(2, 3)));
            Assert.IsTrue(tiles.Contains(new GridCoord(2, 4)));
            Assert.IsTrue(tiles.Contains(new GridCoord(2, 5)));
        }

        [Test]
        public void ComputeDirectionalBand_DiagonalDelta_DominantAxisWins()
        {
            // Arrange — |dx|(=5) > |dy|(=1) ⇒ Este, igual que Cardinal.FromDelta.
            _grid.LoadRoom(NavGraph.Rect(11, 9));
            var self = new GridCoord(2, 4);
            var player = new GridCoord(7, 5);

            // Act
            var tiles = ThreatAreaShape.ComputeDirectionalBand(_grid, self, player, halfWidth: 1, depth: 2);

            // Assert
            Assert.AreEqual(6, tiles.Count);
            Assert.IsTrue(tiles.Contains(new GridCoord(3, 3)));
            Assert.IsTrue(tiles.Contains(new GridCoord(4, 3)));
        }

        [Test]
        public void ComputeDirectionalBand_NearGridEdge_ClipsOutOfBoundsTiles()
        {
            // Arrange — boss en la esquina (0,0): la banda perpendicular (eje X, centrada
            // en 0) pide columna -1, que no existe en la grilla 5x5 (X en [0,4]).
            _grid.LoadRoom(NavGraph.Rect(5, 5));
            var self = new GridCoord(0, 0);
            var player = new GridCoord(0, 3);

            // Act
            var tiles = ThreatAreaShape.ComputeDirectionalBand(_grid, self, player, halfWidth: 1, depth: 2);

            // Assert — de las 6 casillas pedidas, las 2 con X=-1 quedan recortadas.
            Assert.AreEqual(4, tiles.Count);
            Assert.IsTrue(tiles.Contains(new GridCoord(0, 1)));
            Assert.IsTrue(tiles.Contains(new GridCoord(1, 1)));
            Assert.IsTrue(tiles.Contains(new GridCoord(0, 2)));
            Assert.IsTrue(tiles.Contains(new GridCoord(1, 2)));
            Assert.IsFalse(tiles.Contains(new GridCoord(-1, 1)));
            Assert.IsFalse(tiles.Contains(new GridCoord(-1, 2)));
        }

        [Test]
        public void ComputeDirectionalBand_NullGrid_ReturnsEmpty()
        {
            var tiles = ThreatAreaShape.ComputeDirectionalBand(
                null, new GridCoord(0, 0), new GridCoord(0, 1), halfWidth: 1, depth: 2);

            Assert.AreEqual(0, tiles.Count);
        }

        [Test]
        public void ComputeScatteredSquares_ReturnsAtMostCountTimesWidthSquaredTiles()
        {
            // Arrange — sala grande, sin recorte esperable contra el borde.
            _grid.LoadRoom(NavGraph.Rect(20, 20));

            // Act
            var tiles = ThreatAreaShape.ComputeScatteredSquares(_grid, new System.Random(1), count: 3, squareWidth: 2);

            // Assert
            Assert.Greater(tiles.Count, 0);
            Assert.LessOrEqual(tiles.Count, 3 * 2 * 2);
        }

        [Test]
        public void ComputeScatteredSquares_AllTilesAreWithinGridBounds()
        {
            // Arrange
            _grid.LoadRoom(NavGraph.Rect(9, 9));

            // Act
            var tiles = ThreatAreaShape.ComputeScatteredSquares(_grid, new System.Random(7), count: 5, squareWidth: 2);

            // Assert
            foreach (var c in tiles)
            {
                Assert.IsTrue(c.X >= 0 && c.X < 9 && c.Y >= 0 && c.Y < 9,
                    $"Tile {c} cayó fuera de la grilla 9x9.");
            }
        }

        [Test]
        public void ComputeScatteredSquares_SameSeed_ProducesSameTiles()
        {
            // Arrange
            _grid.LoadRoom(NavGraph.Rect(9, 9));

            // Act
            var first = ThreatAreaShape.ComputeScatteredSquares(_grid, new System.Random(42), count: 3, squareWidth: 2);
            var second = ThreatAreaShape.ComputeScatteredSquares(_grid, new System.Random(42), count: 3, squareWidth: 2);

            // Assert
            CollectionAssert.AreEquivalent(first, second);
        }

        [Test]
        public void ComputeScatteredSquares_ZeroCount_ReturnsEmpty()
        {
            _grid.LoadRoom(NavGraph.Rect(9, 9));

            var tiles = ThreatAreaShape.ComputeScatteredSquares(_grid, new System.Random(1), count: 0, squareWidth: 2);

            Assert.AreEqual(0, tiles.Count);
        }

        [Test]
        public void ComputeScatteredSquares_EmptyRoomGraph_ReturnsEmpty()
        {
            // _grid nunca cargó una sala (stub "infinito", sin tiles para enumerar).
            var tiles = ThreatAreaShape.ComputeScatteredSquares(_grid, new System.Random(1), count: 3, squareWidth: 2);

            Assert.AreEqual(0, tiles.Count);
        }

        [Test]
        public void ComputeScatteredSquares_NullGrid_ReturnsEmpty()
        {
            var tiles = ThreatAreaShape.ComputeScatteredSquares(null, new System.Random(1), count: 3, squareWidth: 2);

            Assert.AreEqual(0, tiles.Count);
        }
    }
}
