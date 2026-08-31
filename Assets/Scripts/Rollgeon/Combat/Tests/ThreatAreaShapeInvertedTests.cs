using System;
using NUnit.Framework;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;

namespace Rollgeon.Combat.Tests
{
    /// <summary>
    /// Tests de <see cref="ThreatShape.AllExceptSquareAroundSelf"/> — La Banca del Tahúr: la sala
    /// entera menos La Mesa, su 3×3. Incluye la no-regresión de las formas que ya existían y del
    /// índice serializado del enum.
    /// </summary>
    [TestFixture]
    public class ThreatAreaShapeInvertedTests
    {
        private const int RoomWidth = 11;
        private const int RoomHeight = 7;
        private const int RoomTileCount = RoomWidth * RoomHeight;

        private GridManager _grid;

        [SetUp]
        public void SetUp()
        {
            _grid = new GridManager();
        }

        /// <summary>Sala canónica del Tahúr, sin las columnas.</summary>
        private void LoadTahurRoom() => _grid.LoadRoom(NavGraph.Rect(RoomWidth, RoomHeight));

        private static NavGraph RoomWithWalls(int width, int height, params GridCoord[] walls)
        {
            var walkable = new bool[width * height];
            for (int i = 0; i < walkable.Length; i++) walkable[i] = true;
            foreach (var w in walls) walkable[w.Y * width + w.X] = false;
            return NavGraph.FromSnapshot(new GridSnapshot(width, height, walkable));
        }

        [Test]
        public void ComputeAllExceptSquareAroundSelf_BossAtCenter_CoversRoomMinusItsThreeByThree()
        {
            // Arrange
            LoadTahurRoom();
            var self = new GridCoord(5, 3);

            // Act
            var tiles = ThreatAreaShape.ComputeAllExceptSquareAroundSelf(_grid, self, radius: 1);

            // Assert
            Assert.AreEqual(RoomTileCount - 9, tiles.Count, "77 casillas menos La Mesa (3×3).");
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                var mesa = new GridCoord(self.X + dx, self.Y + dy);
                Assert.IsFalse(tiles.Contains(mesa), $"{mesa} es La Mesa — no puede estar amenazada.");
            }
        }

        [Test]
        public void ComputeAllExceptSquareAroundSelf_BossAtCorner_ClipsTheHoleAgainstTheWalls()
        {
            // Arrange — en (0,0) el 3×3 se recorta a las 4 casillas que existen.
            LoadTahurRoom();
            var self = new GridCoord(0, 0);

            // Act
            var tiles = ThreatAreaShape.ComputeAllExceptSquareAroundSelf(_grid, self, radius: 1);

            // Assert
            Assert.AreEqual(RoomTileCount - 4, tiles.Count, "El hueco se recorta solo contra el borde.");
            Assert.IsFalse(tiles.Contains(new GridCoord(0, 0)));
            Assert.IsFalse(tiles.Contains(new GridCoord(1, 0)));
            Assert.IsFalse(tiles.Contains(new GridCoord(0, 1)));
            Assert.IsFalse(tiles.Contains(new GridCoord(1, 1)));
            Assert.IsTrue(tiles.Contains(new GridCoord(2, 0)), "La casilla pegada al hueco sí cobra.");
            Assert.IsTrue(tiles.Contains(new GridCoord(0, 2)));
        }

        [Test]
        public void ComputeAllExceptSquareAroundSelf_BossAgainstAWall_LeavesAWayOut()
        {
            // Arrange — la salida de la sala es meterse en el hueco: con el jefe pegado a la pared
            // tiene que seguir habiendo casillas sin amenazar donde pararse.
            LoadTahurRoom();
            var self = new GridCoord(5, 0);

            // Act
            var tiles = ThreatAreaShape.ComputeAllExceptSquareAroundSelf(_grid, self, radius: 1);

            // Assert — 3 columnas × 2 filas de hueco (la fila y=-1 no existe).
            Assert.AreEqual(RoomTileCount - 6, tiles.Count);
            Assert.IsFalse(tiles.Contains(new GridCoord(4, 0)));
            Assert.IsFalse(tiles.Contains(new GridCoord(6, 1)));
        }

        [Test]
        public void ComputeAllExceptSquareAroundSelf_ZeroRadius_StillSparesTheBossTile()
        {
            // Arrange
            LoadTahurRoom();
            var self = new GridCoord(5, 3);

            // Act
            var tiles = ThreatAreaShape.ComputeAllExceptSquareAroundSelf(_grid, self, radius: 0);

            // Assert
            Assert.AreEqual(RoomTileCount - 1, tiles.Count);
            Assert.IsFalse(tiles.Contains(self));
        }

        [Test]
        public void ComputeAllExceptSquareAroundSelf_RoomWithColumns_NeverPaintsTheObstacles()
        {
            // Arrange — las 4 columnas de la sala del Tahúr, ninguna dentro de su 3×3.
            var walls = new[]
            {
                new GridCoord(3, 1), new GridCoord(7, 1),
                new GridCoord(3, 5), new GridCoord(7, 5),
            };
            _grid.LoadRoom(RoomWithWalls(RoomWidth, RoomHeight, walls));
            var self = new GridCoord(5, 3);

            // Act
            var tiles = ThreatAreaShape.ComputeAllExceptSquareAroundSelf(_grid, self, radius: 1);

            // Assert
            Assert.AreEqual(RoomTileCount - walls.Length - 9, tiles.Count);
            foreach (var wall in walls)
                Assert.IsFalse(tiles.Contains(wall), $"La columna {wall} no es casilla caminable.");
        }

        [Test]
        public void ComputeAllExceptSquareAroundSelf_PlusSquareAroundSelf_PartitionTheWholeRoom()
        {
            // Arrange — la Banca es el complemento exacto de La Mesa: sin intersección y sin huecos.
            LoadTahurRoom();
            var self = new GridCoord(2, 5);

            // Act
            var banca = ThreatAreaShape.ComputeAllExceptSquareAroundSelf(_grid, self, radius: 1);
            var mesa = ThreatAreaShape.Compute(_grid, self, ThreatShape.SquareAroundSelf, size: 1, HalfRoomAxis.Vertical);

            // Assert
            foreach (var tile in mesa)
                Assert.IsFalse(banca.Contains(tile), $"{tile} está en La Mesa y en La Banca a la vez.");
            Assert.AreEqual(RoomTileCount, banca.Count + mesa.Count);
        }

        [Test]
        public void ComputeAllExceptSquareAroundSelf_EmptyRoomGraph_ReturnsEmpty()
        {
            // Arrange — _grid nunca cargó una sala: sin bounds reales no hay sala que pintar,
            // igual que Row/Column/HalfRoom/RoomSector.
            // Act + Assert
            Assert.IsEmpty(ThreatAreaShape.ComputeAllExceptSquareAroundSelf(_grid, GridCoord.Zero, radius: 1));
        }

        [Test]
        public void ComputeAllExceptSquareAroundSelf_NullGrid_ReturnsEmpty()
        {
            Assert.IsEmpty(ThreatAreaShape.ComputeAllExceptSquareAroundSelf(null, GridCoord.Zero, radius: 1));
        }

        [Test]
        public void Compute_AllExceptSquareAroundSelfShape_MatchesTheDirectHelper()
        {
            // Arrange — el nodo de AI entra por Compute con el radio en `size`.
            LoadTahurRoom();
            var self = new GridCoord(5, 3);

            // Act
            var viaCompute = ThreatAreaShape.Compute(
                _grid, self, ThreatShape.AllExceptSquareAroundSelf, size: 1, HalfRoomAxis.Vertical);
            var direct = ThreatAreaShape.ComputeAllExceptSquareAroundSelf(_grid, self, radius: 1);

            // Assert
            CollectionAssert.AreEquivalent(direct, viaCompute);
        }

        [Test]
        public void AnchorsOnSelf_OnlyTheSelfCenteredShapes_AreTrue()
        {
            // Arrange + Act + Assert — el caller que resuelve el centro decide por acá; una shape
            // nueva anclada en el boss tiene que sumarse al helper, no a cada nodo.
            var selfCentered = new[]
            {
                ThreatShape.SquareAroundSelf,
                ThreatShape.AllExceptSquareAroundSelf,
                ThreatShape.ColumnAroundSelf,
                ThreatShape.CrossAroundSelf,
            };

            foreach (var shape in selfCentered)
                Assert.IsTrue(ThreatAreaShape.AnchorsOnSelf(shape), $"{shape} se centra en el jefe.");

            foreach (ThreatShape shape in Enum.GetValues(typeof(ThreatShape)))
            {
                if (Array.IndexOf(selfCentered, shape) >= 0) continue;
                Assert.IsFalse(ThreatAreaShape.AnchorsOnSelf(shape), $"{shape} se centra en el jugador.");
            }
        }

        // =====================================================================
        // No-regresión — la forma nueva se appendeó, no se insertó
        // =====================================================================

        [Test]
        public void ThreatShape_SerializedIndices_DidNotShift()
        {
            // Arrange + Act + Assert — los .asset de los jefes guardan el índice, no el nombre:
            // si alguno de estos números cambia, jefes ya autorados cambian de forma solos.
            Assert.AreEqual(0, (int)ThreatShape.SquareAroundPlayer);
            Assert.AreEqual(1, (int)ThreatShape.Row);
            Assert.AreEqual(2, (int)ThreatShape.Column);
            Assert.AreEqual(3, (int)ThreatShape.HalfRoom);
            Assert.AreEqual(4, (int)ThreatShape.DirectionalBand);
            Assert.AreEqual(5, (int)ThreatShape.ScatteredSquares);
            Assert.AreEqual(6, (int)ThreatShape.SquareAroundSelf);
            Assert.AreEqual(7, (int)ThreatShape.RoomSector);
            Assert.AreEqual(8, (int)ThreatShape.AllExceptSquareAroundSelf);
            Assert.AreEqual(9, (int)ThreatShape.ColumnAroundSelf);
            Assert.AreEqual(10, (int)ThreatShape.GridPartition);
            Assert.AreEqual(11, (int)ThreatShape.DirectionalCone,
                "La forma nueva va al final de la lista.");
        }

        [Test]
        public void Compute_ExistingShapes_KeepTheirCoverage()
        {
            // Arrange
            LoadTahurRoom();
            var center = new GridCoord(5, 3);

            // Act
            var square = ThreatAreaShape.Compute(_grid, center, ThreatShape.SquareAroundPlayer, 1, HalfRoomAxis.Vertical);
            var selfSquare = ThreatAreaShape.Compute(_grid, center, ThreatShape.SquareAroundSelf, 1, HalfRoomAxis.Vertical);
            var row = ThreatAreaShape.Compute(_grid, center, ThreatShape.Row, 1, HalfRoomAxis.Vertical);
            var column = ThreatAreaShape.Compute(_grid, center, ThreatShape.Column, 1, HalfRoomAxis.Vertical);
            var half = ThreatAreaShape.Compute(_grid, center, ThreatShape.HalfRoom, 1, HalfRoomAxis.Vertical);
            var sector = ThreatAreaShape.Compute(_grid, center, ThreatShape.RoomSector, 1, HalfRoomAxis.Vertical);

            // Assert
            Assert.AreEqual(9, square.Count, "3×3 centrado en el jugador.");
            Assert.AreEqual(9, selfSquare.Count, "3×3 centrado en el boss.");
            Assert.AreEqual(RoomWidth, row.Count, "La fila del jugador, entera.");
            Assert.AreEqual(RoomHeight, column.Count, "La columna del jugador, entera.");
            Assert.AreEqual(6 * RoomHeight, half.Count, "Mitad izquierda: columnas 0..5.");
            // 4×4 y no el 4×3 de antes: las bandas en Y pasaron a partirse con la misma regla que
            // las de X, así que la fila del medio dejó de quedar afuera de todo bloque. Era la fila
            // del Croupier — 11 casillas donde no se prendía fuego nunca y el jugador acampaba.
            Assert.AreEqual(16, sector.Count, "Los bloques del paño miden 4×4.");
        }
    }
}
