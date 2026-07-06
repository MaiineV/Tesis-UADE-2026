using NUnit.Framework;
using Rollgeon.Dice.Throw;
using UnityEngine;

namespace Rollgeon.Dice.Tests
{
    [TestFixture]
    public class DiceFaceReaderTests
    {
        [Test]
        public void ReadTopFace_IdentityRotation_ReadsOne()
        {
            int face = DiceFaceReader.ReadTopFace(Quaternion.identity, out float dot);

            Assert.AreEqual(1, face, "+Y arriba = cara 1");
            Assert.AreEqual(1f, dot, 1e-4f);
        }

        // Cada rotación canónica que deja una cara distinta mirando arriba.
        [TestCase(180f, 0f, 0f, 6, TestName = "ReadTopFace_FlippedX_ReadsSix")]
        // Euler +Z rota de +X hacia +Y (left-handed): +90° deja la cara de +X (3) arriba.
        [TestCase(0f, 0f, 90f, 3, TestName = "ReadTopFace_RollTowardY_ReadsThree")]
        [TestCase(0f, 0f, -90f, 4, TestName = "ReadTopFace_RollAwayFromY_ReadsFour")]
        [TestCase(-90f, 0f, 0f, 2, TestName = "ReadTopFace_PitchBack_ReadsTwo")]
        [TestCase(90f, 0f, 0f, 5, TestName = "ReadTopFace_PitchForward_ReadsFive")]
        public void ReadTopFace_CanonicalRotations(float x, float y, float z, int expected)
        {
            var rot = Quaternion.Euler(x, y, z);

            int face = DiceFaceReader.ReadTopFace(rot, out float dot);

            Assert.AreEqual(expected, face);
            Assert.Greater(dot, 0.999f, "cara perfectamente plana");
        }

        [Test]
        public void ReadTopFace_YawDoesNotChangeTopFace()
        {
            // Girar sobre el eje vertical no cambia qué cara mira arriba.
            for (int yaw = 0; yaw < 360; yaw += 45)
            {
                int face = DiceFaceReader.ReadTopFace(Quaternion.Euler(0f, yaw, 0f), out _);
                Assert.AreEqual(1, face, $"yaw={yaw}");
            }
        }

        [Test]
        public void IsCocked_45DegreeTilt_IsCocked()
        {
            // A 45° entre dos caras, el mejor dot es ~0.707 — dado de canto.
            DiceFaceReader.ReadTopFace(Quaternion.Euler(45f, 0f, 0f), out float dot);

            Assert.IsTrue(DiceFaceReader.IsCocked(dot, threshold: 0.92f));
            Assert.AreEqual(0.7071f, dot, 1e-3f);
        }

        [Test]
        public void IsCocked_SlightTilt_IsNotCocked()
        {
            DiceFaceReader.ReadTopFace(Quaternion.Euler(8f, 0f, 0f), out float dot);
            Assert.IsFalse(DiceFaceReader.IsCocked(dot, threshold: 0.92f));
        }

        [Test]
        public void SnapToNearestFace_TiltedDie_BecomesFlat_KeepingDominantFace()
        {
            // Inclinado 20° hacia adelante: la cara 1 sigue dominando.
            var tilted = Quaternion.Euler(20f, 30f, 0f);
            int before = DiceFaceReader.ReadTopFace(tilted, out float dotBefore);
            Assert.Less(dotBefore, 0.999f);

            var snapped = DiceFaceReader.SnapToNearestFace(tilted);
            int after = DiceFaceReader.ReadTopFace(snapped, out float dotAfter);

            Assert.AreEqual(before, after, "el snap conserva la cara dominante");
            Assert.Greater(dotAfter, 0.9999f, "la cara queda perfectamente plana");
        }

        [Test]
        public void StandardD6_OppositeFacesSumSeven()
        {
            foreach (var (normal, face) in DiceFaceReader.StandardD6)
            {
                bool foundOpposite = false;
                foreach (var (n2, f2) in DiceFaceReader.StandardD6)
                {
                    if (Vector3.Dot(normal, n2) < -0.99f)
                    {
                        Assert.AreEqual(7, face + f2, $"cara {face} vs opuesta {f2}");
                        foundOpposite = true;
                    }
                }
                Assert.IsTrue(foundOpposite);
            }
        }
    }
}
