using NUnit.Framework;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Grid.Tests
{
    public class RenderTextureCursorTests
    {
        [Test]
        public void test_rtScale_identity_whenRtMatchesScreen_returnsSamePoint()
        {
            // Arrange
            var screen = new Vector2(960f, 540f);

            // Act
            var result = RenderTextureCursor.ScreenToRt(screen, 1920f, 1080f, 1920f, 1080f);

            // Assert
            Assert.AreEqual(960f, result.x, 1e-4f);
            Assert.AreEqual(540f, result.y, 1e-4f);
        }

        [Test]
        public void test_rtScale_halfResRt_halvesCoordinates()
        {
            // Arrange
            var screen = new Vector2(1000f, 800f);

            // Act — RT es la mitad de la pantalla en ambos ejes.
            var result = RenderTextureCursor.ScreenToRt(screen, 1920f, 1080f, 960f, 540f);

            // Assert
            Assert.AreEqual(500f, result.x, 1e-4f);
            Assert.AreEqual(400f, result.y, 1e-4f);
        }

        [Test]
        public void test_rtScale_nonSquareRt_scalesAxesIndependently()
        {
            // Arrange — esquina superior derecha de la pantalla mapea a la esquina del RT.
            var screen = new Vector2(1920f, 1080f);

            // Act
            var result = RenderTextureCursor.ScreenToRt(screen, 1920f, 1080f, 320f, 180f);

            // Assert
            Assert.AreEqual(320f, result.x, 1e-4f);
            Assert.AreEqual(180f, result.y, 1e-4f);
        }

        [Test]
        public void test_rtScale_zeroScreenDim_returnsZeroWithoutDivideByZero()
        {
            // Act — pantalla degenerada no debe producir NaN/Inf.
            var result = RenderTextureCursor.ScreenToRt(new Vector2(100f, 100f), 0f, 0f, 320f, 180f);

            // Assert
            Assert.AreEqual(Vector2.zero, result);
        }
    }
}
