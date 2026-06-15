using NUnit.Framework;
using Rollgeon.Attributes;

namespace Rollgeon.Upgrades.Dice.Tests
{
    [TestFixture]
    public class EnchantmentScratchTests
    {
        [Test]
        public void Modify_CreatesAccumulatorForNewTarget()
        {
            var scratch = new EnchantmentScratch();

            scratch.Modify(ResourceTarget.Gold, ResourceOperation.Add, 5);

            Assert.IsTrue(scratch.Resources.TryGetValue(ResourceTarget.Gold, out var acc));
            Assert.AreEqual(5, acc.Resolve(0));
        }

        [Test]
        public void Modify_AccumulatesOnSameTarget()
        {
            var scratch = new EnchantmentScratch();

            scratch.Modify(ResourceTarget.Gold, ResourceOperation.Add, 5);
            scratch.Modify(ResourceTarget.Gold, ResourceOperation.Add, 3);

            scratch.Resources.TryGetValue(ResourceTarget.Gold, out var acc);
            Assert.AreEqual(8, acc.Resolve(0));
        }

        [Test]
        public void Modify_KeepsTargetsSeparate()
        {
            var scratch = new EnchantmentScratch();
            var shield = ResourceTarget.OfStat(StatType.Shield);

            scratch.Modify(ResourceTarget.Gold, ResourceOperation.Add, 5);
            scratch.Modify(shield, ResourceOperation.Add, 2);

            Assert.AreEqual(2, scratch.Resources.Count);
            scratch.Resources.TryGetValue(ResourceTarget.Gold, out var goldAcc);
            scratch.Resources.TryGetValue(shield, out var shieldAcc);
            Assert.AreEqual(5, goldAcc.Resolve(0));
            Assert.AreEqual(2, shieldAcc.Resolve(0));
        }

        [Test]
        public void Modify_DistinguishesStatsByType()
        {
            var scratch = new EnchantmentScratch();

            scratch.Modify(ResourceTarget.OfStat(StatType.Shield), ResourceOperation.Add, 2);
            scratch.Modify(ResourceTarget.OfStat(StatType.Health), ResourceOperation.Add, 7);

            Assert.AreEqual(2, scratch.Resources.Count);
        }

        [Test]
        public void Reset_ClearsResourcesAndLegacyFields()
        {
            var scratch = new EnchantmentScratch();
            scratch.Modify(ResourceTarget.Gold, ResourceOperation.Add, 5);
            scratch.BonusGold = 10;
            scratch.BonusShield = 4;

            scratch.Reset();

            Assert.AreEqual(0, scratch.Resources.Count);
            Assert.AreEqual(0, scratch.BonusGold);
            Assert.AreEqual(0, scratch.BonusShield);
        }
    }
}
