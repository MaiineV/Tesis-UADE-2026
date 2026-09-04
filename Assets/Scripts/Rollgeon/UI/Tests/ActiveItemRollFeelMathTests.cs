using NUnit.Framework;
using Rollgeon.Items.Active;
using Rollgeon.UI.HUD;
using Rollgeon.UI.HUD.DiceAnim;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Feel de la tirada de la ficha de ítem activo (GDD "Ítems Activos" §18/§19).
    /// Lógica pura, testeable sin escena — mismo patrón que <c>ChestRevealFeelMathTests</c>.
    /// La tirada vive en dos segmentos: pendiente (giro + cara cruda sostenida hasta que
    /// el jugador acepte o re-tire) y resolución (destello del encantamiento + hold final).
    /// </summary>
    [TestFixture]
    public sealed class ActiveItemRollFeelMathTests
    {
        private static readonly float Spin = ActiveItemRollFeelMath.SpinSeconds;
        private const float Flash = ActiveItemRollFeelMath.EnchantFlashSeconds;
        private const float Hold = ActiveItemRollFeelMath.ResultHoldSeconds;

        // ------------------------------------------------------------------
        // Segmento pendiente
        // ------------------------------------------------------------------

        [Test]
        public void test_pending_spinsAndThenSettles()
        {
            // Act + Assert
            Assert.AreEqual(ActiveItemRollPhase.Idle,
                ActiveItemRollFeelMath.PendingPhaseAt(-0.01f));
            Assert.AreEqual(ActiveItemRollPhase.Spinning,
                ActiveItemRollFeelMath.PendingPhaseAt(0f));
            Assert.AreEqual(ActiveItemRollPhase.Spinning,
                ActiveItemRollFeelMath.PendingPhaseAt(Spin - 0.01f));
            Assert.AreEqual(ActiveItemRollPhase.Settled,
                ActiveItemRollFeelMath.PendingPhaseAt(Spin));
        }

        [Test]
        public void test_pending_neverEndsByTime()
        {
            // Arrange — la cara cruda queda sostenida mientras el jugador decide si
            // acepta o re-tira: no hay vuelta al reposo por tiempo.
            Assert.AreEqual(ActiveItemRollPhase.Settled,
                ActiveItemRollFeelMath.PendingPhaseAt(Spin + 60f),
                "un minuto despues sigue esperando la decision");
        }

        [Test]
        public void test_pendingScale_popsOnSettleAndReturnsToOne()
        {
            // Arrange — el pop marca que el dado se asento; durante el giro no hay nada
            // que marcar, y la espera larga no puede quedar agrandada.
            Assert.AreEqual(1f, ActiveItemRollFeelMath.PendingScaleAt(Spin * 0.5f), 0.001f,
                "sin pop durante el giro");
            Assert.Greater(ActiveItemRollFeelMath.PendingScaleAt(Spin), 1f,
                "pop al asentarse");
            Assert.AreEqual(1f, ActiveItemRollFeelMath.PendingScaleAt(Spin + 10f), 0.001f,
                "la espera vuelve a escala de reposo");
        }

        // ------------------------------------------------------------------
        // Segmento de resolucion
        // ------------------------------------------------------------------

        [Test]
        public void test_resolve_withoutEnchantment_holdsAndThenIdles()
        {
            // Act + Assert
            Assert.AreEqual(ActiveItemRollPhase.Holding,
                ActiveItemRollFeelMath.ResolvePhaseAt(0f, wasEnchanted: false));
            Assert.AreEqual(ActiveItemRollPhase.Idle,
                ActiveItemRollFeelMath.ResolvePhaseAt(Hold + 0.01f, wasEnchanted: false));
        }

        [Test]
        public void test_resolve_withoutEnchantment_neverPassesThroughTheEnchantedPhase()
        {
            // Arrange — un destello sin encantamiento seria un destello sin nada que
            // comunicar.
            for (float t = 0f; t < ActiveItemRollFeelMath.ResolveTotalSeconds(false); t += 0.01f)
            {
                // Act
                var phase = ActiveItemRollFeelMath.ResolvePhaseAt(t, wasEnchanted: false);

                // Assert
                Assert.AreNotEqual(ActiveItemRollPhase.Enchanted, phase, $"en t={t}");
            }
        }

        [Test]
        public void test_resolve_withEnchantment_flashesFirstAndThenHolds()
        {
            // Arrange — el salto cruda → ajustada ES el destello: la cruda ya se vio todo
            // el segmento pendiente, y el destello marca que intervino el encantamiento.

            // Act + Assert
            Assert.AreEqual(ActiveItemRollPhase.Enchanted,
                ActiveItemRollFeelMath.ResolvePhaseAt(0f, wasEnchanted: true));
            Assert.AreEqual(ActiveItemRollPhase.Holding,
                ActiveItemRollFeelMath.ResolvePhaseAt(Flash, wasEnchanted: true));
            // El instante EXACTO de ResolveTotalSeconds no se afirma: la suma se recalcula
            // en ResolvePhaseAt y puede diferir por un ULP — fijar el borde seria testear
            // el codegen. Lo que importa es que la animacion termine.
            Assert.AreEqual(ActiveItemRollPhase.Idle,
                ActiveItemRollFeelMath.ResolvePhaseAt(
                    ActiveItemRollFeelMath.ResolveTotalSeconds(true) + 0.01f, true));
        }

        [Test]
        public void test_enchantedResolve_takesLongerThanThePlainOne()
        {
            // Assert — la diferencia es exactamente el destello.
            Assert.Greater(ActiveItemRollFeelMath.ResolveTotalSeconds(true),
                           ActiveItemRollFeelMath.ResolveTotalSeconds(false));
        }

        // ------------------------------------------------------------------
        // Giro compartido
        // ------------------------------------------------------------------

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
        // Escala de la resolucion
        // ------------------------------------------------------------------

        [Test]
        public void test_resolveScale_returnsToOneWhenIdle()
        {
            // Arrange — si no volviera a 1, la ficha quedaria agrandada para siempre.
            float atEnd = ActiveItemRollFeelMath.ResolveScaleAt(
                ActiveItemRollFeelMath.ResolveTotalSeconds(false) + 0.01f,
                wasEnchanted: false, ActiveItemBand.Positive);

            // Assert
            Assert.AreEqual(1f, atEnd, 0.001f);
        }

        [Test]
        public void test_resolveScale_popsHarderOnTheStrongerBand()
        {
            // Act
            float negative = ActiveItemRollFeelMath.ResolveScaleAt(0f, false, ActiveItemBand.Negative);
            float positive = ActiveItemRollFeelMath.ResolveScaleAt(0f, false, ActiveItemBand.Positive);

            // Assert
            Assert.Greater(positive, negative);
            Assert.Greater(negative, 1f, "la banda negativa igual se siente — no es un no-evento");
        }

        // ------------------------------------------------------------------
        // Feature#0085 — intensidad por estructura de resolucion
        // ------------------------------------------------------------------

        [Test]
        public void test_intensity_bands_matchesTheLegacyBandTable()
        {
            // Arrange — Bands tiene que seguir leyendo la tabla vieja tal cual.
            var resolution = new ActiveItemRollResolution(6, 6, 6, ActiveItemBand.Positive,
                ActiveItemResolution.Bands, magnitude: 0);

            // Act + Assert
            Assert.AreEqual(ActiveItemRollFeelMath.Intensity01(ActiveItemBand.Positive),
                ActiveItemRollFeelMath.Intensity01(resolution), 0.0001f);
        }

        [TestCase(ActiveItemBand.Negative, 0.55f)]
        [TestCase(ActiveItemBand.Positive, 1f)]
        public void test_intensity_binary_hasNoMixedStep(ActiveItemBand band, float expected)
        {
            // Arrange — Binary no tiene banda mixta: solo dos escalones.
            var resolution = new ActiveItemRollResolution(1, 1, 4, band, ActiveItemResolution.Binary, magnitude: 0);

            // Act + Assert
            Assert.AreEqual(expected, ActiveItemRollFeelMath.Intensity01(resolution), 0.0001f);
        }

        [Test]
        public void test_intensity_gradient_scalesContinuouslyWithMagnitude01()
        {
            // Arrange — cara 1 de 6 (Magnitude01 = 0) vs cara 6 de 6 (Magnitude01 = 1).
            var low = new ActiveItemRollResolution(1, 1, 6, ActiveItemBand.Negative,
                ActiveItemResolution.Gradient, magnitude: 1);
            var high = new ActiveItemRollResolution(6, 6, 6, ActiveItemBand.Positive,
                ActiveItemResolution.Gradient, magnitude: 6);

            // Act + Assert
            Assert.AreEqual(0.25f, ActiveItemRollFeelMath.Intensity01(low), 0.0001f);
            Assert.AreEqual(1f, ActiveItemRollFeelMath.Intensity01(high), 0.0001f);
        }

        [Test]
        public void test_intensity_hierarchy_scalesContinuouslyWithMagnitude01()
        {
            // Arrange — mitad del rango (D4, cara 3 → Magnitude01 = 2/3).
            var mid = new ActiveItemRollResolution(3, 3, 4, ActiveItemBand.Mixed,
                ActiveItemResolution.Hierarchy, magnitude: 3);

            // Act
            float expected = 0.25f + 0.75f * mid.Magnitude01;

            // Assert
            Assert.AreEqual(expected, ActiveItemRollFeelMath.Intensity01(mid), 0.0001f);
        }

        [Test]
        public void test_hitstop_bandsAndBinary_reservedForPositive()
        {
            // Arrange
            var positive = new ActiveItemRollResolution(6, 6, 6, ActiveItemBand.Positive,
                ActiveItemResolution.Bands, magnitude: 0);
            var negative = new ActiveItemRollResolution(1, 1, 6, ActiveItemBand.Negative,
                ActiveItemResolution.Binary, magnitude: 0);

            // Act + Assert
            Assert.IsTrue(ActiveItemRollFeelMath.HitstopAllowed(positive));
            Assert.IsFalse(ActiveItemRollFeelMath.HitstopAllowed(negative));
        }

        [Test]
        public void test_hitstop_gradientAndHierarchy_requiresMaxFace()
        {
            // Arrange — sin banda positiva "de verdad": el criterio pasa a ser la cara maxima.
            var maxFace = new ActiveItemRollResolution(6, 6, 6, ActiveItemBand.Positive,
                ActiveItemResolution.Gradient, magnitude: 6);
            var notMaxFace = new ActiveItemRollResolution(5, 5, 6, ActiveItemBand.Positive,
                ActiveItemResolution.Gradient, magnitude: 5);

            // Act + Assert
            Assert.IsTrue(ActiveItemRollFeelMath.HitstopAllowed(maxFace));
            Assert.IsFalse(ActiveItemRollFeelMath.HitstopAllowed(notMaxFace));
        }
    }
}
