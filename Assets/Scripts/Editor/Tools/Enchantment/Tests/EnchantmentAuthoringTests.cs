using NUnit.Framework;
using Rollgeon.Upgrades.Dice;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enchantment.Tests
{
    /// <summary>
    /// Mismo trato que <c>ItemAuthoringTests</c>: escribir sobre los assets vivos
    /// (Content/EnchantmentPool/EnchantmentCatalog) está deliberadamente fuera de
    /// alcance — un test no muta assets del proyecto. Por eso todos los tests de
    /// <c>CreateEnchantment</c> son de FALLO, y la aserción central es que el conteo de
    /// assets no cambió.
    /// </summary>
    public class EnchantmentAuthoringTests
    {
        const string ParentFolder = "Assets/Rollgeon/Upgrades/Dice/Enchantments";
        const string TestFolderName = "__EnchantmentAuthoringTests";
        const string TestFolder = ParentFolder + "/" + TestFolderName;
        const string ProbeId = "ench.test_probe";

        EnchantmentSO _probe;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TestFolder))
                AssetDatabase.CreateFolder(ParentFolder, TestFolderName);

            _probe = ScriptableObject.CreateInstance<EnchantmentSO>();
            _probe.EditorSetUpgradeId(ProbeId);
            _probe.EditorSetDisplayName("Test Probe");
            AssetDatabase.CreateAsset(_probe, TestFolder + "/Ench_TestProbe.asset");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TestFolder + "/Ench_TestProbe.asset");
            AssetDatabase.DeleteAsset(TestFolder);
        }

        static int CountEnchantmentAssets() => AssetDatabase.FindAssets("t:EnchantmentSO").Length;

        static EnchantmentCreationSpec ValidSpec(string displayName) => new()
        {
            DisplayName = displayName,
            Description = "desc",
            Category = EnchantmentCategory.Control,
        };

        // ---- IsIdAvailable ----------------------------------------------------------

        [Test]
        public void IsIdAvailable_UnusedId_ReturnsTrueWithNullOwner()
        {
            // Act
            bool available = EnchantmentAuthoring.IsIdAvailable("ench.id_que_nadie_usa", out var owner);

            // Assert
            Assert.IsTrue(available);
            Assert.IsNull(owner);
        }

        [Test]
        public void IsIdAvailable_UsedId_ReturnsFalseWithOwner()
        {
            // Act
            bool available = EnchantmentAuthoring.IsIdAvailable(ProbeId, out var owner);

            // Assert
            Assert.IsFalse(available);
            Assert.AreSame(_probe, owner);
        }

        [Test]
        public void IsIdAvailable_EmptyOrNullId_ReturnsFalse()
        {
            Assert.IsFalse(EnchantmentAuthoring.IsIdAvailable(null, out _));
            Assert.IsFalse(EnchantmentAuthoring.IsIdAvailable(string.Empty, out _));
        }

        // ---- CreateEnchantment: caminos de fallo ------------------------------------

        [Test]
        public void CreateEnchantment_EmptyDisplayName_FailsWithoutCreatingAnAsset()
        {
            // Arrange
            int before = CountEnchantmentAssets();
            var spec = ValidSpec("   ");

            // Act
            var result = EnchantmentAuthoring.CreateEnchantment(spec);

            // Assert
            Assert.IsFalse(result.Success);
            Assert.AreEqual(before, CountEnchantmentAssets());
        }

        [Test]
        public void CreateEnchantment_CategoryNone_FailsNamingTheAudit()
        {
            // Arrange — la auditoría AllEnchantmentAssets_HaveACategoryAssigned rechaza None:
            // mejor fallar el alta que crear un asset que deja la suite roja.
            int before = CountEnchantmentAssets();
            var spec = ValidSpec("Sin Categoria");
            spec.Category = EnchantmentCategory.None;

            // Act
            var result = EnchantmentAuthoring.CreateEnchantment(spec);

            // Assert
            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Errors[0].Contains("Category"), $"error inesperado: {result.Errors[0]}");
            Assert.AreEqual(before, CountEnchantmentAssets());
        }

        [Test]
        public void CreateEnchantment_DuplicateId_FailsAndNamesTheOwner()
        {
            // Arrange — "Test Probe" deriva ench.test_probe, el id del probe.
            int before = CountEnchantmentAssets();
            var spec = ValidSpec("Test Probe");

            // Act
            var result = EnchantmentAuthoring.CreateEnchantment(spec);

            // Assert
            Assert.IsFalse(result.Success);
            Assert.IsTrue(
                result.Errors[0].Contains(ProbeId) && result.Errors[0].Contains("Ench_TestProbe"),
                $"el error debe nombrar id y dueño: {result.Errors[0]}");
            Assert.AreEqual(before, CountEnchantmentAssets());
        }

        [Test]
        public void CreateEnchantment_UnknownTriggerId_FailsWithoutCreatingAnAsset()
        {
            // Arrange
            int before = CountEnchantmentAssets();
            var spec = ValidSpec("Trigger Inexistente");
            spec.TriggerId = "trigger.que.no.existe";

            // Act
            var result = EnchantmentAuthoring.CreateEnchantment(spec);

            // Assert
            Assert.IsFalse(result.Success);
            Assert.AreEqual(before, CountEnchantmentAssets());
        }

        [Test]
        public void CreateEnchantment_CarrierGateOnNonComboTrigger_Fails()
        {
            // Arrange — RequireCarrierParticipates solo tiene sentido en hooks de combo.
            int before = CountEnchantmentAssets();
            var spec = ValidSpec("Portador Sin Combo");
            spec.TriggerId = "turn.finished";
            spec.RequireCarrierParticipates = true;

            // Act
            var result = EnchantmentAuthoring.CreateEnchantment(spec);

            // Assert
            Assert.IsFalse(result.Success);
            Assert.AreEqual(before, CountEnchantmentAssets());
        }

        [Test]
        public void CreateEnchantment_NegativePoolWeight_Fails()
        {
            // Arrange
            int before = CountEnchantmentAssets();
            var spec = ValidSpec("Peso Negativo");
            spec.PoolWeight = -1f;

            // Act
            var result = EnchantmentAuthoring.CreateEnchantment(spec);

            // Assert
            Assert.IsFalse(result.Success);
            Assert.AreEqual(before, CountEnchantmentAssets());
        }

        // ---- Rename -----------------------------------------------------------------

        [Test]
        public void RenameEnchantmentId_WithoutChannelPrefix_Fails()
        {
            // Act
            var result = EnchantmentAuthoring.RenameEnchantmentId(_probe, "sin_prefijo");

            // Assert
            Assert.IsFalse(result.Success);
            StringAssert.Contains("ench.", result.ErrorMessage);
            Assert.AreEqual(ProbeId, _probe.UpgradeId);
        }

        [Test]
        public void RenameEnchantmentId_ToAnOwnedId_Fails()
        {
            // Arrange — el probe ya es dueño de su id; renombrar otro asset a ese id debe fallar.
            var other = ScriptableObject.CreateInstance<EnchantmentSO>();
            other.EditorSetUpgradeId("ench.otro_id");
            AssetDatabase.CreateAsset(other, TestFolder + "/Ench_Otro.asset");
            try
            {
                // Act
                var result = EnchantmentAuthoring.RenameEnchantmentId(other, ProbeId);

                // Assert
                Assert.IsFalse(result.Success);
                StringAssert.Contains(ProbeId, result.ErrorMessage);
                Assert.AreEqual("ench.otro_id", other.UpgradeId);
            }
            finally
            {
                AssetDatabase.DeleteAsset(TestFolder + "/Ench_Otro.asset");
            }
        }
    }
}
