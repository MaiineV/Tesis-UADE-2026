using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Dungeon.Components;
using Rollgeon.Dungeon.State;
using Rollgeon.Entities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Dungeon.Tests
{
    /// <summary>
    /// Tests de persistencia de estado al re-entrar a salas. Foco: combinar
    /// <see cref="DoorState.Forced"/>, <see cref="EnemySpawnState"/>, y el
    /// hook <see cref="EventName.OnEntityDestroyed"/> → victoria automática.
    /// TECHNICAL.md §13.6.
    /// </summary>
    [TestFixture]
    public class RoomReentryStateTests
    {
        private DungeonManager _manager;
        private readonly List<Object> _createdObjects = new();

        [SetUp]
        public void SetUp() => _manager = new DungeonManager();

        [TearDown]
        public void TearDown()
        {
            _manager?.Dispose();
            foreach (var obj in _createdObjects)
                if (obj != null) Object.DestroyImmediate(obj);
            _createdObjects.Clear();

            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        private FloorLayoutSO CreateLayout()
        {
            var layout = ScriptableObject.CreateInstance<FloorLayoutSO>();
            _createdObjects.Add(layout);
            layout.RoomCountMin = 5;
            layout.RoomCountMax = 5;
            layout.StartRoom = CreateRoom("start_0", RoomType.Start);
            layout.CombatRooms = new List<RoomSO>
            {
                CreateRoom("combat_0", RoomType.Combat),
                CreateRoom("combat_1", RoomType.Combat),
                CreateRoom("combat_2", RoomType.Combat),
            };
            layout.ShopRooms = new List<RoomSO> { CreateRoom("shop_0", RoomType.Shop) };
            layout.PotionRooms = new List<RoomSO> { CreateRoom("potion_0", RoomType.Potion) };
            layout.BossCandidates = new List<EnemyDataSO> { CreateEnemy("boss_0") };
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

        [Test]
        public void OnEntityDestroyed_LastAliveEnemy_ClearsSpawnedEnemies()
        {
            _manager.GenerateFloor(CreateLayout(), 42);
            var combat = _manager.GetAllRoomInstances().Values
                .FirstOrDefault(i => i.Template.Type == RoomType.Combat);
            if (combat == null) Assert.Pass();

            _manager.EnterRoomByInstanceId(combat.InstanceId);

            var enemyId = Guid.NewGuid();
            combat.SpawnedEnemies.Add(enemyId);
            combat.ObjectStates.Set("enemy_0", new EnemySpawnState
            {
                SpawnPointId = "enemy_0",
                EnemyDataSOId = "test.enemy",
                CurrentHP = 10,
                IsDead = false,
                SpawnPointIndex = 0,
            });

            EventManager.Trigger(EventName.OnEntityDestroyed, enemyId, Guid.Empty);

            Assert.AreEqual(0, combat.SpawnedEnemies.Count,
                "SpawnedEnemies should be empty after last enemy destroyed.");
            combat.ObjectStates.TryGet<EnemySpawnState>("enemy_0", out var state);
            Assert.IsNotNull(state);
            Assert.IsTrue(state.IsDead,
                "EnemySpawnState.IsDead should be true for destroyed enemy.");
        }

        [Test]
        public void OnEntityDestroyed_MarksEnemyStateDead()
        {
            _manager.GenerateFloor(CreateLayout(), 42);
            var combat = _manager.GetAllRoomInstances().Values
                .FirstOrDefault(i => i.Template.Type == RoomType.Combat);
            if (combat == null) Assert.Pass();

            _manager.EnterRoomByInstanceId(combat.InstanceId);

            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            combat.SpawnedEnemies.Add(a);
            combat.SpawnedEnemies.Add(b);
            combat.ObjectStates.Set("enemy_0", new EnemySpawnState
            {
                SpawnPointId = "enemy_0", EnemyDataSOId = "e.a",
                CurrentHP = 5, IsDead = false, SpawnPointIndex = 0,
            });
            combat.ObjectStates.Set("enemy_1", new EnemySpawnState
            {
                SpawnPointId = "enemy_1", EnemyDataSOId = "e.b",
                CurrentHP = 5, IsDead = false, SpawnPointIndex = 1,
            });

            EventManager.Trigger(EventName.OnEntityDestroyed, a, Guid.Empty);

            combat.ObjectStates.TryGet<EnemySpawnState>("enemy_0", out var state0);
            Assert.IsNotNull(state0);
            Assert.IsTrue(state0.IsDead, "El EnemySpawnState del enemigo muerto debe quedar IsDead=true.");
            Assert.IsFalse(combat.SpawnedEnemies.Contains(a),
                "El Guid destruido debe salir de SpawnedEnemies.");
            Assert.AreEqual(RoomState.Uncleared, combat.State,
                "Con un enemigo vivo restante, la sala sigue Uncleared.");
        }

        [Test]
        public void DoorStateForced_Persists_AcrossExitReenter()
        {
            _manager.GenerateFloor(CreateLayout(), 42);

            var start = _manager.CurrentRoomInstance;
            var firstDir = start.Connections.Keys.First();

            start.ObjectStates.TryGet<DoorState>(DoorKey(firstDir), out var doorState);
            Assert.IsNotNull(doorState);
            doorState.Forced = true;

            // Cruzar a la vecina y volver.
            var originalStartId = start.InstanceId;
            Assume.That(_manager.EnterRoomByDoor(firstDir), Is.True);
            var backDir = OppositeOf(firstDir);
            if (!_manager.CanEnterRoomByDoor(backDir, out _))
            {
                // Si es combat Uncleared no podemos volver — saltamos el test.
                Assert.Pass("Vecino combat Uncleared sin forced — escenario no aplica.");
            }
            Assume.That(_manager.EnterRoomByDoor(backDir), Is.True);

            // El start ya debe seguir siendo el mismo, y la door Forced intacta.
            Assert.AreEqual(originalStartId, _manager.CurrentRoomInstance.InstanceId);
            _manager.CurrentRoomInstance.ObjectStates.TryGet<DoorState>(DoorKey(firstDir), out var restored);
            Assert.IsNotNull(restored);
            Assert.IsTrue(restored.Forced, "DoorState.Forced debe persistir entre exits.");
        }

        private static DoorDirection OppositeOf(DoorDirection dir) => dir switch
        {
            DoorDirection.North => DoorDirection.South,
            DoorDirection.South => DoorDirection.North,
            DoorDirection.East  => DoorDirection.West,
            DoorDirection.West  => DoorDirection.East,
            _                   => dir,
        };

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
