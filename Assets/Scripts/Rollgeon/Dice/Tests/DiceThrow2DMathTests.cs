using NUnit.Framework;
using Rollgeon.Dice.Throw;
using UnityEngine;

namespace Rollgeon.Dice.Tests
{
    [TestFixture]
    public class DiceThrow2DMathTests
    {
        private const float Dt = 1f / 60f;

        [Test]
        public void SpringStep_ConvergesTowardTarget()
        {
            // Arrange
            var pos = Vector2.zero;
            var vel = Vector2.zero;
            var target = new Vector2(100f, 50f);

            // Act: simular 2 segundos de spring con el tuning default.
            for (int i = 0; i < 120; i++)
                pos = DiceThrow2DMath.SpringStep(pos, ref vel, target, 250f, 18f, Dt);

            // Assert
            Assert.Less((pos - target).magnitude, 5f, "el spring debe converger al target");
            Assert.Less(vel.magnitude, 50f, "la velocidad debe amortiguarse");
        }

        [Test]
        public void FlightStep_DragReducesSpeedOverTime()
        {
            var pos = Vector2.zero;
            var vel = new Vector2(1000f, 0f);
            float initial = vel.magnitude;

            for (int i = 0; i < 60; i++)
                pos = DiceThrow2DMath.FlightStep(pos, ref vel, drag: 1f, Dt);

            Assert.Less(vel.magnitude, initial * 0.6f, "1 segundo de drag=1 debe frenar sensiblemente");
            Assert.Greater(pos.x, 0f, "el dado avanzó en la dirección del vuelo");
        }

        [Test]
        public void BounceInRect_ReflectsVelocity_AndClampsInside()
        {
            var rect = new Rect(-100f, -100f, 200f, 200f);
            var pos = new Vector2(120f, 0f); // fuera por la derecha
            var vel = new Vector2(300f, 40f);

            bool bounced = DiceThrow2DMath.BounceInRect(ref pos, ref vel, rect, halfSize: 10f, restitution: 0.5f);

            Assert.IsTrue(bounced);
            Assert.AreEqual(90f, pos.x, 1e-3f, "clampeado al borde interior (xMax - halfSize)");
            Assert.AreEqual(-150f, vel.x, 1e-3f, "reflejado con restitution 0.5");
            Assert.AreEqual(40f, vel.y, 1e-3f, "la componente que no rebota no cambia");
        }

        [Test]
        public void BounceInRect_InsideRect_NoChange()
        {
            var rect = new Rect(-100f, -100f, 200f, 200f);
            var pos = new Vector2(10f, -20f);
            var vel = new Vector2(50f, 60f);

            bool bounced = DiceThrow2DMath.BounceInRect(ref pos, ref vel, rect, 10f, 0.5f);

            Assert.IsFalse(bounced);
            Assert.AreEqual(new Vector2(10f, -20f), pos);
            Assert.AreEqual(new Vector2(50f, 60f), vel);
        }

        [Test]
        public void SmoothVelocity_ApproachesInstantaneous()
        {
            var smoothed = Vector2.zero;
            var target = new Vector2(900f, 0f);

            // ~5 taus de convergencia (tau=0.05 → 0.25s a 60fps = 15 frames).
            for (int i = 0; i < 15; i++)
                smoothed = DiceThrow2DMath.SmoothVelocity(smoothed, target, tau: 0.05f, Dt);

            Assert.Greater(smoothed.x, target.x * 0.9f, "tras ~5·tau debe estar >90% del valor");
        }

        [Test]
        public void SettleTick_RequiresSustainedLowSpeed_AndResetsOnSpike()
        {
            float held = 0f;

            // 0.1s por debajo del umbral — todavía no asienta (hold 0.15s).
            for (int i = 0; i < 6; i++)
                Assert.IsFalse(DiceThrow2DMath.SettleTick(10f, 25f, 0.15f, Dt, ref held));

            // Pico de velocidad: el contador se resetea.
            Assert.IsFalse(DiceThrow2DMath.SettleTick(100f, 25f, 0.15f, Dt, ref held));
            Assert.AreEqual(0f, held);

            // 0.15s sostenidos → settle.
            bool settled = false;
            for (int i = 0; i < 10 && !settled; i++)
                settled = DiceThrow2DMath.SettleTick(10f, 25f, 0.15f, Dt, ref held);
            Assert.IsTrue(settled);
        }
    }
}
