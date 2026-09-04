using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rollgeon.Dice;
using Rollgeon.Items.Active;

namespace Rollgeon.Editor.Tools.Item.Tests
{
    /// <summary>
    /// Cubre <see cref="ActiveItemCatalogSeed.Specs"/> (Feature#0084 §A6): las 7 fichas del
    /// doc quedan bien tipadas — sin tocar disco, catálogo ni localización (eso es
    /// responsabilidad de <see cref="ActiveItemAuthoringTests"/> / <see cref="ActiveItemCatalogSeed.Run"/>).
    /// </summary>
    public sealed class ActiveItemCatalogSeedTests
    {
        static IReadOnlyList<ActiveItemCreationSpec> Specs() => ActiveItemCatalogSeed.Specs();

        [Test]
        public void Specs_ReturnsExactlySevenItems()
        {
            Assert.AreEqual(7, Specs().Count);
        }

        [Test]
        public void Specs_AllIdsAreUnique()
        {
            var ids = Specs().Select(s => s.ItemId).ToList();
            Assert.AreEqual(ids.Count, ids.Distinct().Count(), "IDs duplicados entre las 7 specs.");
        }

        [Test]
        public void Specs_ContainTheSevenExpectedIds()
        {
            var ids = Specs().Select(s => s.ItemId).ToList();
            CollectionAssert.AreEquivalent(new[]
            {
                "blood.transfusion",
                "coin.shield",
                "grapple.claw",
                "justa.de.justicia",
                "probability.drive",
                "blood.d6",
                "bottle.o.thunder",
            }, ids);
        }

        [Test]
        public void Specs_AllRareAndSixtyGold()
        {
            foreach (var spec in Specs())
            {
                Assert.AreEqual(Rollgeon.Items.ItemRarity.Rare, spec.Rarity, $"[{spec.ItemId}] rareza placeholder.");
                Assert.AreEqual(60, spec.BasePrice, $"[{spec.ItemId}] precio placeholder.");
            }
        }

        [Test]
        public void Specs_DescriptionsDifferBetweenSpanishAndEnglish()
        {
            foreach (var spec in Specs())
            {
                Assert.IsNotEmpty(spec.DescriptionEs, $"[{spec.ItemId}] sin descripción ES.");
                Assert.IsNotEmpty(spec.DescriptionEn, $"[{spec.ItemId}] sin descripción EN.");
                Assert.AreNotEqual(spec.DescriptionEs, spec.DescriptionEn,
                    $"[{spec.ItemId}] la descripción EN no puede repetir la ES.");
            }
        }

        [Test]
        public void Specs_BloodTransfusion_IsD10BandsWithDocCuts()
        {
            var spec = Specs().Single(s => s.ItemId == "blood.transfusion");
            Assert.AreEqual(DiceType.D10, spec.Die);
            Assert.AreEqual(ActiveItemResolution.Bands, spec.Resolution);
            // Doc: 1-3 / 4-7 / 8-10. Los tercios puros darian 4-6 mixta.
            Assert.AreEqual(3, spec.NegativeMaxFace);
            Assert.AreEqual(7, spec.MixedMaxFace);
        }

        [Test]
        public void Specs_CoinShield_IsD4BinaryEvenPositive()
        {
            var spec = Specs().Single(s => s.ItemId == "coin.shield");
            Assert.AreEqual(DiceType.D4, spec.Die);
            Assert.AreEqual(ActiveItemResolution.Binary, spec.Resolution);
            Assert.AreEqual(ActiveItemParity.Even, spec.BinaryPositiveParity);
        }

        [Test]
        public void Specs_GrappleClaw_IsD6Gradient()
        {
            var spec = Specs().Single(s => s.ItemId == "grapple.claw");
            Assert.AreEqual(DiceType.D6, spec.Die);
            Assert.AreEqual(ActiveItemResolution.Gradient, spec.Resolution);
        }

        [Test]
        public void Specs_JustaDeJusticia_IsD12BandsThirds()
        {
            var spec = Specs().Single(s => s.ItemId == "justa.de.justicia");
            Assert.AreEqual(DiceType.D12, spec.Die);
            Assert.AreEqual(ActiveItemResolution.Bands, spec.Resolution);
            Assert.AreEqual(0, spec.NegativeMaxFace, "Tercios: sin corte custom.");
            Assert.AreEqual(0, spec.MixedMaxFace, "Tercios: sin corte custom.");
        }

        [Test]
        public void Specs_ProbabilityDrive_IsD4BandsWithCustomCuts()
        {
            var spec = Specs().Single(s => s.ItemId == "probability.drive");
            Assert.AreEqual(DiceType.D4, spec.Die);
            Assert.AreEqual(ActiveItemResolution.Bands, spec.Resolution);
            Assert.AreEqual(1, spec.NegativeMaxFace, "Doc: negativa = solo cara 1.");
            Assert.AreEqual(3, spec.MixedMaxFace, "Doc: mixta = caras 2-3.");
        }

        [Test]
        public void Specs_BloodD6_IsD6Gradient()
        {
            var spec = Specs().Single(s => s.ItemId == "blood.d6");
            Assert.AreEqual(DiceType.D6, spec.Die);
            Assert.AreEqual(ActiveItemResolution.Gradient, spec.Resolution);
        }

        [Test]
        public void Specs_BottleOThunder_IsD4Hierarchy()
        {
            var spec = Specs().Single(s => s.ItemId == "bottle.o.thunder");
            Assert.AreEqual(DiceType.D4, spec.Die);
            Assert.AreEqual(ActiveItemResolution.Hierarchy, spec.Resolution);
        }
    }
}
