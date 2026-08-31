using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Movement.Tests
{
    /// <summary>Fase B: Move/CommitPath/Teleport/GetReachableAnchors respetan el footprint.</summary>
    [TestFixture]
    public class MovementServiceFootprintTests
    {
        static readonly Vector2Int Two = new Vector2Int(2, 2);

        private GridManager _grid;
        private MovementService _movement;
        private Guid _big;

        [SetUp]
        public void SetUp()
        {
            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(6, 6));
            _movement = new MovementService(_grid);
            _big = Guid.NewGuid();
        }

        void RegisterBig(GridCoord anchor)
        {
            Assert.IsTrue(_grid.TryRegister(_big, anchor, Two), "setup: el 2×2 tiene que caber");
        }

        [Test]
        public void Move_2x2_OpenGrid_MovesWholeRectangle()
        {
            RegisterBig(new GridCoord(0, 0));

            Assert.IsTrue(_movement.Move(_big, new GridCoord(3, 3)));
            Assert.IsTrue(_grid.TryGetPosition(_big, out var anchor));
            Assert.AreEqual(new GridCoord(3, 3), anchor);
            // Las 4 celdas viejas quedaron libres, las 4 nuevas ocupadas.
            Assert.IsFalse(_grid.IsOccupied(new GridCoord(0, 0)));
            Assert.IsTrue(_grid.IsOccupied(new GridCoord(4, 4)));
        }

        [Test]
        public void Move_2x2_OneWideCorridor_NoPath()
        {
            // Sala 6×6 con una pared vertical en x=2 salvo un hueco de 1 celda en y=2:
            // un 1×1 pasa, un 2×2 no.
            var walkable = new bool[36];
            for (int y = 0; y < 6; y++)
                for (int x = 0; x < 6; x++)
                    walkable[y * 6 + x] = x != 2 || y == 2;
            _grid.LoadRoom(NavGraph.FromSnapshot(new GridSnapshot(6, 6, walkable)));
            RegisterBig(new GridCoord(0, 0));

            Assert.IsFalse(_movement.Move(_big, new GridCoord(4, 0)), "2×2 no entra por hueco de 1");

            var small = Guid.NewGuid();
            _grid.Register(small, new GridCoord(0, 4));
            Assert.IsTrue(_movement.Move(small, new GridCoord(4, 2)), "el 1×1 sigue pasando");
        }

        [Test]
        public void Move_2x2_DestinationPartiallyOccupied_ReturnsFalse()
        {
            RegisterBig(new GridCoord(0, 0));
            _grid.Register(Guid.NewGuid(), new GridCoord(4, 3)); // pisa una celda del destino (3,3)-(4,4)

            Assert.IsFalse(_movement.Move(_big, new GridCoord(3, 3)));
            Assert.IsTrue(_grid.TryGetPosition(_big, out var anchor));
            Assert.AreEqual(new GridCoord(0, 0), anchor, "sin cambios");
        }

        [Test]
        public void CommitPath_2x2_StepWhereRectDoesNotFit_ReturnsFalse()
        {
            RegisterBig(new GridCoord(0, 0));
            _grid.Register(Guid.NewGuid(), new GridCoord(2, 1)); // bloquea el rect en ancla (1,0)

            var path = new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 0) };
            Assert.IsFalse(_movement.CommitPath(_big, path));
        }

        [Test]
        public void CommitPath_2x2_FreeSteps_Moves()
        {
            RegisterBig(new GridCoord(0, 0));
            var path = new List<GridCoord>
            {
                new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(2, 0),
            };
            Assert.IsTrue(_movement.CommitPath(_big, path));
            Assert.IsTrue(_grid.TryGetPosition(_big, out var anchor));
            Assert.AreEqual(new GridCoord(2, 0), anchor);
        }

        [Test]
        public void Teleport_2x2_PartiallyBlockedDestination_ReturnsFalse()
        {
            RegisterBig(new GridCoord(0, 0));
            _grid.Register(Guid.NewGuid(), new GridCoord(3, 4));

            Assert.IsFalse(_movement.Teleport(_big, new GridCoord(3, 3)));
            Assert.IsTrue(_movement.Teleport(_big, new GridCoord(4, 0)));
            Assert.IsTrue(_grid.IsOccupied(new GridCoord(5, 1)));
        }

        [Test]
        public void GetReachableAnchors_Unit_SameResultAndOrderAsGetReachableTiles()
        {
            var small = Guid.NewGuid();
            _grid.Register(small, new GridCoord(2, 2));
            _grid.Register(Guid.NewGuid(), new GridCoord(3, 2)); // un ocupante para ramificar el BFS

            var anchors = _movement.GetReachableAnchors(small, 3);
            var tiles = _movement.GetReachableTiles(new GridCoord(2, 2), 3, includeOrigin: false);
            CollectionAssert.AreEqual(tiles, anchors, "mismo orden de descubrimiento");
        }

        [Test]
        public void GetReachableAnchors_2x2_ExcludesAnchorsWhereRectDoesNotFit()
        {
            RegisterBig(new GridCoord(0, 0));
            var anchors = _movement.GetReachableAnchors(_big, 8); // (4,4) queda a 8 pasos BFS

            // Anclas de la última fila/columna: el rect se sale de la sala 6×6.
            Assert.IsFalse(anchors.Contains(new GridCoord(5, 0)));
            Assert.IsFalse(anchors.Contains(new GridCoord(0, 5)));
            Assert.IsTrue(anchors.Contains(new GridCoord(4, 4)));
        }

        [Test]
        public void GetReachableAnchors_DefaultInterfaceMember_ReturnsNull()
        {
            IPathedMovementService minimal = new MinimalPathed();
            Assert.IsNull(minimal.GetReachableAnchors(Guid.NewGuid(), 3));
        }

        /// <summary>Fake mínimo: solo los miembros abstractos — ejercita el default member.</summary>
        private sealed class MinimalPathed : IPathedMovementService
        {
            public bool CommitPath(Guid entity, IReadOnlyList<GridCoord> path, bool applyPathFilter = false) => false;
            public bool Teleport(Guid entity, GridCoord to) => false;
            public void SetPathFilter(IMovementPathFilter filter) { }
            public event Action<Guid, GridCoord, GridCoord> OnEntityTeleported { add { } remove { } }
        }
    }
}
