using NUnit.Framework;
using Rollgeon.Combos.Concretes;
using Rollgeon.Combos.Tests;
using UnityEngine;

namespace Rollgeon.Heroes.Tests
{
    /// <summary>
    /// Tests de la tabla <c>daño_combo_base</c> por clase (Spec Daño v2):
    /// <see cref="ContractSheet.BaseDamageTable"/> + <see cref="ContractSheet.GetBaseDamage"/>.
    /// </summary>
    [TestFixture]
    public class ContractSheetBaseDamageTests
    {
        private Combo_Par _par;

        [SetUp]
        public void SetUp()
        {
            _par = ComboTestUtils.CreateCombo<Combo_Par>("combo.par", 18);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_par);
        }

        private static ContractSheet SheetWith(string comboId, int baseDamage)
        {
            var sheet = new ContractSheet();
            sheet.BaseDamageTable.Add(new ComboBaseDamageEntry
            {
                ComboId = comboId,
                BaseDamage = baseDamage,
            });
            return sheet;
        }

        [Test]
        public void GetBaseDamage_NoEntry_FallsBackToComboSO()
        {
            var sheet = new ContractSheet();

            Assert.AreEqual(18, sheet.GetBaseDamage(_par),
                "Sin entrada en la tabla, el base debe salir del SO (backward-compat).");
        }

        [Test]
        public void GetBaseDamage_WithEntry_ReturnsClassValue()
        {
            var sheet = SheetWith("combo.par", 30);

            Assert.AreEqual(30, sheet.GetBaseDamage(_par));
        }

        [Test]
        public void TwoSheets_SharingSameComboSO_ResolveDifferentBases()
        {
            // Requisito headline del spec: "daño_combo_base vive en tablas separadas por
            // clase" — el mismo asset de combo puede valer distinto en cada clase.
            var sheetA = SheetWith("combo.par", 18);
            var sheetB = SheetWith("combo.par", 30);

            Assert.AreEqual(18, sheetA.GetBaseDamage(_par));
            Assert.AreEqual(30, sheetB.GetBaseDamage(_par));
            Assert.AreEqual(18, _par.BaseDamage,
                "La tabla es un overlay — nunca muta el SO compartido.");
        }

        [Test]
        public void GetBaseDamageOverride_UnknownOrNullComboId_ReturnsNull()
        {
            var sheet = SheetWith("combo.par", 30);

            Assert.IsNull(sheet.GetBaseDamageOverride("combo.trio"));
            Assert.IsNull(sheet.GetBaseDamageOverride(null));
        }

        [Test]
        public void Instantiate_CopiesTable_MutationDoesNotLeakToOriginal()
        {
            var original = SheetWith("combo.par", 30);

            var runCopy = original.Instantiate();
            runCopy.BaseDamageTable.Add(new ComboBaseDamageEntry
            {
                ComboId = "combo.trio",
                BaseDamage = 99,
            });

            Assert.AreEqual(30, runCopy.GetBaseDamage(_par),
                "La copia de run conserva las entradas originales.");
            Assert.IsNull(original.GetBaseDamageOverride("combo.trio"),
                "Mutar la tabla de la run no debe tocar el asset original.");
        }
    }
}
