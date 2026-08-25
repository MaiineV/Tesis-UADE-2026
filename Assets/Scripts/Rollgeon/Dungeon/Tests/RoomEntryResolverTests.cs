using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Dungeon.Components;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Dungeon.Tests
{
    /// <summary>
    /// La casilla por la que se entra a una sala. La comparten el jugador (dónde aparece) y el
    /// jefe (contra qué pared arranca), así que lo que se fija acá es que sea UNA sola.
    /// </summary>
    [TestFixture]
    public class RoomEntryResolverTests
    {
        private readonly List<GameObject> _objects = new();
        private GridManager _grid;
        private RoomLayout _layout;

        [SetUp]
        public void SetUp()
        {
            _grid = new GridManager();

            var go = new GameObject("TestRoom");
            _objects.Add(go);

            _layout = go.AddComponent<RoomLayout>();
            _layout.TileSize = 1f;
            _layout.NavGraph = NavGraph.Rect(11, 11);
            _grid.LoadRoom(_layout.NavGraph, _layout.GetOrigin(), _layout.TileSize);

            _layout.PlayerSpawnPoint = Point("PlayerSpawn", new GridCoord(5, 2));
            _layout.DoorSlots = new List<DoorSlotRef>
            {
                Slot(DoorDirection.South, new GridCoord(5, 0)),
                Slot(DoorDirection.North, new GridCoord(5, 10)),
                Slot(DoorDirection.West, new GridCoord(0, 5)),
            };
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects)
                if (go != null) Object.DestroyImmediate(go);
            _objects.Clear();
        }

        private Transform Point(string name, GridCoord coord)
        {
            var go = new GameObject(name);
            _objects.Add(go);
            go.transform.SetParent(_layout.transform);
            go.transform.position = _grid.GridToWorld(coord);
            return go.transform;
        }

        private DoorSlotRef Slot(DoorDirection direction, GridCoord anchor) => new DoorSlotRef
        {
            Direction = direction,
            Anchor = Point($"Door_{direction}", anchor),
        };

        /// <summary>El anchor está sobre la pared: la casilla que cuenta es la primera de adentro,
        /// que es la que se pisa al cruzar.</summary>
        [Test]
        public void ADeclaredDoor_ResolvesToTheTileJustInsideIt()
        {
            Assert.IsTrue(RoomEntryResolver.TryResolve(_grid, _layout, DoorDirection.South, out var coord));
            Assert.AreEqual(new GridCoord(5, 1), coord);
        }

        [Test]
        public void EachDoor_ResolvesToItsOwnSide()
        {
            RoomEntryResolver.TryResolve(_grid, _layout, DoorDirection.North, out var north);
            RoomEntryResolver.TryResolve(_grid, _layout, DoorDirection.West, out var west);

            Assert.AreEqual(new GridCoord(5, 9), north);
            Assert.AreEqual(new GridCoord(1, 5), west);
        }

        /// <summary>Arranque directo por bootstrap: no se entró por ninguna puerta.</summary>
        [Test]
        public void WithoutAnEntryDirection_ItFallsBackToTheAuthoredPlayerSpawn()
        {
            Assert.IsTrue(RoomEntryResolver.TryResolve(_grid, _layout, null, out var coord));
            Assert.AreEqual(new GridCoord(5, 2), coord);
        }

        /// <summary>Una dirección que el prefab no tiene autorada no puede inventar una casilla.</summary>
        [Test]
        public void AnUnauthoredDoor_FallsBackToTheAuthoredPlayerSpawn()
        {
            Assert.IsTrue(RoomEntryResolver.TryResolve(_grid, _layout, DoorDirection.East, out var coord));
            Assert.AreEqual(new GridCoord(5, 2), coord);
        }

        [Test]
        public void WithoutDoorsOrPlayerSpawn_ItResolvesToNothing()
        {
            _layout.DoorSlots.Clear();
            _layout.PlayerSpawnPoint = null;

            Assert.IsFalse(RoomEntryResolver.TryResolve(_grid, _layout, DoorDirection.South, out _));
        }

        [Test]
        public void NullLayoutOrGrid_ResolveToNothing()
        {
            Assert.IsFalse(RoomEntryResolver.TryResolve(_grid, null, DoorDirection.South, out _));
            Assert.IsFalse(RoomEntryResolver.TryResolve(null, _layout, DoorDirection.South, out _));
        }
    }
}
