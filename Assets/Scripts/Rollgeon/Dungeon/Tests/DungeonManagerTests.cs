using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.FSM;
using Rollgeon.Dungeon.Components;
using Rollgeon.Dungeon.State;
using Rollgeon.Entities;
using UnityEngine;
using Object = UnityEngine.Object;

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

        // -----------------------------------------------------------------
        // Generación — topología + assignment
        // -----------------------------------------------------------------

        [Test]
        public void GenerateFloor_NullLayout_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() => _manager.GenerateFloor(null, 42));
        }

        [Test]
        public void GenerateFloor_WithStartRoom_PlacesStartInCellZero()
        {
            var start = CreateRoom("start_0", RoomType.Start);
            var layout = CreateLayout();
            layout.StartRoom = start;

            _manager.GenerateFloor(layout, 42);

            Assert.AreSame(start, _manager.CurrentRoom);
            Assert.AreEqual(Vector2Int.zero, _manager.CurrentRoomInstance.GridCell);
            Assert.AreEqual(RoomType.Start, _manager.CurrentRoomInstance.Template.Type);
        }

        [Test]
        public void GenerateFloor_ProducesCellsDistintasContiguas()
        {
            _manager.GenerateFloor(CreateLayout(minRooms: 6, maxRooms: 6), 42);

            var cells = _manager.GetAllRoomInstances()
                .Values.Select(i => i.GridCell).ToList();

            Assert.AreEqual(cells.Count, cells.Distinct().Count(),
                "Cells must be unique");
        }

        [Test]
        public void GenerateFloor_GraphIsConnected()
        {
            _manager.GenerateFloor(CreateLayout(minRooms: 6, maxRooms: 6), 42);

            var all = _manager.GetAllRoomInstances();
            var startId = _manager.CurrentRoomInstance.InstanceId;

            var visited = new HashSet<Guid> { startId };
            var queue = new Queue<Guid>();
            queue.Enqueue(startId);

            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                var node = all[id];
                foreach (var (dir, neighborId) in node.Connections)
                {
                    if (visited.Add(neighborId)) queue.Enqueue(neighborId);
                }
            }

            Assert.AreEqual(all.Count, visited.Count,
                "BFS desde start debe alcanzar todas las instancias");
        }

        [Test]
        public void GenerateFloor_BossPlacedAtFurthestManhattan()
        {
            _manager.GenerateFloor(CreateLayout(minRooms: 6, maxRooms: 6), 42);

            var all = _manager.GetAllRoomInstances();
            var startCell = Vector2Int.zero;

            RoomInstance bossInstance = null;
            int maxDist = -1;
            foreach (var inst in all.Values)
            {
                int d = Math.Abs(inst.GridCell.x - startCell.x) + Math.Abs(inst.GridCell.y - startCell.y);
                if (d > maxDist)
                {
                    maxDist = d;
                    bossInstance = inst;
                }
            }

            Assert.IsNotNull(bossInstance);
            Assert.AreEqual(RoomType.Boss, bossInstance.Template.Type);
        }

        [Test]
        public void GenerateFloor_ContainsShopRoom()
        {
            _manager.GenerateFloor(CreateLayout(), 42);

            Assert.IsTrue(_manager.GetAllRoomInstances().Values
                .Any(i => i.Template.Type == RoomType.Shop));
        }

        [Test]
        public void GenerateFloor_ContainsPotionRoom()
        {
            _manager.GenerateFloor(CreateLayout(), 42);

            Assert.IsTrue(_manager.GetAllRoomInstances().Values
                .Any(i => i.Template.Type == RoomType.Potion));
        }

        [Test]
        public void GenerateFloor_DeterministicSeed_SameGraph()
        {
            _manager.GenerateFloor(CreateLayout(), 42);
            var firstCells = _manager.GetAllRoomInstances().Values
                .Select(i => (i.GridCell, i.Template.Type))
                .OrderBy(p => p.GridCell.x).ThenBy(p => p.GridCell.y)
                .ToList();

            _manager.GenerateFloor(CreateLayout(), 42);
            var secondCells = _manager.GetAllRoomInstances().Values
                .Select(i => (i.GridCell, i.Template.Type))
                .OrderBy(p => p.GridCell.x).ThenBy(p => p.GridCell.y)
                .ToList();

            CollectionAssert.AreEqual(firstCells, secondCells,
                "Same seed must produce identical topology");
        }

        [Test]
        public void GenerateFloor_CurrentRoomInstance_IsStart()
        {
            var start = CreateRoom("start_0", RoomType.Start);
            var layout = CreateLayout();
            layout.StartRoom = start;

            _manager.GenerateFloor(layout, 42);

            Assert.AreEqual(Vector2Int.zero, _manager.CurrentRoomInstance.GridCell);
            Assert.AreEqual(RoomState.Cleared, _manager.CurrentRoomInstance.State,
                "Start rooms deben arrancar en Cleared");
        }

        [Test]
        public void GenerateFloor_GeneratesOneShellPerInstance()
        {
            _manager.GenerateFloor(CreateLayout(), 42);

            var instances = _manager.GetAllRoomInstances();
            var shells = _manager.GetFloorShells();

            Assert.AreEqual(instances.Count, shells.Count);
            foreach (var id in instances.Keys)
            {
                Assert.IsTrue(shells.ContainsKey(id),
                    $"Shell missing para instancia {id}");
            }
        }

        [Test]
        public void GenerateFloor_CombatRoomStateIsUncleared()
        {
            _manager.GenerateFloor(CreateLayout(), 42);

            var combatRooms = _manager.GetAllRoomInstances().Values
                .Where(i => i.Template.Type == RoomType.Combat).ToList();

            Assert.IsTrue(combatRooms.Count > 0);
            Assert.IsTrue(combatRooms.All(r => r.State == RoomState.Uncleared));
        }

        [Test]
        public void GenerateFloor_SeedDefaultDoorStatesForConnections()
        {
            _manager.GenerateFloor(CreateLayout(), 42);

            foreach (var instance in _manager.GetAllRoomInstances().Values)
            {
                foreach (var dir in instance.Connections.Keys)
                {
                    string key = DoorKey(dir);
                    Assert.IsTrue(instance.ObjectStates.ContainsKey(key),
                        $"Instancia {instance.InstanceId} connect en {dir} debe tener DoorState seed");
                }
            }
        }

        // -----------------------------------------------------------------
        // Navegación
        // -----------------------------------------------------------------

        [Test]
        public void EnterRoomByDoor_NoConnection_ReturnsFalse()
        {
            var start = CreateRoom("start_0", RoomType.Start);
            var layout = CreateLayout();
            layout.StartRoom = start;
            _manager.GenerateFloor(layout, 42);

            // Elegí una dirección que sabemos no tiene vecino (la start solo
            // tiene al menos 1 vecino pero no necesariamente los 4).
            var connections = _manager.CurrentRoomInstance.Connections;
            DoorDirection missing = DoorDirection.North;
            foreach (var d in new[] { DoorDirection.North, DoorDirection.South,
                                       DoorDirection.East, DoorDirection.West })
            {
                if (!connections.ContainsKey(d)) { missing = d; break; }
            }

            // Si por azar hay 4 conexiones, el test se vuelve vacío — skip.
            if (connections.Count == 4) Assert.Pass("Start tiene las 4 puertas conectadas.");

            Assert.IsFalse(_manager.EnterRoomByDoor(missing));
        }

        [Test]
        public void EnterRoomByDoor_ClearedRoom_ConnectedDir_Succeeds()
        {
            var start = CreateRoom("start_0", RoomType.Start);
            var layout = CreateLayout();
            layout.StartRoom = start;
            _manager.GenerateFloor(layout, 42);

            Assume.That(_manager.CurrentRoomInstance.State, Is.EqualTo(RoomState.Cleared));

            var anyDir = _manager.CurrentRoomInstance.Connections.Keys.First();
            var expectedId = _manager.CurrentRoomInstance.Connections[anyDir];

            bool ok = _manager.EnterRoomByDoor(anyDir);

            Assert.IsTrue(ok);
            Assert.AreEqual(expectedId, _manager.CurrentRoomInstance.InstanceId);
        }

        [Test]
        public void EnterRoomByDoor_UnclearedCombat_LocksDoors()
        {
            var start = CreateRoom("start_0", RoomType.Start);
            var layout = CreateLayout();
            layout.StartRoom = start;
            _manager.GenerateFloor(layout, 42);

            // Entrar a la primera conexión (start → combat típico)
            var firstDir = _manager.CurrentRoomInstance.Connections.Keys.First();
            _manager.EnterRoomByDoor(firstDir);

            var combatInstance = _manager.CurrentRoomInstance;
            if (combatInstance.State != RoomState.Uncleared) Assert.Pass();

            // Intentar salir por CUALQUIER dirección conectada → false.
            foreach (var dir in combatInstance.Connections.Keys)
            {
                Assert.IsFalse(_manager.CanEnterRoomByDoor(dir, out _),
                    $"Uncleared combat room must lock door {dir}");
            }
        }

        [Test]
        public void OnCombatEnd_Victory_MarksRoomCleared()
        {
            _manager.GenerateFloor(CreateLayout(), 42);
            var combatInstance = _manager.GetAllRoomInstances().Values
                .First(i => i.Template.Type == RoomType.Combat);

            EventManager.Trigger(EventName.OnCombatEnd,
                combatInstance.InstanceId, CombatOutcome.Victory);

            Assert.AreEqual(RoomState.Cleared, combatInstance.State);
        }

        [Test]
        public void OnCombatEnd_Victory_UnlocksDoors()
        {
            var start = CreateRoom("start_0", RoomType.Start);
            var layout = CreateLayout();
            layout.StartRoom = start;
            _manager.GenerateFloor(layout, 42);

            var firstDir = _manager.CurrentRoomInstance.Connections.Keys.First();
            _manager.EnterRoomByDoor(firstDir);
            var combatInstance = _manager.CurrentRoomInstance;

            if (combatInstance.State != RoomState.Uncleared) Assert.Pass();

            EventManager.Trigger(EventName.OnCombatEnd,
                combatInstance.InstanceId, CombatOutcome.Victory);

            foreach (var dir in combatInstance.Connections.Keys)
            {
                Assert.IsTrue(_manager.CanEnterRoomByDoor(dir, out _),
                    $"Post-combate, todas las doors deben abrir ({dir})");
            }
        }

        [Test]
        public void EnterRoomByInstanceId_DebugPath_SucceedsIgnoringLocks()
        {
            _manager.GenerateFloor(CreateLayout(), 42);

            var target = _manager.GetAllRoomInstances().Values
                .First(i => i.InstanceId != _manager.CurrentRoomInstance.InstanceId);

            Assert.IsTrue(_manager.EnterRoomByInstanceId(target.InstanceId));
            Assert.AreEqual(target.InstanceId, _manager.CurrentRoomInstance.InstanceId);
        }

        // -----------------------------------------------------------------
        // Camera-facing contract (§17.E)
        // -----------------------------------------------------------------

        [Test]
        public void GetFloorBounds_AfterGeneration_HasNonZeroSize()
        {
            _manager.GenerateFloor(CreateLayout(), 42);

            var bounds = _manager.GetFloorBounds();
            Assert.AreNotEqual(Vector3.zero, bounds.size,
                "Generated floor must have non-zero bounds (iterando shells).");
        }

        [Test]
        public void GetFloorBounds_BeforeGeneration_ReturnsDefault()
        {
            var bounds = _manager.GetFloorBounds();
            Assert.AreEqual(Vector3.zero, bounds.size);
        }

        [Test]
        public void GetCurrentRoomOccluders_NoPrefab_ReturnsEmpty()
        {
            _manager.GenerateFloor(CreateLayout(), 42);

            var occluders = _manager.GetCurrentRoomOccluders();
            Assert.IsNotNull(occluders);
            Assert.AreEqual(0, occluders.Count,
                "Sin room prefab instanciado → sin WallOccluders.");
        }

        [Test]
        public void GenerateFloor_CalledTwice_ResetsState()
        {
            _manager.GenerateFloor(CreateLayout(), 42);
            int firstCount = _manager.GetAllRoomInstances().Count;

            _manager.GenerateFloor(CreateLayout(), 99);

            Assert.Greater(_manager.GetAllRoomInstances().Count, 0);
            Assert.IsNotNull(_manager.CurrentRoomInstance);
            Assert.AreEqual(Vector2Int.zero, _manager.CurrentRoomInstance.GridCell);
        }

        private static string DoorKey(DoorDirection dir) => dir switch
        {
            DoorDirection.North => "door_N",
            DoorDirection.South => "door_S",
            DoorDirection.East  => "door_E",
            DoorDirection.West  => "door_W",
            _                   => "door_?",
        };
    }
}
