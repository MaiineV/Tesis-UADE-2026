using NUnit.Framework;

namespace Rollgeon.Chests.Tests
{
    [TestFixture]
    public class ChestMimicHintTests
    {
        [Test]
        public void EvaluateAngle01_ReturnsZero_AtStart()
        {
            // Arrange + Act
            float angle = ChestMimicHint.EvaluateAngle01(0f);

            // Assert
            Assert.AreEqual(0f, angle, 1e-4f);
        }

        [Test]
        public void EvaluateAngle01_ReturnsZero_AtEnd()
        {
            // Arrange + Act — el damping (1 - t) garantiza que el twitch termina
            // exactamente en la pose base, sin snap visible.
            float angle = ChestMimicHint.EvaluateAngle01(1f);

            // Assert
            Assert.AreEqual(0f, angle, 1e-4f);
        }

        [Test]
        public void EvaluateAngle01_Oscillates_MidTwitch()
        {
            // Arrange + Act — pico de la primera oscilación (~1/12 del ciclo de 3 vueltas).
            float angle = ChestMimicHint.EvaluateAngle01(1f / 12f);

            // Assert
            Assert.Greater(System.Math.Abs(angle), 0.5f);
        }

        [Test]
        public void EvaluateAngle01_ClampsInput_OutsideRange()
        {
            // Arrange + Act
            float below = ChestMimicHint.EvaluateAngle01(-1f);
            float above = ChestMimicHint.EvaluateAngle01(2f);

            // Assert
            Assert.AreEqual(0f, below, 1e-4f);
            Assert.AreEqual(0f, above, 1e-4f);
        }
    }
}
