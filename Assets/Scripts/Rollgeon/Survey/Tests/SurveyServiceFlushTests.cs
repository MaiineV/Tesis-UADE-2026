using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Run;

namespace Rollgeon.Survey.Tests
{
    /// <summary>Reintento de pendientes (Feature#0074): secuencial, sin duplicar, tolerante a fallos.</summary>
    [TestFixture]
    public class SurveyServiceFlushTests
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
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        private SurveyService Build(SurveyConfigSO config = null)
        {
            _service = new SurveyService(config ?? SurveyTestConfig.Make(), _store, _sink, isEventBuild: false);
            _service.Subscribe();
            return _service;
        }

        private void SeedPending(string key, string responseId)
        {
            var response = SurveyTestConfig.MakeResponse(id: responseId);
            response.event_id = "seed";
            _store.Pending[key] = SurveyPayload.ToStoredJson(response);
        }

        [Test]
        public void test_flush_two_pending_sends_both_in_order_and_marks_sent()
        {
            var service = Build();
            SeedPending("20260101-000001_a", "a");
            SeedPending("20260101-000002_b", "b");

            service.FlushPending();

            Assert.AreEqual(2, _sink.SentBodies.Count);
            StringAssert.Contains("\"response_id\":\"a\"", _sink.SentBodies[0]);
            StringAssert.Contains("\"response_id\":\"b\"", _sink.SentBodies[1]);
            Assert.AreEqual(0, _store.PendingCount);
            Assert.AreEqual(2, _store.Sent.Count);
        }

        [Test]
        public void test_flush_first_fails_second_still_attempted()
        {
            var service = Build();
            SeedPending("20260101-000001_a", "a");
            SeedPending("20260101-000002_b", "b");
            _sink.AutoComplete = false;

            service.FlushPending();
            _sink.CompleteNext(false);
            _sink.CompleteNext(true);

            Assert.AreEqual(2, _sink.SentBodies.Count);
            Assert.AreEqual(1, _store.PendingCount, "La que falló sigue pendiente.");
            Assert.IsTrue(_store.Pending.ContainsKey("20260101-000001_a"));
            Assert.IsTrue(_store.Sent.ContainsKey("20260101-000002_b"));
        }

        [Test]
        public void test_flush_reentrant_does_not_duplicate()
        {
            var service = Build();
            SeedPending("20260101-000001_a", "a");
            _sink.AutoComplete = false;

            service.FlushPending();
            service.FlushPending();

            Assert.AreEqual(1, _sink.SentBodies.Count);
            Assert.AreEqual(1, _sink.InFlightCount);
        }

        [Test]
        public void test_flush_after_previous_completes_runs_again()
        {
            var service = Build();
            SeedPending("20260101-000001_a", "a");
            _sink.AutoComplete = false;

            service.FlushPending();
            _sink.CompleteNext(false);
            service.FlushPending();

            Assert.AreEqual(2, _sink.SentBodies.Count, "Tras terminar el flush anterior se reintenta.");
        }

        [Test]
        public void test_flush_empty_store_no_send()
        {
            var service = Build();

            service.FlushPending();

            Assert.AreEqual(0, _sink.SentBodies.Count);
        }

        [Test]
        public void test_flush_disabled_service_no_send()
        {
            var service = Build(SurveyTestConfig.Make(enabled: false));
            SeedPending("20260101-000001_a", "a");

            service.FlushPending();

            Assert.AreEqual(0, _sink.SentBodies.Count);
            Assert.AreEqual(1, _store.PendingCount);
        }

        [Test]
        public void test_flush_sink_not_configured_no_send()
        {
            var service = Build();
            SeedPending("20260101-000001_a", "a");
            _sink.IsConfigured = false;

            service.FlushPending();

            Assert.AreEqual(0, _sink.SentBodies.Count);
        }

        [Test]
        public void test_flush_skips_key_with_submit_in_flight()
        {
            var service = Build();
            _sink.AutoComplete = false;
            service.Submit(SurveyTestConfig.MakeResponse(id: "live"));

            service.FlushPending();

            Assert.AreEqual(1, _sink.SentBodies.Count, "La respuesta en vuelo no se manda dos veces.");
        }

        [Test]
        public void test_flush_corrupt_file_is_skipped()
        {
            var service = Build();
            _store.Pending["20260101-000001_bad"] = "esto no es json";
            SeedPending("20260101-000002_ok", "ok");

            service.FlushPending();

            Assert.AreEqual(1, _sink.SentBodies.Count);
            Assert.IsTrue(_store.Sent.ContainsKey("20260101-000002_ok"));
            Assert.IsTrue(_store.Pending.ContainsKey("20260101-000001_bad"), "El corrupto queda para inspección manual.");
        }

        [Test]
        public void test_flush_run_start_triggers_flush()
        {
            var service = Build();
            SeedPending("20260101-000001_a", "a");

            EventManager.Trigger(EventName.OnRunStart, Guid.NewGuid(), "default");

            Assert.AreEqual(1, _sink.SentBodies.Count);
        }

        [Test]
        public void test_flush_successful_submit_drains_older_pending()
        {
            var service = Build();
            SeedPending("20260101-000001_old", "old");

            service.Submit(SurveyTestConfig.MakeResponse(id: "new"));

            Assert.AreEqual(2, _sink.SentBodies.Count);
            Assert.AreEqual(0, _store.PendingCount);
        }

        [Test]
        public void test_flush_pending_keys_reports_store_order()
        {
            var service = Build();
            SeedPending("20260101-000002_b", "b");
            SeedPending("20260101-000001_a", "a");

            CollectionAssert.AreEqual(new[] { "20260101-000001_a", "20260101-000002_b" }, service.PendingKeys);
        }
    }
}
