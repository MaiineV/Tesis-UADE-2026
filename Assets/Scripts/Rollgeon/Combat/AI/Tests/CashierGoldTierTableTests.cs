using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Combat.Cashier;

namespace Rollgeon.Combat.AI.Tests
{
    [TestFixture]
    public class CashierGoldTierTableTests
    {
        /// <summary>Tabla de la ficha: &lt;100 ⇒ Size 1 / 14, 100-249 ⇒ Size 3 / 28, ≥250 ⇒ Size 3 / 35.</summary>
        private static List<CashierGoldTier> FichaTiers() => new List<CashierGoldTier>
        {
            new CashierGoldTier { MinGold = 0,   ColumnSize = 1, Damage = 14 },
            new CashierGoldTier { MinGold = 100, ColumnSize = 3, Damage = 28 },
            new CashierGoldTier { MinGold = 250, ColumnSize = 3, Damage = 35 },
        };

        [TestCase(0,   1, 14, 0)]
        [TestCase(1,   1, 14, 0)]
        [TestCase(99,  1, 14, 0)]   // borde: un oro menos que el umbral sigue siendo el escalón pobre.
        [TestCase(100, 3, 28, 1)]   // borde: el umbral es inclusive.
        [TestCase(101, 3, 28, 1)]
        [TestCase(249, 3, 28, 1)]   // borde: un oro menos que el escalón rico.
        [TestCase(250, 3, 35, 2)]   // borde: el umbral es inclusive.
        [TestCase(9999, 3, 35, 2)]
        public void Resolve_MapsGoldToTheFichaTier(int gold, int expectedSize, int expectedDamage, int expectedRank)
        {
            var tier = CashierGoldTierTable.Resolve(FichaTiers(), gold, stepDown: 0, out int rank);

            Assert.IsNotNull(tier, $"Con {gold} de oro debería haber escalón elegible.");
            Assert.AreEqual(expectedSize, tier.ColumnSize, $"Ancho de columna con {gold} de oro.");
            Assert.AreEqual(expectedDamage, tier.Damage, $"Daño con {gold} de oro.");
            Assert.AreEqual(expectedRank, rank);
        }

        [Test]
        public void Resolve_TopTier_NeverExceedsFloor2DamageCeiling()
        {
            var tier = CashierGoldTierTable.Resolve(FichaTiers(), gold: 100000, stepDown: 0);

            Assert.LessOrEqual(tier.Damage, 35,
                "El techo de daño de piso 2 es 35 por golpe — el escalón rico no puede pasarlo.");
        }

        [TestCase(250, 3, 28)] // rico sobornado ⇒ paga como el escalón medio.
        [TestCase(100, 1, 14)] // medio sobornado ⇒ paga como el pobre.
        [TestCase(0,   1, 14)] // pobre sobornado ⇒ ya está abajo, se clampea.
        public void Resolve_StepDown_DropsExactlyOneTier(int gold, int expectedSize, int expectedDamage)
        {
            var tier = CashierGoldTierTable.Resolve(FichaTiers(), gold, stepDown: 1);

            Assert.AreEqual(expectedSize, tier.ColumnSize);
            Assert.AreEqual(expectedDamage, tier.Damage);
        }

        [Test]
        public void Resolve_StepDownBiggerThanTable_ClampsToCheapestTier()
        {
            var tier = CashierGoldTierTable.Resolve(FichaTiers(), gold: 250, stepDown: 99, out int rank);

            Assert.AreEqual(0, rank, "No se puede bajar del escalón más barato.");
            Assert.AreEqual(14, tier.Damage);
        }

        [Test]
        public void Resolve_NegativeStepDown_IsIgnored_NeverUpgradesTheTier()
        {
            var tier = CashierGoldTierTable.Resolve(FichaTiers(), gold: 0, stepDown: -2, out int rank);

            Assert.AreEqual(0, rank, "Un stepDown negativo no debe promover al jefe de escalón.");
            Assert.AreEqual(14, tier.Damage);
        }

        [Test]
        public void Resolve_UnsortedTable_RanksByMinGold_SoStepDownStaysMeaningful()
        {
            // Mismo contenido que la ficha, arrastrado al revés en el inspector.
            var shuffled = new List<CashierGoldTier>
            {
                new CashierGoldTier { MinGold = 250, ColumnSize = 3, Damage = 35 },
                new CashierGoldTier { MinGold = 0,   ColumnSize = 1, Damage = 14 },
                new CashierGoldTier { MinGold = 100, ColumnSize = 3, Damage = 28 },
            };

            Assert.AreEqual(28, CashierGoldTierTable.Resolve(shuffled, 100, 0).Damage);
            Assert.AreEqual(35, CashierGoldTierTable.Resolve(shuffled, 250, 0).Damage);
            Assert.AreEqual(28, CashierGoldTierTable.Resolve(shuffled, 250, 1).Damage,
                "Bajar un escalón desde el rico tiene que dar el medio, no el que quedó al lado.");
        }

        [Test]
        public void Resolve_GoldBelowEveryThreshold_FallsBackToCheapestTier()
        {
            var tiers = new List<CashierGoldTier>
            {
                new CashierGoldTier { MinGold = 50, ColumnSize = 1, Damage = 14 },
                new CashierGoldTier { MinGold = 100, ColumnSize = 3, Damage = 28 },
            };

            var tier = CashierGoldTierTable.Resolve(tiers, gold: 0, stepDown: 0, out int rank);

            Assert.AreEqual(0, rank, "Sin escalón que cubra ese oro se usa el más barato, no null.");
            Assert.AreEqual(14, tier.Damage);
        }

        [Test]
        public void Resolve_NullEntriesAreSkipped()
        {
            var tiers = new List<CashierGoldTier>
            {
                null,
                new CashierGoldTier { MinGold = 0, ColumnSize = 1, Damage = 14 },
                null,
                new CashierGoldTier { MinGold = 100, ColumnSize = 3, Damage = 28 },
            };

            Assert.AreEqual(28, CashierGoldTierTable.Resolve(tiers, 150, 0).Damage);
            Assert.AreEqual(2, CashierGoldTierTable.Rank(tiers).Count);
        }

        [Test]
        public void Resolve_EmptyOrNullTable_ReturnsNull()
        {
            Assert.IsNull(CashierGoldTierTable.Resolve(new List<CashierGoldTier>(), 100, 0, out int emptyRank));
            Assert.AreEqual(-1, emptyRank);
            Assert.IsNull(CashierGoldTierTable.Resolve(null, 100, 0, out int nullRank));
            Assert.AreEqual(-1, nullRank);
        }
    }
}
