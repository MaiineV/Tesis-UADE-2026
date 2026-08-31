using NUnit.Framework;
using Rollgeon.Items;

namespace Rollgeon.UI.ChestReveal.Tests
{
    [TestFixture]
    public class ChestRevealFeelMathTests
    {
        [Test]
        public void Intensity01_ShouldBeStrictlyIncreasing_ByRarity()
        {
            // Arrange
            float common = ChestRevealFeelMath.Intensity01(ItemRarity.Common);
            float uncommon = ChestRevealFeelMath.Intensity01(ItemRarity.Uncommon);
            float rare = ChestRevealFeelMath.Intensity01(ItemRarity.Rare);
            float legendary = ChestRevealFeelMath.Intensity01(ItemRarity.Legendary);

            // Assert — Common apenas se siente, Legendary es el techo exacto.
            Assert.Less(common, uncommon);
            Assert.Less(uncommon, rare);
            Assert.Less(rare, legendary);
            Assert.Greater(common, 0f);
            Assert.AreEqual(1f, legendary);
        }

        [Test]
        public void Intensity01_God_ShouldShareTheCeilingWithLegendary()
        {
            // God no puede ser "más que 1" — el rango del sistema de knobs es 0..1
            // y Legendary ya es el techo. Antes del fix, God caía en el default:
            // del switch y volvía la intensidad de Common (0.15f) en vez de esto.
            Assert.AreEqual(1f, ChestRevealFeelMath.Intensity01(ItemRarity.God));
        }

        [Test]
        public void Knob_ShouldClampIntensity_OutsideUnitRange()
        {
            // Act + Assert
            Assert.AreEqual(2f, ChestRevealFeelMath.Knob(2f, 8f, -0.5f), 0.001f);
            Assert.AreEqual(8f, ChestRevealFeelMath.Knob(2f, 8f, 1.5f), 0.001f);
            Assert.AreEqual(5f, ChestRevealFeelMath.Knob(2f, 8f, 0.5f), 0.001f);
        }

        [Test]
        public void TickPitch_ShouldHitEndpoints_AtProgressExtremes()
        {
            // Act + Assert
            Assert.AreEqual(0.9f, ChestRevealFeelMath.TickPitch(0f, 0.9f, 1.5f), 0.001f);
            Assert.AreEqual(1.5f, ChestRevealFeelMath.TickPitch(1f, 0.9f, 1.5f), 0.001f);
        }

        [Test]
        public void TickPitch_ShouldBeMonotonic_InProgress()
        {
            // Arrange
            float previous = float.MinValue;

            // Act + Assert
            for (float t = 0f; t <= 1.001f; t += 0.1f)
            {
                float pitch = ChestRevealFeelMath.TickPitch(t, 0.9f, 1.5f);
                Assert.GreaterOrEqual(pitch, previous, $"pitch retrocedió en t={t}");
                previous = pitch;
            }
        }

        [Test]
        public void TickPitch_ShouldClampToSafeRange_WithExtremeSettings()
        {
            // Act + Assert — el rango sano de PlaySfx2D es 0.5..2.
            Assert.AreEqual(2f, ChestRevealFeelMath.TickPitch(1f, 1f, 5f), 0.001f);
            Assert.AreEqual(0.5f, ChestRevealFeelMath.TickPitch(0f, 0.1f, 1f), 0.001f);
        }

        [Test]
        public void NextTickTime_ShouldShrinkInterval_WithGameSpeed()
        {
            // Arrange
            const float now = 10f;
            const float interval = 0.04f;

            // Act
            float atX1 = ChestRevealFeelMath.NextTickTime(now, interval, 1);
            float atX4 = ChestRevealFeelMath.NextTickTime(now, interval, 4);

            // Assert — a x4 el limiter cede, pero nunca por debajo de "ahora".
            Assert.AreEqual(now + interval, atX1, 0.0001f);
            Assert.AreEqual(now + interval / 4f, atX4, 0.0001f);
            Assert.Greater(atX4, now);
        }

        [Test]
        public void NextTickTime_ShouldKeepMinimumFloor_WithDegenerateInputs()
        {
            // Act
            float next = ChestRevealFeelMath.NextTickTime(5f, 0f, 100);

            // Assert — intervalo 0 o multiplier absurdo no pueden colapsar el limiter.
            Assert.Greater(next, 5f);
        }

        [Test]
        public void HitstopAllowed_ShouldBeLegendaryAndAbove()
        {
            // Renombrado de "LegendaryOnly": con God agregado, HitstopAllowed
            // compara por rango (>=) — el hitstop del landing es "para el tier
            // máximo", y eso ya no es un único valor.
            Assert.IsFalse(ChestRevealFeelMath.HitstopAllowed(ItemRarity.Common));
            Assert.IsFalse(ChestRevealFeelMath.HitstopAllowed(ItemRarity.Uncommon));
            Assert.IsFalse(ChestRevealFeelMath.HitstopAllowed(ItemRarity.Rare));
            Assert.IsTrue(ChestRevealFeelMath.HitstopAllowed(ItemRarity.Legendary));
            Assert.IsTrue(ChestRevealFeelMath.HitstopAllowed(ItemRarity.God));
        }

        [Test]
        public void DuckAllowed_ShouldBeRareAndAbove()
        {
            // Act + Assert
            Assert.IsFalse(ChestRevealFeelMath.DuckAllowed(ItemRarity.Common));
            Assert.IsFalse(ChestRevealFeelMath.DuckAllowed(ItemRarity.Uncommon));
            Assert.IsTrue(ChestRevealFeelMath.DuckAllowed(ItemRarity.Rare));
            Assert.IsTrue(ChestRevealFeelMath.DuckAllowed(ItemRarity.Legendary));
            Assert.IsTrue(ChestRevealFeelMath.DuckAllowed(ItemRarity.God));
        }

        [Test]
        public void CountUpShown_ShouldHitExactEndpoints()
        {
            // Act + Assert
            Assert.AreEqual(0, ChestRevealFeelMath.CountUpShown(0f, 38));
            Assert.AreEqual(38, ChestRevealFeelMath.CountUpShown(1f, 38));
            Assert.AreEqual(38, ChestRevealFeelMath.CountUpShown(1.2f, 38));
            Assert.AreEqual(0, ChestRevealFeelMath.CountUpShown(-0.2f, 38));
        }

        [Test]
        public void CountUpShown_ShouldBeMonotonic_WithoutRoundingRegressions()
        {
            // Arrange
            const int total = 37;
            int previous = -1;

            // Act + Assert
            for (float t = 0f; t <= 1.001f; t += 0.01f)
            {
                int shown = ChestRevealFeelMath.CountUpShown(t, total);
                Assert.GreaterOrEqual(shown, previous, $"count-up retrocedió en t={t}");
                Assert.LessOrEqual(shown, total);
                previous = shown;
            }
        }

        [Test]
        public void CountUpShown_ShouldReturnZero_ForNonPositiveTotals()
        {
            // Act + Assert
            Assert.AreEqual(0, ChestRevealFeelMath.CountUpShown(0.5f, 0));
            Assert.AreEqual(0, ChestRevealFeelMath.CountUpShown(1f, -5));
        }
    }
}
