using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Dungeon.Tests
{
    [TestFixture]
    public class RoomGridLoaderTests
    {
        private FakeDungeonService _dungeon;
        private GridManager _grid;
        private RoomSO _room;

        [SetUp]
        public void SetUp()
        {
            _dungeon = new FakeDungeonService();
            _grid = new GridManager();
            _room = ScriptableObject.CreateInstance<RoomSO>();
            _room.RoomId = "test";
            _room.GridLayout = GridSnapshot.Rect(4, 4);
            _dungeon.Current = _room;
        }

        [TearDown]
        public void TearDown()
        {
            if (_room != null) UnityEngine.Object.DestroyImmediate(_room);
            EventManager.ResetEventDictionary();
        }

        [Test]
        public void Ctor_LoadsCurrentRoomGrid()
        {
            using var loader = new RoomGridLoader(_grid, _dungeon);
            Assert.AreEqual(4, _grid.Snapshot.Width);
            Assert.AreEqual(4, _grid.Snapshot.Height);
        }

        [Test]
        public void OnRoomEntered_ReloadsGrid()
        {
            using var loader = new RoomGridLoader(_grid, _dungeon);

            var newRoom = ScriptableObject.CreateInstance<RoomSO>();
            try
            {
                newRoom.RoomId = "next";
                newRoom.GridLayout = GridSnapshot.Rect(6, 6);
                _dungeon.Current = newRoom;

                EventManager.Trigger(EventName.OnRoomEntered, Guid.NewGuid(), "next");

                Assert.AreEqual(6, _grid.Snapshot.Width);
                Assert.AreEqual(6, _grid.Snapshot.Height);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(newRoom);
            }
        }

        [Test]
        public void Dispose_Unsubscribes()
        {
            var loader = new RoomGridLoader(_grid, _dungeon);
            loader.Dispose();

            _dungeon.Current = null; // cambio que NO debe propagarse
            EventManager.Trigger(EventName.OnRoomEntered, Guid.NewGuid(), "ignored");

            // El snapshot sigue siendo el original 4x4 (no se cargó la Empty del nuevo current).
            Assert.AreEqual(4, _grid.Snapshot.Width);
        }

        [Test]
        public void Ctor_NullGrid_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new RoomGridLoader(null, _dungeon));
        }

        [Test]
        public void Ctor_NullDungeon_ResolvesLazilyFromServiceLocator()
        {
            // Sin dungeon explícito ni service registrado → LoadCurrent es no-op
            // y grid queda con su snapshot previo (Empty por default).
            using var loader = new RoomGridLoader(_grid, dungeon: null);
            Assert.IsTrue(_grid.Snapshot.IsEmpty);
        }

        private sealed class FakeDungeonService : IDungeonService
        {
            public RoomSO Current;
            public RoomSO CurrentRoom => Current;
            public int CurrentRoomIndex => 0;
            public int RoomCount => 1;
            public bool IsLastRoom => true;
            public void GenerateFloor(FloorLayoutSO layout, int seed) { }
            public bool NextRoom() => false;
            public System.Collections.Generic.IReadOnlyList<RoomSO> GetFloorRooms() =>
                System.Array.Empty<RoomSO>();
            public UnityEngine.Bounds GetFloorBounds() => default;
            public System.Collections.Generic.IReadOnlyList<Rollgeon.GameCamera.WallOccluder> GetCurrentRoomOccluders() =>
                System.Array.Empty<Rollgeon.GameCamera.WallOccluder>();
        }
    }
}
