using NUnit.Framework;
using Rollgeon.UI.HUD.Breakdown;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Tests de <see cref="SpinnerDrumMath"/>: endpoints de la proyección
    /// cilíndrica, simetría saliente/entrante, monotonía, clamp y ease.
    /// </summary>
    [TestFixture]
    public class SpinnerDrumMathTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void OutgoingScaleY_Endpoints_VanDeUnoACero()
        {
            Assert.AreEqual(1f, SpinnerDrumMath.OutgoingScaleY(0f), Tolerance);
            Assert.AreEqual(0f, SpinnerDrumMath.OutgoingScaleY(1f), Tolerance);
        }

        [Test]
        public void OutgoingOffsetY_Endpoints_VanDeCeroATravel()
        {
            Assert.AreEqual(0f, SpinnerDrumMath.OutgoingOffsetY(0f, 70f), Tolerance);
            Assert.AreEqual(70f, SpinnerDrumMath.OutgoingOffsetY(1f, 70f), Tolerance);
        }

        [Test]
        public void IncomingScaleY_Endpoints_VanDeCeroAUno()
        {
            Assert.AreEqual(0f, SpinnerDrumMath.IncomingScaleY(0f), Tolerance);
            Assert.AreEqual(1f, SpinnerDrumMath.IncomingScaleY(1f), Tolerance);
        }

        [Test]
        public void IncomingOffsetY_Endpoints_VanDeMenosTravelACero()
        {
            Assert.AreEqual(-70f, SpinnerDrumMath.IncomingOffsetY(0f, 70f), Tolerance);
            Assert.AreEqual(0f, SpinnerDrumMath.IncomingOffsetY(1f, 70f), Tolerance);
        }

        [TestCase(0f)]
        [TestCase(0.25f)]
        [TestCase(0.5f)]
        [TestCase(0.75f)]
        [TestCase(1f)]
        public void Drum_EntranteEsEspejoDelSaliente(float t)
        {
            Assert.AreEqual(SpinnerDrumMath.OutgoingScaleY(1f - t),
                SpinnerDrumMath.IncomingScaleY(t), Tolerance);
        }

        [Test]
        public void Drum_Monotonia_SalienteBajaYEntranteSube()
        {
            for (float t = 0.05f; t <= 1f; t += 0.05f)
            {
                float prev = t - 0.05f;
                Assert.Less(SpinnerDrumMath.OutgoingScaleY(t), SpinnerDrumMath.OutgoingScaleY(prev),
                    $"OutgoingScaleY no decrece en t={t}");
                Assert.Greater(SpinnerDrumMath.OutgoingOffsetY(t, 70f), SpinnerDrumMath.OutgoingOffsetY(prev, 70f),
                    $"OutgoingOffsetY no crece en t={t}");
                Assert.Greater(SpinnerDrumMath.IncomingScaleY(t), SpinnerDrumMath.IncomingScaleY(prev),
                    $"IncomingScaleY no crece en t={t}");
                Assert.Greater(SpinnerDrumMath.IncomingOffsetY(t, 70f), SpinnerDrumMath.IncomingOffsetY(prev, 70f),
                    $"IncomingOffsetY no crece en t={t}");
            }
        }

        [Test]
        public void Drum_ClampFueraDeRango_DevuelveEndpoints()
        {
            Assert.AreEqual(1f, SpinnerDrumMath.OutgoingScaleY(-1f), Tolerance);
            Assert.AreEqual(0f, SpinnerDrumMath.OutgoingScaleY(2f), Tolerance);
            Assert.AreEqual(0f, SpinnerDrumMath.IncomingScaleY(-1f), Tolerance);
            Assert.AreEqual(1f, SpinnerDrumMath.IncomingScaleY(2f), Tolerance);
            Assert.AreEqual(-70f, SpinnerDrumMath.IncomingOffsetY(-1f, 70f), Tolerance);
            Assert.AreEqual(70f, SpinnerDrumMath.OutgoingOffsetY(2f, 70f), Tolerance);
        }

        [Test]
        public void EaseSpin_EsDecelMonotonaConEndpointsExactos()
        {
            Assert.AreEqual(0f, SpinnerDrumMath.EaseSpin(0f), Tolerance);
            Assert.AreEqual(1f, SpinnerDrumMath.EaseSpin(1f), Tolerance);
            // Decel: a mitad de tiempo ya recorrió más de la mitad.
            Assert.Greater(SpinnerDrumMath.EaseSpin(0.5f), 0.5f);
            for (float t = 0.05f; t <= 1f; t += 0.05f)
            {
                Assert.GreaterOrEqual(SpinnerDrumMath.EaseSpin(t), SpinnerDrumMath.EaseSpin(t - 0.05f),
                    $"EaseSpin no es monótona en t={t}");
            }
        }

        [Test]
        public void Travel_MediaVentanaMasMedioSlot()
        {
            Assert.AreEqual(70f, SpinnerDrumMath.Travel(84f, 56f), Tolerance);
        }
    }
}
