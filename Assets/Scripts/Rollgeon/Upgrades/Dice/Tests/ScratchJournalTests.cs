using NUnit.Framework;

namespace Rollgeon.Upgrades.Dice.Tests
{
    /// <summary>
    /// Tests del journal de atribución por fuente: <see cref="EnchantmentScratch.Journal"/> +
    /// <see cref="ScratchSnapshot.RecordDelta"/> (captura snapshot-delta en el dispatch).
    /// </summary>
    [TestFixture]
    public class ScratchJournalTests
    {
        [Test]
        public void RecordDelta_BonusChanged_RecordsEntryWithDelta()
        {
            // Arrange
            var scratch = new EnchantmentScratch { BonusComboDamage = 3 };
            var before = ScratchSnapshot.Of(scratch);

            // Act — la "fuente" suma +2
            scratch.BonusComboDamage += 2;
            ScratchSnapshot.RecordDelta(scratch, in before,
                ScratchSourceKind.Enchantment, "ench.resonante", null, bagSlot: 1);

            // Assert
            Assert.AreEqual(1, scratch.Journal.Count);
            var entry = scratch.Journal[0];
            Assert.AreEqual(ScratchSourceKind.Enchantment, entry.Kind);
            Assert.AreEqual("ench.resonante", entry.SourceId);
            Assert.AreEqual(1, entry.BagSlot);
            Assert.AreEqual(2, entry.BonusDelta);
            Assert.AreEqual(1f, entry.MultiplierFactor, 0.0001f);
            Assert.IsFalse(entry.SetBlock);
        }

        [Test]
        public void RecordDelta_MultiplierChanged_RecordsFactorNotAbsolute()
        {
            // Arrange — otro trigger ya dejó el multi en ×2
            var scratch = new EnchantmentScratch { ComboDamageMultiplier = 2f };
            var before = ScratchSnapshot.Of(scratch);

            // Act — esta fuente compone ×1.5 (multi total ×3)
            scratch.ComboDamageMultiplier *= 1.5f;
            ScratchSnapshot.RecordDelta(scratch, in before,
                ScratchSourceKind.Item, "item.gemelo", null, bagSlot: -1);

            // Assert — el factor es el aporte propio (1.5), no el acumulado (3)
            Assert.AreEqual(1.5f, scratch.Journal[0].MultiplierFactor, 0.0001f);
            Assert.AreEqual(0, scratch.Journal[0].BonusDelta);
        }

        [Test]
        public void RecordDelta_NeutralSource_RecordsNothing()
        {
            // Arrange
            var scratch = new EnchantmentScratch { BonusComboDamage = 5 };
            var before = ScratchSnapshot.Of(scratch);

            // Act — la fuente no tocó nada de combo
            scratch.BonusGold += 3; // canal de recursos, no de combo
            ScratchSnapshot.RecordDelta(scratch, in before,
                ScratchSourceKind.Enchantment, "ench.avaro", null, bagSlot: 0);

            // Assert — sin entrada NI alocación
            Assert.IsNull(scratch.Journal);
        }

        [Test]
        public void RecordDelta_BlockActivated_RecordsSetBlock()
        {
            var scratch = new EnchantmentScratch();
            var before = ScratchSnapshot.Of(scratch);

            scratch.BlockComboDamage = true;
            ScratchSnapshot.RecordDelta(scratch, in before,
                ScratchSourceKind.Enchantment, "ench.egoista", null, bagSlot: 2);

            Assert.IsTrue(scratch.Journal[0].SetBlock);
        }

        [Test]
        public void RecordDelta_MultipleSources_AccumulateInDispatchOrder()
        {
            var scratch = new EnchantmentScratch();

            var before = ScratchSnapshot.Of(scratch);
            scratch.BonusComboDamage += 2;
            ScratchSnapshot.RecordDelta(scratch, in before,
                ScratchSourceKind.Enchantment, "primero", null, 0);

            before = ScratchSnapshot.Of(scratch);
            scratch.ComboDamageMultiplier *= 2f;
            ScratchSnapshot.RecordDelta(scratch, in before,
                ScratchSourceKind.Item, "segundo", null, -1);

            Assert.AreEqual(2, scratch.Journal.Count);
            Assert.AreEqual("primero", scratch.Journal[0].SourceId);
            Assert.AreEqual("segundo", scratch.Journal[1].SourceId);
        }

        [Test]
        public void Reset_ClearsJournal()
        {
            var scratch = new EnchantmentScratch();
            var before = ScratchSnapshot.Of(scratch);
            scratch.BonusComboDamage += 1;
            ScratchSnapshot.RecordDelta(scratch, in before,
                ScratchSourceKind.Enchantment, "x", null, 0);

            scratch.Reset();

            Assert.IsTrue(scratch.Journal == null || scratch.Journal.Count == 0);
        }

        // ---- Canal aditivo sobre M (ComboMultiplierBonus) ---------------------------

        [Test]
        public void Reset_ClearsComboMultiplierBonus()
        {
            var scratch = new EnchantmentScratch { ComboMultiplierBonus = 2f };

            scratch.Reset();

            Assert.AreEqual(0f, scratch.ComboMultiplierBonus, 0.0001f);
        }

        [Test]
        public void RecordDelta_MultiplierBonusChanged_RecordsDeltaNotAbsolute()
        {
            // Arrange — otra fuente ya dejó +1 en el bono de M
            var scratch = new EnchantmentScratch { ComboMultiplierBonus = 1f };
            var before = ScratchSnapshot.Of(scratch);

            // Act — esta fuente suma +2 (total +3)
            scratch.ComboMultiplierBonus += 2f;
            ScratchSnapshot.RecordDelta(scratch, in before,
                ScratchSourceKind.Item, "piedra.angular", null, bagSlot: -1);

            // Assert — el aporte propio (2), no el acumulado (3); los otros canales neutros
            var entry = scratch.Journal[0];
            Assert.AreEqual(2f, entry.MultiplierBonusDelta, 0.0001f);
            Assert.AreEqual(0, entry.BonusDelta);
            Assert.AreEqual(1f, entry.MultiplierFactor, 0.0001f);
            Assert.IsFalse(entry.SetBlock);
        }

        [Test]
        public void RecordDelta_MultiplierBonusOnly_IsNotNeutral()
        {
            var scratch = new EnchantmentScratch();
            var before = ScratchSnapshot.Of(scratch);

            scratch.ComboMultiplierBonus += 0.05f; // Vértigo: un combo
            ScratchSnapshot.RecordDelta(scratch, in before,
                ScratchSourceKind.Item, "vertigo", null, bagSlot: -1);

            Assert.IsNotNull(scratch.Journal);
            Assert.AreEqual(1, scratch.Journal.Count);
            Assert.AreEqual(0.05f, scratch.Journal[0].MultiplierBonusDelta, 0.0001f);
        }
    }
}
