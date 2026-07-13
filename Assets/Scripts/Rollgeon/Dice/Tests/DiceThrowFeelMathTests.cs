using NUnit.Framework;
using Rollgeon.Dice.Throw;
using UnityEngine;

namespace Rollgeon.Dice.Tests
{
    public class DiceThrowFeelMathTests
    {
        // ---- Intensity01 ----------------------------------------------------

        [Test]
        public void Intensity01_ClampsToUnitRange()
        {
            Assert.AreEqual(0f, DiceThrowFeelMath.Intensity01(0f, 100f));
            Assert.AreEqual(0.5f, DiceThrowFeelMath.Intensity01(50f, 100f), 1e-5f);
            Assert.AreEqual(1f, DiceThrowFeelMath.Intensity01(250f, 100f));
        }

        [Test]
        public void Intensity01_NoReferenceSpeed_IsFull()
        {
            Assert.AreEqual(1f, DiceThrowFeelMath.Intensity01(5f, 0f));
            Assert.AreEqual(1f, DiceThrowFeelMath.Intensity01(5f, -10f));
        }

        // ---- ImpactVolume / ImpactPitch -------------------------------------

        [Test]
        public void ImpactVolume_HasAudibleFloor()
        {
            Assert.AreEqual(0.25f, DiceThrowFeelMath.ImpactVolume(0f, 100f), 1e-5f);
            Assert.AreEqual(1f, DiceThrowFeelMath.ImpactVolume(100f, 100f), 1e-5f);
        }

        [Test]
        public void ImpactVolume_ScalesBetweenFloorAndFull()
        {
            float half = DiceThrowFeelMath.ImpactVolume(50f, 100f, floor: 0.2f);
            Assert.AreEqual(0.6f, half, 1e-5f);
        }

        [Test]
        public void ImpactPitch_RampsUpWithSpeed()
        {
            float soft = DiceThrowFeelMath.ImpactPitch(0f, 100f);
            float hard = DiceThrowFeelMath.ImpactPitch(100f, 100f);
            Assert.AreEqual(0.9f, soft, 1e-5f);
            Assert.AreEqual(1.2f, hard, 1e-5f);
            Assert.Less(soft, hard);
        }

        // ---- RattleInterval --------------------------------------------------

        [Test]
        public void RattleInterval_FastHandIsDenser()
        {
            float slow = DiceThrowFeelMath.RattleInterval(0f, 1000f);
            float fast = DiceThrowFeelMath.RattleInterval(1000f, 1000f);
            Assert.AreEqual(0.28f, slow, 1e-5f);
            Assert.AreEqual(0.08f, fast, 1e-5f);
            Assert.Greater(slow, fast);
        }

        [Test]
        public void RattleInterval_MinClampNeverReachesZero()
        {
            float interval = DiceThrowFeelMath.RattleInterval(9999f, 100f, min: 0f);
            Assert.GreaterOrEqual(interval, 0.01f);
        }

        // ---- SpinDecayStep ---------------------------------------------------

        [Test]
        public void SpinDecayStep_DecaysExponentially()
        {
            float vel = 720f;
            float after = DiceThrowFeelMath.SpinDecayStep(vel, decayPerSecond: 1f, dt: 1f);
            Assert.AreEqual(720f * Mathf.Exp(-1f), after, 1e-3f);
            Assert.Less(Mathf.Abs(after), Mathf.Abs(vel));
        }

        [Test]
        public void SpinDecayStep_IsFrameRateIndependent()
        {
            float whole = DiceThrowFeelMath.SpinDecayStep(360f, 2f, 0.5f);
            float halves = DiceThrowFeelMath.SpinDecayStep(
                DiceThrowFeelMath.SpinDecayStep(360f, 2f, 0.25f), 2f, 0.25f);
            Assert.AreEqual(whole, halves, 1e-3f);
        }

        [Test]
        public void SpinDecayStep_NoDecayOrNoTime_Unchanged()
        {
            Assert.AreEqual(500f, DiceThrowFeelMath.SpinDecayStep(500f, 0f, 0.1f));
            Assert.AreEqual(500f, DiceThrowFeelMath.SpinDecayStep(500f, 3f, 0f));
        }

        [Test]
        public void SpinDecayStep_PreservesSign()
        {
            Assert.Less(DiceThrowFeelMath.SpinDecayStep(-720f, 1f, 0.1f), 0f);
        }

        // ---- FlickAngularVelocity ---------------------------------------------

        [Test]
        public void FlickAngularVelocity_RightFlickSpinsClockwise()
        {
            // Horario en UI = z negativo.
            float spin = DiceThrowFeelMath.FlickAngularVelocity(new Vector2(1000f, 0f), 0.5f, 2000f);
            Assert.Less(spin, 0f);
        }

        [Test]
        public void FlickAngularVelocity_LeftFlickSpinsCounterClockwise()
        {
            float spin = DiceThrowFeelMath.FlickAngularVelocity(new Vector2(-1000f, 0f), 0.5f, 2000f);
            Assert.Greater(spin, 0f);
        }

        [Test]
        public void FlickAngularVelocity_ClampsToMax()
        {
            float spin = DiceThrowFeelMath.FlickAngularVelocity(new Vector2(-99999f, 0f), 1f, 1080f);
            Assert.AreEqual(1080f, spin, 1e-5f);
        }

        [Test]
        public void FlickAngularVelocity_NoClampWhenMaxIsZero()
        {
            float spin = DiceThrowFeelMath.FlickAngularVelocity(new Vector2(-4000f, 0f), 1f, 0f);
            Assert.AreEqual(4000f, spin, 1e-5f);
        }
    }
}
