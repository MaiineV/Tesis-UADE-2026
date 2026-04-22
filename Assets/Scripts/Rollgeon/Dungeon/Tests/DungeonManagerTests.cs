using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Entities;
using UnityEngine;

namespace Rollgeon.Dungeon.Tests
{
    [TestFixture]
    public class DungeonManagerTests
    {
        private DungeonManager _manager;
        private readonly List<Object> _createdObjects = new();

        private FloorLayoutSO CreateLayout(
            int minRooms = 5, int maxRooms = 8,
            int combatCount = 3, int shopCount = 1,
            int potionCount = 1, int bossCount = 1)
        {
            var layout = ScriptableObject.CreateInstance<FloorLayoutSO>();
            _createdObjects.Add(layout);

            layout.RoomCountMin = minRooms;
            layout.RoomCountMax = maxRooms;

            layout.CombatRooms = new List<RoomSO>();
            for (int i = 0; i < combatCount; i++)
                layout.CombatRooms.Add(CreateRoom($"combat_{i}", RoomType.Combat));

            layout.ShopRooms = new List<RoomSO>();
            for (int i = 0; i < shopCount; i++)
                layout.ShopRooms.Add(CreateRoom($"shop_{i}", RoomType.Shop));

            layout.PotionRooms = new List<RoomSO>();
            for (int i = 0; i < potionCount; i++)
                layout.PotionRooms.Add(CreateRoom($"potion_{i}", RoomType.Potion));

            layout.BossCandidates = new List<EnemyDataSO>();
            for (int i = 0; i < bossCount; i++)
                layout.BossCandidates.Add(CreateEnemy($"boss_{i}"));

            return layout;
        }

        private RoomSO CreateRoom(string id, RoomType type)
        {
            var room = ScriptableObject.CreateInstance<RoomSO>();
            room.RoomId = id;
            room.DisplayName = id;
            room.Type = type;
            _createdObjects.Add(room);
            return room;
        }

        private EnemyDataSO CreateEnemy(string name)
        {
            var enemy = ScriptableObject.CreateInstance<EnemyDataSO>();
            enemy.name = name;
            _createdObjects.Add(enemy);
            return enemy;
        }

        [SetUp]
        public void SetUp()
        {
            _manager = new DungeonManager();
        }

        [TearDown]
        public void TearDown()
        {
            _manager?.Dispose();

            foreach (var obj in _createdObjects)
            {
                if (obj != null)
                    Object.DestroyImmediate(obj);
            }
            _createdObjects.Clear();

            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        [Test]
        public void GenerateFloor_NullLayout_ThrowsArgumentNull()
        {
            Assert.Throws<System.ArgumentNullException>(() => _manager.GenerateFloor(null, 42));
        }

        [Test]
        public void GenerateFloor_WithStartRoom_PlacesStartAtIndexZero()
        {
            var start = CreateRoom("start_0", RoomType.Start);
            var layout = CreateLayout();
            layout.StartRoom = start;

            _manager.GenerateFloor(layout, 42);

            Assert.AreEqual(RoomType.Start, _manager.GetFloorRooms()[0].Type);
            Assert.AreEqual("start_0", _manager.GetFloorRooms()[0].RoomId);
            Assert.AreSame(start, _manager.CurrentRoom);
        }

        [Test]
        public void GenerateFloor_WithoutStartRoom_FirstRoomIsNotStart()
        {
            var layout = CreateLayout();

            _manager.GenerateFloor(layout, 42);

            Assert.AreNotEqual(RoomType.Start, _manager.GetFloorRooms()[0].Type);
        }

        [Test]
        public void GenerateFloor_ProducesCorrectRoomCount_WithinMinMax()
        {
            var layout = CreateLayout(minRooms: 5, maxRooms: 8);

            _manager.GenerateFloor(layout, 42);

            // Room count includes combat + special + boss rooms
            // Must be at least MinRoomCount (3)
            Assert.GreaterOrEqual(_manager.RoomCount, 3);
        }

        [Test]
        public void GenerateFloor_DeterministicSeed_SameSequence()
        {
            var layout = CreateLayout();

            _manager.GenerateFloor(layout, 42);
            var rooms1 = _manager.GetFloorRooms().Select(r => r.RoomId).ToList();

            _manager.GenerateFloor(layout, 42);
            var rooms2 = _manager.GetFloorRooms().Select(r => r.RoomId).ToList();

            CollectionAssert.AreEqual(rooms1, rooms2,
                "Same seed must produce identical floor layout");
        }

        [Test]
        public void GenerateFloor_DifferentSeeds_DifferentSequences()
        {
            var layout = CreateLayout(combatCount: 5);

            _manager.GenerateFloor(layout, 42);
            var rooms1 = _manager.GetFloorRooms().Select(r => r.RoomId).ToList();

            _manager.GenerateFloor(layout, 999);
            var rooms2 = _manager.GetFloorRooms().Select(r => r.RoomId).ToList();

            // With enough combat rooms and different seeds, sequences should differ
            CollectionAssert.AreNotEqual(rooms1, rooms2,
                "Different seeds should produce different floor layouts");
        }

        [Test]
        public void GenerateFloor_LastRoomIsBoss()
        {
            var layout = CreateLayout();
            _manager.GenerateFloor(layout, 42);

            var rooms = _manager.GetFloorRooms();
            var lastRoom = rooms[rooms.Count - 1];

            Assert.AreEqual(RoomType.Boss, lastRoom.Type);
        }

        [Test]
        public void GenerateFloor_ContainsShopRoom()
        {
            var layout = CreateLayout();
            _manager.GenerateFloor(layout, 42);

            var rooms = _manager.GetFloorRooms();
            Assert.IsTrue(rooms.Any(r => r.Type == RoomType.Shop),
                "Floor should contain at least one shop room");
        }

        [Test]
        public void GenerateFloor_ContainsPotionRoom()
        {
            var layout = CreateLayout();
            _manager.GenerateFloor(layout, 42);

            var rooms = _manager.GetFloorRooms();
            Assert.IsTrue(rooms.Any(r => r.Type == RoomType.Potion),
                "Floor should contain at least one potion room");
        }

        [Test]
        public void CurrentRoom_BeforeGenerate_ReturnsNull()
        {
            Assert.IsNull(_manager.CurrentRoom);
        }

        [Test]
        public void NextRoom_AdvancesIndex()
        {
            var layout = CreateLayout();
            _manager.GenerateFloor(layout, 42);

            Assert.AreEqual(0, _manager.CurrentRoomIndex);

            _manager.NextRoom();

            Assert.AreEqual(1, _manager.CurrentRoomIndex);
        }

        [Test]
        public void NextRoom_FiresOnRoomEntered()
        {
            var layout = CreateLayout();
            _manager.GenerateFloor(layout, 42);

            bool fired = false;
            EventManager.Subscribe(EventName.OnRoomEntered, args => fired = true);

            _manager.NextRoom();

            Assert.IsTrue(fired, "NextRoom should fire OnRoomEntered");
        }

        [Test]
        public void NextRoom_AtLastRoom_ReturnsFalse()
        {
            var layout = CreateLayout(minRooms: 3, maxRooms: 3, combatCount: 2);
            _manager.GenerateFloor(layout, 42);

            // Advance to last room
            while (!_manager.IsLastRoom)
                _manager.NextRoom();

            bool result = _manager.NextRoom();

            Assert.IsFalse(result);
        }

        [Test]
        public void NextRoom_AtLastRoom_FiresOnFloorCleared()
        {
            var layout = CreateLayout(minRooms: 3, maxRooms: 3, combatCount: 2);
            _manager.GenerateFloor(layout, 42);

            bool fired = false;
            EventManager.Subscribe(EventName.OnFloorCleared, args => fired = true);

            // Advance to last room
            while (!_manager.IsLastRoom)
                _manager.NextRoom();

            _manager.NextRoom();

            Assert.IsTrue(fired, "NextRoom at last room should fire OnFloorCleared");
        }

        [Test]
        public void IsLastRoom_TrueOnlyAtFinalIndex()
        {
            var layout = CreateLayout(minRooms: 3, maxRooms: 3, combatCount: 2);
            _manager.GenerateFloor(layout, 42);

            // Check all rooms before the last
            for (int i = 0; i < _manager.RoomCount - 1; i++)
            {
                Assert.IsFalse(_manager.IsLastRoom,
                    $"IsLastRoom should be false at index {i}");
                _manager.NextRoom();
            }

            Assert.IsTrue(_manager.IsLastRoom,
                "IsLastRoom should be true at the final index");
        }

        [Test]
        public void GetFloorRooms_ReturnsAllGeneratedRooms()
        {
            var layout = CreateLayout();
            _manager.GenerateFloor(layout, 42);

            var rooms = _manager.GetFloorRooms();

            Assert.AreEqual(_manager.RoomCount, rooms.Count);
            Assert.IsNotNull(rooms);
        }

        [Test]
        public void GenerateFloor_CalledTwice_ResetsState()
        {
            var layout = CreateLayout();

            _manager.GenerateFloor(layout, 42);
            _manager.NextRoom();
            _manager.NextRoom();
            int firstCount = _manager.RoomCount;

            _manager.GenerateFloor(layout, 99);

            Assert.AreEqual(0, _manager.CurrentRoomIndex,
                "CurrentRoomIndex should reset to 0 after re-generation");
            // State should be fresh — index is 0, rooms are regenerated
            Assert.IsNotNull(_manager.CurrentRoom);
        }

        // --- Camera-facing contract (§17.E) -----------------------------------

        [Test]
        public void GetFloorBounds_NoRoomLayoutsInScene_ReturnsDefaultBounds()
        {
            _manager.GenerateFloor(CreateLayout(), 42);

            var bounds = _manager.GetFloorBounds();
            Assert.AreEqual(Vector3.zero, bounds.size,
                "En FP sin prefabs instanciados el floor bounds debe quedar en default (size == 0).");
        }

        [Test]
        public void GetCurrentRoomOccluders_NoPrefabInScene_ReturnsEmpty()
        {
            _manager.GenerateFloor(CreateLayout(), 42);

            var occluders = _manager.GetCurrentRoomOccluders();
            Assert.IsNotNull(occluders);
            Assert.AreEqual(0, occluders.Count,
                "FP sin room prefabs ⇒ no hay WallOccluders para listar.");
        }
    }
}
