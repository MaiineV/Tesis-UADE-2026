using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.FSM;
using Rollgeon.Dungeon.Components;
using Rollgeon.Dungeon.State;
using Rollgeon.UI.Tooltips;
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
        // Visited — fog of war del floor view
        // -----------------------------------------------------------------

        [Test]
        public void GenerateFloor_OnlyStartRoomIsVisited()
        {
            var start = CreateRoom("start_0", RoomType.Start);
            var layout = CreateLayout();
            SetStartRoom(layout, start);

            _manager.GenerateFloor(layout, 42);

            var startId = _manager.CurrentRoomInstance.InstanceId;
            foreach (var instance in _manager.GetAllRoomInstances().Values)
            {
                bool expected = instance.InstanceId == startId;
                Assert.AreEqual(expected, instance.Visited,
                    $"Solo la start room nace visitada (id {instance.InstanceId}).");
            }
        }

        [Test]
        public void EnterRoomByDoor_MarksDestinationVisited()
        {
            var start = CreateRoom("start_0", RoomType.Start);
            var layout = CreateLayout();
            SetStartRoom(layout, start);
            _manager.GenerateFloor(layout, 42);

            var dir = _manager.CurrentRoomInstance.Connections.Keys.First();
            var destId = _manager.CurrentRoomInstance.Connections[dir];
            Assume.That(_manager.GetAllRoomInstances()[destId].Visited, Is.False,
                "Precondición: la sala destino arranca no visitada.");

            _manager.EnterRoomByDoor(dir);

            Assert.IsTrue(_manager.GetAllRoomInstances()[destId].Visited,
                "Entrar a una sala la marca como visitada.");
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

        // -----------------------------------------------------------------
        // Puertas — BUG-014
        // -----------------------------------------------------------------

        /// <summary>
        /// Prefab de sala con 4 DoorControllers físicos activos pero cuyos
        /// DoorSlotRefs no tienen DoorRoot cableado — el estado real de
        /// Shop_Room01 que disparó BUG-014. El root queda inactivo para que
        /// Instantiate no dispare los Awake de tooltips en EditMode.
        /// </summary>
        private GameObject CreateRoomPrefabWithOrphanDoors()
        {
            var root = new GameObject("RoomTemplate_OrphanDoors");
            root.SetActive(false);
            _createdObjects.Add(root);

            var layout = root.AddComponent<Components.RoomLayout>();
            foreach (DoorDirection dir in Enum.GetValues(typeof(DoorDirection)))
            {
                var door = new GameObject($"Door_{dir}");
                door.transform.SetParent(root.transform, false);
                var ctrl = door.AddComponent<DoorController>();
                ctrl.Direction = dir;

                layout.DoorSlots.Add(new DoorSlotRef { Direction = dir });
            }
            return root;
        }

        [Test]
        public void GenerateFloor_DoorSlotWithoutDoorRoot_TapiadaDoorsWithoutNeighbor()
        {
            // Arrange
            var prefab = CreateRoomPrefabWithOrphanDoors();
            var layout = CreateLayout();
            foreach (var slot in layout.Slots)
                foreach (var room in slot.Pool)
                    room.RoomPrefab = prefab;

            // Act — el warning "DoorController sin DoorSlotRef" es esperado acá.
            _manager.GenerateFloor(layout, 42);

            // Assert — sin vecino la puerta queda Tapiada pero ACTIVA para que el
            // zócalo (wall-fill) complete la pared en vez de dejar un hueco.
            int orphansChecked = 0;
            foreach (var instance in _manager.GetAllRoomInstances().Values)
            {
                Assert.IsNotNull(instance.SpawnedPrefab,
                    $"'{instance.Template.RoomId}' debe instanciar su prefab.");

                var controllers = instance.SpawnedPrefab
                    .GetComponentsInChildren<DoorController>(includeInactive: true);

                foreach (var ctrl in controllers)
                {
                    if (ctrl.IsExit) continue;
                    if (instance.Connections.ContainsKey(ctrl.Direction)) continue;

                    orphansChecked++;
                    Assert.IsTrue(ctrl.gameObject.activeSelf,
                        $"puerta {ctrl.Direction} de '{instance.Template.RoomId}' " +
                        "debe quedar activa para mostrar el zócalo sin vecino.");
                    Assert.AreEqual(DoorVisualState.Tapiada, ctrl.CurrentState,
                        $"BUG-014: puerta {ctrl.Direction} sin vecino debe quedar Tapiada.");
                }
            }

            // La boss room es dead-end (1 conexión), así que siempre hay
            // al menos 3 puertas sin vecino — el assert no puede ser vacuo.
            Assert.Greater(orphansChecked, 0, "El escenario debe producir puertas sin vecino.");
        }

        // -----------------------------------------------------------------
        // Visuales de puerta — CNF-012 v2 (abierta / reja / apagada)
        // -----------------------------------------------------------------

        /// <summary>
        /// Prefab de sala espejo de los reales: cada DoorSlotRef tiene DoorRoot
        /// (con DoorController) y los cuatro meshes hijos (open/closed/reja/wall-fill)
        /// ACTIVOS por default — la estructura del Door.prefab anidado, donde el
        /// WallPlug (la reja) es HIJO del DoorRoot. El root queda inactivo para que
        /// Instantiate no dispare los Awake de tooltips en EditMode.
        /// </summary>
        private GameObject CreateRoomPrefabWithRejaDoors()
        {
            var root = new GameObject("RoomTemplate_RejaDoors");
            root.SetActive(false);
            _createdObjects.Add(root);

            var layout = root.AddComponent<Components.RoomLayout>();
            foreach (DoorDirection dir in Enum.GetValues(typeof(DoorDirection)))
            {
                var doorRoot = new GameObject($"Door_{dir}");
                doorRoot.transform.SetParent(root.transform, false);
                var ctrl = doorRoot.AddComponent<DoorController>();
                ctrl.Direction = dir;

                var meshOpen   = CreateDoorChild(doorRoot, "MeshOpen");
                var meshClosed = CreateDoorChild(doorRoot, "MeshClose");
                var reja       = CreateDoorChild(doorRoot, "WallPlug");
                var wallFill   = CreateDoorChild(doorRoot, "WallFill");

                SetPrivateField(ctrl, DoorController.EditorMeshOpenField, meshOpen);
                SetPrivateField(ctrl, DoorController.EditorMeshClosedField, meshClosed);
                SetPrivateField(ctrl, DoorController.EditorWallPlugField, reja);
                SetPrivateField(ctrl, DoorController.EditorMeshWallFillField, wallFill);

                layout.DoorSlots.Add(new DoorSlotRef
                {
                    Direction = dir,
                    DoorRoot = doorRoot,
                    WallPlug = reja,
                });
            }
            return root;
        }

        private static GameObject CreateDoorChild(GameObject parent, string name)
        {
            // Espejo del Door.prefab real: los meshes arrancan activos y es
            // SetState quien los colapsa al estado correcto.
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            return child;
        }

        [Test]
        public void GenerateFloor_SlotWithoutNeighbor_ShowsWallFillZocalo()
        {
            // Arrange
            var prefab = CreateRoomPrefabWithRejaDoors();
            var layout = CreateLayout();
            foreach (var slot in layout.Slots)
                foreach (var room in slot.Pool)
                    room.RoomPrefab = prefab;

            // Act
            _manager.GenerateFloor(layout, 42);

            // Assert — sin camino la pared se completa con el zócalo: DoorRoot activo,
            // estado Tapiada, solo el wall-fill prendido (open y reja apagados).
            int blockedChecked = 0;
            foreach (var instance in _manager.GetAllRoomInstances().Values)
            {
                var roomLayout = instance.SpawnedPrefab.GetComponent<Components.RoomLayout>();
                foreach (var slot in roomLayout.DoorSlots)
                {
                    var ctrl = slot.DoorRoot.GetComponentInChildren<DoorController>(includeInactive: true);
                    if (ctrl.IsExit) continue; // salida de piso: canal propio (#158)
                    if (instance.Connections.ContainsKey(slot.Direction)) continue;

                    blockedChecked++;
                    Assert.IsTrue(slot.DoorRoot.activeSelf,
                        $"CNF-012 v2 rev: DoorRoot {slot.Direction} de '{instance.Template.RoomId}' " +
                        "sin vecino debe quedar activo para mostrar el zócalo.");
                    Assert.AreEqual(DoorVisualState.Tapiada, ctrl.CurrentState,
                        $"Puerta {slot.Direction} sin vecino debe quedar Tapiada.");
                    Assert.IsTrue(ctrl.EditorMeshWallFill.activeSelf,
                        $"El zócalo de {slot.Direction} debe prenderse sin camino.");
                    Assert.IsFalse(ctrl.EditorWallPlug.activeSelf,
                        $"La reja de {slot.Direction} debe quedar apagada sin camino.");
                    Assert.IsFalse(ctrl.EditorMeshOpen.activeSelf,
                        $"El mesh open de {slot.Direction} debe quedar apagado sin camino.");
                }
            }

            // La boss room es dead-end (1 conexión + 1 exit), así que siempre
            // hay al menos 2 puertas bloqueadas — el assert no puede ser vacuo.
            Assert.Greater(blockedChecked, 0, "El escenario debe producir puertas bloqueadas.");
        }

        [Test]
        public void GenerateFloor_SlotWithNeighbor_ShowsOpenMeshOrRejaByState()
        {
            // Arrange
            var prefab = CreateRoomPrefabWithRejaDoors();
            var layout = CreateLayout();
            foreach (var slot in layout.Slots)
                foreach (var room in slot.Pool)
                    room.RoomPrefab = prefab;

            // Act
            _manager.GenerateFloor(layout, 42);

            // Assert — puerta con vecino: abierta = mesh open, bloqueada = reja,
            // y la puerta sólida (mesh closed) nunca se prende.
            int connectedChecked = 0;
            foreach (var instance in _manager.GetAllRoomInstances().Values)
            {
                var roomLayout = instance.SpawnedPrefab.GetComponent<Components.RoomLayout>();
                foreach (var slot in roomLayout.DoorSlots)
                {
                    var ctrl = slot.DoorRoot.GetComponentInChildren<DoorController>(includeInactive: true);
                    if (ctrl.IsExit) continue;
                    if (!instance.Connections.ContainsKey(slot.Direction)) continue;

                    connectedChecked++;
                    Assert.IsTrue(slot.DoorRoot.activeSelf,
                        $"Puerta {slot.Direction} de '{instance.Template.RoomId}' con vecino debe estar activa.");
                    Assert.AreNotEqual(DoorVisualState.Tapiada, ctrl.CurrentState,
                        $"Puerta {slot.Direction} con vecino no puede quedar Tapiada.");

                    bool open   = ctrl.CurrentState == DoorVisualState.Open;
                    bool locked = ctrl.CurrentState == DoorVisualState.LockedCombat
                                  || ctrl.CurrentState == DoorVisualState.LockedSkillCheck;
                    Assert.AreEqual(open, ctrl.EditorMeshOpen.activeSelf,
                        $"Mesh open de {slot.Direction} debe seguir al estado Open.");
                    Assert.AreEqual(locked, ctrl.EditorWallPlug.activeSelf,
                        $"La reja de {slot.Direction} debe prenderse solo bloqueada (estado {ctrl.CurrentState}).");
                    Assert.IsFalse(ctrl.EditorMeshClosed.activeSelf,
                        $"La puerta sólida de {slot.Direction} no tiene estado asignado — siempre off.");
                    Assert.IsFalse(ctrl.EditorMeshWallFill.activeSelf,
                        $"El zócalo de {slot.Direction} solo se ve sin camino, no con vecino.");
                }
            }

            Assert.Greater(connectedChecked, 0, "El escenario debe producir puertas conectadas.");
        }

        [Test]
        public void SetState_MapsVisuals_OpenMesh_RejaWhenLocked_ZocaloWhenTapiada()
        {
            // Arrange
            var go = new GameObject("Door_VisualMap");
            _createdObjects.Add(go);
            var ctrl = go.AddComponent<DoorController>();
            var meshOpen   = CreateDoorChild(go, "MeshOpen");
            var meshClosed = CreateDoorChild(go, "MeshClose");
            var reja       = CreateDoorChild(go, "WallPlug");
            var wallFill   = CreateDoorChild(go, "WallFill");
            SetPrivateField(ctrl, DoorController.EditorMeshOpenField, meshOpen);
            SetPrivateField(ctrl, DoorController.EditorMeshClosedField, meshClosed);
            SetPrivateField(ctrl, DoorController.EditorWallPlugField, reja);
            SetPrivateField(ctrl, DoorController.EditorMeshWallFillField, wallFill);

            // Act + Assert — abierta: solo mesh open.
            ctrl.SetState(DoorVisualState.Open);
            Assert.IsTrue(meshOpen.activeSelf, "Open debe prender el mesh de puerta abierta.");
            Assert.IsFalse(reja.activeSelf, "Open no muestra la reja.");
            Assert.IsFalse(meshClosed.activeSelf, "La puerta sólida queda siempre off.");
            Assert.IsFalse(wallFill.activeSelf, "Open no muestra el zócalo.");

            // Bloqueada (forzable): solo la reja — tanto lock de combate como skill check.
            ctrl.SetState(DoorVisualState.LockedCombat);
            Assert.IsTrue(reja.activeSelf, "LockedCombat debe mostrar la reja.");
            Assert.IsFalse(meshOpen.activeSelf, "LockedCombat no muestra la puerta abierta.");
            Assert.IsFalse(meshClosed.activeSelf, "La puerta sólida queda siempre off.");
            Assert.IsFalse(wallFill.activeSelf, "LockedCombat no muestra el zócalo.");

            ctrl.SetState(DoorVisualState.LockedSkillCheck);
            Assert.IsTrue(reja.activeSelf, "LockedSkillCheck debe mostrar la reja.");

            // Sin camino: solo el zócalo que completa la pared.
            ctrl.SetState(DoorVisualState.Tapiada);
            Assert.IsTrue(wallFill.activeSelf, "Tapiada debe mostrar el zócalo.");
            Assert.IsFalse(meshOpen.activeSelf, "Tapiada no muestra la puerta abierta.");
            Assert.IsFalse(meshClosed.activeSelf, "Tapiada no muestra la puerta sólida.");
            Assert.IsFalse(reja.activeSelf, "Tapiada no muestra la reja.");
        }

        [Test]
        public void SetState_Tapiada_DisablesForceDoorTooltip_AndOpenReenablesIt()
        {
            // Arrange — Awake por reflection (en EditMode no corre solo).
            var go = new GameObject("Door_TooltipGate");
            _createdObjects.Add(go);
            var ctrl = go.AddComponent<DoorController>();
            InvokeAwake(ctrl);

            var trigger = go.GetComponent<WorldTooltipTrigger>();
            Assert.IsNotNull(trigger, "Awake debe auto-agregar el WorldTooltipTrigger.");

            // Act + Assert — sin camino no hay acción: sin tooltip de Forzar Puerta.
            ctrl.SetState(DoorVisualState.Tapiada);
            Assert.IsFalse(trigger.enabled,
                "CNF-012: Tapiada debe deshabilitar el tooltip de Forzar Puerta.");

            ctrl.SetState(DoorVisualState.Open);
            Assert.IsTrue(trigger.enabled,
                "Volver a Open debe rehabilitar el tooltip.");
        }

        private static void InvokeAwake(object target)
        {
            var awake = target.GetType().GetMethod("Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            awake?.Invoke(target, null);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' no encontrado en {target.GetType().Name}.");
            field.SetValue(target, value);
        }
    }
}
