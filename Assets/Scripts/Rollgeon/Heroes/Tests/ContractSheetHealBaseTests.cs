using NUnit.Framework;

namespace Rollgeon.Heroes.Tests
{
    /// <summary>
    /// Tests de la tabla <c>curacion_combo_base</c> por clase (Spec Heal N×M):
    /// <see cref="ContractSheet.HealBaseTable"/> + <see cref="ContractSheet.GetHealBase"/>.
    /// Espejo de <see cref="ContractSheetShieldBaseTests"/>: la invariante central es la
    /// independencia total de las tablas de daño y escudo.
    /// </summary>
    [TestFixture]
    public class ContractSheetHealBaseTests
    {
        private ContractSheet _sheet;

        [SetUp]
        public void SetUp()
        {
            _sheet = new ContractSheet();
        }

        [Test]
        public void GetHealBase_EntryPresent_ReturnsTableValue()
        {
            // Arrange
            _sheet.HealBaseTable.Add(new ComboHealBaseEntry { ComboId = "combo.par", HealBase = 8 });

            // Act / Assert
            Assert.AreEqual(8, _sheet.GetHealBase("combo.par"));
        }

        [Test]
        public void GetHealBase_MissingEntry_ReturnsZero()
        {
            // Arrange — tabla con otra entrada, sin la buscada
            _sheet.HealBaseTable.Add(new ComboHealBaseEntry { ComboId = "combo.trio", HealBase = 22 });

            // Act / Assert — fallback explícito: sin entrada, la clase no cura con ese combo
            Assert.AreEqual(0, _sheet.GetHealBase("combo.par"));
        }

        [Test]
        public void GetHealBase_NullOrEmptyComboId_ReturnsZero()
        {
            // Arrange
            _sheet.HealBaseTable.Add(new ComboHealBaseEntry { ComboId = "combo.par", HealBase = 8 });

            // Act / Assert
            Assert.AreEqual(0, _sheet.GetHealBase(null));
            Assert.AreEqual(0, _sheet.GetHealBase(string.Empty));
        }

        [Test]
        public void GetHealBase_IsIndependentFromDamageAndShieldTables()
        {
            // Arrange — mismo combo con daño y escudo configurados, heal propio
            _sheet.BaseDamageTable.Add(new ComboBaseDamageEntry { ComboId = "combo.par", BaseDamage = 99 });
            _sheet.ShieldBaseTable.Add(new ComboShieldBaseEntry { ComboId = "combo.par", ShieldBase = 50 });
            _sheet.HealBaseTable.Add(new ComboHealBaseEntry { ComboId = "combo.par", HealBase = 8 });

            // Act
            int healBefore = _sheet.GetHealBase("combo.par");
            _sheet.BaseDamageTable[0] = new ComboBaseDamageEntry { ComboId = "combo.par", BaseDamage = 500 };
            _sheet.ShieldBaseTable[0] = new ComboShieldBaseEntry { ComboId = "combo.par", ShieldBase = 200 };
            int healAfter = _sheet.GetHealBase("combo.par");

            // Assert — mutar las otras tablas no mueve el heal
            Assert.AreEqual(8, healBefore);
            Assert.AreEqual(8, healAfter);
        }

        [Test]
        public void GetHealBase_DamageTableEntryWithoutHealEntry_StillReturnsZero()
        {
            // Arrange — tener daño configurado no otorga curación implícita
            _sheet.BaseDamageTable.Add(new ComboBaseDamageEntry { ComboId = "combo.generala", BaseDamage = 90 });

            // Act / Assert
            Assert.AreEqual(0, _sheet.GetHealBase("combo.generala"));
        }

        [Test]
        public void Instantiate_CopiesHealTableByValue()
        {
            // Arrange
            _sheet.HealBaseTable.Add(new ComboHealBaseEntry { ComboId = "combo.par", HealBase = 8 });

            // Act — mutar el clon de la run no debe tocar el "asset"
            var runCopy = _sheet.Instantiate();
            runCopy.HealBaseTable.Add(new ComboHealBaseEntry { ComboId = "combo.trio", HealBase = 22 });
            runCopy.HealBaseTable[0] = new ComboHealBaseEntry { ComboId = "combo.par", HealBase = 15 };

            // Assert
            Assert.AreEqual(1, _sheet.HealBaseTable.Count);
            Assert.AreEqual(8, _sheet.GetHealBase("combo.par"));
            Assert.AreEqual(15, runCopy.GetHealBase("combo.par"));
            Assert.AreEqual(22, runCopy.GetHealBase("combo.trio"));
        }
    }
}
