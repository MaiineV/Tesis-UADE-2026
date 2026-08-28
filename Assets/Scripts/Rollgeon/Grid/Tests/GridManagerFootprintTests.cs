using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rollgeon.Grid.Tests
{
    /// <summary>Footprint multi-celda (Fase A): muchas celdas → un guid; el 1×1 no cambia.</summary>
    [TestFixture]
    public class GridManagerFootprintTests
    {
        static readonly Vector2Int TwoByTwo = new Vector2Int(2, 2);

        private GridManager _grid;

        [SetUp]
        public void SetUp()
        {
            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(6, 6));
        }

        static HashSet<GridCoord> Cells(params (int x, int y)[] coords)
            => new HashSet<GridCoord>(coords.Select(c => new GridCoord(c.x, c.y)));

        [Test]
        public void TryRegister_2x2_OccupiesFourCells_PositionIsAnchor()
        {
            var guid = Guid.NewGuid();

            Assert.IsTrue(_grid.TryRegister(guid, new GridCoord(1, 1), TwoByTwo));

            Assert.IsTrue(_grid.TryGetPosition(guid, out var anchor));
            Assert.AreEqual(new GridCoord(1, 1), anchor);
            Assert.AreEqual(TwoByTwo, _grid.GetFootprint(guid));
            CollectionAssert.AreEquivalent(Cells((1, 1), (2, 1), (1, 2), (2, 2)), _grid.OccupiedCells(guid).ToList());
            foreach (var c in _grid.OccupiedCells(guid)) Assert.IsTrue(_grid.IsOccupied(c), c.ToString());
            Assert.IsFalse(_grid.IsOccupied(new GridCoord(3, 1)));
            Assert.AreEqual(1, _grid.Occupants().Count(), "un par por entidad, no por celda");
        }

        [Test]
        public void TryGetOccupant_NonAnchorCell_ReturnsTheGuid()
        {
            var guid = Guid.NewGuid();
            _grid.TryRegister(guid, new GridCoord(0, 0), TwoByTwo);

            Assert.IsTrue(_grid.TryGetOccupant(new GridCoord(1, 1), out var occupant));
            Assert.AreEqual(guid, occupant);
        }

        [Test]
        public void TryRegister_Overlap_ReturnsFalse_WithoutMutating()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            _grid.Register(a, new GridCoord(2, 2));

            Assert.IsFalse(_grid.TryRegister(b, new GridCoord(1, 1), TwoByTwo));

            Assert.IsFalse(_grid.TryGetPosition(b, out _));
            Assert.IsFalse(_grid.IsOccupied(new GridCoord(1, 1)));
            Assert.IsTrue(_grid.TryGetOccupant(new GridCoord(2, 2), out var occupant));
            Assert.AreEqual(a, occupant, "el 1×1 previo no se desaloja");
        }

        [Test]
        public void TryRegister_NonWalkableCell_ReturnsFalse()
        {
            // (5,5) es la última celda: un 2×2 anclado ahí se sale del grafo.
            Assert.IsFalse(_grid.TryRegister(Guid.NewGuid(), new GridCoord(5, 5), TwoByTwo));
            Assert.IsFalse(_grid.IsOccupied(new GridCoord(5, 5)));
        }

        [Test]
        public void TryRegister_Unit_BehavesAsRegister()
        {
            var guid = Guid.NewGuid();
            Assert.IsTrue(_grid.TryRegister(guid, new GridCoord(3, 3), Vector2Int.one));
            Assert.AreEqual(Vector2Int.one, _grid.GetFootprint(guid));
            Assert.AreEqual(1, _grid.OccupiedCells(guid).Count());
        }

        [Test]
        public void Register_1x1_OverOccupied_StillEvictsWithWarning()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            _grid.Register(a, new GridCoord(1, 1));

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Sobrescribiendo"));
            _grid.Register(b, new GridCoord(1, 1));

            Assert.IsTrue(_grid.TryGetOccupant(new GridCoord(1, 1), out var occupant));
            Assert.AreEqual(b, occupant);
            Assert.IsFalse(_grid.TryGetPosition(a, out _));
        }

        [Test]
        public void Register_1x1_OverMultiCell_EvictsTheWholeRectangle()
        {
            var big = Guid.NewGuid();
            var small = Guid.NewGuid();
            _grid.TryRegister(big, new GridCoord(1, 1), TwoByTwo);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Sobrescribiendo"));
            _grid.Register(small, new GridCoord(2, 2));

            Assert.IsFalse(_grid.TryGetPosition(big, out _), "un rectángulo a medias sería inconsistente: se va entero");
            Assert.IsFalse(_grid.IsOccupied(new GridCoord(1, 1)));
            Assert.IsTrue(_grid.TryGetOccupant(new GridCoord(2, 2), out var occupant));
            Assert.AreEqual(small, occupant);
        }

        [Test]
        public void Register_ExistingMultiCellGuid_KeepsFootprint()
        {
            var guid = Guid.NewGuid();
            _grid.TryRegister(guid, new GridCoord(0, 0), TwoByTwo);

            _grid.Register(guid, new GridCoord(3, 3));

            Assert.AreEqual(TwoByTwo, _grid.GetFootprint(guid));
            CollectionAssert.AreEquivalent(Cells((3, 3), (4, 3), (3, 4), (4, 4)), _grid.OccupiedCells(guid).ToList());
            Assert.IsFalse(_grid.IsOccupied(new GridCoord(0, 0)));
        }

        [Test]
        public void Register_ExistingMultiCellGuid_ThatDoesNotFit_LogsErrorAndKeepsPlace()
        {
            var guid = Guid.NewGuid();
            _grid.TryRegister(guid, new GridCoord(0, 0), TwoByTwo);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("no cabe"));
            _grid.Register(guid, new GridCoord(5, 5));

            Assert.IsTrue(_grid.TryGetPosition(guid, out var anchor));
            Assert.AreEqual(new GridCoord(0, 0), anchor);
        }

        [Test]
        public void Unregister_2x2_FreesAllCells()
        {
            var guid = Guid.NewGuid();
            _grid.TryRegister(guid, new GridCoord(1, 1), TwoByTwo);

            _grid.Unregister(guid);

            Assert.IsFalse(_grid.TryGetPosition(guid, out _));
            Assert.AreEqual(Vector2Int.one, _grid.GetFootprint(guid));
            foreach (var c in Cells((1, 1), (2, 1), (1, 2), (2, 2))) Assert.IsFalse(_grid.IsOccupied(c), c.ToString());
            Assert.IsEmpty(_grid.OccupiedCells(guid));
        }

        [Test]
        public void Move_2x2_SelfOverlapAllowed()
        {
            var guid = Guid.NewGuid();
            _grid.TryRegister(guid, new GridCoord(1, 1), TwoByTwo);

            Assert.IsTrue(_grid.Move(guid, new GridCoord(2, 1)));

            CollectionAssert.AreEquivalent(Cells((2, 1), (3, 1), (2, 2), (3, 2)), _grid.OccupiedCells(guid).ToList());
            Assert.IsFalse(_grid.IsOccupied(new GridCoord(1, 1)));
            Assert.IsFalse(_grid.IsOccupied(new GridCoord(1, 2)));
        }

        [Test]
        public void Move_2x2_BlockedByOtherInAnyCell_ReturnsFalse()
        {
            var guid = Guid.NewGuid();
            var other = Guid.NewGuid();
            _grid.TryRegister(guid, new GridCoord(1, 1), TwoByTwo);
            _grid.Register(other, new GridCoord(4, 2));

            Assert.IsFalse(_grid.Move(guid, new GridCoord(3, 1)), "(4,2) es la esquina superior derecha del destino");

            Assert.IsTrue(_grid.TryGetPosition(guid, out var anchor));
            Assert.AreEqual(new GridCoord(1, 1), anchor);
        }

        [Test]
        public void CanPlace_IgnoresTheGivenGuid()
        {
            var guid = Guid.NewGuid();
            _grid.TryRegister(guid, new GridCoord(1, 1), TwoByTwo);

            Assert.IsFalse(_grid.CanPlace(new GridCoord(2, 2), TwoByTwo));
            Assert.IsTrue(_grid.CanPlace(new GridCoord(2, 2), TwoByTwo, ignore: guid));
        }

        [Test]
        public void GetFootprint_Unregistered_ReturnsUnit()
        {
            Assert.AreEqual(Vector2Int.one, _grid.GetFootprint(Guid.NewGuid()));
        }

        [Test]
        public void LoadRoom_ClearsFootprints()
        {
            var guid = Guid.NewGuid();
            _grid.TryRegister(guid, new GridCoord(1, 1), TwoByTwo);

            _grid.LoadRoom(NavGraph.Rect(6, 6));

            Assert.AreEqual(Vector2Int.one, _grid.GetFootprint(guid));
            Assert.IsFalse(_grid.IsOccupied(new GridCoord(2, 2)));
        }

        [Test]
        public void TryRegister_ShrinkBackToUnit_FreesTheRectangle()
        {
            var guid = Guid.NewGuid();
            _grid.TryRegister(guid, new GridCoord(1, 1), TwoByTwo);

            Assert.IsTrue(_grid.TryRegister(guid, new GridCoord(1, 1), Vector2Int.one));

            Assert.AreEqual(Vector2Int.one, _grid.GetFootprint(guid));
            Assert.IsTrue(_grid.IsOccupied(new GridCoord(1, 1)));
            Assert.IsFalse(_grid.IsOccupied(new GridCoord(2, 2)));
        }

        // ---- default interface members sobre una implementación mínima ----------

        /// <summary>Fake 1×1 que solo implementa los miembros abstractos (como los fakes de otros tests).</summary>
        sealed class MinimalGrid : IGridManager
        {
            readonly Dictionary<Guid, GridCoord> _pos = new Dictionary<Guid, GridCoord>();
            public NavGraph Graph { get; } = NavGraph.Rect(4, 4);
            public Vector3 GridOrigin => Vector3.zero;
            public float TileSize => 1f;
            public void LoadRoom(NavGraph graph, Vector3 origin = default, float tileSize = 1f) { }
            public bool InBounds(GridCoord c) => Graph.InBounds(c);
            public bool IsWalkable(GridCoord c) => Graph.HasNode(c);
            public bool IsOccupied(GridCoord c) => _pos.ContainsValue(c);
            public bool IsFree(GridCoord c) => IsWalkable(c) && !IsOccupied(c);
            public bool TryGetOccupant(GridCoord c, out Guid g) { foreach (var kv in _pos) if (kv.Value == c) { g = kv.Key; return true; } g = default; return false; }
            public bool TryGetPosition(Guid g, out GridCoord c) => _pos.TryGetValue(g, out c);
            public void Register(Guid g, GridCoord c) => _pos[g] = c;
            public void Unregister(Guid g) => _pos.Remove(g);
            public bool Move(Guid g, GridCoord to) { _pos[g] = to; return true; }
            public Vector3 GridToWorld(GridCoord c) => new Vector3(c.X, 0, c.Y);
            public GridCoord WorldToGrid(Vector3 w) => new GridCoord((int)w.x, (int)w.z);
            public IEnumerable<KeyValuePair<Guid, GridCoord>> Occupants() => _pos;
        }

        [Test]
        public void DefaultMembers_OnMinimalFake_CheckEveryCell_AndRegisterAsUnit()
        {
            IGridManager fake = new MinimalGrid();
            var blocker = Guid.NewGuid();
            fake.Register(blocker, new GridCoord(1, 1));

            Assert.IsFalse(fake.CanPlace(new GridCoord(0, 0), TwoByTwo), "(1,1) está tomada");
            Assert.IsTrue(fake.CanPlace(new GridCoord(2, 2), TwoByTwo));
            Assert.IsFalse(fake.CanPlace(new GridCoord(3, 3), TwoByTwo), "se sale del grafo");

            var guid = Guid.NewGuid();
            Assert.IsTrue(fake.TryRegister(guid, new GridCoord(2, 2), TwoByTwo));
            Assert.AreEqual(Vector2Int.one, fake.GetFootprint(guid), "un fake sin footprint reporta 1×1");
            Assert.AreEqual(1, fake.OccupiedCells(guid).Count());
        }
    }

    [TestFixture]
    public class GridFootprintTests
    {
        [Test]
        public void Cells_2x1_YieldsAnchorAndRight()
        {
            var cells = GridFootprint.Cells(new GridCoord(3, 4), new Vector2Int(2, 1)).ToList();
            CollectionAssert.AreEqual(new[] { new GridCoord(3, 4), new GridCoord(4, 4) }, cells);
        }

        [Test]
        public void Cells_2x2_RowMajorFromAnchor()
        {
            var cells = GridFootprint.Cells(new GridCoord(0, 0), new Vector2Int(2, 2)).ToList();
            CollectionAssert.AreEqual(new[] { new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(0, 1), new GridCoord(1, 1) }, cells);
        }

        [Test]
        public void Normalize_ClampsToOne_AndIsUnit()
        {
            Assert.AreEqual(Vector2Int.one, GridFootprint.Normalize(new Vector2Int(0, -2)));
            Assert.IsTrue(GridFootprint.IsUnit(new Vector2Int(0, 0)));
            Assert.IsFalse(GridFootprint.IsUnit(new Vector2Int(2, 1)));
        }

        [Test]
        public void CenterOffset_HalfTilePerExtraCell()
        {
            Assert.AreEqual(Vector3.zero, GridFootprint.CenterOffset(Vector2Int.one, 2f));
            Assert.AreEqual(new Vector3(1f, 0f, 1f), GridFootprint.CenterOffset(new Vector2Int(2, 2), 2f));
            Assert.AreEqual(new Vector3(0.5f, 0f, 0f), GridFootprint.CenterOffset(new Vector2Int(2, 1), 1f));
        }
    }
}
