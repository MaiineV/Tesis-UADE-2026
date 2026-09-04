using NUnit.Framework;
using UnityEngine;

namespace Rollgeon.Rendering.Tests
{
    public sealed class PedestalItemFloatMathTests
    {
        private const float Tau = Mathf.PI * 2f;

        // ====================================================================
        // PhaseFromWorldXZ
        // ====================================================================

        [Test]
        public void PhaseFromWorldXZ_IsWithinOneTurn()
        {
            for (float x = -20f; x <= 20f; x += 1.7f)
            for (float z = -20f; z <= 20f; z += 2.3f)
            {
                float phase = PedestalItemFloatMath.PhaseFromWorldXZ(x, z);
                Assert.GreaterOrEqual(phase, 0f, $"({x}, {z})");
                Assert.Less(phase, Tau, $"({x}, {z})");
            }
        }

        [Test]
        public void PhaseFromWorldXZ_IsDeterministic()
        {
            Assert.AreEqual(
                PedestalItemFloatMath.PhaseFromWorldXZ(3.5f, -7.25f),
                PedestalItemFloatMath.PhaseFromWorldXZ(3.5f, -7.25f));
        }

        [Test]
        public void PhaseFromWorldXZ_DiffersBetweenNearbyPedestals()
        {
            // El caso real: varios pedestales en la misma sala, separados ~2u.
            // Si les tocara la misma fase, los ítems flotarían sincronizados.
            float a = PedestalItemFloatMath.PhaseFromWorldXZ(0f, 0f);
            float b = PedestalItemFloatMath.PhaseFromWorldXZ(2f, 0f);
            float c = PedestalItemFloatMath.PhaseFromWorldXZ(4f, 0f);

            Assert.AreNotEqual(a, b);
            Assert.AreNotEqual(b, c);
            Assert.AreNotEqual(a, c);
        }

        // ====================================================================
        // VerticalOffset
        // ====================================================================

        [Test]
        public void VerticalOffset_StaysWithinAmplitude()
        {
            for (float t = 0f; t < 10f; t += 0.05f)
            {
                float y = PedestalItemFloatMath.VerticalOffset(t, 0.7f, 1.6f, 0.05f);
                Assert.LessOrEqual(Mathf.Abs(y), 0.05f + 1e-5f, $"t {t}");
            }
        }

        [Test]
        public void VerticalOffset_ZeroAmplitude_IsFlat()
        {
            Assert.AreEqual(0f, PedestalItemFloatMath.VerticalOffset(3.3f, 1.1f, 1.6f, 0f), 1e-6f);
        }

        [Test]
        public void VerticalOffset_OscillatesAroundRest()
        {
            // Media vuelta del seno más tarde, el offset es el opuesto: el ítem
            // vuelve a pasar por su posición de reposo en vez de derivar.
            float speed = 1.6f;
            float t = 1f;
            float half = Mathf.PI / speed;

            float a = PedestalItemFloatMath.VerticalOffset(t, 0f, speed, 0.05f);
            float b = PedestalItemFloatMath.VerticalOffset(t + half, 0f, speed, 0.05f);

            Assert.AreEqual(-a, b, 1e-5f);
        }

        [Test]
        public void VerticalOffset_DifferentPhases_AreNotInSync()
        {
            float a = PedestalItemFloatMath.VerticalOffset(1f, 0f, 1.6f, 0.05f);
            float b = PedestalItemFloatMath.VerticalOffset(1f, Mathf.PI, 1.6f, 0.05f);

            Assert.AreEqual(-a, b, 1e-5f);
        }
    }
}
