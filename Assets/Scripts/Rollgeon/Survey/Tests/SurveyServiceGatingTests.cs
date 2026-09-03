using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Run;
using UnityEngine;

namespace Rollgeon.Survey.Tests
{
    /// <summary>Cuándo se muestra la encuesta (Feature#0074): config, piso, una vez por run, tutorial.</summary>
    [TestFixture]
    public class SurveyServiceGatingTests
    {
        private InMemorySurveyStore _store;
        private FakeSurveySink _sink;
        private SurveyService _service;

        [SetUp]
        public void Setup()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            PendingRunRequest.Clear();
            _store = new InMemorySurveyStore();
            _sink = new FakeSurveySink();
        }

        [TearDown]
        public void Teardown()
        {
            _service?.Dispose();
            PendingRunRequest.Clear();
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        private SurveyService Build(SurveyConfigSO config, bool eventBuild = false)
        {
            _service = new SurveyService(config, _store, _sink, isEventBuild: eventBuild);
            _service.Subscribe();
            return _service;
        }

        [Test]
        public void test_gating_disabled_config_should_prompt_false()
        {
            var service = Build(SurveyTestConfig.Make(enabled: false, triggerFloor: 0));

            Assert.IsFalse(service.IsEnabled);
            Assert.IsFalse(service.ShouldPrompt(0));
        }

        [Test]
        public void test_gating_wrong_floor_false()
        {
            var service = Build(SurveyTestConfig.Make(triggerFloor: 1));

            Assert.IsFalse(service.ShouldPrompt(0));
            Assert.IsFalse(service.ShouldPrompt(2));
        }

        [Test]
        public void test_gating_matching_floor_true()
        {
            var service = Build(SurveyTestConfig.Make(triggerFloor: 1));

            Assert.IsTrue(service.ShouldPrompt(1));
        }

        [Test]
        public void test_gating_after_mark_prompted_false()
        {
            var service = Build(SurveyTestConfig.Make(triggerFloor: 0));

            service.MarkPrompted();

            Assert.IsFalse(service.ShouldPrompt(0));
            Assert.IsTrue(service.PromptedThisRun);
        }

        [Test]
        public void test_gating_run_start_resets_guard()
        {
            var service = Build(SurveyTestConfig.Make(triggerFloor: 0));
            service.MarkPrompted();

            EventManager.Trigger(EventName.OnRunStart, Guid.NewGuid(), "default");

            Assert.IsTrue(service.ShouldPrompt(0));
        }

        [Test]
        public void test_gating_reset_prompt_guard_reenables()
        {
            var service = Build(SurveyTestConfig.Make(triggerFloor: 0));
            service.MarkPrompted();

            service.ResetPromptGuard();

            Assert.IsTrue(service.ShouldPrompt(0));
        }

        [Test]
        public void test_gating_tutorial_run_false()
        {
            var service = Build(SurveyTestConfig.Make(triggerFloor: 0));
            PendingRunRequest.Set(null, Guid.NewGuid(), "default", isTutorial: true);

            EventManager.Trigger(EventName.OnRunStart, Guid.NewGuid(), "default");

            Assert.IsFalse(service.ShouldPrompt(0));
        }

        [Test]
        public void test_gating_normal_run_after_tutorial_true()
        {
            var service = Build(SurveyTestConfig.Make(triggerFloor: 0));
            PendingRunRequest.Set(null, Guid.NewGuid(), "default", isTutorial: true);
            EventManager.Trigger(EventName.OnRunStart, Guid.NewGuid(), "default");
            PendingRunRequest.Clear();

            EventManager.Trigger(EventName.OnRunStart, Guid.NewGuid(), "default");

            Assert.IsTrue(service.ShouldPrompt(0));
        }

        [Test]
        public void test_gating_null_config_false()
        {
            var service = Build(null);

            Assert.IsFalse(service.IsEnabled);
            Assert.IsFalse(service.ShouldPrompt(0));
        }

        [Test]
        public void test_gating_no_questions_false()
        {
            var service = Build(SurveyTestConfig.Make(questionCount: 0));

            Assert.IsFalse(service.IsEnabled);
        }

        [Test]
        public void test_gating_event_build_overrides_disabled_tick()
        {
            var config = SurveyTestConfig.Make(enabled: false, triggerFloor: 0);

            Assert.IsTrue(SurveyService.ResolveEnabled(config, isEventBuild: true));
            Assert.IsFalse(SurveyService.ResolveEnabled(config, isEventBuild: false));

            var service = Build(config, eventBuild: true);
            Assert.IsTrue(service.IsEnabled);
            Assert.IsTrue(service.IsEventBuild);
            Assert.IsTrue(service.ShouldPrompt(0));
        }

        [Test]
        public void test_gating_event_build_without_questions_still_false()
        {
            var config = SurveyTestConfig.Make(enabled: true, questionCount: 0);

            Assert.IsFalse(SurveyService.ResolveEnabled(config, isEventBuild: true));
        }

        [Test]
        public void test_gating_unsubscribe_stops_listening_run_start()
        {
            var service = Build(SurveyTestConfig.Make(triggerFloor: 0));
            service.MarkPrompted();
            service.Unsubscribe();

            EventManager.Trigger(EventName.OnRunStart, Guid.NewGuid(), "default");

            Assert.IsFalse(service.ShouldPrompt(0), "Desuscripto no debería resetear el guard.");
        }

        [Test]
        public void test_gating_config_defaults_from_setup_are_valid_and_disabled()
        {
            var config = ScriptableObject.CreateInstance<SurveyConfigSO>();
            SurveyConfigDefaults.Populate(config);

            Assert.IsFalse(config.Enabled, "El default no puede salir prendido en builds normales.");
            Assert.IsTrue(config.HasQuestions);
        }
    }
}
