using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.FSM;
using Rollgeon.Dungeon.Components;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Dungeon.Tests
{
    /// <summary>
    /// Repro del bug reportado: la exit door de la boss room debe MANTENERSE abierta
    /// (y con el cartel visible) al salir de la sala y volver a entrar después de
    /// derrotar al boss.
    /// </summary>
    [TestFixture]
    public class BossExitDoorReentryTests
    {
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
        /// Espejo del DoorBoss.prefab anidado: DoorRoot + DoorController + reja hija +
        /// DoorExitSignView con su cartel (inactivo). Root inactivo para no disparar
        /// Awakes de tooltips en EditMode.
        /// </summary>
        private GameObject CreateRoomPrefab(string name)
        {
            var root = new GameObject(name);
            root.SetActive(false);
            _createdObjects.Add(root);

            var layout = root.AddComponent<RoomLayout>();
            foreach (DoorDirection dir in Enum.GetValues(typeof(DoorDirection)))
            {
                var doorRoot = new GameObject($"Door_{dir}");
                doorRoot.transform.SetParent(root.transform, false);
                var ctrl = doorRoot.AddComponent<DoorController>();
                ctrl.Direction = dir;

                var reja = new GameObject("WallPlug");
                reja.transform.SetParent(doorRoot.transform, false);
                SetPrivateField(ctrl, DoorController.EditorWallPlugField, reja);

                var view = doorRoot.AddComponent<DoorExitSignView>();
                var sign = new GameObject("ExitSign");
                sign.transform.SetParent(doorRoot.transform, false);
                sign.SetActive(false);
                SetPrivateField(view, DoorExitSignView.EditorSignField, sign);

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

        private static void SetPrivateField(object target, string field, object value) =>
            target.GetType()
                .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(target, value);

        /// <summary>Start(0,0) — Boss(0,1). El boss queda dead-end: exit = North.</summary>
        private FloorTopologyPlanner.Plan CreatePlan(GameObject roomPrefab)
        {
            var assignments = new Dictionary<Vector2Int, RoomSO>
            {
                [new Vector2Int(0, 0)] = CreateRoom("start", RoomType.Start, roomPrefab),
                [new Vector2Int(0, 1)] = CreateRoom("boss",  RoomType.Boss,  roomPrefab),
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

        private static DoorController ExitControllerOf(RoomInstance boss)
        {
            foreach (var ctrl in boss.SpawnedPrefab
                         .GetComponentsInChildren<DoorController>(includeInactive: true))
                if (ctrl.IsExit)
                    return ctrl;
            return null;
        }

        [Test]
        public void ExitDoor_AfterBossKilledLeaveAndReenter_StaysOpenWithSign()
        {
            // Arrange — piso Start–Boss; entrar a la boss room y matar al boss.
            var plan = CreatePlan(CreateRoomPrefab("RoomTemplate"));
            _manager.GenerateFromPlan(plan);

            var boss = InstanceAt(new Vector2Int(0, 1));
            Assert.IsTrue(_manager.EnterRoomByDoor(DoorDirection.North),
                "Precondición: entrar del start a la boss room.");

            EventManager.Trigger(EventName.OnCombatEnd, boss.InstanceId, CombatOutcome.Victory);

            var exit = ExitControllerOf(boss);
            Assert.IsNotNull(exit, "Precondición: la boss room debe tener exit door designada.");
            Assert.AreEqual(DoorVisualState.Open, exit.CurrentState,
                "Precondición: la exit door abre al morir el boss.");

            // Act — salir de la boss room y volver a entrar.
            Assert.IsTrue(_manager.EnterRoomByDoor(DoorDirection.South), "Salir de la boss room.");
            Assert.IsTrue(_manager.EnterRoomByDoor(DoorDirection.North), "Re-entrar a la boss room.");

            // Assert — la exit door sigue abierta y el cartel visible.
            exit = ExitControllerOf(boss);
            Assert.IsNotNull(exit, "La exit door debe seguir designada tras la re-entrada.");
            Assert.AreEqual(DoorVisualState.Open, exit.CurrentState,
                "La exit door debe MANTENERSE abierta al re-entrar con el boss ya muerto.");

            var view = exit.GetComponent<DoorExitSignView>();
            Assert.IsTrue(view.EditorSign.activeSelf,
                "El ExitSign debe seguir visible al re-entrar a la boss room cleared.");
        }
    }
}
