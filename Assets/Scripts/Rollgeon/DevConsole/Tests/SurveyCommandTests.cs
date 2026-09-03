using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.DevConsole.Commands;
using Rollgeon.Survey;
using Rollgeon.UI;
using UnityEngine;

namespace Rollgeon.DevConsole.Tests
{
    /// <summary>Comando <c>survey</c> (Feature#0074): subcomandos contra un servicio fake.</summary>
    public class SurveyCommandTests
    {
        private sealed class FakeSurveyService : ISurveyService
        {
            public bool IsEnabled { get; set; } = true;
            public bool IsEventBuild => false;
            public SurveyConfigSO Config { get; set; }
            public bool PromptedThisRun { get; set; }
            public int PendingCount => Pending.Count;
            public List<string> Pending { get; } = new List<string>();
            public IReadOnlyList<string> PendingKeys => Pending;
            public int FlushCalls;
            public int ResetCalls;
            public readonly List<SurveyResponse> Submitted = new List<SurveyResponse>();

            public bool ShouldPrompt(int floorIndex) => false;
            public void MarkPrompted() => PromptedThisRun = true;
            public void ResetPromptGuard() => ResetCalls++;
            public void Submit(SurveyResponse response)
            {
                response.response_id = "fake-id";
                Submitted.Add(response);
            }
            public void FlushPending() => FlushCalls++;
#pragma warning disable 67
            public event Action<string, SurveyDeliveryState> DeliveryChanged;
#pragma warning restore 67
        }

        private sealed class SpyScreenManager : IScreenManager
        {
            public IBaseScreen Current => null;
            public List<string> PushedOverlays { get; } = new List<string>();
            public void Push<TScreen>(IScreenPayload payload = null) where TScreen : class, IBaseScreen { }
            public void PushByStringId(string screenId, IScreenPayload payload = null) { }
            public void PopCurrent() { }
            public void PushOverlay<TScreen>(IScreenPayload payload = null) where TScreen : class, IBaseScreen
                => PushedOverlays.Add(typeof(TScreen).Name);
            public void PopOverlay() { }
            public void RegisterScreen(IBaseScreen screen) { }
            public void UnregisterScreen(IBaseScreen screen) { }
        }

        private FakeConsoleContext _ctx;
        private FakeSurveyService _survey;
        private SurveyConfigSO _config;
        private SurveyCommand _cmd;

        [SetUp]
        public void SetUp()
        {
            _ctx = new FakeConsoleContext();
            _config = ScriptableObject.CreateInstance<SurveyConfigSO>();
            SurveyConfigDefaults.Populate(_config);
            _config.EndpointUrl = "https://script.google.com/macros/s/x/exec";
            _survey = new FakeSurveyService { Config = _config };
            _ctx.Register<ISurveyService>(_survey);
            _cmd = new SurveyCommand();
        }

        [TearDown]
        public void TearDown()
        {
            if (_config != null) UnityEngine.Object.DestroyImmediate(_config);
        }

        [Test]
        public void test_survey_noArgs_fails()
        {
            var result = _cmd.Execute(new string[0], _ctx);

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void test_survey_status_reportsConfigAndPending()
        {
            _survey.Pending.Add("k1");

            var result = _cmd.Execute(new[] { "status" }, _ctx);

            Assert.IsTrue(result.Success);
            StringAssert.Contains("evento-2026", result.Message);
            StringAssert.Contains("pendientes: 1", result.Message);
        }

        [Test]
        public void test_survey_status_withoutService_fails()
        {
            var ctx = new FakeConsoleContext();

            var result = _cmd.Execute(new[] { "status" }, ctx);

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void test_survey_show_pushesOverlay()
        {
            var screens = new SpyScreenManager();
            _ctx.Register<IScreenManager>(screens);

            var result = _cmd.Execute(new[] { "show" }, _ctx);

            Assert.IsTrue(result.Success);
            CollectionAssert.AreEqual(new[] { "SurveyOverlay" }, screens.PushedOverlays);
        }

        [Test]
        public void test_survey_pending_listsKeys()
        {
            _survey.Pending.Add("20260101-000001_a");
            _survey.Pending.Add("20260101-000002_b");

            var result = _cmd.Execute(new[] { "pending" }, _ctx);

            Assert.IsTrue(result.Success);
            StringAssert.Contains("20260101-000001_a", result.Message);
            StringAssert.Contains("2 pendiente", result.Message);
        }

        [Test]
        public void test_survey_flush_callsService()
        {
            _survey.Pending.Add("k");

            var result = _cmd.Execute(new[] { "flush" }, _ctx);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, _survey.FlushCalls);
        }

        [Test]
        public void test_survey_flush_withoutEndpoint_fails()
        {
            _survey.Pending.Add("k");
            _config.EndpointUrl = "";

            var result = _cmd.Execute(new[] { "flush" }, _ctx);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(0, _survey.FlushCalls);
        }

        [Test]
        public void test_survey_test_submitsSyntheticResponse()
        {
            var result = _cmd.Execute(new[] { "test" }, _ctx);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, _survey.Submitted.Count);
            Assert.AreEqual("test", _survey.Submitted[0].answers[0].id);
            StringAssert.Contains("fake-id", result.Message);
        }

        [Test]
        public void test_survey_reset_callsService()
        {
            var result = _cmd.Execute(new[] { "reset" }, _ctx);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, _survey.ResetCalls);
        }

        [Test]
        public void test_survey_unknownSubcommand_fails()
        {
            var result = _cmd.Execute(new[] { "wat" }, _ctx);

            Assert.IsFalse(result.Success);
        }
    }
}
