using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rollgeon.Entities;
using UnityEngine;

namespace Rollgeon.Dungeon.Tests
{
    [TestFixture]
    public class EnemyPoolTests
    {
        private EnemyPoolSO _pool;
        private readonly List<Object> _createdObjects = new();

        private EnemyDataSO CreateEnemy(string name)
        {
            var enemy = ScriptableObject.CreateInstance<EnemyDataSO>();
            enemy.name = name;
            _createdObjects.Add(enemy);
            return enemy;
        }

        [SetUp]
        public void SetUp()
        {
            _pool = ScriptableObject.CreateInstance<EnemyPoolSO>();
            _createdObjects.Add(_pool);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _createdObjects)
            {
                if (obj != null)
                    Object.DestroyImmediate(obj);
            }
            _createdObjects.Clear();
        }

        [Test]
        public void RollForSpawns_EmptyEntries_ReturnsEmptyList()
        {
            _pool.Entries = new List<WeightedEntry<EnemyDataSO>>();
            var result = _pool.RollForSpawns(3, new System.Random(42));
            Assert.IsEmpty(result);
        }

        [Test]
        public void RollForSpawns_ZeroCount_ReturnsEmptyList()
        {
            var enemy = CreateEnemy("Goblin");
            _pool.Entries = new List<WeightedEntry<EnemyDataSO>>
            {
                new(enemy, 1f)
            };
            var result = _pool.RollForSpawns(0, new System.Random(42));
            Assert.IsEmpty(result);
        }

        [Test]
        public void RollForSpawns_SingleEntry_ReturnsOnlyThatEnemy()
        {
            var enemy = CreateEnemy("Goblin");
            _pool.Entries = new List<WeightedEntry<EnemyDataSO>>
            {
                new(enemy, 1f)
            };
            var result = _pool.RollForSpawns(5, new System.Random(42));
            Assert.AreEqual(5, result.Count);
            Assert.IsTrue(result.All(e => e == enemy));
        }

        [Test]
        public void RollForSpawns_CountN_ReturnsNEnemies()
        {
            var enemyA = CreateEnemy("A");
            var enemyB = CreateEnemy("B");
            _pool.Entries = new List<WeightedEntry<EnemyDataSO>>
            {
                new(enemyA, 1f),
                new(enemyB, 1f)
            };
            var result = _pool.RollForSpawns(3, new System.Random(42));
            Assert.AreEqual(3, result.Count);
        }

        [Test]
        public void RollForSpawns_WeightDistribution_HighWeightDominates()
        {
            var heavy = CreateEnemy("Heavy");
            var light = CreateEnemy("Light");
            _pool.Entries = new List<WeightedEntry<EnemyDataSO>>
            {
                new(heavy, 90f),
                new(light, 10f)
            };

            var result = _pool.RollForSpawns(1000, new System.Random(42));
            int heavyCount = result.Count(e => e == heavy);

            Assert.Greater(heavyCount, 700,
                $"Expected heavy ({heavyCount}/1000) to appear > 70% of the time");
        }

        [Test]
        public void RollForSpawns_ZeroWeightEntry_NeverSelected()
        {
            var valid = CreateEnemy("Valid");
            var zero = CreateEnemy("Zero");
            _pool.Entries = new List<WeightedEntry<EnemyDataSO>>
            {
                new(valid, 1f),
                new(zero, 0f)
            };

            var result = _pool.RollForSpawns(100, new System.Random(42));
            Assert.IsFalse(result.Any(e => e == zero),
                "Zero-weight entry should never be selected");
        }

        [Test]
        public void RollForSpawns_DeterministicSeed_SameResults()
        {
            var enemyA = CreateEnemy("A");
            var enemyB = CreateEnemy("B");
            _pool.Entries = new List<WeightedEntry<EnemyDataSO>>
            {
                new(enemyA, 50f),
                new(enemyB, 50f)
            };

            var result1 = _pool.RollForSpawns(10, new System.Random(42));
            var result2 = _pool.RollForSpawns(10, new System.Random(42));

            CollectionAssert.AreEqual(result1, result2,
                "Same seed must produce identical results");
        }
    }
}
