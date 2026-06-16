using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Dungeon.Components;
using Rollgeon.GameCamera;
using Rollgeon.Grid;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Dungeon.Tests
{
    [TestFixture]
    public class PlayerRoomTransitionerTests
    {
        private GridManager _grid;
        private FakeDungeon _dungeon;
        private FakePlayer _player;
        private readonly List<Object> _objects = new();
        private readonly List<ScriptableObject> _sos = new();

        [SetUp]
        public void SetUp()
        {
            _grid = new GridManager();
            _dungeon = new FakeDungeon();
            _player = new FakePlayer { PlayerGuid = Guid.NewGuid() };

            ServiceLocator.AddService<IDungeonService>(_dungeon, ServiceScope.Run);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _objects) if (o != null) Object.DestroyImmediate(o);
            _objects.Clear();
            foreach (var s in _sos) if (s != null) Object.DestroyImmediate(s);
            _sos.Clear();
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        [Test]
        public void WithEntryDirection_RegistersPlayerAtInteriorCellInFrontOfDoor()
        {
            var (layout, _) = CreateRoomWithDoors(NavGraph.Rect(6, 6));
            _dungeon.EntryDirection = DoorDirection.South;
            SetCurrentRoom(layout);
            _grid.LoadRoom(layout.NavGraph, layout.GetOrigin(), layout.TileSize);

            using var trans = new PlayerRoomTransitioner(_grid, _player);
            EventManager.Trigger(EventName.OnRoomEntered, Guid.NewGuid(), "test");

            Assert.IsTrue(_grid.TryGetPosition(_player.PlayerGuid, out var coord));
            var southAnchor = layout.GetDoorSlot(DoorDirection.South).Anchor;
            // El player spawnea en la primera celda interior frente a la puerta, no en el
            // anchor mismo (spec de spawn al cambiar de sala).
            var expected = _grid.WorldToGrid(southAnchor.position) + DoorDirection.South.InwardOffset();
            Assert.AreEqual(expected, coord);
        }

        [Test]
        public void NullEntryDirection_RegistersAtPlayerSpawnPoint()
        {
            var (layout, _) = CreateRoomWithDoors(NavGraph.Rect(6, 6));
            _dungeon.EntryDirection = null;
            SetCurrentRoom(layout);
            _grid.LoadRoom(layout.NavGraph, layout.GetOrigin(), layout.TileSize);

            using var trans = new PlayerRoomTransitioner(_grid, _player);
            EventManager.Trigger(EventName.OnRoomEntered, Guid.NewGuid(), "test");

            Assert.IsTrue(_grid.TryGetPosition(_player.PlayerGuid, out var coord));
            var expected = _grid.WorldToGrid(layout.PlayerSpawnPoint.position);
            Assert.AreEqual(expected, coord);
        }

        [Test]
        public void MissingDoorSlot_FallsBackToPlayerSpawnPoint()
        {
            var (layout, _) = CreateRoomWithDoors(NavGraph.Rect(6, 6));
            _dungeon.EntryDirection = DoorDirection.West;
            SetCurrentRoom(layout);
            _grid.LoadRoom(layout.NavGraph, layout.GetOrigin(), layout.TileSize);

            using var trans = new PlayerRoomTransitioner(_grid, _player);
            EventManager.Trigger(EventName.OnRoomEntered, Guid.NewGuid(), "test");

            Assert.IsTrue(_grid.TryGetPosition(_player.PlayerGuid, out var coord));
            var expected = _grid.WorldToGrid(layout.PlayerSpawnPoint.position);
            Assert.AreEqual(expected, coord);
        }

        [Test]
        public void NoPrefab_NoOp()
        {
            _dungeon.CurrentInstance = new RoomInstance
            {
                InstanceId = Guid.NewGuid(),
                SpawnedPrefab = null,
            };

            using var trans = new PlayerRoomTransitioner(_grid, _player);
            EventManager.Trigger(EventName.OnRoomEntered, Guid.NewGuid(), "test");

            Assert.IsFalse(_grid.TryGetPosition(_player.PlayerGuid, out _));
        }

        [Test]
        public void NoPlayerGuid_NoOp()
        {
            _player.PlayerGuid = Guid.Empty;
            var (layout, _) = CreateRoomWithDoors(NavGraph.Rect(6, 6));
            SetCurrentRoom(layout);
            _grid.LoadRoom(layout.NavGraph, layout.GetOrigin(), layout.TileSize);

            using var trans = new PlayerRoomTransitioner(_grid, _player);
            EventManager.Trigger(EventName.OnRoomEntered, Guid.NewGuid(), "test");

            Assert.AreEqual(0, new List<KeyValuePair<Guid, GridCoord>>((IEnumerable<KeyValuePair<Guid, GridCoord>>)_grid.Occupants()).Count);
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        private (RoomLayout layout, GameObject go) CreateRoomWithDoors(NavGraph graph)
        {
            var go = new GameObject("TestRoom");
            _objects.Add(go);

            var layout = go.AddComponent<RoomLayout>();
            layout.NavGraph = graph;
            layout.TileSize = 1f;

            var spawnPt = new GameObject("PlayerSpawn");
            spawnPt.transform.SetParent(go.transform);
            spawnPt.transform.localPosition = new Vector3(2, 0, 2);
            _objects.Add(spawnPt);
            layout.PlayerSpawnPoint = spawnPt.transform;

            var northAnchor = new GameObject("DoorAnchor_N");
            northAnchor.transform.SetParent(go.transform);
            northAnchor.transform.localPosition = new Vector3(3, 0, 5);
            _objects.Add(northAnchor);

            var southAnchor = new GameObject("DoorAnchor_S");
            southAnchor.transform.SetParent(go.transform);
            southAnchor.transform.localPosition = new Vector3(3, 0, 0);
            _objects.Add(southAnchor);

            layout.DoorSlots = new List<DoorSlotRef>
            {
                new DoorSlotRef { Direction = DoorDirection.North, Anchor = northAnchor.transform },
                new DoorSlotRef { Direction = DoorDirection.South, Anchor = southAnchor.transform },
            };

            return (layout, go);
        }

        private void SetCurrentRoom(RoomLayout layout)
        {
            var room = ScriptableObject.CreateInstance<RoomSO>();
            room.RoomId = "test";
            _sos.Add(room);

            _dungeon.CurrentInstance = new RoomInstance
            {
                InstanceId = Guid.NewGuid(),
                Template = room,
                SpawnedPrefab = layout.gameObject,
            };
        }

        // -----------------------------------------------------------------
        // Stubs
        // -----------------------------------------------------------------

        private sealed class FakeDungeon : IDungeonService
        {
            public RoomInstance CurrentInstance;
            public DoorDirection? EntryDirection;

            public RoomSO CurrentRoom => CurrentInstance?.Template;
            public RoomInstance CurrentRoomInstance => CurrentInstance;
            public DoorDirection? LastEntryDirection => EntryDirection;

            public void GenerateFloor(FloorLayoutSO layout, int seed) { }
            public IReadOnlyDictionary<Guid, RoomInstance> GetAllRoomInstances() => new Dictionary<Guid, RoomInstance>();
            public IReadOnlyDictionary<Guid, FloorShell> GetFloorShells() => new Dictionary<Guid, FloorShell>();
            public bool CanEnterRoomByDoor(DoorDirection dir, out Guid id) { id = Guid.Empty; return false; }
            public bool EnterRoomByDoor(DoorDirection dir) => false;
            public bool EnterRoomByInstanceId(Guid id) => false;
            public Bounds GetFloorBounds() => default;
            public IReadOnlyList<WallOccluder> GetCurrentRoomOccluders() => Array.Empty<WallOccluder>();
        }

        private sealed class FakePlayer : Rollgeon.Player.IPlayerService
        {
            public Guid PlayerGuid { get; set; }
            public Guid RunId => Guid.Empty;
            public Rollgeon.Heroes.ClassHeroSO CurrentHero => null;
            public Rollgeon.Dice.DiceBagSO DiceBag => null;
            public void SetPlayer(Rollgeon.Heroes.ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(Rollgeon.Dice.DiceBagSO bag) { }
            public void ClearPlayer() { }
            public event Action<Rollgeon.Heroes.ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;
        }
    }
}
