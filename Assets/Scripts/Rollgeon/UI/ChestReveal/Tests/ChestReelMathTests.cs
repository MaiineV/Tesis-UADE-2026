using NUnit.Framework;

namespace Rollgeon.UI.ChestReveal.Tests
{
    [TestFixture]
    public class ChestReelMathTests
    {
        private const float Width = 96f;
        private const float Spacing = 8f;

        [Test]
        public void CellCenterX_ShouldBeHalfWidth_AtIndexZero()
        {
            Assert.AreEqual(48f, ChestReelMath.CellCenterX(0, Width, Spacing), 0.001f);
        }

        [Test]
        public void CellCenterX_ShouldAdvanceByPitch_PerIndex()
        {
            float delta = ChestReelMath.CellCenterX(5, Width, Spacing)
                          - ChestReelMath.CellCenterX(4, Width, Spacing);
            Assert.AreEqual(Width + Spacing, delta, 0.001f);
        }

        [Test]
        public void TargetOffset_ShouldNeverLandOutsideWinnerCell_AtJitterExtremes()
        {
            // Arrange — jitter en ambos extremos (y más allá: se clampea).
            const int winner = 36;
            const int total = 40;
            foreach (float jitter in new[] { -1.5f, -1f, -0.5f, 0f, 0.5f, 1f, 1.5f })
            {
                // Act
                float offset = ChestReelMath.TargetOffset(winner, Width, Spacing, jitter, maxJitter01: 0.35f);
                int landed = ChestReelMath.CellIndexAtOffset(offset, Width, Spacing, total);

                // Assert
                Assert.AreEqual(winner, landed, $"jitter={jitter} aterrizó fuera del ganador");
            }
        }

        [Test]
        public void CellIndexAtOffset_ShouldBeMonotonicNonDecreasing()
        {
            int previous = 0;
            for (float offset = 0f; offset < 4000f; offset += 7f)
            {
                int index = ChestReelMath.CellIndexAtOffset(offset, Width, Spacing, 40);
                Assert.GreaterOrEqual(index, previous);
                previous = index;
            }
        }

        [Test]
        public void CellIndexAtOffset_ShouldClampToLastCell()
        {
            Assert.AreEqual(39, ChestReelMath.CellIndexAtOffset(999999f, Width, Spacing, 40));
            Assert.AreEqual(0, ChestReelMath.CellIndexAtOffset(-50f, Width, Spacing, 40));
        }

        [Test]
        public void PickWinnerIndex_ShouldRespectBounds()
        {
            var rng = new System.Random(77);
            for (int i = 0; i < 200; i++)
            {
                int winner = ChestReelMath.PickWinnerIndex(totalCells: 40, minSpinCells: 28, rng);
                Assert.GreaterOrEqual(winner, 28);
                Assert.Less(winner, 40);
            }
        }

        [Test]
        public void PickWinnerIndex_ShouldReturnZero_ForSingleCell()
        {
            Assert.AreEqual(0, ChestReelMath.PickWinnerIndex(1, 5, new System.Random(1)));
        }

        [Test]
        public void Decelerate01_ShouldHitEndpoints_AndBeMonotonic()
        {
            Assert.AreEqual(0f, ChestReelMath.Decelerate01(0f), 0.0001f);
            Assert.AreEqual(1f, ChestReelMath.Decelerate01(1f), 0.0001f);

            float previous = 0f;
            for (float t = 0f; t <= 1f; t += 0.01f)
            {
                float value = ChestReelMath.Decelerate01(t);
                Assert.GreaterOrEqual(value, previous);
                previous = value;
            }
        }
    }
}
