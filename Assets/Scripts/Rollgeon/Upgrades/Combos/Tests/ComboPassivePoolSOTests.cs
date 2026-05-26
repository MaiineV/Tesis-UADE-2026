using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Random = System.Random;

namespace Rollgeon.Upgrades.Combos.Tests
{
    /// <summary>
    /// Cobertura del <see cref="ComboPassivePoolSO.Roll"/> — peso, floorDepth, exclude.
    /// </summary>
    [TestFixture]
    public class ComboPassivePoolSOTests
    {
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
            {
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            }
            _created.Clear();
        }

        private ComboPassiveSO MakePassive(string id)
        {
            var passive = ScriptableObject.CreateInstance<ComboPassiveSO>();
            passive.name = id;
            _created.Add(passive);
            typeof(UpgradeSO).GetField("_upgradeId", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(passive, id);
            return passive;
        }

        private ComboPassivePoolSO MakePool(params WeightedComboPassive[] entries)
        {
            var pool = ScriptableObject.CreateInstance<ComboPassivePoolSO>();
            pool.Entries = new List<WeightedComboPassive>(entries);
            _created.Add(pool);
            return pool;
        }

        [Test]
        public void Roll_EmptyPool_ReturnsNull()
        {
            var pool = MakePool();
            Assert.IsNull(pool.Roll(new Random(42), floorDepth: 0));
        }

        [Test]
        public void Roll_WeightZero_SkipsEntry()
        {
            var active = MakePassive("active");
            var disabled = MakePassive("disabled");
            var pool = MakePool(
                new WeightedComboPassive { Passive = active, Weight = 1f },
                new WeightedComboPassive { Passive = disabled, Weight = 0f }
            );
            var rng = new Random(42);

            for (int i = 0; i < 10; i++)
            {
                Assert.AreSame(active, pool.Roll(rng, floorDepth: 0));
            }
        }

        [Test]
        public void Roll_FloorDepthBelowMin_FiltersOut()
        {
            var early = MakePassive("early");
            var late = MakePassive("late");
            var pool = MakePool(
                new WeightedComboPassive { Passive = early, Weight = 1f, MinFloorDepth = 0 },
                new WeightedComboPassive { Passive = late,  Weight = 1f, MinFloorDepth = 3 }
            );
            var rng = new Random(42);

            for (int i = 0; i < 10; i++)
            {
                Assert.AreSame(early, pool.Roll(rng, floorDepth: 0));
            }
        }

        [Test]
        public void Roll_ExcludeBlocksEntry()
        {
            var a = MakePassive("a");
            var b = MakePassive("b");
            var pool = MakePool(
                new WeightedComboPassive { Passive = a, Weight = 1f },
                new WeightedComboPassive { Passive = b, Weight = 1f }
            );
            var rng = new Random(42);

            for (int i = 0; i < 20; i++)
            {
                var result = pool.Roll(rng, floorDepth: 0,
                    exclude: new HashSet<ComboPassiveSO> { a });
                Assert.AreSame(b, result);
            }
        }
    }
}
