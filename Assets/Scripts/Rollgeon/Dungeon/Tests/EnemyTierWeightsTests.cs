using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Entities;
using UnityEngine;

namespace Rollgeon.Dungeon.Tests
{
    /// <summary>
    /// Tests de <see cref="EnemyTierWeights"/> / <see cref="EnemyTierRoll"/> (#158):
    /// default a Tier 1, distribución acorde a pesos, y clamp contra los tiers que el
    /// enemigo realmente define.
    /// </summary>
    [TestFixture]
    public class EnemyTierWeightsTests
    {
        [Test]
        public void RollTier_EmptyWeights_ReturnsTier1()
        {
            var w = new EnemyTierWeights();

            Assert.AreEqual(1, w.RollTier(new System.Random(1)));
        }

        [Test]
        public void RollTier_ZeroWeights_ReturnsTier1()
        {
            var w = new EnemyTierWeights
            {
                Weights = new List<TierWeight>
                {
                    new TierWeight { Tier = 2, Weight = 0f },
                    new TierWeight { Tier = 3, Weight = 0f },
                },
            };

            Assert.AreEqual(1, w.RollTier(new System.Random(1)));
        }

        [Test]
        public void RollTier_Distribution_MatchesWeightsWithinTolerance()
        {
            var w = new EnemyTierWeights
            {
                Weights = new List<TierWeight>
                {
                    new TierWeight { Tier = 1, Weight = 80f },
                    new TierWeight { Tier = 2, Weight = 20f },
                },
            };
            var rng = new System.Random(12345);

            const int n = 10000;
            int t1 = 0, t2 = 0;
            for (int i = 0; i < n; i++)
            {
                int t = w.RollTier(rng);
                if (t == 1) t1++;
                else if (t == 2) t2++;
            }

            Assert.AreEqual(0.80f, t1 / (float)n, 0.02f, "Tier 1 ≈ 80%");
            Assert.AreEqual(0.20f, t2 / (float)n, 0.02f, "Tier 2 ≈ 20%");
        }

        [Test]
        public void EnemyTierRoll_ClampsAgainstEnemyTierCount()
        {
            var enemy = ScriptableObject.CreateInstance<EnemyDataSO>();
            enemy.ExtraTiers.Add(new EnemyTier()); // TierCount = 2
            var w = new EnemyTierWeights
            {
                Weights = new List<TierWeight> { new TierWeight { Tier = 3, Weight = 100f } },
            };

            int tier = EnemyTierRoll.Roll(w, enemy, new System.Random(7));

            Assert.LessOrEqual(tier, 2, "El enemigo solo tiene 2 tiers — no debe spawnear T3.");
            Assert.GreaterOrEqual(tier, 1);
            Object.DestroyImmediate(enemy);
        }

        [Test]
        public void EnemyTierRoll_NullWeights_ReturnsTier1()
        {
            var enemy = ScriptableObject.CreateInstance<EnemyDataSO>();

            Assert.AreEqual(1, EnemyTierRoll.Roll(null, enemy, new System.Random(1)));
            Object.DestroyImmediate(enemy);
        }
    }
}
