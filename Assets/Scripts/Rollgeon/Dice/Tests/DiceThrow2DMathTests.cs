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
        public void BounceInRect_NormalPointsInward_AndDiagonalOnCorner()
        {
            // Arrange
            var rect = new Rect(-100f, -100f, 200f, 200f);
            var posRight = new Vector2(120f, 0f);
            var velRight = new Vector2(300f, 0f);
            var posCorner = new Vector2(120f, 120f);
            var velCorner = new Vector2(300f, 300f);

            // Act
            DiceThrow2DMath.BounceInRect(ref posRight, ref velRight, rect, 10f, 0.5f, out var nRight);
            DiceThrow2DMath.BounceInRect(ref posCorner, ref velCorner, rect, 10f, 0.5f, out var nCorner);

            // Assert
            Assert.AreEqual(Vector2.left, nRight, "borde derecho: normal hacia adentro (-x)");
            Assert.AreEqual(1f, nCorner.magnitude, 1e-3f, "la normal de esquina viene normalizada");
            Assert.Less(nCorner.x, 0f, "esquina superior-derecha: componente -x");
            Assert.Less(nCorner.y, 0f, "esquina superior-derecha: componente -y");
        }

        [Test]
        public void DragIntent_MovementBeyondSlop_IsDrag()
        {
            // Arrange
            var press = new Vector2(10f, 10f);
            var moved = new Vector2(10f, 30f); // 20px > slop 14

            // Act + Assert
            Assert.IsTrue(DiceThrow2DMath.DragIntent(press, moved, heldSeconds: 0.01f,
                slopPixels: 14f, clickSeconds: 0.22f));
        }

        [Test]
        public void DragIntent_HoldBeyondWindow_IsDrag_EvenWithoutMoving()
        {
            var press = new Vector2(10f, 10f);

            Assert.IsTrue(DiceThrow2DMath.DragIntent(press, press, heldSeconds: 0.25f,
                slopPixels: 14f, clickSeconds: 0.22f));
        }

        [Test]
        public void DragIntent_QuickStillPress_IsClick()
        {
            var press = new Vector2(10f, 10f);
            var jitter = new Vector2(14f, 14f); // ~5.7px < slop

            Assert.IsFalse(DiceThrow2DMath.DragIntent(press, jitter, heldSeconds: 0.1f,
                slopPixels: 14f, clickSeconds: 0.22f),
                "un press corto con jitter chico sigue siendo click (selección)");
        }

        [Test]
        public void ResolveDiePair_HeadOn_SwapsNormalVelocities_AndSeparates()
        {
            // Arrange: A va hacia B (quieto en vuelo), superpuestos 4px.
            var posA = new Vector2(0f, 0f);
            var velA = new Vector2(400f, 0f);
            var posB = new Vector2(36f, 0f); // radio 20 → minDist 40 → overlap 4
            var velB = Vector2.zero;

            // Act
            float approach = DiceThrow2DMath.ResolveDiePair(ref posA, ref velA, ref posB, ref velB,
                radius: 20f, restitution: 1f);

            // Assert: elástico perfecto de masas iguales = intercambio total.
            Assert.AreEqual(400f, approach, 1e-3f, "velocidad de aproximación reportada");
            Assert.AreEqual(0f, velA.x, 1e-3f, "A cede toda su componente normal");
            Assert.AreEqual(400f, velB.x, 1e-3f, "B la recibe entera");
            Assert.AreEqual(40f, posB.x - posA.x, 1e-3f, "separados exactamente a 2·radio");
        }

        [Test]
        public void ResolveDiePair_Separating_NoImpulse()
        {
            // Arrange: superpuestos pero alejándose — separar sin re-impulsar.
            var posA = new Vector2(0f, 0f);
            var velA = new Vector2(-100f, 0f);
            var posB = new Vector2(30f, 0f);
            var velB = new Vector2(100f, 0f);

            // Act
            float approach = DiceThrow2DMath.ResolveDiePair(ref posA, ref velA, ref posB, ref velB, 20f, 1f);

            // Assert
            Assert.AreEqual(0f, approach, "sin aproximación no hay impacto que sonar");
            Assert.AreEqual(-100f, velA.x, 1e-3f, "velocidades intactas");
            Assert.AreEqual(100f, velB.x, 1e-3f);
            Assert.AreEqual(40f, posB.x - posA.x, 1e-3f, "pero la superposición sí se separa");
        }

        [Test]
        public void ResolveDiePair_Apart_IsNoOp()
        {
            var posA = Vector2.zero;
            var velA = new Vector2(400f, 0f);
            var posB = new Vector2(100f, 0f);
            var velB = Vector2.zero;

            float approach = DiceThrow2DMath.ResolveDiePair(ref posA, ref velA, ref posB, ref velB, 20f, 1f);

            Assert.AreEqual(0f, approach);
            Assert.AreEqual(Vector2.zero, posA);
            Assert.AreEqual(new Vector2(100f, 0f), posB);
        }

        [Test]
        public void ResolveDieStatic_FlyerBouncesBack_StaticGetsBoundedShove()
        {
            // Arrange: dado en vuelo entra de lleno a uno asentado.
            var posFly = new Vector2(0f, 0f);
            var velFly = new Vector2(500f, 0f);
            var posStill = new Vector2(30f, 0f);

            // Act
            float approach = DiceThrow2DMath.ResolveDieStatic(ref posFly, ref velFly, ref posStill,
                radius: 20f, restitution: 0.8f, shovePerSpeed: 0.04f, maxShove: 25f);

            // Assert
            Assert.AreEqual(500f, approach, 1e-3f);
            Assert.AreEqual(-400f, velFly.x, 1e-3f, "reflejo contra masa pesada: -(v·e) sobre la normal");
            Assert.AreEqual(50f, posStill.x, 1e-3f, "empujón = min(500·0.04, 25) = 20px");
            Assert.Less(posFly.x, posStill.x - 39.9f, "el que vuela cedió toda la superposición");
        }

        [Test]
        public void SeparateOverlap_SplitsEvenly_AndReportsContact()
        {
            // Arrange: dos asentados pisándose 10px.
            var posA = new Vector2(0f, 0f);
            var posB = new Vector2(30f, 0f);

            // Act
            bool touched = DiceThrow2DMath.SeparateOverlap(ref posA, ref posB, radius: 20f);

            // Assert
            Assert.IsTrue(touched);
            Assert.AreEqual(-5f, posA.x, 1e-3f, "mitad de la superposición para cada lado");
            Assert.AreEqual(35f, posB.x, 1e-3f);
            Assert.IsFalse(DiceThrow2DMath.SeparateOverlap(ref posA, ref posB, 20f),
                "ya separados: no-op");
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
