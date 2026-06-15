using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rollgeon.Feedback.Tests
{
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
    }
}
