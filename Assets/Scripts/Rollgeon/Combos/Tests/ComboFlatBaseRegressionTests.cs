using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.ActionRolls;
using Rollgeon.Combos.Concretes;
using UnityEngine;

namespace Rollgeon.Combos.Tests
{
    /// <summary>
    /// Regresión Fix#0047 (doble conteo de caras): el <c>BaseDamage</c> de un
    /// <see cref="ComboDetectionResult"/> NUNCA debe contener valores de dados — es el
    /// término <c>comboBase</c> de la fórmula v3, y las caras contribuyentes ya entran
    /// una vez vía Σcaras. Si un <c>Detect()</c> custom vuelve a meter caras dentro del
    /// base, el daño las cuenta dos veces (el bug original pegaba +70% en Fuerza Bruta).
    /// La parte dependiente de dados va en <c>DynamicBonus</c>, que solo consume la
    /// formula B legacy (<c>ActionRollTotals</c> — Force Door / Heal).
    /// </summary>
    [TestFixture]
    public class ComboFlatBaseRegressionTests
    {
        private Combo_SumaX _sumaX;
        private Combo_HigherNumber _higherNumber;
        private Combo_FuerzaBruta _fuerzaBruta;

        [SetUp]
        public void SetUp()
        {
            _sumaX = ComboTestUtils.CreateCombo<Combo_SumaX>("combo.suma_x", 25);
            ComboTestUtils.SetField(_sumaX, "_x", 4);
            ComboTestUtils.SetField(_sumaX, "_baseDamageConfigurable", 25);

            _higherNumber = ComboTestUtils.CreateCombo<Combo_HigherNumber>(ComboId.HigherNumber, 5);

            _fuerzaBruta = ComboTestUtils.CreateCombo<Combo_FuerzaBruta>(ComboId.BruteForce, 5);
            ComboTestUtils.SetField(_fuerzaBruta, "_baseDamageConfigurable", 5);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_sumaX);
            Object.DestroyImmediate(_higherNumber);
            Object.DestroyImmediate(_fuerzaBruta);
        }

        [Test]
        public void DynamicCombos_BaseDamage_IsIndependentOfDiceFaces()
        {
            // Arrange: dos tiradas distintas que matchean el mismo combo. Si BaseDamage
            // dependiera de las caras, estos pares diferirían.
            var sumaXLow = _sumaX.Detect(new[] { 4, 1, 2, 3, 5 }, null);
            var sumaXHigh = _sumaX.Detect(new[] { 4, 4, 4, 4, 4 }, null);
            var higherLow = _higherNumber.Detect(new[] { 2, 1 }, null);
            var higherHigh = _higherNumber.Detect(new[] { 6, 1 }, null);
            var bruteLow = _fuerzaBruta.Detect(new[] { 4, 4, 4, 4, 4 }, null);
            var bruteHigh = _fuerzaBruta.Detect(new[] { 6, 6, 6, 6, 6 }, null);

            // Assert: base plano idéntico entre tirada mínima y máxima.
            Assert.AreEqual(sumaXLow.BaseDamage, sumaXHigh.BaseDamage,
                "SumaX: BaseDamage debe ser plano — sin caras adentro.");
            Assert.AreEqual(higherLow.BaseDamage, higherHigh.BaseDamage,
                "HigherNumber: BaseDamage debe ser plano — sin caras adentro.");
            Assert.AreEqual(bruteLow.BaseDamage, bruteHigh.BaseDamage,
                "FuerzaBruta: BaseDamage debe ser plano — sin caras adentro.");
        }

        [Test]
        public void DynamicCombos_EffectiveTotal_EqualsFlatBasePlusDynamicPart()
        {
            // Arrange / Act
            var sumaX = _sumaX.Detect(new[] { 4, 4, 1, 2, 3 }, null);
            var higher = _higherNumber.Detect(new[] { 3, 6, 2 }, null);
            var brute = _fuerzaBruta.Detect(new[] { 4, 5, 6, 4, 5 }, null);

            // Assert: la formula B (Force Door / Heal) conserva el valor pre-Fix#0047.
            Assert.AreEqual(25 + 4 * 2, sumaX.EffectiveTotal);
            Assert.AreEqual(5 + 6, higher.EffectiveTotal);
            Assert.AreEqual(5 + (4 + 5 + 6 + 4 + 5), brute.EffectiveTotal);
        }

        [Test]
        public void StaticCombos_DynamicBonus_IsZero()
        {
            // Arrange
            var par = ComboTestUtils.CreateCombo<Combo_Par>(ComboId.Par, 8);
            try
            {
                // Act
                var result = par.Detect(new[] { 3, 3, 1, 2, 5 }, null);

                // Assert: en combos planos EffectiveTotal == BaseDamage.
                Assert.IsTrue(result.IsMatch);
                Assert.AreEqual(0, result.DynamicBonus);
                Assert.AreEqual(result.BaseDamage, result.EffectiveTotal);
            }
            finally
            {
                Object.DestroyImmediate(par);
            }
        }

        [Test]
        public void ResolveEffectiveTotal_UsesEffectiveTotal_NotFlatBase()
        {
            // Arrange: FB matcheada — formula B debe valer piso + Σcaras, no el piso solo
            // (un piso de 5 contra un threshold de Force Door de 25 haría imposible pasar
            // con la mejor tirada posible).
            var dice = new List<int> { 4, 5, 6, 4, 5 };
            var combo = _fuerzaBruta.Detect(dice, null);
            Assert.IsTrue(combo.IsMatch);

            // Act
            int effective = ActionRollTotals.ResolveEffectiveTotal(dice, combo);

            // Assert
            Assert.AreEqual(5 + 24, effective);
        }
    }
}
