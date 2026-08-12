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
        public void SumaX_Detect_NullOverride_FlatBaseAndDynamicBonusSeparated()
        {
            // Fix#0047: X=4, floor=25, dos hits → BaseDamage plano 25, DynamicBonus 4×2.
            var result = _sumaX.Detect(new[] { 4, 4, 1, 2, 3 }, null);

            Assert.IsTrue(result.IsMatch);
            Assert.AreEqual(25, result.BaseDamage);
            Assert.AreEqual(8, result.DynamicBonus);
            Assert.AreEqual(33, result.EffectiveTotal, "Formula B conserva 25 + 4×2.");
            Assert.AreEqual(2, result.CountUsed);
            CollectionAssert.AreEqual(new[] { 0, 1 }, result.ContributingIndices);
        }

        [Test]
        public void SumaX_Detect_WithOverride_ReplacesFlatPartOnly()
        {
            // El override reemplaza solo el piso plano; X×hits queda en DynamicBonus.
            var result = _sumaX.Detect(new[] { 4, 4, 1, 2, 3 }, 40);

            Assert.IsTrue(result.IsMatch);
            Assert.AreEqual(40, result.BaseDamage);
            Assert.AreEqual(8, result.DynamicBonus);
            Assert.AreEqual(48, result.EffectiveTotal);
            Assert.AreEqual(2, result.CountUsed);
        }

        [Test]
        public void FuerzaBruta_Detect_NullOverride_FlatBaseAndDynamicBonusSeparated()
        {
            // Requiere los 5 dados de la bolsa (DiceBagSO.RequiredSize) — todos en mitad
            // superior. Fix#0047: BaseDamage plano 5; Σ(5+6+7+5+6)=29 en DynamicBonus.
            var types = new[]
            {
                Rollgeon.Dice.DiceType.D6, Rollgeon.Dice.DiceType.D8, Rollgeon.Dice.DiceType.D12,
                Rollgeon.Dice.DiceType.D6, Rollgeon.Dice.DiceType.D8,
            };
            var result = _fuerzaBruta.Detect(new[] { 5, 6, 7, 5, 6 }, types, null);

            Assert.IsTrue(result.IsMatch);
            Assert.AreEqual(5, result.BaseDamage);
            Assert.AreEqual(29, result.DynamicBonus);
            Assert.AreEqual(34, result.EffectiveTotal, "Formula B conserva piso + Σcaras.");
            Assert.AreEqual(5, result.CountUsed);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, result.ContributingIndices);
        }

        [Test]
        public void FuerzaBruta_Detect_WithOverride_ReplacesFlatPartOnly()
        {
            // El override reemplaza solo el piso; la suma dinámica queda en DynamicBonus.
            var types = new[]
            {
                Rollgeon.Dice.DiceType.D6, Rollgeon.Dice.DiceType.D8, Rollgeon.Dice.DiceType.D12,
                Rollgeon.Dice.DiceType.D6, Rollgeon.Dice.DiceType.D8,
            };
            var result = _fuerzaBruta.Detect(new[] { 5, 6, 7, 5, 6 }, types, 40);

            Assert.IsTrue(result.IsMatch);
            Assert.AreEqual(40, result.BaseDamage);
            Assert.AreEqual(29, result.DynamicBonus);
            Assert.AreEqual(69, result.EffectiveTotal);
            Assert.AreEqual(5, result.CountUsed);
        }

        [Test]
        public void FuerzaBruta_Detect_SubsetOfThreeAllUpperHalf_NoMatch()
        {
            // Regresión Bocco (2026-07-14): un subset "kept" de 3 dados, todos en mitad
            // superior, NO debe matchear — Fuerza Bruta exige la bolsa completa (5).
            var types = new[]
            {
                Rollgeon.Dice.DiceType.D6, Rollgeon.Dice.DiceType.D8, Rollgeon.Dice.DiceType.D12,
            };
            var result = _fuerzaBruta.Detect(new[] { 5, 6, 7 }, types, null);

            Assert.IsFalse(result.IsMatch);
            Assert.AreEqual(0, result.BaseDamage);
        }
    }
}
