using NUnit.Framework;
using Rollgeon.Combos.Counters;

namespace Rollgeon.Combos.Counters.Tests
{
    /// <summary>
    /// Tests de la fórmula <see cref="ComboCountersConfig.ComputeMultiplier"/> + cap enforcement
    /// + validación. Plan §9.1.
    /// </summary>
    [TestFixture]
    public class ComboCountersConfigTests
    {
        private const float Tolerance = 1e-5f;

        [Test]
        public void Defaults_MatchBriefValues()
        {
            var cfg = new ComboCountersConfig();
            Assert.AreEqual(0.02f, cfg.PerUseBonus, Tolerance);
            Assert.AreEqual(0.20f, cfg.MaxBonus, Tolerance);
        }

        [Test]
        public void ComputeMultiplier_CountZero_ReturnsOne()
        {
            var cfg = new ComboCountersConfig { PerUseBonus = 0.02f, MaxBonus = 0.20f };
            Assert.AreEqual(1f, cfg.ComputeMultiplier(0), Tolerance);
        }

        [Test]
        public void ComputeMultiplier_CountNegative_ReturnsOne()
        {
            var cfg = new ComboCountersConfig { PerUseBonus = 0.02f, MaxBonus = 0.20f };
            Assert.AreEqual(1f, cfg.ComputeMultiplier(-5), Tolerance);
        }

        [Test]
        public void ComputeMultiplier_UnderCap_LinearGrowth()
        {
            var cfg = new ComboCountersConfig { PerUseBonus = 0.02f, MaxBonus = 0.20f };

            // 1 use  -> 1.02, 5 uses -> 1.10, 10 uses -> 1.20 (exactly at cap).
            Assert.AreEqual(1.02f, cfg.ComputeMultiplier(1), Tolerance);
            Assert.AreEqual(1.10f, cfg.ComputeMultiplier(5), Tolerance);
            Assert.AreEqual(1.20f, cfg.ComputeMultiplier(10), Tolerance);
        }

        [Test]
        public void ComputeMultiplier_OverCap_ClampedToMaxBonus()
        {
            var cfg = new ComboCountersConfig { PerUseBonus = 0.02f, MaxBonus = 0.20f };

            // 11 uses raw bonus = 0.22 → capped to 0.20.
            Assert.AreEqual(1.20f, cfg.ComputeMultiplier(11), Tolerance);
            Assert.AreEqual(1.20f, cfg.ComputeMultiplier(100), Tolerance);
            Assert.AreEqual(1.20f, cfg.ComputeMultiplier(9999), Tolerance);
        }

        [Test]
        public void ComputeMultiplier_PerUseBonusZero_Always_One()
        {
            var cfg = new ComboCountersConfig { PerUseBonus = 0f, MaxBonus = 0.20f };

            Assert.AreEqual(1f, cfg.ComputeMultiplier(1), Tolerance);
            Assert.AreEqual(1f, cfg.ComputeMultiplier(100), Tolerance);
        }

        [Test]
        public void ComputeMultiplier_MaxBonusZero_Always_One()
        {
            var cfg = new ComboCountersConfig { PerUseBonus = 0.02f, MaxBonus = 0f };

            Assert.AreEqual(1f, cfg.ComputeMultiplier(1), Tolerance);
            Assert.AreEqual(1f, cfg.ComputeMultiplier(100), Tolerance);
        }

        [Test]
        public void ComputeMultiplier_CustomConfig_HigherCap()
        {
            var cfg = new ComboCountersConfig { PerUseBonus = 0.05f, MaxBonus = 0.50f };

            // 5 uses -> 1.25, 10 uses -> 1.50 (cap), 11+ also -> 1.50.
            Assert.AreEqual(1.25f, cfg.ComputeMultiplier(5), Tolerance);
            Assert.AreEqual(1.50f, cfg.ComputeMultiplier(10), Tolerance);
            Assert.AreEqual(1.50f, cfg.ComputeMultiplier(20), Tolerance);
        }

        [Test]
        public void Validate_ClampsNegatives()
        {
            var cfg = new ComboCountersConfig { PerUseBonus = -0.1f, MaxBonus = -0.5f };
            cfg.Validate();

            Assert.AreEqual(0f, cfg.PerUseBonus, Tolerance);
            Assert.AreEqual(0f, cfg.MaxBonus, Tolerance);
        }

        [Test]
        public void Validate_LeavesValidValuesUnchanged()
        {
            var cfg = new ComboCountersConfig { PerUseBonus = 0.03f, MaxBonus = 0.30f };
            cfg.Validate();

            Assert.AreEqual(0.03f, cfg.PerUseBonus, Tolerance);
            Assert.AreEqual(0.30f, cfg.MaxBonus, Tolerance);
        }
    }
}
