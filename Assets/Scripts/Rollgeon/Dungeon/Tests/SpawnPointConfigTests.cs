using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Dungeon.Components;
using Rollgeon.Entities;
using UnityEngine;

namespace Rollgeon.Dungeon.Tests
{
    [TestFixture]
    public class SpawnPointConfigTests
    {
        private readonly List<Object> _created = new();

        private EnemyDataSO MakeEnemy(string name)
        {
            var e = ScriptableObject.CreateInstance<EnemyDataSO>();
            e.name = name;
            _created.Add(e);
            return e;
        }

        private SpawnPointConfig MakeConfig(params EnemyDataSO[] enemies)
        {
            var go = new GameObject("SpawnPoint");
            _created.Add(go);
            var config = go.AddComponent<SpawnPointConfig>();
            config.EnemySets = new List<EnemyDataSO>(enemies);
            return config;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created)
                if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        [Test]
        public void GetEnemyForSet_ValidIndex_ReturnsCorrectEnemy()
        {
            var goblin = MakeEnemy("Goblin");
            var orc = MakeEnemy("Orc");
            var config = MakeConfig(goblin, orc);

            Assert.AreSame(goblin, config.GetEnemyForSet(0));
            Assert.AreSame(orc, config.GetEnemyForSet(1));
        }

        [Test]
        public void GetEnemyForSet_IndexOutOfRange_ReturnsNull()
        {
            var config = MakeConfig(MakeEnemy("Goblin"));

            Assert.IsNull(config.GetEnemyForSet(5));
        }

        [Test]
        public void GetEnemyForSet_NegativeIndex_ReturnsNull()
        {
            var config = MakeConfig(MakeEnemy("Goblin"));

            Assert.IsNull(config.GetEnemyForSet(-1));
        }

        [Test]
        public void SetCount_EmptyList_ReturnsZero()
        {
            var config = MakeConfig();

            Assert.AreEqual(0, config.SetCount);
        }

        [Test]
        public void SetCount_NullList_ReturnsZero()
        {
            var go = new GameObject("SpawnPoint");
            _created.Add(go);
            var config = go.AddComponent<SpawnPointConfig>();
            config.EnemySets = null;

            Assert.AreEqual(0, config.SetCount);
        }
    }
}
