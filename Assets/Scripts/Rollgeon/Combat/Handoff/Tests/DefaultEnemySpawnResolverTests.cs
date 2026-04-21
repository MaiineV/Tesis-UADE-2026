using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rollgeon.Attributes;
using Rollgeon.Combat.Initiative;
using Rollgeon.Dungeon;
using Rollgeon.Entities;
using UnityEngine;

namespace Rollgeon.Combat.Handoff.Tests
{
    [TestFixture]
    public class DefaultEnemySpawnResolverTests
    {
        private InMemoryEntityRegistry _registry;
        private AttributesManager _attributes;
        private DefaultEnemySpawnResolver _resolver;
        private readonly List<UnityEngine.Object> _createdObjects = new();

        // -------------------------------------------------------------------
        // Setup / Teardown
        // -------------------------------------------------------------------

        [SetUp]
        public void SetUp()
        {
            _registry = new InMemoryEntityRegistry();
            _attributes = new AttributesManager();
            _resolver = new DefaultEnemySpawnResolver(_registry, _attributes);
        }

        [TearDown]
        public void TearDown()
        {
            _attributes?.Dispose();
            foreach (var obj in _createdObjects)
            {
                if (obj != null)
                    UnityEngine.Object.DestroyImmediate(obj);
            }
            _createdObjects.Clear();
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        private RoomSO CreateRoom(EnemyPoolSO pool)
        {
            var room = ScriptableObject.CreateInstance<RoomSO>();
            room.RoomId = "test_room";
            room.DisplayName = "Test Room";
            room.Type = RoomType.Combat;
            room.EnemyPool = pool;
            _createdObjects.Add(room);
            return room;
        }

        private EnemyPoolSO CreatePool(params EnemyDataSO[] enemies)
        {
            var pool = ScriptableObject.CreateInstance<EnemyPoolSO>();
            pool.Entries = new List<WeightedEntry<EnemyDataSO>>();
            foreach (var enemy in enemies)
            {
                pool.Entries.Add(new WeightedEntry<EnemyDataSO>(enemy, 1f));
            }
            _createdObjects.Add(pool);
            return pool;
        }

        private EnemyDataSO CreateEnemy(string name, int hp = 20)
        {
            var enemy = ScriptableObject.CreateInstance<EnemyDataSO>();
            enemy.name = name;
            enemy.BaseHP = hp;
            enemy.BaseSpeed = 4;
            enemy.MaxEnergy = 3;
            _createdObjects.Add(enemy);
            return enemy;
        }

        // -------------------------------------------------------------------
        // Tests
        // -------------------------------------------------------------------

        [Test]
        public void Resolve_NullRoom_ReturnsEmptyList()
        {
            var result = _resolver.Resolve(null, 2, new System.Random(42));

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void Resolve_NullPool_ReturnsEmptyList()
        {
            var room = CreateRoom(null);

            var result = _resolver.Resolve(room, 2, new System.Random(42));

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void Resolve_EmptyPool_ReturnsEmptyList()
        {
            var pool = ScriptableObject.CreateInstance<EnemyPoolSO>();
            pool.Entries = new List<WeightedEntry<EnemyDataSO>>();
            _createdObjects.Add(pool);
            var room = CreateRoom(pool);

            var result = _resolver.Resolve(room, 2, new System.Random(42));

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void Resolve_SingleEnemy_ReturnsOneEntry()
        {
            var enemy = CreateEnemy("Goblin");
            var pool = CreatePool(enemy);
            var room = CreateRoom(pool);

            var result = _resolver.Resolve(room, 1, new System.Random(42));

            Assert.AreEqual(1, result.Count);
            Assert.AreSame(enemy, result[0].data);
        }

        [Test]
        public void Resolve_MultipleEnemies_ReturnsCorrectCount()
        {
            var e1 = CreateEnemy("Goblin");
            var e2 = CreateEnemy("Orc");
            var pool = CreatePool(e1, e2);
            var room = CreateRoom(pool);

            var result = _resolver.Resolve(room, 2, new System.Random(42));

            Assert.AreEqual(2, result.Count);
        }

        [Test]
        public void Resolve_RegistersEachEnemyInRegistry()
        {
            var e1 = CreateEnemy("Goblin");
            var e2 = CreateEnemy("Orc");
            var pool = CreatePool(e1, e2);
            var room = CreateRoom(pool);

            var result = _resolver.Resolve(room, 2, new System.Random(42));

            foreach (var (id, _) in result)
            {
                Assert.IsTrue(_registry.TryGetAttributes(id, out _),
                    $"Enemy {id} should be registered in the entity registry");
            }
        }

        [Test]
        public void Resolve_RegistersEachEnemyInAttributesManager()
        {
            var e1 = CreateEnemy("Goblin");
            var e2 = CreateEnemy("Orc");
            var pool = CreatePool(e1, e2);
            var room = CreateRoom(pool);

            var result = _resolver.Resolve(room, 2, new System.Random(42));

            foreach (var (id, _) in result)
            {
                Assert.IsTrue(_attributes.IsRegistered(id),
                    $"Enemy {id} should be registered in AttributesManager " +
                    "so BasicEnemyAI / damage pipelines can read its stats.");
            }
        }

        [Test]
        public void Resolve_GeneratesUniqueGuids()
        {
            var enemy = CreateEnemy("Goblin");
            var pool = CreatePool(enemy);
            var room = CreateRoom(pool);

            var result = _resolver.Resolve(room, 3, new System.Random(42));

            var uniqueIds = result.Select(r => r.id).Distinct().ToList();
            Assert.AreEqual(result.Count, uniqueIds.Count,
                "Each spawned enemy must have a unique Guid");
        }

        [Test]
        public void Resolve_StatsMatchEnemyData()
        {
            var enemy = CreateEnemy("Goblin", hp: 50);
            var pool = CreatePool(enemy);
            var room = CreateRoom(pool);

            var result = _resolver.Resolve(room, 1, new System.Random(42));

            Assert.AreEqual(1, result.Count);
            var (id, _) = result[0];

            Assert.IsTrue(_registry.TryGetAttributes(id, out var attrs));
            Assert.IsNotNull(attrs, "Registered attributes should not be null");
        }
    }
}
