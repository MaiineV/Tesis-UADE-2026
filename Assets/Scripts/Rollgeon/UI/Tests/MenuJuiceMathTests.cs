using NUnit.Framework;
using Rollgeon.UI.Menu;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    [TestFixture]
    public class MenuJuiceMathTests
    {
        private static readonly Color PaletteText = new Color32(0xE7, 0xE3, 0xE2, 0xFF);
        private static readonly Color PaletteAccent = new Color32(0xE0, 0xC0, 0xA9, 0xFF);
        private static readonly Color PaletteMuted = new Color32(0x5F, 0x73, 0x7A, 0xFF);

        [Test]
        public void SmoothLerpFactor_WithPositiveSpeedAndDt_ReturnsFactorBetweenZeroAndOne()
        {
            // Arrange
            const float speed = 8f;
            const float dt = 1f / 60f;

            // Act
            float factor = MenuJuiceMath.SmoothLerpFactor(speed, dt);

            // Assert
            Assert.Greater(factor, 0f);
            Assert.Less(factor, 1f);
        }

        [Test]
        public void SmoothLerpFactor_WithLargerDt_ReturnsLargerFactor()
        {
            // Arrange
            const float speed = 8f;

            // Act
            float slow = MenuJuiceMath.SmoothLerpFactor(speed, 1f / 120f);
            float fast = MenuJuiceMath.SmoothLerpFactor(speed, 1f / 30f);

            // Assert
            Assert.Greater(fast, slow);
        }

        [Test]
        public void SmoothLerpFactor_WithZeroDt_ReturnsZero()
        {
            // Arrange & Act
            float factor = MenuJuiceMath.SmoothLerpFactor(10f, 0f);

            // Assert
            Assert.AreEqual(0f, factor, 1e-6f);
        }

        [Test]
        public void PastelCycle_AcrossFullPeriod_StaysWithinPaletteChannelBounds()
        {
            // Arrange
            float minR = Mathf.Min(PaletteText.r, PaletteAccent.r, PaletteMuted.r);
            float maxR = Mathf.Max(PaletteText.r, PaletteAccent.r, PaletteMuted.r);
            float minG = Mathf.Min(PaletteText.g, PaletteAccent.g, PaletteMuted.g);
            float maxG = Mathf.Max(PaletteText.g, PaletteAccent.g, PaletteMuted.g);
            float minB = Mathf.Min(PaletteText.b, PaletteAccent.b, PaletteMuted.b);
            float maxB = Mathf.Max(PaletteText.b, PaletteAccent.b, PaletteMuted.b);
            const float tolerance = 1e-4f;

            // Act & Assert
            for (int i = 0; i <= 100; i++)
            {
                float time = i * (2f * Mathf.PI / 100f);
                Color result = MenuJuiceMath.PastelCycle(time, 1f, PaletteText, PaletteAccent, PaletteMuted);

                Assert.GreaterOrEqual(result.r, minR - tolerance, $"R fuera de rango en t={time}");
                Assert.LessOrEqual(result.r, maxR + tolerance, $"R fuera de rango en t={time}");
                Assert.GreaterOrEqual(result.g, minG - tolerance, $"G fuera de rango en t={time}");
                Assert.LessOrEqual(result.g, maxG + tolerance, $"G fuera de rango en t={time}");
                Assert.GreaterOrEqual(result.b, minB - tolerance, $"B fuera de rango en t={time}");
                Assert.LessOrEqual(result.b, maxB + tolerance, $"B fuera de rango en t={time}");
            }
        }

        [Test]
        public void PastelCycle_WithSpeedZero_IsConstantOverTime()
        {
            // Arrange & Act
            Color first = MenuJuiceMath.PastelCycle(0f, 0f, PaletteText, PaletteAccent, PaletteMuted);
            Color later = MenuJuiceMath.PastelCycle(123.4f, 0f, PaletteText, PaletteAccent, PaletteMuted);

            // Assert
            Assert.AreEqual(first, later);
        }

        [Test]
        public void PastelCycle_IsPeriodicOverTwoPi()
        {
            // Arrange
            const float speed = 1f;
            const float t0 = 0.7f;

            // Act
            Color atT0 = MenuJuiceMath.PastelCycle(t0, speed, PaletteText, PaletteAccent, PaletteMuted);
            Color atT0PlusPeriod = MenuJuiceMath.PastelCycle(
                t0 + 2f * Mathf.PI, speed, PaletteText, PaletteAccent, PaletteMuted);

            // Assert
            Assert.AreEqual(atT0.r, atT0PlusPeriod.r, 1e-4f);
            Assert.AreEqual(atT0.g, atT0PlusPeriod.g, 1e-4f);
            Assert.AreEqual(atT0.b, atT0PlusPeriod.b, 1e-4f);
        }

        [Test]
        public void StaggerDelay_WithIndexThreeAndStepPointFifteen_ReturnsPointFortyFive()
        {
            // Arrange & Act
            float delay = MenuJuiceMath.StaggerDelay(3, 0.15f);

            // Assert
            Assert.AreEqual(0.45f, delay, 1e-6f);
        }

        [Test]
        public void StaggerDelay_WithIndexZero_ReturnsZero()
        {
            // Arrange & Act
            float delay = MenuJuiceMath.StaggerDelay(0, 0.15f);

            // Assert
            Assert.AreEqual(0f, delay, 1e-6f);
        }

        [Test]
        public void PulseScale_AcrossFullPeriod_StaysWithinAmplitudeBounds()
        {
            // Arrange
            const float frequency = 6f;
            const float amplitude = 0.15f;

            // Act & Assert
            for (int i = 0; i <= 100; i++)
            {
                float time = i * (2f * Mathf.PI / frequency / 100f);
                float scale = MenuJuiceMath.PulseScale(time, frequency, amplitude);

                Assert.GreaterOrEqual(scale, 1f - amplitude - 1e-5f);
                Assert.LessOrEqual(scale, 1f + amplitude + 1e-5f);
            }
        }

        [Test]
        public void UnderlineTargetWidth_WithTextWidthAndPad_ReturnsSum()
        {
            // Arrange & Act
            float width = MenuJuiceMath.UnderlineTargetWidth(80f, 12f);

            // Assert
            Assert.AreEqual(92f, width, 1e-6f);
        }
    }
}
