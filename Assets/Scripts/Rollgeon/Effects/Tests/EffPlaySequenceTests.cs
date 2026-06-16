using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Effects.Concretes;
using Rollgeon.Feedback;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rollgeon.Effects.Tests
{
    [TestFixture]
    public class EffPlaySequenceTests
    {
        private RecordingFeedbackService _service;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _service = new RecordingFeedbackService();
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
        }

        // ── GetFeedbackRequest ──────────────────────────────────────────

        [Test]
        public void GetFeedbackRequest_MarksSequence_AndCarriesSteps()
        {
            var steps = new List<FeedbackSequenceStep> { new FeedbackSequenceStep() };
            var eff = CreateEffect(steps);
            var ctx = new EffectContext { SourceGuid = Guid.NewGuid(), TargetGuid = Guid.NewGuid() };

            var req = eff.GetFeedbackRequest(ctx);

            Assert.IsTrue(req.IsSequence);
            Assert.AreSame(steps, req.SequenceSteps);
            Assert.IsTrue(string.IsNullOrEmpty(req.FeedbackId));
            Assert.AreEqual(ctx.SourceGuid, req.SourceGuid);
            Assert.AreEqual(ctx.TargetGuid, req.TargetGuid);
        }

        // ── ApplyEffect ─────────────────────────────────────────────────

        [Test]
        public void ApplyEffect_NullContext_ReturnsFalse()
        {
            var eff = CreateEffect(new List<FeedbackSequenceStep> { new FeedbackSequenceStep() });

            Assert.IsFalse(eff.ApplyEffect(null));
        }

        [Test]
        public void ApplyEffect_EmptySteps_NoOp_DoesNotTouchService()
        {
            ServiceLocator.AddService<IFeedbackService>(_service);
            var eff = CreateEffect(new List<FeedbackSequenceStep>());

            LogAssert.Expect(LogType.Warning, "[EffPlaySequence] Sin steps autorados — no-op.");
            var result = eff.ApplyEffect(new EffectContext());

            Assert.IsTrue(result);
            Assert.AreEqual(0, _service.Requests.Count);
        }

        [Test]
        public void ApplyEffect_NoService_NoOp_ReturnsTrue()
        {
            var eff = CreateEffect(new List<FeedbackSequenceStep> { new FeedbackSequenceStep() });

            LogAssert.Expect(LogType.Warning, "[EffPlaySequence] IFeedbackService no registrado — no-op.");
            var result = eff.ApplyEffect(new EffectContext());

            Assert.IsTrue(result);
        }

        [Test]
        public void ApplyEffect_SendsSequenceRequestToService()
        {
            ServiceLocator.AddService<IFeedbackService>(_service);
            var steps = new List<FeedbackSequenceStep>
            {
                new FeedbackSequenceStep(),
                new FeedbackSequenceStep { BlockSequence = false },
            };
            var eff = CreateEffect(steps);

            var result = eff.ApplyEffect(new EffectContext { SourceGuid = Guid.NewGuid() });

            Assert.IsTrue(result);
            Assert.AreEqual(1, _service.Requests.Count);
            var sent = _service.Requests[0];
            Assert.IsTrue(sent.IsSequence);
            Assert.AreEqual(2, sent.SequenceSteps.Count);
        }

        [Test]
        public void ApplyEffect_CompletionCallback_WithoutTurnManager_DoesNotThrow()
        {
            ServiceLocator.AddService<IFeedbackService>(_service);
            var eff = CreateEffect(new List<FeedbackSequenceStep> { new FeedbackSequenceStep() });

            eff.ApplyEffect(new EffectContext());

            Assert.DoesNotThrow(() => _service.Callbacks[0].Invoke());
        }

        // ── Helpers ─────────────────────────────────────────────────────

        private static EffPlaySequence CreateEffect(List<FeedbackSequenceStep> steps)
        {
            var eff = new EffPlaySequence();
            typeof(EffPlaySequence)
                .GetField("_steps", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(eff, steps);
            return eff;
        }

        private sealed class RecordingFeedbackService : IFeedbackService
        {
            public readonly List<FeedbackRequest> Requests = new List<FeedbackRequest>();
            public readonly List<Action> Callbacks = new List<Action>();

            public void RequestFeedbackBlocking(FeedbackRequest request, Action onComplete)
            {
                Requests.Add(request);
                Callbacks.Add(onComplete);
            }
        }
    }
}
