using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Dungeon.Components;
using Rollgeon.Grid;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Dungeon.Tests
{
    [TestFixture]
    public class RoomGridLoaderTests
    {
        private FakeDungeonService _dungeon;
        private GridManager _grid;
        private readonly List<Object> _spawnedObjects = new();
        private readonly List<ScriptableObject> _createdSOs = new();

        [SetUp]
        public void SetUp()
        {
            _dungeon = new FakeDungeonService();
            _grid = new GridManager();
            SetCurrentRoom("test", NavGraph.Rect(4, 4));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _spawnedObjects)
                if (obj != null) Object.DestroyImmediate(obj);
            _spawnedObjects.Clear();

            foreach (var so in _createdSOs)
                if (so != null) Object.DestroyImmediate(so);
            _createdSOs.Clear();

            EventManager.ResetEventDictionary();
        }

        [Test]
        public void Ctor_LoadsCurrentRoomGrid()
        {
            using var loader = new RoomGridLoader(_grid, _dungeon);
            Assert.AreEqual(4, _grid.Graph.Width);
            Assert.AreEqual(4, _grid.Graph.Height);
        }

        [Test]
        public void OnRoomEntered_ReloadsGrid()
        {
            using var loader = new RoomGridLoader(_grid, _dungeon);

            SetCurrentRoom("next", NavGraph.Rect(6, 6));

            EventManager.Trigger(EventName.OnRoomEntered, Guid.NewGuid(), "next");

            Assert.AreEqual(6, _grid.Graph.Width);
            Assert.AreEqual(6, _grid.Graph.Height);
        }

        [Test]
        public void Dispose_Unsubscribes()
        {
            var loader = new RoomGridLoader(_grid, _dungeon);
            loader.Dispose();

            // Simulo "cambio que NO debe propagarse" — forzar un LoadRoom via el evento.
            _dungeon.CurrentInstance = null;
            EventManager.Trigger(EventName.OnRoomEntered, Guid.NewGuid(), "ignored");

            Assert.AreEqual(4, _grid.Graph.Width,
                "Snapshot sigue siendo el original 4x4 (loader unsub'd).");
        }

        [Test]
        public void Ctor_NullGrid_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new RoomGridLoader(null, _dungeon));
        }

        [Test]
        public void Ctor_NullDungeon_ResolvesLazilyFromServiceLocator()
        {
            using var loader = new RoomGridLoader(_grid, dungeon: null);
            Assert.IsTrue(_grid.Graph.IsEmpty);
        }

        [Test]
        public void OnRoomEntered_PrefabWithoutLayout_LoadsEmpty()
        {
            using var loader = new RoomGridLoader(_grid, _dungeon);

            var bareGO = new GameObject("bare_prefab");
            _spawnedObjects.Add(bareGO);

            var room = ScriptableObject.CreateInstance<RoomSO>();
            room.RoomId = "bare";
            _createdSOs.Add(room);

            _dungeon.CurrentInstance = new RoomInstance
            {
                InstanceId = Guid.NewGuid(),
                Template = room,
                SpawnedPrefab = bareGO,
            };

            EventManager.Trigger(EventName.OnRoomEntered, Guid.NewGuid(), "bare");

            Assert.IsTrue(_grid.Graph.IsEmpty,
                "Sin RoomLayout en el prefab, el loader carga snapshot Empty.");
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        private void SetCurrentRoom(string id, NavGraph graph)
        {
            var room = ScriptableObject.CreateInstance<RoomSO>();
            room.RoomId = id;
            _createdSOs.Add(room);

            var prefab = new GameObject($"RoomPrefab_{id}");
            prefab.SetActive(false);
            _spawnedObjects.Add(prefab);

            var layout = prefab.AddComponent<RoomLayout>();
            layout.NavGraph = graph;

            _dungeon.CurrentInstance = new RoomInstance
            {
                InstanceId = Guid.NewGuid(),
                Template = room,
                SpawnedPrefab = prefab,
            };
        }

        // -----------------------------------------------------------------
        // Stub service
        // -----------------------------------------------------------------

        private sealed class FakeDungeonService : IDungeonService
        {
            public RoomInstance CurrentInstance;

            public RoomSO CurrentRoom => CurrentInstance?.Template;
            public RoomInstance CurrentRoomInstance => CurrentInstance;

            public void GenerateFloor(FloorLayoutSO layout, int seed) { }

            public IReadOnlyDictionary<Guid, RoomInstance> GetAllRoomInstances() =>
                new Dictionary<Guid, RoomInstance>();

            public IReadOnlyDictionary<Guid, FloorShell> GetFloorShells() =>
                new Dictionary<Guid, FloorShell>();

            public bool CanEnterRoomByDoor(DoorDirection dir, out Guid id)
            {
                id = Guid.Empty;
                return false;
            }

            public DoorDirection? LastEntryDirection => null;
            public bool EnterRoomByDoor(DoorDirection dir) => false;
            public bool EnterRoomByInstanceId(Guid id) => false;

            public Bounds GetFloorBounds() => default;
            public IReadOnlyList<Rollgeon.GameCamera.WallOccluder> GetCurrentRoomOccluders() =>
                Array.Empty<Rollgeon.GameCamera.WallOccluder>();
        }
    }
}
