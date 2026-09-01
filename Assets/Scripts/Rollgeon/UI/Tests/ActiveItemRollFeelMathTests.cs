using NUnit.Framework;
using Rollgeon.Items.Active;
using Rollgeon.UI.HUD;
using Rollgeon.UI.HUD.DiceAnim;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Feel de la tirada de la ficha de ítem activo (GDD "Ítems Activos" §18/§19).
    /// Lógica pura, testeable sin escena — mismo patrón que <c>ChestRevealFeelMathTests</c>.
    /// </summary>
    [TestFixture]
    public sealed class ActiveItemRollFeelMathTests
    {
        private static readonly float Spin = ActiveItemRollFeelMath.SpinSeconds;
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
            // El instante EXACTO de TotalSeconds no se afirma: la suma se recalcula en
            // PhaseAt y difiere de TotalSeconds por un ULP, asi que fijar el borde seria
            // testear el codegen. Lo que importa es que la animacion termine.
            Assert.AreEqual(ActiveItemRollPhase.Idle,
                ActiveItemRollFeelMath.PhaseAt(ActiveItemRollFeelMath.TotalSeconds(true) + 0.01f, true));
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
            const int raw = 4, final = 5;

            // Act + Assert
            Assert.AreEqual(raw, ActiveItemRollFeelMath.SettledFaceAt(
                Spin, true, raw, final), "primero muestra la cruda");
            Assert.AreEqual(final, ActiveItemRollFeelMath.SettledFaceAt(
                Spin + RawHold, true, raw, final), "despues la ajustada");
            Assert.AreEqual(final, ActiveItemRollFeelMath.SettledFaceAt(
                Spin + RawHold + Flash, true, raw, final));
        }

        [Test]
        public void test_face_withoutEnchantment_settlesStraightOnTheResult()
        {
            // Act + Assert
            Assert.AreEqual(3, ActiveItemRollFeelMath.SettledFaceAt(
                Spin, false, rawRoll: 3, finalRoll: 3));
        }

        [Test]
        public void test_spinTicks_useTheSharedDiceChoreography()
        {
            // Arrange — el giro no lo inventa esta clase: los ticks salen de
            // DiceAnimChoreographer, el mismo que coreografia los dados de combate.
            var t = DiceAnimTimings.Defaults;
            int tickCount = DiceAnimChoreographer.TickCount(t.SpinSeconds, t.SpinTickSeconds);

            // Assert
            Assert.Greater(tickCount, 0, "el giro tiene que tener ticks");
            Assert.AreEqual(0, ActiveItemRollFeelMath.SpinTickAt(-0.1f, tickCount),
                "antes de empezar no paso ningun tick");
            Assert.AreEqual(tickCount, ActiveItemRollFeelMath.SpinTickAt(Spin, tickCount),
                "al terminar el giro pasaron todos");
        }

        [Test]
        public void test_spinTicks_advanceMonotonically()
        {
            // Arrange — el tick nunca puede retroceder, o el dado "desgiraria".
            var t = DiceAnimTimings.Defaults;
            int tickCount = DiceAnimChoreographer.TickCount(t.SpinSeconds, t.SpinTickSeconds);
            int previous = 0;

            for (float e = 0f; e <= Spin; e += 0.005f)
            {
                // Act
                int tick = ActiveItemRollFeelMath.SpinTickAt(e, tickCount);

                // Assert
                Assert.GreaterOrEqual(tick, previous, $"retrocedio en t={e}");
                previous = tick;
            }
        }

        [Test]
        public void test_spinTicks_decelerate()
        {
            // Arrange — el dado frena: en la primera mitad del giro pasan mas ticks que
            // en la segunda. Sale de la desaceleracion de DiceAnimChoreographer.
            var t = DiceAnimTimings.Defaults;
            int tickCount = DiceAnimChoreographer.TickCount(t.SpinSeconds, t.SpinTickSeconds);

            // Act
            int atHalf = ActiveItemRollFeelMath.SpinTickAt(Spin * 0.5f, tickCount);
            float firstGap = DiceAnimChoreographer.TickTime(1, tickCount, t.SpinSeconds, t.SpinDecelerationPower);
            float lastGap =
                DiceAnimChoreographer.TickTime(tickCount, tickCount, t.SpinSeconds, t.SpinDecelerationPower)
                - DiceAnimChoreographer.TickTime(tickCount - 1, tickCount, t.SpinSeconds, t.SpinDecelerationPower);

            // Assert — desacelerar es que los intervalos CREZCAN: los primeros ticks caen
            // pegados y el ultimo se hace esperar. Como efecto, la mayoria de los ticks
            // pasa en la primera mitad del giro.
            Assert.Greater(lastGap, firstGap,
                $"el ultimo intervalo ({lastGap:F3}s) no es mayor que el primero ({firstGap:F3}s) — no frena");
            Assert.Greater(atHalf, tickCount / 2,
                $"a mitad del giro pasaron solo {atHalf} de {tickCount} ticks — no arranca rapido");
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
