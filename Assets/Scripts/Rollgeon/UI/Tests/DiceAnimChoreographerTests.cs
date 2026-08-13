using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Dice;
using Rollgeon.UI.HUD.DiceAnim;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Cobertura pura (sin GameObjects, sin tweens) de la coreografía de dados
    /// legacy: planes de spin, planes de outro y helpers de timing/preview.
    /// </summary>
    [TestFixture]
    public class DiceAnimChoreographerTests
    {
        private static DiceAnimTimings T() => DiceAnimTimings.Defaults;

        // ── BuildSpinPlans ───────────────────────────────────────────────

        [Test]
        public void BuildSpinPlans_HeldSlots_DoNotSpin()
        {
            var willReveal = new[] { true, false, true, false, true };

            var plans = DiceAnimChoreographer.BuildSpinPlans(willReveal, T());

            Assert.IsTrue(plans[0].Spins);
            Assert.IsFalse(plans[1].Spins);
            Assert.IsTrue(plans[2].Spins);
            Assert.IsFalse(plans[3].Spins);
            Assert.IsTrue(plans[4].Spins);
        }

        [Test]
        public void BuildSpinPlans_StaggerAccumulatesOnlyOverSpinningSlots()
        {
            // El slot 1 no spinea: el slot 2 debe arrancar como "segundo", no como "tercero".
            var willReveal = new[] { true, false, true };
            var t = T();

            var plans = DiceAnimChoreographer.BuildSpinPlans(willReveal, t);

            Assert.AreEqual(0f, plans[0].Delay);
            Assert.AreEqual(t.SpinStaggerSeconds, plans[2].Delay, 1e-5f);
        }

        [Test]
        public void BuildSpinPlans_ZeroSpinSeconds_NobodySpins()
        {
            var t = T();
            t.SpinSeconds = 0f;

            var plans = DiceAnimChoreographer.BuildSpinPlans(new[] { true, true }, t);

            Assert.IsFalse(plans[0].Spins);
            Assert.IsFalse(plans[1].Spins);
        }

        [Test]
        public void BuildSpinPlans_NullInput_ReturnsEmpty()
        {
            var plans = DiceAnimChoreographer.BuildSpinPlans(null, T());

            Assert.AreEqual(0, plans.Length);
        }

        // ── TickCount / TickTime ─────────────────────────────────────────

        [Test]
        public void TickCount_DefaultConfig_YieldsEightTicks()
        {
            Assert.AreEqual(8, DiceAnimChoreographer.TickCount(0.5f, 0.06f));
        }

        [Test]
        public void TickCount_NonPositiveInterval_YieldsZero()
        {
            Assert.AreEqual(0, DiceAnimChoreographer.TickCount(0.5f, 0f));
            Assert.AreEqual(0, DiceAnimChoreographer.TickCount(0.5f, -1f));
            Assert.AreEqual(0, DiceAnimChoreographer.TickCount(0f, 0.06f));
        }

        [Test]
        public void TickTime_DeceleratingPower_ProducesIncreasingIntervals()
        {
            const int count = 8;
            const float spin = 0.5f;
            const float power = 2f;

            float prevTime = 0f;
            float prevInterval = -1f;
            for (int k = 1; k <= count; k++)
            {
                float time = DiceAnimChoreographer.TickTime(k, count, spin, power);
                float interval = time - prevTime;
                if (prevInterval >= 0f)
                    Assert.GreaterOrEqual(interval, prevInterval - 1e-5f,
                        $"El intervalo del tick {k} debería ser >= al anterior (desaceleración).");
                prevInterval = interval;
                prevTime = time;
            }
            Assert.AreEqual(spin, prevTime, 1e-5f, "El último tick cae exactamente al final del spin.");
        }

        [Test]
        public void TickTime_PowerOne_IsLinear()
        {
            float mid = DiceAnimChoreographer.TickTime(4, 8, 0.5f, 1f);

            Assert.AreEqual(0.25f, mid, 1e-5f);
        }

        // ── NextPreviewFace ──────────────────────────────────────────────

        [Test]
        public void NextPreviewFace_NeverRepeatsPrevious_WhenFaceMaxAboveOne()
        {
            var rng = new System.Random(1234);

            int prev = 3;
            for (int i = 0; i < 200; i++)
            {
                int face = DiceAnimChoreographer.NextPreviewFace(rng, 6, prev);
                Assert.AreNotEqual(prev, face);
                Assert.That(face, Is.InRange(1, 6));
                prev = face;
            }
        }

        [Test]
        public void PreviewFaceRange_CoversFinalFaceAboveConfiguredMax()
        {
            Assert.AreEqual(8, DiceAnimChoreographer.PreviewFaceRange(6, 8));
            Assert.AreEqual(6, DiceAnimChoreographer.PreviewFaceRange(6, 4));
        }

        // ── BuildOutroPlans ──────────────────────────────────────────────

        private static (DiceOutroPlan[] plans, DiceAnimTimings t) BuildDefaultOutro()
        {
            var held = new[] { true, false, true, false, false };
            var active = new[] { true, true, true, true, false };
            var positions = new List<Vector2>
            {
                new Vector2(-250, 0), new Vector2(-125, 0), new Vector2(0, 0),
                new Vector2(125, 0), new Vector2(250, 0),
            };
            var t = T();
            var plans = DiceAnimChoreographer.BuildOutroPlans(
                held, active, positions, new Vector2(0, 40), t);
            return (plans, t);
        }

        [Test]
        public void BuildOutroPlans_HeldActive_ThrowTowardCenter()
        {
            var (plans, t) = BuildDefaultOutro();

            Assert.AreEqual(DiceOutroKind.Throw, plans[0].Kind);
            Assert.AreEqual(new Vector2(0, 40), plans[0].TargetPosition);
            Assert.AreEqual(t.ThrowEndScale, plans[0].EndScale);
        }

        [Test]
        public void BuildOutroPlans_UnheldActive_DiscardInPlace()
        {
            var (plans, t) = BuildDefaultOutro();

            Assert.AreEqual(DiceOutroKind.Discard, plans[1].Kind);
            Assert.AreEqual(new Vector2(-125, 0), plans[1].TargetPosition);
            Assert.AreEqual(t.DiscardEndScale, plans[1].EndScale);
        }

        [Test]
        public void BuildOutroPlans_InactiveSlot_IsSkipped()
        {
            var (plans, _) = BuildDefaultOutro();

            Assert.AreEqual(DiceOutroKind.Skip, plans[4].Kind);
        }

        [Test]
        public void BuildOutroPlans_ThrowStaggerAccumulatesOnlyOverThrows()
        {
            var (plans, t) = BuildDefaultOutro();

            // Slots 0 y 2 son los throws: el segundo arranca un stagger después.
            Assert.AreEqual(0f, plans[0].Delay);
            Assert.AreEqual(t.ThrowStaggerSeconds, plans[2].Delay, 1e-5f);
            // Los descartes arrancan todos juntos.
            Assert.AreEqual(0f, plans[1].Delay);
            Assert.AreEqual(0f, plans[3].Delay);
        }

        [Test]
        public void BuildOutroPlans_ThrowFade_HappensInFinalPortion()
        {
            var (plans, t) = BuildDefaultOutro();

            float expectedFade = t.ThrowSeconds * t.ThrowFadePortion;
            Assert.AreEqual(expectedFade, plans[0].FadeDuration, 1e-5f);
            Assert.AreEqual(t.ThrowSeconds - expectedFade, plans[0].FadeDelay, 1e-5f);
        }

        [Test]
        public void BuildOutroPlans_ZeroDurations_EverythingSkips()
        {
            var t = T();
            t.ThrowSeconds = 0f;
            t.DiscardSeconds = 0f;

            var plans = DiceAnimChoreographer.BuildOutroPlans(
                new[] { true, false }, new[] { true, true }, null, Vector2.zero, t);

            Assert.AreEqual(DiceOutroKind.Skip, plans[0].Kind);
            Assert.AreEqual(DiceOutroKind.Skip, plans[1].Kind);
        }

        // ── ParabolaOffset ───────────────────────────────────────────────

        [Test]
        public void ParabolaOffset_IsZeroAtEndsAndPeaksAtMidpoint()
        {
            Assert.AreEqual(0f, DiceAnimChoreographer.ParabolaOffset(0f, 40f), 1e-5f);
            Assert.AreEqual(0f, DiceAnimChoreographer.ParabolaOffset(1f, 40f), 1e-5f);
            Assert.AreEqual(40f, DiceAnimChoreographer.ParabolaOffset(0.5f, 40f), 1e-5f);
        }

        [Test]
        public void ParabolaOffset_ClampsTimeOutsideUnitRange()
        {
            Assert.AreEqual(0f, DiceAnimChoreographer.ParabolaOffset(-0.5f, 40f), 1e-5f);
            Assert.AreEqual(0f, DiceAnimChoreographer.ParabolaOffset(1.5f, 40f), 1e-5f);
        }

        // ── OutroTotalSeconds ────────────────────────────────────────────

        [Test]
        public void OutroTotalSeconds_IsLatestTweenEndPlusHold()
        {
            var (plans, t) = BuildDefaultOutro();

            // El último throw (slot 2, delay = stagger) es el que termina más tarde.
            float expected = t.ThrowStaggerSeconds + t.ThrowSeconds + t.OutroHoldSeconds;
            Assert.AreEqual(expected, DiceAnimChoreographer.OutroTotalSeconds(plans, t.OutroHoldSeconds), 1e-5f);
        }

        [Test]
        public void OutroTotalSeconds_AllSkipped_IsZero()
        {
            var plans = new[] { DiceOutroPlan.Skipped, DiceOutroPlan.Skipped };

            Assert.AreEqual(0f, DiceAnimChoreographer.OutroTotalSeconds(plans, 0.05f));
        }

        // ── SpinRole ─────────────────────────────────────────────────────

        [Test]
        public void SpinRole_AlternatesFrontAndSides_InTheAuthoredSequence()
        {
            // Arrange & Act — 8 ticks con el lateral A primero.
            var sequence = new DiceShapeRole[8];
            for (int tick = 1; tick <= 8; tick++)
                sequence[tick - 1] = DiceAnimChoreographer.SpinRole(tick, sideSeed: 0);

            // Assert — la secuencia 0-1-0-2 del brief.
            Assert.AreEqual(
                new[]
                {
                    DiceShapeRole.Front, DiceShapeRole.SideA,
                    DiceShapeRole.Front, DiceShapeRole.SideB,
                    DiceShapeRole.Front, DiceShapeRole.SideA,
                    DiceShapeRole.Front, DiceShapeRole.SideB,
                },
                sequence);
        }

        [Test]
        public void SpinRole_StartsOnTheOtherSide_WhenSeedIsOne()
        {
            // Act — el seed solo cambia con cuál lateral arranca; dos dados no van en fase.
            Assert.AreEqual(DiceShapeRole.SideB, DiceAnimChoreographer.SpinRole(2, sideSeed: 1));
            Assert.AreEqual(DiceShapeRole.SideA, DiceAnimChoreographer.SpinRole(4, sideSeed: 1));
        }

        [Test]
        public void SpinRole_NeverReturnsHoverOrSelected()
        {
            // Arrange & Act — el ciclado solo usa el frontal y los dos laterales.
            for (int tick = 1; tick <= 32; tick++)
            {
                for (int seed = 0; seed <= 1; seed++)
                {
                    var role = DiceAnimChoreographer.SpinRole(tick, seed);

                    // Assert
                    Assert.That(role, Is.EqualTo(DiceShapeRole.Front)
                                        .Or.EqualTo(DiceShapeRole.SideA)
                                        .Or.EqualTo(DiceShapeRole.SideB),
                        $"tick {tick}, seed {seed} devolvió {role}");
                }
            }
        }

        /// <summary>
        /// El dado tiene que descansar en el frontal antes del reveal, no cortar desde un
        /// lateral. Con el tuning shippeado (SpinSeconds 0.5, SpinTickSeconds 0.06) hay 8 ticks
        /// pero el 8 nunca dispara — TickTime(8,8,…) da exactamente la duración y el loop del
        /// animator corre mientras elapsed &lt; duración. El último que llega a verse es el 7.
        /// </summary>
        [Test]
        public void SpinRole_LastReachableTick_IsFront_WithShippedTuning()
        {
            // Arrange
            var t = DiceAnimTimings.Defaults;
            int ticks = DiceAnimChoreographer.TickCount(t.SpinSeconds, t.SpinTickSeconds);
            Assert.AreEqual(8, ticks, "Cambió el tuning: revisar en qué tick aterriza el ciclado.");
            Assert.AreEqual(t.SpinSeconds,
                DiceAnimChoreographer.TickTime(ticks, ticks, t.SpinSeconds, t.SpinDecelerationPower),
                1e-5f, "El último tick ya no cae fuera del loop.");

            // Act
            var lastReachable = DiceAnimChoreographer.SpinRole(ticks - 1, sideSeed: 0);

            // Assert
            Assert.AreEqual(DiceShapeRole.Front, lastReachable);
        }
    }
}
