using NUnit.Framework;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Entities.Behaviors;
using UnityEngine;

namespace Rollgeon.Entities.Tests
{
    /// <summary>
    /// Tests del sistema de tiers de <see cref="EnemyDataSO"/> (#158): Tier 1 = base,
    /// modo Multiplicador (redondea) y Manual (exacto), mezcla por-stat, clamp, y la
    /// salvaguarda del multiplicador 0. Backward-compat: sin tiers ⇒ idéntico al base.
    /// </summary>
    [TestFixture]
    public class EnemyTierTests
    {
        private EnemyDataSO _so;

        [SetUp]
        public void SetUp()
        {
            _so = ScriptableObject.CreateInstance<EnemyDataSO>();
            _so.BaseHP = 20;
            _so.BaseAttack = 10;
            _so.BaseSpeed = 4;
            _so.MaxEnergy = 3;
            _so.BaseHealStrength = 5;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_so);

        [Test]
        public void TierCount_NoExtraTiers_IsOne()
        {
            Assert.AreEqual(1, _so.TierCount);
        }

        [Test]
        public void CreateRuntimeStats_Tier1_EqualsBaseStats()
        {
            var attrs = _so.CreateRuntimeStats(1);

            Assert.AreEqual(20, attrs.GetAttributeValue<Health, int>());
            Assert.AreEqual(10, attrs.GetAttributeValue<Attack, int>());
            Assert.AreEqual(4, attrs.GetAttributeValue<Speed, int>());
            Assert.AreEqual(3, attrs.GetAttributeValue<Energy, int>());
            Assert.AreEqual(5, attrs.GetAttributeValue<HealStrength, int>());
        }

        [Test]
        public void Multiplier_RoundsBaseValue()
        {
            _so.ExtraTiers.Add(new EnemyTier
            {
                HP = new TierStat { Mode = StatMode.Multiplier, Multiplier = 1.3f },
                Attack = new TierStat { Mode = StatMode.Multiplier, Multiplier = 1.2f },
                Speed = new TierStat { Mode = StatMode.Multiplier, Multiplier = 1.1f },
            });

            var attrs = _so.CreateRuntimeStats(2);

            Assert.AreEqual(26, attrs.GetAttributeValue<Health, int>(), "20 * 1.3 = 26");
            Assert.AreEqual(12, attrs.GetAttributeValue<Attack, int>(), "10 * 1.2 = 12");
            Assert.AreEqual(4, attrs.GetAttributeValue<Speed, int>(), "round(4 * 1.1 = 4.4) = 4");
        }

        [Test]
        public void Manual_ReturnsExactValue()
        {
            _so.ExtraTiers.Add(new EnemyTier
            {
                HP = new TierStat { Mode = StatMode.Manual, ManualValue = 50 },
            });

            Assert.AreEqual(50, _so.CreateRuntimeStats(2).GetAttributeValue<Health, int>());
            Assert.AreEqual(50, _so.ResolveMaxHP(2));
        }

        [Test]
        public void MixedModes_PerStat_ResolveIndependently()
        {
            _so.ExtraTiers.Add(new EnemyTier
            {
                HP = new TierStat { Mode = StatMode.Manual, ManualValue = 50 },
                Speed = new TierStat { Mode = StatMode.Multiplier, Multiplier = 2f },
                Attack = new TierStat { Mode = StatMode.Multiplier, Multiplier = 1.5f },
            });

            var attrs = _so.CreateRuntimeStats(2);

            Assert.AreEqual(50, attrs.GetAttributeValue<Health, int>(), "HP manual = 50");
            Assert.AreEqual(8, attrs.GetAttributeValue<Speed, int>(), "Speed 4 * 2 = 8");
            Assert.AreEqual(15, attrs.GetAttributeValue<Attack, int>(), "Attack 10 * 1.5 = 15");
        }

        [Test]
        public void UnsetStat_InTier_StaysAtBase()
        {
            // Solo HP definido en T2; el resto queda en TierStat.Base (×1).
            _so.ExtraTiers.Add(new EnemyTier
            {
                HP = new TierStat { Mode = StatMode.Multiplier, Multiplier = 2f },
            });

            var attrs = _so.CreateRuntimeStats(2);

            Assert.AreEqual(40, attrs.GetAttributeValue<Health, int>(), "20 * 2 = 40");
            Assert.AreEqual(10, attrs.GetAttributeValue<Attack, int>(), "Attack queda en base");
            Assert.AreEqual(4, attrs.GetAttributeValue<Speed, int>(), "Speed queda en base");
        }

        [Test]
        public void ZeroMultiplier_Safeguard_ReturnsBase()
        {
            // default(TierStat) tiene Multiplier = 0 — la salvaguarda evita producir HP = 0.
            _so.ExtraTiers.Add(new EnemyTier { HP = default });

            Assert.AreEqual(20, _so.ResolveMaxHP(2));
        }

        [Test]
        public void ClampTier_OutOfRange_ClampsToAvailable()
        {
            _so.ExtraTiers.Add(new EnemyTier()); // TierCount = 2

            Assert.AreEqual(2, _so.ClampTier(5));
            Assert.AreEqual(1, _so.ClampTier(0));
            Assert.AreEqual(1, _so.ClampTier(-3));
        }

        [Test]
        public void ResolveMaxHP_TierOutOfRange_FallsBackToBase()
        {
            Assert.AreEqual(20, _so.ResolveMaxHP(5), "sin extra tiers, cualquier tier ⇒ base");
        }
    }
}
