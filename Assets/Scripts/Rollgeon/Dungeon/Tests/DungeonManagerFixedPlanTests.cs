using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Dungeon.Components;
using Rollgeon.Dungeon.State;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Dungeon.Tests
{
    /// <summary>
    /// Cobertura de los seams agregados para pisos autorados (tutorial):
    /// <see cref="DungeonManager.GenerateFromPlan"/>, <see cref="DungeonManager.SetRoomState"/>
    /// y el enforcement de <see cref="RoomState.Locked"/> en
    /// <see cref="DungeonManager.CanEnterRoomByDoor"/>.
    /// </summary>
    [TestFixture]
    public class DungeonManagerFixedPlanTests
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

        private RoomSO CreateRoom(string id, RoomType type)
        {
            var room = ScriptableObject.CreateInstance<RoomSO>();
            room.RoomId = id;
            room.DisplayName = id;
            room.Type = type;
            _createdObjects.Add(room);
            return room;
        }

        /// <summary>
        /// Topología del tutorial: A Start(0,0) — B Combat(0,1) — C Combat(0,2),
        /// D Shop(-1,1) colgada de B, E Enchant(1,2) colgada de C.
        /// </summary>
        private FloorTopologyPlanner.Plan CreateTutorialLikePlan(
            out Dictionary<string, Vector2Int> cellsById)
        {
            cellsById = new Dictionary<string, Vector2Int>
            {
                ["start"]   = new Vector2Int(0, 0),
                ["combat1"] = new Vector2Int(0, 1),
                ["combat2"] = new Vector2Int(0, 2),
                ["shop"]    = new Vector2Int(-1, 1),
                ["enchant"] = new Vector2Int(1, 2),
            };

            var assignments = new Dictionary<Vector2Int, RoomSO>
            {
                [cellsById["start"]]   = CreateRoom("start",   RoomType.Start),
                [cellsById["combat1"]] = CreateRoom("combat1", RoomType.Combat),
                [cellsById["combat2"]] = CreateRoom("combat2", RoomType.Combat),
                [cellsById["shop"]]    = CreateRoom("shop",    RoomType.Shop),
                [cellsById["enchant"]] = CreateRoom("enchant", RoomType.Enchantment),
            };

            var types = assignments.ToDictionary(kv => kv.Key, kv => kv.Value.Type);

            return new FloorTopologyPlanner.Plan
            {
                Seed = 0,
                TargetCount = assignments.Count,
                Cells = assignments.Keys.ToList(),
                Assignments = assignments,
                Types = types,
                ResolvedCounts = new Dictionary<RoomType, int>(),
                Warnings = Array.Empty<string>(),
            };
        }

        private RoomInstance InstanceAt(Vector2Int cell) =>
            _manager.GetAllRoomInstances().Values.First(i => i.GridCell == cell);

        // -----------------------------------------------------------------
        // GenerateFromPlan
        // -----------------------------------------------------------------

        [Test]
        public void GenerateFromPlan_NullPlan_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() => _manager.GenerateFromPlan(null));
        }

        [Test]
        public void GenerateFromPlan_TutorialTopology_WiresExactlyExpectedConnections()
        {
            var plan = CreateTutorialLikePlan(out var cells);

            _manager.GenerateFromPlan(plan);

            var expected = new Dictionary<string, int>
            {
                ["start"] = 1,   // A–B
                ["combat1"] = 3, // B–A, B–C, B–D
                ["combat2"] = 2, // C–B, C–E
                ["shop"] = 1,    // D–B
                ["enchant"] = 1, // E–C
            };

            foreach (var (id, count) in expected)
            {
                Assert.AreEqual(count, InstanceAt(cells[id]).Connections.Count,
                    $"'{id}' debe tener {count} conexión(es).");
            }
        }

        [Test]
        public void GenerateFromPlan_StartAtOrigin_BecomesCurrentRoom()
        {
            var plan = CreateTutorialLikePlan(out _);

            _manager.GenerateFromPlan(plan);

            Assert.AreEqual(Vector2Int.zero, _manager.CurrentRoomInstance.GridCell);
            Assert.AreEqual(RoomType.Start, _manager.CurrentRoomInstance.Template.Type);
        }

        [Test]
        public void GenerateFromPlan_CombatRoomsUncleared_SpecialRoomsCleared()
        {
            var plan = CreateTutorialLikePlan(out var cells);

            _manager.GenerateFromPlan(plan);

            Assert.AreEqual(RoomState.Uncleared, InstanceAt(cells["combat1"]).State);
            Assert.AreEqual(RoomState.Uncleared, InstanceAt(cells["combat2"]).State);
            Assert.AreEqual(RoomState.Cleared, InstanceAt(cells["shop"]).State);
            Assert.AreEqual(RoomState.Cleared, InstanceAt(cells["enchant"]).State);
        }

        // -----------------------------------------------------------------
        // SetRoomState
        // -----------------------------------------------------------------

        [Test]
        public void SetRoomState_UnknownInstance_ReturnsFalse()
        {
            _manager.GenerateFromPlan(CreateTutorialLikePlan(out _));

            Assert.IsFalse(_manager.SetRoomState(Guid.NewGuid(), RoomState.Locked));
        }

        [Test]
        public void SetRoomState_LockedToUnclearedToggle_PreservesObjectStates()
        {
            _manager.GenerateFromPlan(CreateTutorialLikePlan(out var cells));
            var combat2 = InstanceAt(cells["combat2"]);
            var doorKey = DoorDirection.South.DoorStateKey();
            Assert.IsTrue(combat2.ObjectStates.TryGet<DoorState>(doorKey, out _),
                "Precondición: la puerta al sur (hacia combat1) debe tener DoorState seedeado.");

            Assert.IsTrue(_manager.SetRoomState(combat2.InstanceId, RoomState.Locked));
            Assert.IsTrue(_manager.SetRoomState(combat2.InstanceId, RoomState.Uncleared));

            Assert.AreEqual(RoomState.Uncleared, combat2.State);
            Assert.IsTrue(combat2.ObjectStates.TryGet<DoorState>(doorKey, out _),
                "El toggle Locked↔Uncleared no debe tocar ObjectStates.");
        }

        // -----------------------------------------------------------------
        // Enforcement de Locked en CanEnterRoomByDoor
        // -----------------------------------------------------------------

        [Test]
        public void CanEnterRoomByDoor_NeighborLocked_BlocksEvenFromClearedRoom()
        {
            _manager.GenerateFromPlan(CreateTutorialLikePlan(out var cells));
            var combat1 = InstanceAt(cells["combat1"]);
            // Start (0,0) es Cleared; su vecino North (combat1) queda Locked.
            _manager.SetRoomState(combat1.InstanceId, RoomState.Locked);

            bool canEnter = _manager.CanEnterRoomByDoor(DoorDirection.North, out _);

            Assert.IsFalse(canEnter,
                "Vecino Locked debe bloquear aunque la sala actual esté Cleared.");
        }

        [Test]
        public void CanEnterRoomByDoor_NeighborLocked_BlocksEvenWithForcedDoor()
        {
            _manager.GenerateFromPlan(CreateTutorialLikePlan(out var cells));
            var start = _manager.CurrentRoomInstance;
            var combat1 = InstanceAt(cells["combat1"]);
            _manager.SetRoomState(combat1.InstanceId, RoomState.Locked);
            // Forced previo (ej. EffForceDoor marcó la puerta antes del gate).
            start.ObjectStates.TryGet<DoorState>(DoorDirection.North.DoorStateKey(), out var doorState);
            doorState.Forced = true;

            bool canEnter = _manager.CanEnterRoomByDoor(DoorDirection.North, out _);

            Assert.IsFalse(canEnter,
                "El check de vecino Locked debe tener precedencia sobre DoorState.Forced.");
        }

        [Test]
        public void CanEnterRoomByDoor_NeighborUnlockedAgain_AllowsEntry()
        {
            _manager.GenerateFromPlan(CreateTutorialLikePlan(out var cells));
            var combat1 = InstanceAt(cells["combat1"]);
            _manager.SetRoomState(combat1.InstanceId, RoomState.Locked);
            _manager.SetRoomState(combat1.InstanceId, RoomState.Uncleared);

            bool canEnter = _manager.CanEnterRoomByDoor(DoorDirection.North, out var neighborId);

            Assert.IsTrue(canEnter, "Al volver a Uncleared el vecino debe ser accesible.");
            Assert.AreEqual(combat1.InstanceId, neighborId);
        }

        [Test]
        public void EnterRoomByDoor_NeighborLocked_DoesNotTransition()
        {
            _manager.GenerateFromPlan(CreateTutorialLikePlan(out var cells));
            var combat1 = InstanceAt(cells["combat1"]);
            _manager.SetRoomState(combat1.InstanceId, RoomState.Locked);

            bool entered = _manager.EnterRoomByDoor(DoorDirection.North);

            Assert.IsFalse(entered);
            Assert.AreEqual(Vector2Int.zero, _manager.CurrentRoomInstance.GridCell,
                "El player debe seguir en la start room.");
        }
    }
}
