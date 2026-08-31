using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rollgeon.Grid;

namespace Rollgeon.Movement.Tests
{
    [TestFixture]
    public class MovementServiceTests
    {
        private GridManager _grid;
        private MovementService _movement;

        [SetUp]
        public void SetUp()
        {
            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(5, 5));
            _movement = new MovementService(_grid);
        }

        [Test]
        public void GetReachableTiles_Range0_EmptyWithoutOrigin()
        {
            var tiles = _movement.GetReachableTiles(new GridCoord(2, 2), 0, includeOrigin: false);
            Assert.AreEqual(0, tiles.Count);
        }

        [Test]
        public void GetReachableTiles_Range0_IncludeOrigin_ReturnsOnlyOrigin()
        {
            var tiles = _movement.GetReachableTiles(new GridCoord(2, 2), 0, includeOrigin: true);
            Assert.AreEqual(1, tiles.Count);
            Assert.AreEqual(new GridCoord(2, 2), tiles[0]);
        }

        [Test]
        public void GetReachableTiles_Range1_Returns4Neighbors()
        {
            var tiles = _movement.GetReachableTiles(new GridCoord(2, 2), 1);
            var expected = new[]
            {
                new GridCoord(2, 3),
                new GridCoord(3, 2),
                new GridCoord(2, 1),
                new GridCoord(1, 2),
            };
            CollectionAssert.AreEquivalent(expected, tiles);
        }

        [Test]
        public void GetReachableTiles_Range2_Returns12TilesInOpenGrid()
        {
            // BFS dist<=2 en grilla abierta 5x5: 12 tiles distintas del origen.
            var tiles = _movement.GetReachableTiles(new GridCoord(2, 2), 2);
            Assert.AreEqual(12, tiles.Count);
            Assert.IsFalse(tiles.Contains(new GridCoord(2, 2)));
        }

        [Test]
        public void GetReachableTiles_SkipsUnwalkable()
        {
            var walkable = Enumerable.Repeat(true, 25).ToArray();
            // (1,2): index 2*5+1 = 11 blocked
            walkable[11] = false;
            _grid.LoadRoom(NavGraph.FromSnapshot(new GridSnapshot(5, 5, walkable)));

            var tiles = _movement.GetReachableTiles(new GridCoord(2, 2), 1);
            Assert.IsFalse(tiles.Contains(new GridCoord(1, 2)));
            Assert.IsTrue(tiles.Contains(new GridCoord(2, 1)));
        }

        [Test]
        public void GetReachableTiles_SkipsOccupiedTiles()
        {
            _grid.Register(Guid.NewGuid(), new GridCoord(3, 2));
            var tiles = _movement.GetReachableTiles(new GridCoord(2, 2), 1);
            Assert.IsFalse(tiles.Contains(new GridCoord(3, 2)));
        }

        // ---------------- TryTruncateMoveAt (cancel de caminata) ----------------

        [Test]
        public void TryTruncateMoveAt_AfterMove_RelocatesEntityWithoutRaisingOnEntityMoved()
        {
            // Arrange — Move ya adelantó la posición lógica al destino; el cancel
            // trunca de vuelta a una celda intermedia del path.
            var entity = Guid.NewGuid();
            _grid.Register(entity, new GridCoord(0, 0));
            int movedCount = 0;
            _movement.OnEntityMoved += (_, _, _, _) => movedCount++;
            int truncatedCount = 0;
            GridCoord truncatedFrom = default, truncatedTo = default;
            ((IMoveTruncationService)_movement).OnEntityMoveTruncated += (guid, from, to) =>
            {
                truncatedCount++;
                truncatedFrom = from;
                truncatedTo = to;
            };
            Assert.IsTrue(_movement.Move(entity, new GridCoord(0, 4)));
            Assert.AreEqual(1, movedCount, "pre-condition");

            // Act
            bool result = ((IMoveTruncationService)_movement).TryTruncateMoveAt(entity, new GridCoord(0, 2));

            // Assert — la entidad quedó en la celda de parada, sin re-emitir
            // OnEntityMoved (los suscriptores de tiles ya procesaron el path completo).
            Assert.IsTrue(result);
            Assert.IsTrue(_grid.TryGetPosition(entity, out var pos));
            Assert.AreEqual(new GridCoord(0, 2), pos);
            Assert.AreEqual(1, movedCount, "Truncar NO debe emitir OnEntityMoved.");
            Assert.AreEqual(1, truncatedCount);
            Assert.AreEqual(new GridCoord(0, 4), truncatedFrom);
            Assert.AreEqual(new GridCoord(0, 2), truncatedTo);
        }

        [Test]
        public void TryTruncateMoveAt_CellOccupied_ReturnsFalseAndKeepsPosition()
        {
            // Arrange
            var entity = Guid.NewGuid();
            _grid.Register(entity, new GridCoord(0, 4));
            _grid.Register(Guid.NewGuid(), new GridCoord(0, 2));

            // Act
            bool result = ((IMoveTruncationService)_movement).TryTruncateMoveAt(entity, new GridCoord(0, 2));

            // Assert — sin truncación la posición lógica queda donde estaba (el
            // resync visual del pawn snapea al destino, sin desync).
            Assert.IsFalse(result);
            Assert.IsTrue(_grid.TryGetPosition(entity, out var pos));
            Assert.AreEqual(new GridCoord(0, 4), pos);
        }

        [Test]
        public void TryTruncateMoveAt_SameCell_ReturnsTrueWithoutEvent()
        {
            // Arrange
            var entity = Guid.NewGuid();
            _grid.Register(entity, new GridCoord(1, 1));
            int truncatedCount = 0;
            ((IMoveTruncationService)_movement).OnEntityMoveTruncated += (_, _, _) => truncatedCount++;

            // Act
            bool result = ((IMoveTruncationService)_movement).TryTruncateMoveAt(entity, new GridCoord(1, 1));

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(0, truncatedCount, "Truncar a la celda propia es no-op sin evento.");
        }

        [Test]
        public void TryTruncateMoveAt_UnregisteredEntity_ReturnsFalse()
        {
            Assert.IsFalse(((IMoveTruncationService)_movement)
                .TryTruncateMoveAt(Guid.NewGuid(), new GridCoord(1, 1)));
        }

        [Test]
        public void FindPath_OpenGrid_ReturnsShortestManhattan()
        {
            var path = _movement.FindPath(new GridCoord(0, 0), new GridCoord(2, 2));
            Assert.AreEqual(5, path.Count); // 4 steps + origin
            Assert.AreEqual(new GridCoord(0, 0), path.First());
            Assert.AreEqual(new GridCoord(2, 2), path.Last());
        }

        [Test]
        public void FindPath_SameCoord_ReturnsSingleton()
        {
            var path = _movement.FindPath(new GridCoord(1, 1), new GridCoord(1, 1));
            Assert.AreEqual(1, path.Count);
            Assert.AreEqual(new GridCoord(1, 1), path[0]);
        }

        [Test]
        public void FindPath_Unreachable_ReturnsEmpty()
        {
            // Wall completo en x=1 — aísla la columna x=0 del resto.
            var w = new bool[25];
            for (int i = 0; i < 25; i++) w[i] = true;
            for (int y = 0; y < 5; y++) w[y * 5 + 1] = false;
            _grid.LoadRoom(NavGraph.FromSnapshot(new GridSnapshot(5, 5, w)));

            var path = _movement.FindPath(new GridCoord(0, 0), new GridCoord(4, 0));
            Assert.AreEqual(0, path.Count);
        }

        [Test]
        public void FindPath_SkipsOccupiedIntermediates()
        {
            // Colocamos entidades en (1,0) (2,0) (3,0) — la ruta horizontal está bloqueada
            // y la impl debe buscar rodear por y=1.
            _grid.Register(Guid.NewGuid(), new GridCoord(1, 0));
            _grid.Register(Guid.NewGuid(), new GridCoord(2, 0));
            _grid.Register(Guid.NewGuid(), new GridCoord(3, 0));

            var path = _movement.FindPath(new GridCoord(0, 0), new GridCoord(4, 0));
            Assert.IsTrue(path.Count > 0, "Debería encontrar ruta rodeando");
            Assert.AreEqual(new GridCoord(4, 0), path.Last());
            Assert.IsFalse(path.Contains(new GridCoord(1, 0)));
        }

        [Test]
        public void Move_UpdatesGridAndFiresEvent()
        {
            var guid = Guid.NewGuid();
            _grid.Register(guid, new GridCoord(0, 0));

            Guid capturedGuid = Guid.Empty;
            GridCoord capturedTo = default;
            IReadOnlyList<GridCoord> capturedPath = null;
            _movement.OnEntityMoved += (g, _, to, path) =>
            {
                capturedGuid = g;
                capturedTo = to;
                capturedPath = path;
            };

            var ok = _movement.Move(guid, new GridCoord(2, 0));

            Assert.IsTrue(ok);
            Assert.IsTrue(_grid.TryGetPosition(guid, out var pos));
            Assert.AreEqual(new GridCoord(2, 0), pos);
            Assert.AreEqual(guid, capturedGuid);
            Assert.AreEqual(new GridCoord(2, 0), capturedTo);
            Assert.IsNotNull(capturedPath);
            Assert.AreEqual(3, capturedPath.Count);
        }

        [Test]
        public void Move_UnreachableDestination_ReturnsFalse()
        {
            var guid = Guid.NewGuid();
            _grid.Register(guid, new GridCoord(0, 0));

            // (2, 2) is OOB in a 2x2 grid
            _grid.LoadRoom(NavGraph.Rect(2, 2));
            _grid.Register(guid, new GridCoord(0, 0));

            var ok = _movement.Move(guid, new GridCoord(5, 5));
            Assert.IsFalse(ok);
        }

        [Test]
        public void Move_UnregisteredEntity_ReturnsFalse()
        {
            var ok = _movement.Move(Guid.NewGuid(), new GridCoord(1, 1));
            Assert.IsFalse(ok);
        }
    }
}
