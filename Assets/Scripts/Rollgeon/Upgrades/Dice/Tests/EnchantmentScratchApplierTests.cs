using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;

namespace Rollgeon.Upgrades.Dice.Tests
{
    /// <summary>
    /// Regresión del bug confirmado en el plan: <c>BonusShield</c> se acumulaba pero el
    /// service solo aplicaba <c>BonusGold</c>, así que el escudo nunca llegaba al jugador.
    /// El applier ahora fusiona ambos campos legacy en los acumuladores genéricos.
    /// </summary>
    /// <remarks>
    /// Se usa <see cref="Guid.Empty"/> y se limpia el <c>ServiceLocator</c> a propósito:
    /// la fusión legacy→acumulador ocurre antes de tocar economía/atributos, así que el
    /// merge es observable sin levantar los sistemas reales (las escrituras hacen early-return).
    /// </remarks>
    [TestFixture]
    public class EnchantmentScratchApplierTests
    {
        [SetUp]
        public void SetUp() => ServiceLocator.Clear();

        [TearDown]
        public void TearDown() => ServiceLocator.Clear();

        [Test]
        public void Apply_MergesBonusShieldIntoShieldAccumulator()
        {
            var scratch = new EnchantmentScratch { BonusShield = 4 };

            EnchantmentScratchApplier.Apply(scratch, Guid.Empty);

            var shield = ResourceTarget.OfStat(StatType.Shield);
            Assert.IsTrue(scratch.Resources.TryGetValue(shield, out var acc),
                "BonusShield debe fusionarse en el acumulador de escudo (bug fix).");
            Assert.AreEqual(4, acc.Resolve(0));
        }

        [Test]
        public void Apply_ZeroesBonusShieldAfterMerge()
        {
            var scratch = new EnchantmentScratch { BonusShield = 4 };

            EnchantmentScratchApplier.Apply(scratch, Guid.Empty);

            Assert.AreEqual(0, scratch.BonusShield);
        }

        [Test]
        public void Apply_MergesBonusGoldIntoGoldAccumulator()
        {
            var scratch = new EnchantmentScratch { BonusGold = 7 };

            EnchantmentScratchApplier.Apply(scratch, Guid.Empty);

            Assert.IsTrue(scratch.Resources.TryGetValue(ResourceTarget.Gold, out var acc));
            Assert.AreEqual(7, acc.Resolve(0));
            Assert.AreEqual(0, scratch.BonusGold);
        }

        [Test]
        public void Apply_NullScratch_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => EnchantmentScratchApplier.Apply(null, Guid.Empty));
        }

        [Test]
        public void Apply_NoResources_DoesNotThrow()
        {
            var scratch = new EnchantmentScratch();

            Assert.DoesNotThrow(() => EnchantmentScratchApplier.Apply(scratch, Guid.Empty));
        }
    }
}
