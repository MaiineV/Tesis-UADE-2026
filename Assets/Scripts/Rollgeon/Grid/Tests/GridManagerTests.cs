using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Rollgeon.Grid.Tests
{
    [TestFixture]
    public class GridManagerTests
    {
        private GridManager _grid;

        [SetUp]
        public void SetUp()
        {
            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(5, 5));
        }

        [Test]
        public void LoadRoom_ClearsPreviousOccupancy()
        {
            var guid = Guid.NewGuid();
            _grid.Register(guid, new GridCoord(1, 1));
            Assert.IsTrue(_grid.IsOccupied(new GridCoord(1, 1)));

            _grid.LoadRoom(NavGraph.Rect(3, 3));
            Assert.IsFalse(_grid.IsOccupied(new GridCoord(1, 1)));
            Assert.IsFalse(_grid.TryGetPosition(guid, out _));
        }

        [Test]
        public void Register_TracksPositionAndOccupancy()
        {
            var guid = Guid.NewGuid();
            var coord = new GridCoord(2, 3);
            _grid.Register(guid, coord);

            Assert.IsTrue(_grid.IsOccupied(coord));
            Assert.IsTrue(_grid.TryGetPosition(guid, out var resolved));
            Assert.AreEqual(coord, resolved);
            Assert.IsTrue(_grid.TryGetOccupant(coord, out var resolvedGuid));
            Assert.AreEqual(guid, resolvedGuid);
        }

        [Test]
        public void Register_SameEntityNewCoord_FreesPrevious()
        {
            var guid = Guid.NewGuid();
            var a = new GridCoord(0, 0);
            var b = new GridCoord(1, 0);

            _grid.Register(guid, a);
            _grid.Register(guid, b);

            Assert.IsFalse(_grid.IsOccupied(a));
            Assert.IsTrue(_grid.IsOccupied(b));
        }

        [Test]
        public void Unregister_FreesPositionAndLookups()
        {
            var guid = Guid.NewGuid();
            var coord = new GridCoord(2, 2);
            _grid.Register(guid, coord);
            _grid.Unregister(guid);

            Assert.IsFalse(_grid.IsOccupied(coord));
            Assert.IsFalse(_grid.TryGetPosition(guid, out _));
            Assert.IsFalse(_grid.TryGetOccupant(coord, out _));
        }

        [Test]
        public void Move_ToFreeTile_Succeeds()
        {
            var guid = Guid.NewGuid();
            _grid.Register(guid, new GridCoord(0, 0));

            var ok = _grid.Move(guid, new GridCoord(1, 1));
            Assert.IsTrue(ok);
            Assert.IsFalse(_grid.IsOccupied(new GridCoord(0, 0)));
            Assert.IsTrue(_grid.IsOccupied(new GridCoord(1, 1)));
        }

        [Test]
        public void Move_ToOccupiedTile_Fails()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            _grid.Register(a, new GridCoord(0, 0));
            _grid.Register(b, new GridCoord(1, 0));

            var ok = _grid.Move(a, new GridCoord(1, 0));
            Assert.IsFalse(ok);
            Assert.IsTrue(_grid.IsOccupied(new GridCoord(0, 0)));
        }

        [Test]
        public void Move_ToUnwalkableTile_Fails()
        {
            var walkable = Enumerable.Repeat(true, 9).ToArray();
            walkable[4] = false; // (1,1) blocked
            _grid.LoadRoom(NavGraph.FromSnapshot(new GridSnapshot(3, 3, walkable)));

            var guid = Guid.NewGuid();
            _grid.Register(guid, new GridCoord(0, 0));

            var ok = _grid.Move(guid, new GridCoord(1, 1));
            Assert.IsFalse(ok);
        }

        [Test]
        public void Register_Guid_Empty_Throws()
        {
            Assert.Throws<ArgumentException>(() => _grid.Register(Guid.Empty, new GridCoord(0, 0)));
        }

        [Test]
        public void GridToWorld_WorldToGrid_RoundTrip()
        {
            _grid.LoadRoom(NavGraph.Rect(5, 5), new Vector3(10f, 0f, 20f), 2f);

            var coord = new GridCoord(3, 4);
            var world = _grid.GridToWorld(coord);
            var back = _grid.WorldToGrid(world);
            Assert.AreEqual(coord, back);
        }

        [Test]
        public void IsFree_CombinesWalkableAndOccupancy()
        {
            var guid = Guid.NewGuid();
            _grid.Register(guid, new GridCoord(1, 1));
            Assert.IsFalse(_grid.IsFree(new GridCoord(1, 1)));
            Assert.IsTrue(_grid.IsFree(new GridCoord(2, 2)));
        }
    }
}
