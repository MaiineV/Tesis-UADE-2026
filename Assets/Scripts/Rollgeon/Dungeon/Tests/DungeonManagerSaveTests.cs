using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Patterns.Save;
using Rollgeon.Dungeon.Components;
using Rollgeon.Dungeon.State;
using Sirenix.Serialization;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Dungeon.Tests
{
    /// <summary>
    /// Persistencia espacial del piso (Feature#0028, Fase 1): <see cref="DungeonManager"/>
    /// como <c>ISaveable</c>. Verifica que capturar + restaurar preserve sala actual,
    /// dirección de entrada, <c>Visited</c>/<c>State</c> y <c>ObjectStates</c> por
    /// <see cref="RoomInstance.GridCell"/> — la topología se regenera con InstanceIds
    /// nuevos, así que la celda es la clave estable.
    /// </summary>
    [TestFixture]
    public class DungeonManagerSaveTests
    {
        private readonly List<Object> _createdObjects = new();
        private readonly List<DungeonManager> _managers = new();

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            SaveSystem.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var m in _managers) m?.Dispose();
            _managers.Clear();

            SaveSystem.ResetForTests();
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            foreach (var obj in _createdObjects)
                if (obj != null) Object.DestroyImmediate(obj);
            _createdObjects.Clear();
        }

        private DungeonManager NewManager()
        {
            var m = new DungeonManager();
            _managers.Add(m);
            return m;
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

        /// <summary>A Start(0,0) — B Combat(0,1) — C Combat(0,2). Sin prefabs.</summary>
        private FloorTopologyPlanner.Plan CreatePlan(out Dictionary<string, Vector2Int> cells)
        {
            cells = new Dictionary<string, Vector2Int>
            {
                ["start"] = new Vector2Int(0, 0),
                ["combat1"] = new Vector2Int(0, 1),
                ["combat2"] = new Vector2Int(0, 2),
            };
            var assignments = new Dictionary<Vector2Int, RoomSO>
            {
                [cells["start"]] = CreateRoom("start", RoomType.Start),
                [cells["combat1"]] = CreateRoom("combat1", RoomType.Combat),
                [cells["combat2"]] = CreateRoom("combat2", RoomType.Combat),
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

        private static RoomInstance At(DungeonManager m, Vector2Int cell) =>
            m.GetAllRoomInstances().Values.First(i => i.GridCell == cell);

        // Muta m1 a un estado "mitad de piso": entra a combat1 por la puerta,
        // la deja cleared, con un enemigo vivo persistido.
        private DungeonManager BuildDirtyManager(FloorTopologyPlanner.Plan plan, Vector2Int combat1Cell)
        {
            var m = NewManager();
            m.GenerateFromPlan(plan);
            // Start(0,0) es Cleared → cruzar North a combat1 es válido.
            Assert.IsTrue(m.EnterRoomByDoor(DoorDirection.North), "precondición: entrar a combat1");
            var combat1 = At(m, combat1Cell);
            combat1.State = RoomState.Cleared;
            combat1.ObjectStates.Set("enemy_0", new EnemySpawnState
            {
                SpawnPointId = "enemy_0",
                EnemyDataSOId = "goblin_01",
                CurrentHP = 9,
                IsDead = false,
                SpawnPointIndex = 0,
                Tier = 2,
            });
            return m;
        }

        [Test]
        public void CaptureRestore_ThroughOdin_PreservesSpatialStateByCell()
        {
            var plan = CreatePlan(out var cells);
            var m1 = BuildDirtyManager(plan, cells["combat1"]);

            // Capture + round-trip por el serializer real (Odin JSON) = simula disco.
            var snap = m1.CaptureState();
            byte[] bytes = SerializationUtility.SerializeValue(snap, DataFormat.JSON);
            var restored = SerializationUtility.DeserializeValue<DungeonSnapshot>(bytes, DataFormat.JSON);

            // Nuevo manager regenera la MISMA topología (InstanceIds nuevos) y aplica.
            var m2 = NewManager();
            m2.GenerateFromPlan(plan);
            m2.RestoreState(restored);
            m2.ResumeFromSave();

            // Sala actual + dirección de entrada por celda.
            Assert.AreEqual(cells["combat1"], m2.CurrentRoomInstance.GridCell,
                "la sala actual se restaura por GridCell");
            Assert.AreEqual(DoorDirection.South, m2.LastEntryDirection,
                "la dirección de entrada se restaura");

            // Estado de la sala + ObjectStates polimórficos.
            var m2Combat1 = At(m2, cells["combat1"]);
            Assert.AreEqual(RoomState.Cleared, m2Combat1.State);
            Assert.IsTrue(m2Combat1.Visited);
            Assert.IsTrue(m2Combat1.ObjectStates.TryGet<EnemySpawnState>("enemy_0", out var enemy));
            Assert.AreEqual("goblin_01", enemy.EnemyDataSOId);
            Assert.AreEqual(9, enemy.CurrentHP);
            Assert.AreEqual(2, enemy.Tier);

            // Sala no visitada permanece fresca.
            Assert.AreEqual(RoomState.Uncleared, At(m2, cells["combat2"]).State);
            Assert.IsFalse(At(m2, cells["combat2"]).Visited);
        }

        [Test]
        public void Register_OnResume_AutoStagesSnapshot_AppliedByResumeFromSave()
        {
            var plan = CreatePlan(out var cells);
            var m1 = BuildDirtyManager(plan, cells["combat1"]);

            // Simula el límite de sesión: m1 se registra y al soltarse (como Dispose)
            // captura su estado final al cache.
            SaveSystem.Register(m1);
            SaveSystem.Unregister(m1);

            // Nueva sesión: el manager se registra con la misma key → auto-restore
            // stagea el snapshot cacheado; ResumeFromSave lo aplica.
            var m2 = NewManager();
            m2.GenerateFromPlan(plan);
            SaveSystem.Register(m2);
            m2.ResumeFromSave();

            Assert.AreEqual(cells["combat1"], m2.CurrentRoomInstance.GridCell);
            var m2Combat1 = At(m2, cells["combat1"]);
            Assert.AreEqual(RoomState.Cleared, m2Combat1.State);
            Assert.IsTrue(m2Combat1.ObjectStates.TryGet<EnemySpawnState>("enemy_0", out var enemy));
            Assert.AreEqual(9, enemy.CurrentHP);
        }

        [Test]
        public void ResumeFromSave_NoPendingRestore_IsNoop()
        {
            var plan = CreatePlan(out _);
            var m = NewManager();
            m.GenerateFromPlan(plan);

            // Sin restore stageado (run nueva) no debe tocar la sala inicial.
            Assert.DoesNotThrow(() => m.ResumeFromSave());
            Assert.AreEqual(Vector2Int.zero, m.CurrentRoomInstance.GridCell);
        }
    }
}
