using NUnit.Framework;
using Rollgeon.Items.Active;
using Rollgeon.UI.HUD;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Feel de la tirada de la ficha de ítem activo (GDD "Ítems Activos" §18/§19).
    /// Lógica pura, testeable sin escena — mismo patrón que <c>ChestRevealFeelMathTests</c>.
    /// </summary>
    [TestFixture]
    public sealed class ActiveItemRollFeelMathTests
    {
        private const float Spin = ActiveItemRollFeelMath.SpinSeconds;
        private const float RawHold = ActiveItemRollFeelMath.RawHoldSeconds;
        private const float Flash = ActiveItemRollFeelMath.EnchantFlashSeconds;
        private const float Hold = ActiveItemRollFeelMath.ResultHoldSeconds;

        // ------------------------------------------------------------------
        // Fases sin encantamiento
        // ------------------------------------------------------------------

        [Test]
        public void test_phases_withoutEnchantment_spinThenSettleThenIdle()
        {
            // Act + Assert
            Assert.AreEqual(ActiveItemRollPhase.Spinning,
                ActiveItemRollFeelMath.PhaseAt(0f, wasEnchanted: false));
            Assert.AreEqual(ActiveItemRollPhase.Spinning,
                ActiveItemRollFeelMath.PhaseAt(Spin - 0.01f, wasEnchanted: false));
            Assert.AreEqual(ActiveItemRollPhase.Settled,
                ActiveItemRollFeelMath.PhaseAt(Spin, wasEnchanted: false));
            Assert.AreEqual(ActiveItemRollPhase.Idle,
                ActiveItemRollFeelMath.PhaseAt(Spin + Hold, wasEnchanted: false));
        }

        [Test]
        public void test_phases_withoutEnchantment_neverPassThroughTheEnchantedPhase()
        {
            // Arrange — una pausa en la cara cruda sin encantamiento seria una pausa sin
            // nada que comunicar.
            for (float t = 0f; t < ActiveItemRollFeelMath.TotalSeconds(false); t += 0.01f)
            {
                // Act
                var phase = ActiveItemRollFeelMath.PhaseAt(t, wasEnchanted: false);

                // Assert
                Assert.AreNotEqual(ActiveItemRollPhase.Enchanted, phase, $"en t={t}");
            }
        }

        // ------------------------------------------------------------------
        // Fases con encantamiento
        // ------------------------------------------------------------------

        [Test]
        public void test_phases_withEnchantment_passThroughTheRawFaceFirst()
        {
            // Arrange — el jugador tiene que ver la cara cruda antes de la ajustada, para
            // leer que intervino el encantamiento y no que el dado salio distinto.

            // Act + Assert
            Assert.AreEqual(ActiveItemRollPhase.Settled,
                ActiveItemRollFeelMath.PhaseAt(Spin, wasEnchanted: true));
            Assert.AreEqual(ActiveItemRollPhase.Enchanted,
                ActiveItemRollFeelMath.PhaseAt(Spin + RawHold, wasEnchanted: true));
            Assert.AreEqual(ActiveItemRollPhase.Holding,
                ActiveItemRollFeelMath.PhaseAt(Spin + RawHold + Flash, wasEnchanted: true));
            Assert.AreEqual(ActiveItemRollPhase.Idle,
                ActiveItemRollFeelMath.PhaseAt(ActiveItemRollFeelMath.TotalSeconds(true), true));
        }

        [Test]
        public void test_enchantedAnimation_takesLongerThanThePlainOne()
        {
            // Assert — la diferencia es exactamente la pausa en la cruda mas el destello.
            Assert.Greater(ActiveItemRollFeelMath.TotalSeconds(true),
                           ActiveItemRollFeelMath.TotalSeconds(false));
        }

        // ------------------------------------------------------------------
        // Cara mostrada
        // ------------------------------------------------------------------

        [Test]
        public void test_face_settlesOnTheRawRollAndThenOnTheFinalOne()
        {
            // Arrange — cara cruda 4, ajustada a 5 por el encantamiento.
            const int raw = 4, final = 5, faces = 6;

            // Act + Assert
            Assert.AreEqual(raw, ActiveItemRollFeelMath.FaceAt(
                Spin, true, raw, final, faces, seed: 1), "primero muestra la cruda");
            Assert.AreEqual(final, ActiveItemRollFeelMath.FaceAt(
                Spin + RawHold, true, raw, final, faces, seed: 1), "despues la ajustada");
            Assert.AreEqual(final, ActiveItemRollFeelMath.FaceAt(
                Spin + RawHold + Flash, true, raw, final, faces, seed: 1));
        }

        [Test]
        public void test_face_withoutEnchantment_settlesStraightOnTheResult()
        {
            // Act + Assert
            Assert.AreEqual(3, ActiveItemRollFeelMath.FaceAt(
                Spin, false, rawRoll: 3, finalRoll: 3, faces: 6, seed: 1));
        }

        [Test]
        public void test_spinFace_staysInsideTheDieRange()
        {
            // Arrange — la cara del giro es adorno, pero no puede mostrar un 7 en un D6.
            foreach (int faces in new[] { 4, 6, 8, 10, 12, 20 })
            {
                for (int seed = 0; seed < 20; seed++)
                {
                    for (float t = 0f; t < Spin; t += 0.005f)
                    {
                        // Act
                        int face = ActiveItemRollFeelMath.SpinFace(t, faces, seed);

                        // Assert
                        Assert.GreaterOrEqual(face, 1, $"d{faces} seed={seed} t={t}");
                        Assert.LessOrEqual(face, faces, $"d{faces} seed={seed} t={t}");
                    }
                }
            }
        }

        [Test]
        public void test_spinFace_isDeterministicForTheSameSeed()
        {
            // Assert — dos frames del mismo instante no pueden parpadear entre caras.
            Assert.AreEqual(ActiveItemRollFeelMath.SpinFace(0.2f, 6, seed: 42),
                            ActiveItemRollFeelMath.SpinFace(0.2f, 6, seed: 42));
        }

        [Test]
        public void test_spinFace_decelerates()
        {
            // Arrange — la tirada tiene que frenar, no cortarse de golpe: en la primera
            // mitad del giro pasan mas caras que en la segunda.
            int firstHalf = CountChanges(0f, Spin * 0.5f);
            int secondHalf = CountChanges(Spin * 0.5f, Spin);

            // Assert
            Assert.Greater(firstHalf, secondHalf,
                $"primera mitad {firstHalf} cambios, segunda {secondHalf}");
        }

        private static int CountChanges(float from, float to)
        {
            int changes = 0;
            int previous = ActiveItemRollFeelMath.SpinFace(from, 20, seed: 7);
            for (float t = from; t < to; t += 0.005f)
            {
                int face = ActiveItemRollFeelMath.SpinFace(t, 20, seed: 7);
                if (face != previous) changes++;
                previous = face;
            }
            return changes;
        }

        [Test]
        public void test_theRawFaceIsShownLongEnoughToBeRead()
        {
            // Arrange — la pausa sobre la cara cruda es lo que hace legible que
            // intervino el encantamiento. Si fuera demasiado corta, el jugador veria un
            // salto de numero sin explicacion.
            Assert.GreaterOrEqual(RawHold, 0.15f, "la pausa en la cara cruda es demasiado corta para leerse");
        }

        // ------------------------------------------------------------------
        // Intensidad por banda
        // ------------------------------------------------------------------

        [Test]
        public void test_intensity_growsFromNegativeToPositive()
        {
            // Assert
            Assert.Less(ActiveItemRollFeelMath.Intensity01(ActiveItemBand.Negative),
                        ActiveItemRollFeelMath.Intensity01(ActiveItemBand.Mixed));
            Assert.Less(ActiveItemRollFeelMath.Intensity01(ActiveItemBand.Mixed),
                        ActiveItemRollFeelMath.Intensity01(ActiveItemBand.Positive));
        }

        [Test]
        public void test_intensity_isNeverZero()
        {
            // Arrange — el GDD: la banda negativa no puede leerse como "no pasó nada".
            foreach (ActiveItemBand band in System.Enum.GetValues(typeof(ActiveItemBand)))
            {
                // Act + Assert
                Assert.Greater(ActiveItemRollFeelMath.Intensity01(band), 0f, band.ToString());
            }
        }

        [Test]
        public void test_hitstop_isReservedForThePositiveBand()
        {
            // Assert — el GDD lo reserva para el mejor desenlace.
            Assert.IsTrue(ActiveItemRollFeelMath.HitstopAllowed(ActiveItemBand.Positive));
            Assert.IsFalse(ActiveItemRollFeelMath.HitstopAllowed(ActiveItemBand.Mixed));
            Assert.IsFalse(ActiveItemRollFeelMath.HitstopAllowed(ActiveItemBand.Negative));
        }

        // ------------------------------------------------------------------
        // Escala
        // ------------------------------------------------------------------

        [Test]
        public void test_scale_returnsToOneWhenIdle()
        {
            // Arrange — si no volviera a 1, la ficha quedaria agrandada para siempre.
            float atEnd = ActiveItemRollFeelMath.ScaleAt(
                ActiveItemRollFeelMath.TotalSeconds(false), false, ActiveItemBand.Positive);

            // Assert
            Assert.AreEqual(1f, atEnd, 0.001f);
        }

        [Test]
        public void test_scale_doesNotPopWhileSpinning()
        {
            // Arrange — el pop marca que el dado se asento; durante el giro no hay nada
            // que marcar.
            float mid = ActiveItemRollFeelMath.ScaleAt(Spin * 0.5f, false, ActiveItemBand.Positive);

            // Assert
            Assert.AreEqual(1f, mid, 0.001f);
        }

        [Test]
        public void test_scale_popsHarderOnTheStrongerBand()
        {
            // Act
            float negative = ActiveItemRollFeelMath.ScaleAt(Spin, false, ActiveItemBand.Negative);
            float positive = ActiveItemRollFeelMath.ScaleAt(Spin, false, ActiveItemBand.Positive);

            // Assert
            Assert.Greater(positive, negative);
            Assert.Greater(negative, 1f, "la banda negativa igual se siente — no es un no-evento");
        }
    }
}
