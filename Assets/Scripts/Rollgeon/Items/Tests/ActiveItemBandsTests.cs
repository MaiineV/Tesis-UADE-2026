using NUnit.Framework;
using Rollgeon.Dice;
using Rollgeon.Items.Active;

namespace Rollgeon.Items.Tests
{
    /// <summary>
    /// Reparto de bandas por tercios proporcionales (GDD "Ítems Activos" §20). El doc da
    /// dos casos concretos que sirven de ancla: D6 reparte 1-2 / 3-4 / 5-6, y D4 reparte
    /// 1 cara negativa, 1 mixta y 2 positivas.
    /// </summary>
    [TestFixture]
    public sealed class ActiveItemBandsTests
    {
        // ------------------------------------------------------------------
        // D6 — el caso confirmado del GDD (ejemplo de Botas Inestables)
        // ------------------------------------------------------------------

        [TestCase(1, ActiveItemBand.Negative)]
        [TestCase(2, ActiveItemBand.Negative)]
        [TestCase(3, ActiveItemBand.Mixed)]
        [TestCase(4, ActiveItemBand.Mixed)]
        [TestCase(5, ActiveItemBand.Positive)]
        [TestCase(6, ActiveItemBand.Positive)]
        public void test_bands_d6_matchesTheGddMapping(int roll, ActiveItemBand expected)
        {
            // Act
            var band = ActiveItemBands.Resolve(roll, faces: 6);

            // Assert
            Assert.AreEqual(expected, band);
        }

        // ------------------------------------------------------------------
        // D4 — el remanente cae en la positiva, tal como advierte el GDD
        // ------------------------------------------------------------------

        [TestCase(1, ActiveItemBand.Negative)]
        [TestCase(2, ActiveItemBand.Mixed)]
        [TestCase(3, ActiveItemBand.Positive)]
        [TestCase(4, ActiveItemBand.Positive)]
        public void test_bands_d4_leavesTheRemainderInThePositiveBand(int roll, ActiveItemBand expected)
        {
            // Act
            var band = ActiveItemBands.Resolve(roll, faces: 4);

            // Assert — el GDD lo nombra: "D4: Negativa 1 cara, Mixta 1 cara, Positiva 2".
            Assert.AreEqual(expected, band);
        }

        // ------------------------------------------------------------------
        // Cobertura del rango de dados soportado (D4 a D20)
        // ------------------------------------------------------------------

        [TestCase(DiceType.D4)]
        [TestCase(DiceType.D6)]
        [TestCase(DiceType.D8)]
        [TestCase(DiceType.D10)]
        [TestCase(DiceType.D12)]
        [TestCase(DiceType.D20)]
        public void test_bands_everyFaceOfEveryDie_fallsInExactlyOneBand(DiceType die)
        {
            // Arrange
            int faces = die.MaxFace();
            var neg = ActiveItemBands.RangeOf(ActiveItemBand.Negative, faces);
            var mix = ActiveItemBands.RangeOf(ActiveItemBand.Mixed, faces);
            var pos = ActiveItemBands.RangeOf(ActiveItemBand.Positive, faces);

            // Act + Assert — las tres bandas cubren [1, N] sin huecos ni solapamientos.
            Assert.AreEqual(1, neg.Min, "la negativa arranca en 1");
            Assert.AreEqual(neg.Max + 1, mix.Min, "la mixta arranca justo despues de la negativa");
            Assert.AreEqual(mix.Max + 1, pos.Min, "la positiva arranca justo despues de la mixta");
            Assert.AreEqual(faces, pos.Max, "la positiva termina en la cara mas alta");

            for (int roll = 1; roll <= faces; roll++)
            {
                var band = ActiveItemBands.Resolve(roll, die);
                var range = ActiveItemBands.RangeOf(band, faces);
                Assert.IsTrue(roll >= range.Min && roll <= range.Max,
                    $"d{faces}: la tirada {roll} cayo en {band}, cuyo rango es [{range.Min},{range.Max}]");
            }
        }

        [TestCase(DiceType.D4)]
        [TestCase(DiceType.D6)]
        [TestCase(DiceType.D8)]
        [TestCase(DiceType.D10)]
        [TestCase(DiceType.D12)]
        [TestCase(DiceType.D20)]
        public void test_bands_noBandIsEmpty(DiceType die)
        {
            // Arrange — una banda vacia seria un item cuyo efecto autorado nunca corre.
            int faces = die.MaxFace();

            // Act + Assert
            foreach (ActiveItemBand band in System.Enum.GetValues(typeof(ActiveItemBand)))
            {
                var range = ActiveItemBands.RangeOf(band, faces);
                Assert.LessOrEqual(range.Min, range.Max, $"d{faces}: la banda {band} quedo vacia");
            }
        }

        // ------------------------------------------------------------------
        // Clamps — un encantamiento no puede sacar el resultado del dado
        // ------------------------------------------------------------------

        [Test]
        public void test_bands_rollAboveTheDie_clampsToPositive()
        {
            // Act — GDD §20: los encantamientos deben clampear por debajo del maximo.
            var band = ActiveItemBands.Resolve(roll: 99, faces: 6);

            // Assert
            Assert.AreEqual(ActiveItemBand.Positive, band);
        }

        [Test]
        public void test_bands_rollBelowOne_clampsToNegative()
        {
            // Act
            var band = ActiveItemBands.Resolve(roll: 0, faces: 6);

            // Assert
            Assert.AreEqual(ActiveItemBand.Negative, band);
        }

        [Test]
        public void test_bands_theLowestAndHighestFaces_areNeverTheSameBand()
        {
            // Arrange — si el 1 y el maximo cayeran juntos, el item no tendria riesgo.
            foreach (DiceType die in new[] { DiceType.D4, DiceType.D6, DiceType.D8, DiceType.D10, DiceType.D12, DiceType.D20 })
            {
                // Act
                var low = ActiveItemBands.Resolve(1, die);
                var high = ActiveItemBands.Resolve(die.MaxFace(), die);

                // Assert
                Assert.AreEqual(ActiveItemBand.Negative, low, $"d{die.MaxFace()}");
                Assert.AreEqual(ActiveItemBand.Positive, high, $"d{die.MaxFace()}");
            }
        }
    }
}
