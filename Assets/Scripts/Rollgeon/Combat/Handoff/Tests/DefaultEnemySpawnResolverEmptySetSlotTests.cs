using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Rollgeon.Attributes;
using Rollgeon.Combat.Initiative;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using Rollgeon.Dungeon.State;
using Rollgeon.Entities;
using Rollgeon.Grid;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rollgeon.Combat.Handoff.Tests
{
    /// <summary>
    /// Un spawn point sin enemigo en el set elegido no spawnea nada: los demás
    /// enemigos del set se quedan en SU spawn point (el plan está alineado con
    /// <see cref="RoomLayout.EnemySpawnPoints"/>) y el hueco NO se rellena desde
    /// el <see cref="RoomSO.EnemyPool"/>. Así un prefab autorea formaciones distintas
    /// por set. Un set totalmente vacío es data rota: cae al pool con warning.
    /// </summary>
    [TestFixture]
    public class DefaultEnemySpawnResolverEmptySetSlotTests
    {
        private readonly List<UnityEngine.Object> _created = new();
        private InMemoryEntityRegistry _registry;
        private AttributesManager _attributes;
        private GridManager _grid;
        private RoomLayout _layout;
        private readonly GridCoord[] _cells = { new GridCoord(2, 2), new GridCoord(5, 5), new GridCoord(8, 8) };

        [SetUp]
        public void SetUp()
        {
            _registry = new InMemoryEntityRegistry();
            _attributes = new AttributesManager();
            _grid = new GridManager();

            var go = new GameObject("TestRoom");
            _created.Add(go);
            _layout = go.AddComponent<RoomLayout>();
            _layout.TileSize = 1f;
            _layout.NavGraph = NavGraph.Rect(11, 11);
            _grid.LoadRoom(_layout.NavGraph, _layout.GetOrigin(), _layout.TileSize);

            _layout.EnemySpawnPoints = new List<Transform>();
            for (int i = 0; i < _cells.Length; i++)
                _layout.EnemySpawnPoints.Add(Point($"EnemySpawn{i}", _cells[i]));
        }

        [TearDown]
        public void TearDown()
        {
            _attributes?.Dispose();
            foreach (var obj in _created)
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            _created.Clear();
        }

        private Transform Point(string name, GridCoord coord)
        {
            var go = new GameObject(name);
            _created.Add(go);
            go.transform.SetParent(_layout.transform);
            go.transform.position = _grid.GridToWorld(coord);
            go.AddComponent<SpawnPointConfig>();
            return go.transform;
        }

        /// <summary>Un único set (índice 0) con la formación dada; <c>null</c> = spawn point vacío.</summary>
        private void AuthorSingleSet(params EnemyDataSO[] perSpawnPoint)
        {
            for (int i = 0; i < perSpawnPoint.Length; i++)
            {
                var config = _layout.EnemySpawnPoints[i].GetComponent<SpawnPointConfig>();
                config.EnemySets = new List<EnemyDataSO> { perSpawnPoint[i] };
            }
        }

        private EnemyDataSO CreateEnemy(string name)
        {
            var enemy = ScriptableObject.CreateInstance<EnemyDataSO>();
            enemy.name = name;
            enemy.EntityId = $"enemy.{name.ToLower()}";
            enemy.BaseHP = 20;
            enemy.BaseSpeed = 4;
            enemy.MaxEnergy = 3;
            _created.Add(enemy);
            return enemy;
        }

        private EnemyPoolSO CreatePool(params EnemyDataSO[] enemies)
        {
            var pool = ScriptableObject.CreateInstance<EnemyPoolSO>();
            pool.Entries = new List<WeightedEntry<EnemyDataSO>>();
            foreach (var enemy in enemies) pool.Entries.Add(new WeightedEntry<EnemyDataSO>(enemy, 1f));
            _created.Add(pool);
            return pool;
        }

        private RoomInstance CreateRoom(EnemyPoolSO pool)
        {
            var room = ScriptableObject.CreateInstance<RoomSO>();
            room.RoomId = "test_room";
            room.DisplayName = "Test Room";
            room.Type = RoomType.Combat;
            room.EnemyPool = pool;
            _created.Add(room);

            return new RoomInstance
            {
                InstanceId = Guid.NewGuid(),
                Template = room,
                State = RoomState.Uncleared,
                SpawnedPrefab = _layout.gameObject,
            };
        }

        private DefaultEnemySpawnResolver Resolver() =>
            new DefaultEnemySpawnResolver(_registry, _attributes, grid: _grid);

        private GridCoord PositionOf(Guid id)
        {
            Assert.IsTrue(_grid.TryGetPosition(id, out var coord), "el enemigo debe estar registrado en la grilla");
            return coord;
        }

        [Test]
        public void EmptySlot_SpawnsNothingThere_AndKeepsTheOthersOnTheirOwnSpawnPoints()
        {
            var goblin = CreateEnemy("Goblin");
            var slime = CreateEnemy("Slime");
            AuthorSingleSet(goblin, null, slime);
            var instance = CreateRoom(CreatePool(CreateEnemy("Filler")));

            var result = Resolver().Resolve(instance, new System.Random(7));

            Assert.AreEqual(2, result.Count, "el spawn point vacío no aporta enemigo");
            var byData = result.ToDictionary(r => r.data, r => r.id);
            Assert.AreEqual(_cells[0], PositionOf(byData[goblin]), "Goblin queda en SU spawn point (índice 0)");
            Assert.AreEqual(_cells[2], PositionOf(byData[slime]),
                "Slime queda en SU spawn point (índice 2): el hueco no corre a los siguientes");
        }

        [Test]
        public void EmptySlot_SeedsStatesWithTheRealSpawnPointIndex_AndNoneForTheGap()
        {
            AuthorSingleSet(CreateEnemy("Goblin"), null, CreateEnemy("Slime"));
            var instance = CreateRoom(CreatePool(CreateEnemy("Filler")));

            Resolver().Resolve(instance, new System.Random(7));

            var states = instance.ObjectStates.Enumerate()
                .Select(kv => kv.Value).OfType<EnemySpawnState>()
                .OrderBy(s => s.SpawnPointIndex).ToList();
            CollectionAssert.AreEqual(new[] { 0, 2 }, states.Select(s => s.SpawnPointIndex).ToArray(),
                "un state por enemigo real, con el índice de su spawn point; ninguno para el hueco");
        }

        [Test]
        public void EmptySlot_DoesNotRollAFillerFromTheRoomPool()
        {
            var filler = CreateEnemy("Filler");
            AuthorSingleSet(CreateEnemy("Goblin"), null, null);
            var instance = CreateRoom(CreatePool(filler));

            var result = Resolver().Resolve(instance, new System.Random(7));

            Assert.AreEqual(1, result.Count);
            Assert.IsFalse(result.Any(r => r.data == filler),
                "un slot vacío es una formación deliberada, no un hueco a rellenar desde el pool");
        }

        [Test]
        public void FullyEmptySet_FallsBackToTheRoomPool_WithAWarning()
        {
            var filler = CreateEnemy("Filler");
            AuthorSingleSet(null, null, null);
            var instance = CreateRoom(CreatePool(filler));
            LogAssert.Expect(LogType.Warning, new Regex("fallback a PossibleSetups/EnemyPool"));

            var result = Resolver().Resolve(instance, new System.Random(7));

            Assert.AreEqual(2, result.Count, "combat default del pool: 2 enemigos");
            Assert.IsTrue(result.All(r => r.data == filler));
        }
    }
}
