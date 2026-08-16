using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Dungeon.Components;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Dungeon.Tests
{
    /// <summary>
    /// Swap runtime del DoorRoot por <see cref="RoomLayout.BossDoorPrefab"/> cuando el
    /// vecino de un slot es la boss room (DungeonManager.TrySwapBossDoor). Topología fija
    /// via <see cref="DungeonManager.GenerateFromPlan"/> para controlar la adyacencia.
    /// </summary>
    [TestFixture]
    public class BossDoorSwapTests
    {
        private const string BossDoorFakeName = "BossDoorFake";

        private DungeonManager _manager;
        private readonly List<Object> _createdObjects = new();

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
        // Fixtures
        // -----------------------------------------------------------------

        private RoomSO CreateRoom(string id, RoomType type, GameObject prefab)
        {
            var room = ScriptableObject.CreateInstance<RoomSO>();
            room.RoomId = id;
            room.DisplayName = id;
            room.Type = type;
            room.RoomPrefab = prefab;
            _createdObjects.Add(room);
            return room;
        }

        /// <summary>
        /// Espejo del Door.prefab anidado: DoorRoot con DoorController + meshes hijos,
        /// Anchor = transform del controller (igual que AutoPopulateDoorSlots) y WallPlug
        /// hijo del root. El root de sala queda inactivo para que Instantiate no dispare
        /// los Awake de tooltips en EditMode.
        /// </summary>
        private GameObject CreateRoomPrefab(string name, GameObject bossDoorPrefab)
        {
            var root = new GameObject(name);
            root.SetActive(false);
            _createdObjects.Add(root);

            var layout = root.AddComponent<RoomLayout>();
            layout.BossDoorPrefab = bossDoorPrefab;

            foreach (DoorDirection dir in Enum.GetValues(typeof(DoorDirection)))
            {
                var doorRoot = new GameObject($"Door_{dir}");
                doorRoot.transform.SetParent(root.transform, false);
                var ctrl = doorRoot.AddComponent<DoorController>();
                ctrl.Direction = dir;

                var reja = new GameObject("WallPlug");
                reja.transform.SetParent(doorRoot.transform, false);
                SetPrivateField(ctrl, DoorController.EditorWallPlugField, reja);

                layout.DoorSlots.Add(new DoorSlotRef
                {
                    Direction = dir,
                    Anchor = ctrl.transform,
                    DoorRoot = doorRoot,
                    WallPlug = reja,
                });
            }
            return root;
        }

        /// <summary>Variante boss con la misma estructura mínima (controller + reja hija).</summary>
        private GameObject CreateBossDoorPrefab()
        {
            var root = new GameObject(BossDoorFakeName);
            root.SetActive(false);
            _createdObjects.Add(root);

            var ctrl = root.AddComponent<DoorController>();
            var reja = new GameObject("WallPlug");
            reja.transform.SetParent(root.transform, false);
            SetPrivateField(ctrl, DoorController.EditorWallPlugField, reja);
            return root;
        }

        private static void SetPrivateField(object target, string field, object value) =>
            target.GetType()
                .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(target, value);

        /// <summary>
        /// Piso fijo: Start(0,0) con Boss al North(0,1) y Combat al East(1,0).
        /// </summary>
        private FloorTopologyPlanner.Plan CreatePlan(GameObject roomPrefab)
        {
            var assignments = new Dictionary<Vector2Int, RoomSO>
            {
                [new Vector2Int(0, 0)] = CreateRoom("start",  RoomType.Start,  roomPrefab),
                [new Vector2Int(0, 1)] = CreateRoom("boss",   RoomType.Boss,   roomPrefab),
                [new Vector2Int(1, 0)] = CreateRoom("combat", RoomType.Combat, roomPrefab),
            };

            return new FloorTopologyPlanner.Plan
            {
                Seed = 0,
                TargetCount = assignments.Count,
                Cells = assignments.Keys.ToList(),
                Assignments = assignments,
                Types = assignments.ToDictionary(kv => kv.Key, kv => kv.Value.Type),
                ResolvedCounts = new Dictionary<RoomType, int>(),
                Warnings = Array.Empty<string>(),
            };
        }

        private RoomInstance InstanceAt(Vector2Int cell) =>
            _manager.GetAllRoomInstances().Values.First(i => i.GridCell == cell);

        private static DoorSlotRef SlotOf(RoomInstance instance, DoorDirection dir) =>
            instance.SpawnedPrefab.GetComponent<RoomLayout>().GetDoorSlot(dir);

        // -----------------------------------------------------------------
        // Swap
        // -----------------------------------------------------------------

        [Test]
        public void GenerateFromPlan_SlotFacingBossRoom_SwapsDoorRootForBossVariant()
        {
            // Arrange
            var bossDoor = CreateBossDoorPrefab();
            var prefab = CreateRoomPrefab("RoomTemplate", bossDoor);
            var plan = CreatePlan(prefab);

            // Act
            _manager.GenerateFromPlan(plan);

            // Assert — el slot North de la start room (vecino boss) quedó con la variante:
            // DoorRoot nuevo, slot re-cableado (Anchor/WallPlug) y controller wireado.
            var start = InstanceAt(Vector2Int.zero);
            var slot = SlotOf(start, DoorDirection.North);

            StringAssert.StartsWith(BossDoorFakeName, slot.DoorRoot.name,
                "El DoorRoot hacia la boss room debe ser instancia del BossDoorPrefab.");

            var ctrl = slot.DoorRoot.GetComponentInChildren<DoorController>(includeInactive: true);
            Assert.AreEqual(DoorDirection.North, ctrl.Direction);
            Assert.AreEqual(DoorDirection.North.DoorStateKey(), ctrl.SpawnPointId);
            Assert.AreEqual(start.InstanceId, ctrl.OwnerRoomInstanceId);
            Assert.AreNotEqual(DoorVisualState.Tapiada, ctrl.CurrentState,
                "La puerta swapeada tiene vecino — no puede quedar Tapiada.");

            Assert.AreSame(ctrl.transform, slot.Anchor,
                "El Anchor debe re-apuntar al controller nuevo (contrato de AutoPopulateDoorSlots).");
            Assert.AreSame(ctrl.WallPlugRef, slot.WallPlug,
                "El WallPlug del slot debe re-apuntar a la reja de la variante.");

            // El original fue destruido — sin door root viejo ni controller duplicado.
            Assert.IsNull(start.SpawnedPrefab.transform.Find("Door_North"),
                "El DoorRoot original debe destruirse tras el swap.");
            int controllers = start.SpawnedPrefab
                .GetComponentsInChildren<DoorController>(includeInactive: true).Length;
            Assert.AreEqual(4, controllers, "Debe haber exactamente un controller por dirección.");
        }

        [Test]
        public void GenerateFromPlan_SlotFacingNonBossNeighbor_KeepsAuthoredDoor()
        {
            // Arrange
            var bossDoor = CreateBossDoorPrefab();
            var prefab = CreateRoomPrefab("RoomTemplate", bossDoor);
            var plan = CreatePlan(prefab);

            // Act
            _manager.GenerateFromPlan(plan);

            // Assert — el slot East de la start room (vecino Combat) conserva su puerta.
            var slot = SlotOf(InstanceAt(Vector2Int.zero), DoorDirection.East);
            Assert.AreEqual("Door_East", slot.DoorRoot.name,
                "Un vecino no-boss no debe swapear el modelo de puerta.");
        }

        [Test]
        public void GenerateFromPlan_BossRoomOwnDoors_KeepAuthoredDoor()
        {
            // Arrange
            var bossDoor = CreateBossDoorPrefab();
            var prefab = CreateRoomPrefab("RoomTemplate", bossDoor);
            var plan = CreatePlan(prefab);

            // Act
            _manager.GenerateFromPlan(plan);

            // Assert — la puerta interna de la boss room (South, hacia start) no cambia.
            var slot = SlotOf(InstanceAt(new Vector2Int(0, 1)), DoorDirection.South);
            Assert.AreEqual("Door_South", slot.DoorRoot.name,
                "Las puertas de la boss room misma no cambian de modelo.");
        }

        [Test]
        public void GenerateFromPlan_WithoutBossDoorPrefab_KeepsAuthoredDoor()
        {
            // Arrange
            var prefab = CreateRoomPrefab("RoomTemplate", bossDoorPrefab: null);
            var plan = CreatePlan(prefab);

            // Act
            _manager.GenerateFromPlan(plan);

            // Assert — sin variante asignada el comportamiento actual queda intacto.
            var slot = SlotOf(InstanceAt(Vector2Int.zero), DoorDirection.North);
            Assert.AreEqual("Door_North", slot.DoorRoot.name,
                "Sin BossDoorPrefab el slot conserva la puerta autorada.");
        }
    }
}
