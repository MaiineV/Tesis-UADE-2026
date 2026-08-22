using NUnit.Framework;
using Rollgeon.UI.HUD;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Math pura del layout del minimapa: posiciones locales fijas (el contenedor es
    /// el que rota) + el ángulo del contenedor derivado del yaw de la cámara.
    /// Convención: North = (0,+1) arriba con yaw 0; al rotar la cámara, el contenedor
    /// gira para que lo que está frente a ella quede arriba.
    /// </summary>
    [TestFixture]
    public class MinimapLayoutTests
    {
        private const float Pitch = 35f;
        private const float Tolerance = 0.001f;

        [Test]
        public void CellPosition_NorthIsUp_EastIsRight()
        {
            AssertPos(MinimapLayout.CellPosition(new Vector2Int(0, 1), Pitch), 0f, Pitch);
            AssertPos(MinimapLayout.CellPosition(new Vector2Int(1, 0), Pitch), Pitch, 0f);
            AssertPos(MinimapLayout.CellPosition(new Vector2Int(0, 0), Pitch), 0f, 0f);
        }

        [Test]
        public void CellPosition_PitchScalesLinearly()
        {
            AssertPos(MinimapLayout.CellPosition(new Vector2Int(2, -1), 10f), 20f, -10f);
        }

        [Test]
        public void ContainerAngle_MatchesYaw_Clockwise()
        {
            // Yaw 90 (cámara al Este) ⇒ contenedor a +90: la celda Este (+x) rota
            // a arriba (+y) — rotación Z positiva en UI es antihoraria.
            Assert.AreEqual(90f, MinimapLayout.ContainerAngle(90f, 0f, clockwise: true), Tolerance);
            Assert.AreEqual(45f, MinimapLayout.ContainerAngle(45f, 0f, clockwise: true), Tolerance);
            Assert.AreEqual(0f, MinimapLayout.ContainerAngle(0f, 0f, clockwise: true), Tolerance);
        }

        [Test]
        public void ContainerAngle_CounterClockwise_FlipsSign()
        {
            // Perilla de calibración para el playtest.
            Assert.AreEqual(-90f, MinimapLayout.ContainerAngle(90f, 0f, clockwise: false), Tolerance);
        }

        [Test]
        public void ContainerAngle_ExtraDegrees_AddsPhase()
        {
            Assert.AreEqual(135f, MinimapLayout.ContainerAngle(90f, 45f, clockwise: true), Tolerance);
            Assert.AreEqual(-135f, MinimapLayout.ContainerAngle(90f, 45f, clockwise: false), Tolerance);
        }

        private static void AssertPos(Vector2 actual, float x, float y)
        {
            Assert.AreEqual(x, actual.x, Tolerance, "x");
            Assert.AreEqual(y, actual.y, Tolerance, "y");
        }
    }
}
