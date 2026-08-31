using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.State;
using Rollgeon.Entities;
using Rollgeon.Grid;
using Rollgeon.Combat.Initiative;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rollgeon.Combat.Handoff.Tests
{
    /// <summary>
    /// Footprint multi-celda (Fase A) en el spawn: el ancla autorada se respeta si cabe, se
    /// corre si no, y si nada cabe el enemigo entra 1×1 para que el combate arranque igual.
    /// Usa el <see cref="GridManager"/> real: los fakes tratan todo como 1×1.
    /// </summary>
    [TestFixture]
    public class DefaultEnemySpawnResolverFootprintTests
    {
        static readonly Vector2Int TwoByTwo = new Vector2Int(2, 2);

        private InMemoryEntityRegistry _registry;
        private AttributesManager _attributes;
        private GridManager _grid;
        private DefaultEnemySpawnResolver _resolver;
        private readonly List<UnityEngine.Object> _created = new();

        [SetUp]
        public void SetUp()
        {
            _registry = new InMemoryEntityRegistry();
            _attributes = new AttributesManager();
            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(8, 8));
            _resolver = new DefaultEnemySpawnResolver(_registry, _attributes, grid: _grid);
        }

        [TearDown]
        public void TearDown()
        {
            _attributes?.Dispose();
            foreach (var o in _created) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _created.Clear();
        }

        RoomInstance CreateInstance(EnemyPoolSO pool)
        {
            var room = ScriptableObject.CreateInstance<RoomSO>();
            room.RoomId = "test_room";
            room.Type = RoomType.Combat;
            room.EnemyPool = pool;
            _created.Add(room);
            return new RoomInstance { InstanceId = Guid.NewGuid(), Template = room, State = RoomState.Uncleared };
        }

        EnemyPoolSO CreatePool(params EnemyDataSO[] enemies)
        {
            var pool = ScriptableObject.CreateInstance<EnemyPoolSO>();
            pool.Entries = new List<WeightedEntry<EnemyDataSO>>();
            foreach (var e in enemies) pool.Entries.Add(new WeightedEntry<EnemyDataSO>(e, 1f));
            _created.Add(pool);
            return pool;
        }

        EnemyDataSO CreateEnemy(string name, Vector2Int footprint)
        {
            var e = ScriptableObject.CreateInstance<EnemyDataSO>();
            e.name = name;
            e.EntityId = "enemy." + name.ToLower();
            e.BaseHP = 20;
            e.BaseSpeed = 4;
            e.MaxEnergy = 3;
            e.Footprint = footprint;
            _created.Add(e);
            return e;
        }

        static EnemySpawnState SavedState(Guid guid, GridCoord cell, string entityId) => new EnemySpawnState
        {
            EnemyDataSOId = entityId,
            CurrentHP = 7,
            IsDead = false,
            SpawnPointIndex = 0,
            Tier = 1,
            HasLastCell = true,
            LastCell = cell,
            Guid = guid.ToString(),
        };

        static HashSet<GridCoord> Cells(params (int x, int y)[] coords)
            => new HashSet<GridCoord>(coords.Select(c => new GridCoord(c.x, c.y)));

        (Guid id, GridCoord anchor) ResumeAt(EnemyDataSO enemy, GridCoord cell)
        {
            var instance = CreateInstance(CreatePool(enemy));
            var saved = Guid.NewGuid();
            instance.ObjectStates.Set("enemy_0", SavedState(saved, cell, enemy.EntityId));
            _resolver.ResumeFromSaveNextSpawn = true;

            var result = _resolver.Resolve(instance, new System.Random(42));

            Assert.AreEqual(1, result.Count);
            Assert.IsTrue(_grid.TryGetPosition(result[0].id, out var anchor));
            return (result[0].id, anchor);
        }

        [Test]
        public void Spawn_2x2_RegistersFourCells_AtAuthoredAnchor()
        {
            var (id, anchor) = ResumeAt(CreateEnemy("Big", TwoByTwo), new GridCoord(5, 5));

            Assert.AreEqual(new GridCoord(5, 5), anchor);
            Assert.AreEqual(TwoByTwo, _grid.GetFootprint(id));
            CollectionAssert.AreEquivalent(Cells((5, 5), (6, 5), (5, 6), (6, 6)), _grid.OccupiedCells(id).ToList());
        }

        [Test]
        public void Spawn_1x1_CoordUnchanged_NoFootprint()
        {
            var (id, anchor) = ResumeAt(CreateEnemy("Small", Vector2Int.one), new GridCoord(5, 7));

            Assert.AreEqual(new GridCoord(5, 7), anchor);
            Assert.AreEqual(Vector2Int.one, _grid.GetFootprint(id));
            Assert.AreEqual(1, _grid.OccupiedCells(id).Count());
        }

        [Test]
        public void Spawn_2x2_AnchorBlocked_ShiftsWithinRadius_LogsWarning()
        {
            // El bloqueador tapa (5,5): el primer ancla del anillo 1 (por Manhattan, X, Y) que cabe es (4,4).
            _grid.Register(Guid.NewGuid(), new GridCoord(5, 5));
            LogAssert.Expect(LogType.Warning, new Regex("corrido a"));

            var (id, anchor) = ResumeAt(CreateEnemy("Big", TwoByTwo), new GridCoord(5, 5));

            Assert.AreNotEqual(new GridCoord(5, 5), anchor);
            Assert.LessOrEqual(Math.Max(Math.Abs(anchor.X - 5), Math.Abs(anchor.Y - 5)), DefaultEnemySpawnResolver.FootprintShiftRadius);
            Assert.AreEqual(TwoByTwo, _grid.GetFootprint(id));
            Assert.AreEqual(4, _grid.OccupiedCells(id).Count());
            Assert.IsFalse(_grid.OccupiedCells(id).Contains(new GridCoord(5, 5)), "no pisa al bloqueador");
        }

        [Test]
        public void Spawn_2x2_NoFit_FallsBackTo1x1_LogsError()
        {
            _grid.LoadRoom(NavGraph.Rect(1, 1));
            LogAssert.Expect(LogType.Error, new Regex("se registra 1×1"));

            var (id, anchor) = ResumeAt(CreateEnemy("Big", TwoByTwo), new GridCoord(0, 0));

            Assert.AreEqual(new GridCoord(0, 0), anchor);
            Assert.AreEqual(Vector2Int.one, _grid.GetFootprint(id), "entra 1×1 para que el combate arranque");
        }

        [Test]
        public void Reentry_Random_2x2_OnlyAnchorsWhereRectFits()
        {
            // 3×3: un 2×2 solo cabe anclado en (0..1, 0..1).
            _grid.LoadRoom(NavGraph.Rect(3, 3));
            var enemy = CreateEnemy("Big", TwoByTwo);
            var instance = CreateInstance(CreatePool(enemy));
            instance.ObjectStates.Set("enemy_0", SavedState(Guid.NewGuid(), new GridCoord(2, 2), enemy.EntityId));
            _resolver.ResumeFromSaveNextSpawn = false; // re-entry normal: posición random

            var result = _resolver.Resolve(instance, new System.Random(7));

            Assert.AreEqual(1, result.Count);
            Assert.IsTrue(_grid.TryGetPosition(result[0].id, out var anchor));
            Assert.LessOrEqual(anchor.X, 1);
            Assert.LessOrEqual(anchor.Y, 1);
            Assert.AreEqual(4, _grid.OccupiedCells(result[0].id).Count());
        }

        [Test]
        public void Spawn_TwoFromPool_2x2_DoNotOverlap()
        {
            // Sin layout el ancla autorada es (3, índice): el segundo 2×2 choca con el primero y se corre.
            var enemy = CreateEnemy("Big", TwoByTwo);
            var instance = CreateInstance(CreatePool(enemy));
            LogAssert.Expect(LogType.Warning, new Regex("corrido a"));

            var result = _resolver.Resolve(instance, new System.Random(42));

            Assert.AreEqual(2, result.Count);
            var all = result.SelectMany(r => _grid.OccupiedCells(r.id)).ToList();
            Assert.AreEqual(8, all.Count);
            Assert.AreEqual(8, all.Distinct().Count(), "ninguna celda compartida");
        }
    }
}
