using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Random = System.Random;

namespace Rollgeon.Upgrades.Character.Tests
{
    /// <summary>
    /// Cobertura del <see cref="CharacterRewardPoolSO.Roll"/> — el patrón "3 opciones
    /// distintas" se construye con N rolls + exclude acumulado por el caller.
    /// </summary>
    [TestFixture]
    public class CharacterRewardPoolSOTests
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

        private CharacterRewardSO MakeReward(string id, CharacterRewardTargetStat target = CharacterRewardTargetStat.Health)
        {
            var reward = ScriptableObject.CreateInstance<CharacterRewardSO>();
            reward.name = id;
            _created.Add(reward);
            typeof(UpgradeSO).GetField("_upgradeId", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(reward, id);
            typeof(CharacterRewardSO).GetField("_targetStat", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(reward, target);
            return reward;
        }

        private CharacterRewardPoolSO MakePool(params WeightedCharacterReward[] entries)
        {
            var pool = ScriptableObject.CreateInstance<CharacterRewardPoolSO>();
            pool.Entries = new List<WeightedCharacterReward>(entries);
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
        public void Roll_DistinctSequence_ExcludeBuildsUp()
        {
            // Simula el patrón del CharacterRewardService.InitializeOrHydrate:
            // 3 rolls sucesivos con exclude acumulando los resultados.
            var hp = MakeReward("reward.hp");
            var energy = MakeReward("reward.energy");
            var speed = MakeReward("reward.speed");
            var attack = MakeReward("reward.attack");
            var pool = MakePool(
                new WeightedCharacterReward { Reward = hp,     Weight = 1f },
                new WeightedCharacterReward { Reward = energy, Weight = 1f },
                new WeightedCharacterReward { Reward = speed,  Weight = 1f },
                new WeightedCharacterReward { Reward = attack, Weight = 1f }
            );
            var rng = new Random(42);
            var exclude = new HashSet<CharacterRewardSO>();

            var picks = new List<CharacterRewardSO>(3);
            for (int i = 0; i < 3; i++)
            {
                var pick = pool.Roll(rng, floorDepth: 0, exclude);
                Assert.IsNotNull(pick, $"roll {i + 1} no devolvió nada");
                picks.Add(pick);
                exclude.Add(pick);
            }

            // Los 3 picks son distintos.
            Assert.AreEqual(3, new HashSet<CharacterRewardSO>(picks).Count,
                "los 3 rolls con exclude acumulando deberían ser distintos");
        }

        [Test]
        public void Roll_FloorDepthBelowMin_FiltersOut()
        {
            var early = MakeReward("early");
            var late = MakeReward("late");
            var pool = MakePool(
                new WeightedCharacterReward { Reward = early, Weight = 1f, MinFloorDepth = 0 },
                new WeightedCharacterReward { Reward = late,  Weight = 1f, MinFloorDepth = 5 }
            );
            var rng = new Random(42);

            for (int i = 0; i < 10; i++)
            {
                Assert.AreSame(early, pool.Roll(rng, floorDepth: 0));
            }
        }
    }
}
