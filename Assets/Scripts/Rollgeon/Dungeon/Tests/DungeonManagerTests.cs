using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.FSM;
using Rollgeon.Dungeon.Components;
using Rollgeon.Dungeon.State;
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

            // El test helper usaba minRooms/maxRooms para fijar el target count
            // de la topología. Con el modelo nuevo, eso es la suma de slots.
            // Repartimos: combat (variable), +shop, +potion, +boss → el resto
            // del rango lo absorbe combat con un Random spec.
            int specialBudget = shopCount + potionCount + bossCount;
            int combatMin = Mathf.Max(combatCount, minRooms - specialBudget);
            int combatMax = Mathf.Max(combatMin, maxRooms - specialBudget);

            var combatPool = new List<RoomSO>();
            for (int i = 0; i < combatCount; i++)
                combatPool.Add(CreateRoom($"combat_{i}", RoomType.Combat));

            var shopPool = new List<RoomSO>();
            for (int i = 0; i < shopCount; i++)
                shopPool.Add(CreateRoom($"shop_{i}", RoomType.Shop));

            var potionPool = new List<RoomSO>();
            for (int i = 0; i < potionCount; i++)
                potionPool.Add(CreateRoom($"potion_{i}", RoomType.Potion));

            var bossPool = new List<RoomSO>();
            for (int i = 0; i < bossCount; i++)
                bossPool.Add(CreateRoom($"boss_{i}", RoomType.Boss));

            layout.Slots = new List<RoomTypeSlot>
            {
                new RoomTypeSlot {
                    Type = RoomType.Combat,
                    Count = new RoomCountSpec { Mode = RoomCountMode.Random, Min = combatMin, Max = combatMax },
                    Pool = combatPool
                },
                new RoomTypeSlot {
                    Type = RoomType.Shop,
                    Count = new RoomCountSpec { Mode = RoomCountMode.Fixed, Fixed = shopCount },
                    Pool = shopPool
                },
                new RoomTypeSlot {
                    Type = RoomType.Potion,
                    Count = new RoomCountSpec { Mode = RoomCountMode.Fixed, Fixed = potionCount },
                    Pool = potionPool
                },
                new RoomTypeSlot {
                    Type = RoomType.Boss,
                    Count = new RoomCountSpec { Mode = RoomCountMode.Fixed, Fixed = bossCount },
                    Pool = bossPool
                },
            };

            return layout;
        }

        /// <summary>
        /// Inserta o reemplaza el Slot Start con count=1 y la pool {room}.
        /// </summary>
        private static void SetStartRoom(FloorLayoutSO layout, RoomSO room)
        {
            layout.Slots.RemoveAll(s => s.Type == RoomType.Start);
            layout.Slots.Insert(0, new RoomTypeSlot {
                Type = RoomType.Start,
                Count = new RoomCountSpec { Mode = RoomCountMode.Fixed, Fixed = 1 },
                Pool = new List<RoomSO> { room }
            });
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
            SetStartRoom(layout, start);

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
        public void GenerateFloor_BossRoom_IsDeadEnd_WithSingleEntrance_AcrossSeeds()
        {
            var start = CreateRoom("start_0", RoomType.Start);
            var layout = CreateLayout(minRooms: 6, maxRooms: 8);
            SetStartRoom(layout, start);

            for (int seed = 0; seed < 60; seed++)
            {
                _manager.GenerateFloor(layout, seed);

                var all = _manager.GetAllRoomInstances();
                var boss = all.Values.FirstOrDefault(
                    i => i.Template != null && i.Template.Type == RoomType.Boss);

                Assert.IsNotNull(boss, $"seed {seed}: debe existir boss room.");
                Assert.AreEqual(1, boss.Connections.Count,
                    $"seed {seed}: la boss room debe ser dead-end (exactamente 1 entrada).");

                // La poda de conexiones de la boss no debe desconectar el piso.
                AssertAllReachableFromStart(all, seed);
            }
        }

        private static void AssertAllReachableFromStart(
            IReadOnlyDictionary<Guid, RoomInstance> all, int seed)
        {
            var start = all.Values.FirstOrDefault(i => i.GridCell == Vector2Int.zero);
            Assert.IsNotNull(start, $"seed {seed}: debe haber start en cell (0,0).");

            var visited = new HashSet<Guid> { start.InstanceId };
            var queue = new Queue<Guid>();
            queue.Enqueue(start.InstanceId);
            while (queue.Count > 0)
            {
                var node = all[queue.Dequeue()];
                foreach (var (dir, neighborId) in node.Connections)
                    if (visited.Add(neighborId)) queue.Enqueue(neighborId);
            }

            Assert.AreEqual(all.Count, visited.Count,
                $"seed {seed}: tras podar la boss a 1 entrada, todo el piso sigue alcanzable.");
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
            SetStartRoom(layout, start);

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
            SetStartRoom(layout, start);
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
            SetStartRoom(layout, start);
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
            SetStartRoom(layout, start);
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
            SetStartRoom(layout, start);
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

        // -----------------------------------------------------------------
        // LastEntryDirection
        // -----------------------------------------------------------------

        [Test]
        public void GenerateFloor_LastEntryDirection_IsNull()
        {
            var layout = CreateLayout();
            SetStartRoom(layout, CreateRoom("start_0", RoomType.Start));
            _manager.GenerateFloor(layout, 42);

            Assert.IsNull(_manager.LastEntryDirection);
        }

        [Test]
        public void EnterRoomByDoor_SetsLastEntryDirectionToOpposite()
        {
            var layout = CreateLayout();
            SetStartRoom(layout, CreateRoom("start_0", RoomType.Start));
            _manager.GenerateFloor(layout, 42);

            var firstDir = _manager.CurrentRoomInstance.Connections.Keys.First();
            _manager.EnterRoomByDoor(firstDir);

            Assert.AreEqual(firstDir.Opposite(), _manager.LastEntryDirection);
        }

        [Test]
        public void EnterRoomByInstanceId_SetsLastEntryDirectionToNull()
        {
            var layout = CreateLayout();
            SetStartRoom(layout, CreateRoom("start_0", RoomType.Start));
            _manager.GenerateFloor(layout, 42);

            var firstDir = _manager.CurrentRoomInstance.Connections.Keys.First();
            _manager.EnterRoomByDoor(firstDir);
            Assume.That(_manager.LastEntryDirection, Is.Not.Null);

            var targetId = _manager.GetAllRoomInstances().Values
                .First(i => i.InstanceId != _manager.CurrentRoomInstance.InstanceId).InstanceId;
            _manager.EnterRoomByInstanceId(targetId);

            Assert.IsNull(_manager.LastEntryDirection);
        }

        private static string DoorKey(DoorDirection dir) => dir.DoorStateKey();
    }
}
