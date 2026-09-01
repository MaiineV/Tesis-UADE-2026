using NUnit.Framework;
using Rollgeon.UI.HUD.Breakdown;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Tests de <see cref="BreakdownFeelMath"/>: bordes de tier, ramps, pitch,
    /// intensidades y color heat de M.
    /// </summary>
    [TestFixture]
    public class BreakdownFeelMathTests
    {
        [TestCase(0, ExpectedResult = 0)]
        [TestCase(29, ExpectedResult = 0)]
        [TestCase(30, ExpectedResult = 1)]
        [TestCase(79, ExpectedResult = 1)]
        [TestCase(80, ExpectedResult = 2)]
        [TestCase(500, ExpectedResult = 2)]
        [TestCase(-5, ExpectedResult = 0)]
        public int TierForTotal_Bordes(int total) => BreakdownFeelMath.TierForTotal(total, 30, 80);

        [Test]
        public void SpeedRampFactor_PrimerDadoSinRecorte_YRespetaElPiso()
        {
            Assert.AreEqual(1f, BreakdownFeelMath.SpeedRampFactor(0, 0.12f, 0.5f));
            Assert.AreEqual(0.88f, BreakdownFeelMath.SpeedRampFactor(1, 0.12f, 0.5f), 1e-5f);
            Assert.AreEqual(0.5f, BreakdownFeelMath.SpeedRampFactor(10, 0.12f, 0.5f));
            // Ramp apagado → siempre tiempo completo.
            Assert.AreEqual(1f, BreakdownFeelMath.SpeedRampFactor(7, 0f, 0.5f));
        }

        /// <summary>
        /// Contrato de los defaults del step ramp (StepSpeedRampPerStep .07,
        /// StepSpeedFloor .45): primer step pleno, aceleración lineal, piso a ~8.
        /// </summary>
        [Test]
        public void SpeedRampFactor_DefaultsDelStepRamp_AceleranHastaElPiso()
        {
            Assert.AreEqual(1f, BreakdownFeelMath.SpeedRampFactor(0, 0.07f, 0.45f));
            Assert.AreEqual(0.65f, BreakdownFeelMath.SpeedRampFactor(5, 0.07f, 0.45f), 1e-5f);
            Assert.AreEqual(0.45f, BreakdownFeelMath.SpeedRampFactor(8, 0.07f, 0.45f), 1e-5f);
            Assert.AreEqual(0.45f, BreakdownFeelMath.SpeedRampFactor(20, 0.07f, 0.45f));
        }

        [Test]
        public void PitchForIndex_SubeYClampea()
        {
            Assert.AreEqual(1f, BreakdownFeelMath.PitchForIndex(0, 0.06f));
            Assert.AreEqual(1.12f, BreakdownFeelMath.PitchForIndex(2, 0.06f), 1e-5f);
            Assert.AreEqual(2f, BreakdownFeelMath.PitchForIndex(50, 0.06f));
            Assert.AreEqual(1f, BreakdownFeelMath.PitchForIndex(-3, 0.06f));
        }

        [Test]
        public void PunchIntensity01_ProporcionClampeada()
        {
            Assert.AreEqual(0.5f, BreakdownFeelMath.PunchIntensity01(10f, 20f), 1e-5f);
            Assert.AreEqual(1f, BreakdownFeelMath.PunchIntensity01(50f, 20f));
            // Final 0 no divide por cero: denominador mínimo 1.
            Assert.AreEqual(1f, BreakdownFeelMath.PunchIntensity01(3f, 0f));
            Assert.AreEqual(0.25f, BreakdownFeelMath.PunchIntensity01(-5f, 20f), 1e-5f);
        }

        [Test]
        public void Accumulate01_ProgresoYFinalNoPositivo()
        {
            Assert.AreEqual(0.5f, BreakdownFeelMath.Accumulate01(10f, 20f), 1e-5f);
            Assert.AreEqual(1f, BreakdownFeelMath.Accumulate01(30f, 20f));
            Assert.AreEqual(1f, BreakdownFeelMath.Accumulate01(5f, 0f));
        }

        [Test]
        public void HeatColor_NeutralWarmHot()
        {
            var neutral = new Color(0.5f, 0.5f, 0.5f);
            var warm = new Color(1f, 0.4f, 0.3f);
            var hot = new Color(1f, 0.2f, 0.1f);

            Assert.AreEqual(neutral, BreakdownFeelMath.HeatColor(1f, neutral, warm, hot));
            Assert.AreEqual(neutral, BreakdownFeelMath.HeatColor(0.5f, neutral, warm, hot));
            Assert.AreEqual(Color.Lerp(neutral, warm, 0.5f),
                BreakdownFeelMath.HeatColor(1.5f, neutral, warm, hot));
            Assert.AreEqual(warm, BreakdownFeelMath.HeatColor(2f, neutral, warm, hot));
            Assert.AreEqual(hot, BreakdownFeelMath.HeatColor(3f, neutral, warm, hot));
            // Sin overflow más allá de 3.
            Assert.AreEqual(hot, BreakdownFeelMath.HeatColor(9f, neutral, warm, hot));
        }

        [Test]
        public void IsFlaming_Umbral()
        {
            Assert.IsFalse(BreakdownFeelMath.IsFlaming(1.9f, 2f));
            Assert.IsTrue(BreakdownFeelMath.IsFlaming(2f, 2f));
        }

        [TestCase(2, ExpectedResult = 0)]
        [TestCase(3, ExpectedResult = 1)]
        [TestCase(4, ExpectedResult = 2)]
        [TestCase(5, ExpectedResult = 3)]
        [TestCase(6, ExpectedResult = 3)]
        [TestCase(0, ExpectedResult = 0)]
        public int ComboTier_PorCantidadDeDados(int count) => BreakdownFeelMath.ComboTier(count);

        [Test]
        public void ComboCelebrateIntensity_PisoYTecho()
        {
            // Arrange — umbrales default (30/80).
            // Act + Assert: el combo mínimo (2 dados, total chico) queda en el piso
            // visible; el máximo (5 dados, total grande) satura en 1.
            Assert.AreEqual(0.25f, BreakdownFeelMath.ComboCelebrateIntensity(5, 30, 80, 2), 1e-5f);
            Assert.AreEqual(1f, BreakdownFeelMath.ComboCelebrateIntensity(120, 30, 80, 5), 1e-5f);
        }

        [Test]
        public void ComboCelebrateIntensity_MonotonaEnTotalYEnCantidadDeDados()
        {
            // Arrange
            const int t1 = 30, t2 = 80;

            // Act
            float smallTotal = BreakdownFeelMath.ComboCelebrateIntensity(10, t1, t2, 3);
            float midTotal = BreakdownFeelMath.ComboCelebrateIntensity(40, t1, t2, 3);
            float bigTotal = BreakdownFeelMath.ComboCelebrateIntensity(100, t1, t2, 3);
            float pair = BreakdownFeelMath.ComboCelebrateIntensity(40, t1, t2, 2);
            float generala = BreakdownFeelMath.ComboCelebrateIntensity(40, t1, t2, 5);

            // Assert — más total y más dados nunca bajan la intensidad.
            Assert.Less(smallTotal, midTotal, "Subir de tier de total debe subir la intensidad.");
            Assert.Less(midTotal, bigTotal, "Subir de tier de total debe subir la intensidad.");
            Assert.Less(pair, generala, "Más dados contribuyentes deben subir la intensidad.");
        }

        // ------------------------------------------------------------------
        // FloorUnlessSkipping — piso de legibilidad del spinner (BUG-063)
        // ------------------------------------------------------------------

        [Test]
        public void FloorUnlessSkipping_BajoElPisoSinSkip_DevuelveElPiso()
        {
            Assert.AreEqual(0.35f, BreakdownFeelMath.FloorUnlessSkipping(0.05f, 0.35f, skipping: false));
        }

        [Test]
        public void FloorUnlessSkipping_SobreElPiso_Passthrough()
        {
            Assert.AreEqual(0.5f, BreakdownFeelMath.FloorUnlessSkipping(0.5f, 0.35f, skipping: false));
        }

        [Test]
        public void FloorUnlessSkipping_ConSkip_IgnoraElPiso()
        {
            // El jugador pidió velocidad explícitamente: la secuencia se comprime
            // igual que siempre.
            Assert.AreEqual(0.05f, BreakdownFeelMath.FloorUnlessSkipping(0.05f, 0.35f, skipping: true));
        }

        [Test]
        public void ComboCelebrateIntensity_SiempreEnRango01()
        {
            foreach (int total in new[] { -10, 0, 29, 30, 79, 80, 5000 })
                foreach (int count in new[] { 0, 2, 3, 5, 9 })
                {
                    float value = BreakdownFeelMath.ComboCelebrateIntensity(total, 30, 80, count);
                    Assert.That(value, Is.InRange(0f, 1f),
                        $"Intensidad fuera de rango para total={total}, count={count}.");
                }
        }
    }
}
