using NUnit.Framework;
using Rollgeon.Combos.Concretes;
using UnityEngine;

namespace Rollgeon.Combos.Tests
{
    /// <summary>
    /// Tests de la sobrecarga <c>Detect(dice, flatBaseOverride)</c> (Spec Daño v2 — tabla
    /// <c>daño_combo_base</c> por clase): el override reemplaza solo el base plano y no toca
    /// <c>CountUsed</c> ni <c>ContributingIndices</c>.
    /// </summary>
    [TestFixture]
    public class ComboDetectFlatBaseOverrideTests
    {
        private Combo_Par _par;
        private Combo_SumaX _sumaX;
        private Combo_FuerzaBruta _fuerzaBruta;

        [SetUp]
        public void SetUp()
        {
            _par = ComboTestUtils.CreateCombo<Combo_Par>("combo.par", 18);
            _sumaX = ComboTestUtils.CreateCombo<Combo_SumaX>("combo.suma_x", 10);
            ComboTestUtils.SetField(_sumaX, "_x", 4);
            ComboTestUtils.SetField(_sumaX, "_baseDamageConfigurable", 25);
            _fuerzaBruta = ComboTestUtils.CreateCombo<Combo_FuerzaBruta>(ComboId.BruteForce, 5);
            ComboTestUtils.SetField(_fuerzaBruta, "_baseDamageConfigurable", 5);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_par);
            Object.DestroyImmediate(_sumaX);
            Object.DestroyImmediate(_fuerzaBruta);
        }

        [Test]
        public void Detect_WithOverride_ReplacesBaseDamage_PreservingCountAndIndices()
        {
            var result = _par.Detect(new[] { 3, 3, 1, 2, 5 }, 42);

            Assert.IsTrue(result.IsMatch);
            Assert.AreEqual(42, result.BaseDamage);
            Assert.AreEqual(2, result.CountUsed);
            CollectionAssert.AreEqual(new[] { 0, 1 }, result.ContributingIndices);
            Assert.AreEqual("combo.par", result.ComboId,
                "Detect debe transportar el ComboId — consumers por-combo (escudo) lo leen del result.");
        }

        [Test]
        public void Detect_WithNullOverride_UsesComboSOBase()
        {
            var result = _par.Detect(new[] { 3, 3, 1, 2, 5 }, null);

            Assert.IsTrue(result.IsMatch);
            Assert.AreEqual(18, result.BaseDamage);
        }

        [Test]
        public void Detect_NoMatch_IgnoresOverride()
        {
            var result = _par.Detect(new[] { 1, 2, 3, 4, 5 }, 42);

            Assert.IsFalse(result.IsMatch);
            Assert.AreEqual(0, result.BaseDamage);
        }

        [Test]
        public void SumaX_Detect_NullOverride_UsesConfigurableFloorPlusDynamicPart()
        {
            // X=4, floor=25, dos hits → 25 + 4×2 = 33.
            var result = _sumaX.Detect(new[] { 4, 4, 1, 2, 3 }, null);

            Assert.IsTrue(result.IsMatch);
            Assert.AreEqual(33, result.BaseDamage);
            Assert.AreEqual(2, result.CountUsed);
            CollectionAssert.AreEqual(new[] { 0, 1 }, result.ContributingIndices);
        }

        [Test]
        public void SumaX_Detect_WithOverride_ReplacesFlatPartOnly()
        {
            // El override reemplaza solo el piso plano; X×hits suma encima: 40 + 4×2 = 48.
            var result = _sumaX.Detect(new[] { 4, 4, 1, 2, 3 }, 40);

            Assert.IsTrue(result.IsMatch);
            Assert.AreEqual(48, result.BaseDamage);
            Assert.AreEqual(2, result.CountUsed);
        }

        [Test]
        public void FuerzaBruta_Detect_NullOverride_UsesConfigurableFloorPlusDynamicPart()
        {
            // d6=5 y d8=6 en mitad superior; d12=6 no. Piso 5 + (5+6) = 16.
            var types = new[]
            {
                Rollgeon.Dice.DiceType.D6, Rollgeon.Dice.DiceType.D8, Rollgeon.Dice.DiceType.D12,
            };
            var result = _fuerzaBruta.Detect(new[] { 5, 6, 6 }, types, null);

            Assert.IsTrue(result.IsMatch);
            Assert.AreEqual(16, result.BaseDamage);
            Assert.AreEqual(2, result.CountUsed);
            CollectionAssert.AreEqual(new[] { 0, 1 }, result.ContributingIndices);
        }

        [Test]
        public void FuerzaBruta_Detect_WithOverride_ReplacesFlatPartOnly()
        {
            // El override reemplaza solo el piso; la suma dinámica va encima: 40 + (5+6) = 51.
            var types = new[]
            {
                Rollgeon.Dice.DiceType.D6, Rollgeon.Dice.DiceType.D8, Rollgeon.Dice.DiceType.D12,
            };
            var result = _fuerzaBruta.Detect(new[] { 5, 6, 6 }, types, 40);

            Assert.IsTrue(result.IsMatch);
            Assert.AreEqual(51, result.BaseDamage);
            Assert.AreEqual(2, result.CountUsed);
        }
    }
}
