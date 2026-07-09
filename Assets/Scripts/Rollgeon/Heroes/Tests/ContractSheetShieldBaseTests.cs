using NUnit.Framework;

namespace Rollgeon.Heroes.Tests
{
    /// <summary>
    /// Tests de la tabla <c>escudo_combo_base</c> por clase (Spec Escudo v2):
    /// <see cref="ContractSheet.ShieldBaseTable"/> + <see cref="ContractSheet.GetShieldBase"/>.
    /// La invariante central es la independencia total de <see cref="ContractSheet.BaseDamageTable"/>
    /// (anti-regresión de BUG-021: escudo derivado de la tabla de ataque).
    /// </summary>
    [TestFixture]
    public class ContractSheetShieldBaseTests
    {
        private ContractSheet _sheet;

        [SetUp]
        public void SetUp()
        {
            _sheet = new ContractSheet();
        }

        [Test]
        public void GetShieldBase_EntryPresent_ReturnsTableValue()
        {
            // Arrange
            _sheet.ShieldBaseTable.Add(new ComboShieldBaseEntry { ComboId = "combo.par", ShieldBase = 2 });

            // Act / Assert
            Assert.AreEqual(2, _sheet.GetShieldBase("combo.par"));
        }

        [Test]
        public void GetShieldBase_MissingEntry_ReturnsZero()
        {
            // Arrange — tabla con otra entrada, sin la buscada
            _sheet.ShieldBaseTable.Add(new ComboShieldBaseEntry { ComboId = "combo.trio", ShieldBase = 4 });

            // Act / Assert — fallback explícito: sin entrada, la clase no genera escudo
            Assert.AreEqual(0, _sheet.GetShieldBase("combo.par"));
        }

        [Test]
        public void GetShieldBase_NullOrEmptyComboId_ReturnsZero()
        {
            // Arrange
            _sheet.ShieldBaseTable.Add(new ComboShieldBaseEntry { ComboId = "combo.par", ShieldBase = 2 });

            // Act / Assert
            Assert.AreEqual(0, _sheet.GetShieldBase(null));
            Assert.AreEqual(0, _sheet.GetShieldBase(string.Empty));
        }

        [Test]
        public void GetShieldBase_IsIndependentFromDamageTable()
        {
            // Regression BUG-021: el escudo NUNCA lee la tabla de daño.
            // Arrange — mismo combo con daño alto y escudo bajo
            _sheet.BaseDamageTable.Add(new ComboBaseDamageEntry { ComboId = "combo.par", BaseDamage = 99 });
            _sheet.ShieldBaseTable.Add(new ComboShieldBaseEntry { ComboId = "combo.par", ShieldBase = 2 });

            // Act
            int shieldBefore = _sheet.GetShieldBase("combo.par");
            _sheet.BaseDamageTable[0] = new ComboBaseDamageEntry { ComboId = "combo.par", BaseDamage = 500 };
            int shieldAfter = _sheet.GetShieldBase("combo.par");

            // Assert — mutar la tabla de daño no mueve el escudo
            Assert.AreEqual(2, shieldBefore);
            Assert.AreEqual(2, shieldAfter);
        }

        [Test]
        public void GetShieldBase_DamageTableEntryWithoutShieldEntry_StillReturnsZero()
        {
            // Regression BUG-021 (variante): tener daño configurado no otorga escudo implícito.
            // Arrange
            _sheet.BaseDamageTable.Add(new ComboBaseDamageEntry { ComboId = "combo.generala", BaseDamage = 90 });

            // Act / Assert
            Assert.AreEqual(0, _sheet.GetShieldBase("combo.generala"));
        }

        [Test]
        public void Instantiate_CopiesShieldTableByValue()
        {
            // Arrange
            _sheet.ShieldBaseTable.Add(new ComboShieldBaseEntry { ComboId = "combo.par", ShieldBase = 2 });

            // Act — mutar el clon de la run no debe tocar el "asset"
            var runCopy = _sheet.Instantiate();
            runCopy.ShieldBaseTable.Add(new ComboShieldBaseEntry { ComboId = "combo.trio", ShieldBase = 4 });
            runCopy.ShieldBaseTable[0] = new ComboShieldBaseEntry { ComboId = "combo.par", ShieldBase = 7 };

            // Assert
            Assert.AreEqual(1, _sheet.ShieldBaseTable.Count);
            Assert.AreEqual(2, _sheet.GetShieldBase("combo.par"));
            Assert.AreEqual(7, runCopy.GetShieldBase("combo.par"));
            Assert.AreEqual(4, runCopy.GetShieldBase("combo.trio"));
        }
    }
}
