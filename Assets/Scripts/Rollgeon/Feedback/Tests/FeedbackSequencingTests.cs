using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Effects;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rollgeon.Feedback.Tests
{
    /// <summary>
    /// Efecto de prueba que solo cuenta invocaciones — sirve para verificar
    /// <b>cuándo</b> corre un step, sin arrastrar el pipeline de daño real.
    /// </summary>
    internal sealed class CountingEffect : BaseEffect
    {
        public int ApplyCount;

        public override string GetEffectName() => "Counting";

        public override bool ApplyEffect(EffectContext context)
        {
            ApplyCount++;
            return true;
        }
    }

    /// <summary>
    /// Cobertura del trío de secuenciación (§10.8): bus latched, puntero runtime
    /// y el puente de Animation Events.
    /// </summary>
    [TestFixture]
    public class FeedbackSequencingTests
    {
        [TearDown]
        public void TearDown()
        {
            FeedbackSequenceRuntime.ClearCurrent(FeedbackSequenceRuntime.Current);
        }

        // ── FeedbackEventBus ────────────────────────────────────────────

        [Test]
        public void Bus_HasFired_FalseBeforePublish_TrueAfter()
        {
            var bus = new FeedbackEventBus();

            Assert.IsFalse(bus.HasFired("hit"));
            bus.Publish("hit");
            Assert.IsTrue(bus.HasFired("hit"));
        }

        [Test]
        public void Bus_IsLatched_KeyStaysFiredForLateSubscribers()
        {
            var bus = new FeedbackEventBus();
            bus.Publish("hit");

            // Un step que pregunta tarde igual resume — previene la race clásica de pub/sub.
            Assert.IsTrue(bus.HasFired("hit"));
            Assert.IsTrue(bus.HasFired("hit"));
        }

        [Test]
        public void Bus_NullOrEmptyKeys_AreSafeNoOps()
        {
            var bus = new FeedbackEventBus();

            Assert.DoesNotThrow(() => bus.Publish(null));
            Assert.DoesNotThrow(() => bus.Publish(""));
            Assert.IsFalse(bus.HasFired(null));
            Assert.IsFalse(bus.HasFired(""));
        }

        [Test]
        public void Bus_Clear_ResetsFiredKeys()
        {
            var bus = new FeedbackEventBus();
            bus.Publish("hit");

            bus.Clear();

            Assert.IsFalse(bus.HasFired("hit"));
        }

        // ── FeedbackSequenceRuntime ─────────────────────────────────────

        [Test]
        public void Runtime_Publish_RoutesToCurrentBus()
        {
            var bus = new FeedbackEventBus();
            FeedbackSequenceRuntime.SetCurrent(bus);

            FeedbackSequenceRuntime.Publish("hit");

            Assert.IsTrue(bus.HasFired("hit"));
        }

        [Test]
        public void Runtime_Publish_WithoutActiveSequence_IsNoOp()
        {
            Assert.IsNull(FeedbackSequenceRuntime.Current);
            Assert.DoesNotThrow(() => FeedbackSequenceRuntime.Publish("hit"));
        }

        [Test]
        public void Runtime_ClearCurrent_OnlyClearsExpectedBus()
        {
            var active = new FeedbackEventBus();
            var stale = new FeedbackEventBus();
            FeedbackSequenceRuntime.SetCurrent(active);

            // Teardown fuera de orden de una secuencia vieja no pisa a la activa.
            FeedbackSequenceRuntime.ClearCurrent(stale);
            Assert.AreSame(active, FeedbackSequenceRuntime.Current);

            FeedbackSequenceRuntime.ClearCurrent(active);
            Assert.IsNull(FeedbackSequenceRuntime.Current);
        }

        // ── AnimationFeedbackEvent ──────────────────────────────────────

        [Test]
        public void AnimationEvent_PublishesKeyToActiveBus()
        {
            var bus = new FeedbackEventBus();
            FeedbackSequenceRuntime.SetCurrent(bus);
            var go = new GameObject("AnimEventPawn");
            try
            {
                var evt = go.AddComponent<AnimationFeedbackEvent>();

                evt.PushFeedbackEvent("slash-impact");

                Assert.IsTrue(bus.HasFired("slash-impact"));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void AnimationEvent_EmptyKey_WarnsAndDoesNotThrow()
        {
            var go = new GameObject("AnimEventPawn");
            try
            {
                var evt = go.AddComponent<AnimationFeedbackEvent>();

                LogAssert.Expect(LogType.Warning,
                    "[AnimationFeedbackEvent] Animation Event sin key en 'AnimEventPawn'.");
                Assert.DoesNotThrow(() => evt.PushFeedbackEvent(""));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void AnimationEvent_WithoutActiveSequence_IsNoOp()
        {
            var go = new GameObject("AnimEventPawn");
            try
            {
                var evt = go.AddComponent<AnimationFeedbackEvent>();

                Assert.IsNull(FeedbackSequenceRuntime.Current);
                Assert.DoesNotThrow(() => evt.PushFeedbackEvent("hit"));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ── StepSource.InlineEffect ─────────────────────────────────────

        private static FeedbackSequenceStep MakeInlineEffectStep(params IEffect[] effects)
        {
            var step = new FeedbackSequenceStep
            {
                Source = StepSource.InlineEffect,
                StartMode = StepStartMode.OnEvent,
                StartOnEventKey = "hit",
                EndMode = StepEndMode.Immediate,
                InlineEffects = new EffectData(),
            };
            step.InlineEffects.Effects = new List<IEffect>(effects);
            return step;
        }

        [Test]
        public void InlineEffect_RunsAuthoredEffects_AndReportsHandled()
        {
            var effect = new CountingEffect();
            var step = MakeInlineEffectStep(effect);
            var ctx = new EffectContext();

            var handled = FeedbackManager.RunInlineEffects(step, ctx, out _);

            Assert.IsTrue(handled, "El step tenía efectos y contexto — debería reportar que corrió.");
            Assert.AreEqual(1, effect.ApplyCount);
        }

        [Test]
        public void InlineEffect_ResetsShortCircuit_SoDeferredEffectsStillRun()
        {
            var effect = new CountingEffect();
            var step = MakeInlineEffectStep(effect);

            // El pass original cortocircuitó después de armar el request. Ese false no debe
            // arrastrarse hasta el frame de impacto y comerse el daño diferido.
            var ctx = new EffectContext { lastResult = false };

            FeedbackManager.RunInlineEffects(step, ctx, out _);

            Assert.AreEqual(1, effect.ApplyCount);
        }

        [Test]
        public void InlineEffect_WithoutContext_WarnsAndSkipsEffects()
        {
            var effect = new CountingEffect();
            var step = MakeInlineEffectStep(effect);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                @"\[FeedbackManager\] Step InlineEffect sin EffectContext"));
            var handled = FeedbackManager.RunInlineEffects(step, null, out _);

            Assert.IsFalse(handled);
            Assert.AreEqual(0, effect.ApplyCount,
                "Sin contexto no hay a quién aplicarle el efecto — no debe correr a ciegas.");
        }

        [Test]
        public void InlineEffect_EmptyStep_IsNoOpAndReportsUnhandled()
        {
            var step = new FeedbackSequenceStep
            {
                Source = StepSource.InlineEffect,
                InlineEffects = null,
            };

            var handled = FeedbackManager.RunInlineEffects(step, new EffectContext(), out _);

            Assert.IsFalse(handled,
                "Un step vacío no debe pisar el StoredValues del request con un snapshot nuevo.");
        }

        [Test]
        public void StepSource_InlineEffect_IsAppendedLast_SoExistingDataKeepsMeaning()
        {
            // Los steps ya autorados serializan Source por índice: si InlineEffect no fuera
            // el último, toda la data existente cambiaría de significado en silencio.
            Assert.AreEqual(0, (int)StepSource.FeedbackRef);
            Assert.AreEqual(1, (int)StepSource.InlineWait);
            Assert.AreEqual(2, (int)StepSource.InlineAnimation);
            Assert.AreEqual(3, (int)StepSource.InlineBehaviorValue);
            Assert.AreEqual(4, (int)StepSource.InlineEffect);
        }
    }
}
