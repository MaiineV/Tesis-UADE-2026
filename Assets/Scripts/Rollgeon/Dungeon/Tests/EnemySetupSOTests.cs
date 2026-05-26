using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Entities;
using UnityEngine;

namespace Rollgeon.Dungeon.Tests
{
    [TestFixture]
    public class EnemySetupSOTests
    {
        private readonly List<Object> _created = new();

        private EnemyDataSO MakeEnemy(string name)
        {
            var e = ScriptableObject.CreateInstance<EnemyDataSO>();
            e.name = name;
            _created.Add(e);
            return e;
        }

        private EnemySetupSO MakeSetup(params (int idx, EnemyDataSO enemy)[] slots)
        {
            var s = ScriptableObject.CreateInstance<EnemySetupSO>();
            s.Slots = new List<SetupSlot>();
            foreach (var (idx, enemy) in slots)
                s.Slots.Add(new SetupSlot { SpawnPointIndex = idx, Enemy = enemy });
            _created.Add(s);
            return s;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created)
                if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        [Test]
        public void TryResolve_AllSlotsValid_ReturnsTrueWithMapping()
        {
            var a = MakeEnemy("goblin");
            var b = MakeEnemy("archer");
            var setup = MakeSetup((0, a), (2, b));

            Assert.IsTrue(setup.TryResolve(spawnPointCount: 3, out var mapping));
            Assert.AreEqual(2, mapping.Count);
            Assert.AreEqual(0, mapping[0].index);
            Assert.AreSame(a, mapping[0].data);
            Assert.AreEqual(2, mapping[1].index);
            Assert.AreSame(b, mapping[1].data);
        }

        [Test]
        public void TryResolve_IndexOutOfRange_ReturnsFalse()
        {
            var a = MakeEnemy("goblin");
            var setup = MakeSetup((0, a), (5, a));

            Assert.IsFalse(setup.TryResolve(spawnPointCount: 3, out _));
        }

        [Test]
        public void TryResolve_NegativeIndex_ReturnsFalse()
        {
            var a = MakeEnemy("goblin");
            var setup = MakeSetup((-1, a));

            Assert.IsFalse(setup.TryResolve(spawnPointCount: 3, out _));
        }

        [Test]
        public void TryResolve_NullEnemy_ReturnsFalse()
        {
            var setup = MakeSetup((0, null));

            Assert.IsFalse(setup.TryResolve(spawnPointCount: 3, out _));
        }

        [Test]
        public void TryResolve_ZeroSpawnPoints_ReturnsFalse()
        {
            var a = MakeEnemy("goblin");
            var setup = MakeSetup((0, a));

            Assert.IsFalse(setup.TryResolve(spawnPointCount: 0, out _));
        }

        [Test]
        public void TryResolve_EmptySlots_ReturnsTrueWithEmptyMapping()
        {
            var setup = MakeSetup();

            Assert.IsTrue(setup.TryResolve(spawnPointCount: 3, out var mapping));
            Assert.AreEqual(0, mapping.Count);
        }
    }
}