using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Rollgeon.Grid.Tests
{
    /// <summary>Fase C: OccupiesAny (víctima ∩ área) y DistinctOccupants (dedupe de AoE).</summary>
    [TestFixture]
    public class GridManagerOccupancyQueryTests
    {
        static readonly Vector2Int Two = new Vector2Int(2, 2);

        private GridManager _grid;
        private Guid _big;
        private Guid _small;

        [SetUp]
        public void SetUp()
        {
            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(6, 6));
            _big = Guid.NewGuid();
            _small = Guid.NewGuid();
            Assert.IsTrue(_grid.TryRegister(_big, new GridCoord(1, 1), Two)); // cubre (1,1)-(2,2)
            _grid.Register(_small, new GridCoord(4, 4));
        }

        [Test]
        public void OccupiesAny_NonAnchorCellInArea_True()
        {
            var area = new HashSet<GridCoord> { new GridCoord(2, 2), new GridCoord(3, 3) };
            Assert.IsTrue(_grid.OccupiesAny(_big, area.Contains), "el ancla (1,1) NO está en el área");
        }

        [Test]
        public void OccupiesAny_NoCellInArea_False()
        {
            var area = new HashSet<GridCoord> { new GridCoord(3, 3), new GridCoord(0, 0) };
            Assert.IsFalse(_grid.OccupiesAny(_big, area.Contains));
        }

        [Test]
        public void OccupiesAny_Unit_MatchesOldAnchorCheck()
        {
            var area = new HashSet<GridCoord> { new GridCoord(4, 4) };
            Assert.IsTrue(_grid.OccupiesAny(_small, area.Contains));
            area.Clear();
            area.Add(new GridCoord(4, 5));
            Assert.IsFalse(_grid.OccupiesAny(_small, area.Contains));
        }

        [Test]
        public void OccupiesAny_UnregisteredOrNull_False()
        {
            Assert.IsFalse(_grid.OccupiesAny(Guid.NewGuid(), _ => true));
            Assert.IsFalse(((IGridManager)_grid).OccupiesAny(_big, null));
        }

        [Test]
        public void DistinctOccupants_MultiCellCoveredByManyCoords_AppearsOnce()
        {
            // 3 celdas del 2×2 + 1 vacía + la del 1×1.
            var coords = new List<GridCoord>
            {
                new GridCoord(1, 1), new GridCoord(2, 1), new GridCoord(2, 2),
                new GridCoord(0, 0), new GridCoord(4, 4),
            };

            var occupants = _grid.DistinctOccupants(coords);

            Assert.AreEqual(2, occupants.Count);
            Assert.AreEqual(_big, occupants[0], "orden de primera aparición");
            Assert.AreEqual(_small, occupants[1]);
        }

        [Test]
        public void DistinctOccupants_EmptyOrNull_EmptyList()
        {
            Assert.AreEqual(0, _grid.DistinctOccupants(new List<GridCoord>()).Count);
            Assert.AreEqual(0, ((IGridManager)_grid).DistinctOccupants(null).Count);
        }
    }
}
