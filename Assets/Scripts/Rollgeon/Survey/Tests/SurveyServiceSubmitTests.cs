using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Run;

namespace Rollgeon.Survey.Tests
{
    /// <summary>Submit offline-first (Feature#0074): disco antes que red, metadata, estados.</summary>
    [TestFixture]
    public class SurveyServiceSubmitTests
    {
        private static readonly DateTime FixedNow = new DateTime(2026, 9, 3, 15, 30, 45, DateTimeKind.Utc);

        private InMemorySurveyStore _store;
        private FakeSurveySink _sink;
        private SurveyService _service;
        private List<(string Id, SurveyDeliveryState State)> _states;

        [SetUp]
        public void Setup()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            PendingRunRequest.Clear();
            _store = new InMemorySurveyStore();
            _sink = new FakeSurveySink();
            _states = new List<(string, SurveyDeliveryState)>();
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
            config ??= SurveyTestConfig.Make(secret: "s3cr3t");
            _service = new SurveyService(
                config, _store, _sink,
                utcNow: () => FixedNow,
                deviceId: () => "device-A",
                appVersion: () => "9.9.9",
                isEventBuild: false);
            _service.DeliveryChanged += (id, state) => _states.Add((id, state));
            return _service;
        }

        [Test]
        public void test_submit_writes_store_before_sink_send()
        {
            var service = Build();
            bool wasOnDiskWhenSent = false;
            _sink.OnSend = _ => wasOnDiskWhenSent = _store.Pending.Count == 1;

            service.Submit(SurveyTestConfig.MakeResponse());

            Assert.IsTrue(wasOnDiskWhenSent, "El sink tiene que ver la respuesta ya guardada en disco.");
            Assert.AreEqual(1, _sink.SentBodies.Count);
        }

        [Test]
        public void test_submit_sink_ok_marks_sent()
        {
            var service = Build();
            _sink.NextResult = true;

            service.Submit(SurveyTestConfig.MakeResponse());

            Assert.AreEqual(0, _store.PendingCount);
            Assert.AreEqual(1, _store.Sent.Count);
            Assert.AreEqual(0, service.PendingCount);
        }

        [Test]
        public void test_submit_sink_fails_keeps_pending()
        {
            var service = Build();
            _sink.NextResult = false;

            service.Submit(SurveyTestConfig.MakeResponse());

            Assert.AreEqual(1, _store.PendingCount);
            Assert.AreEqual(0, _store.Sent.Count);
        }

        [Test]
        public void test_submit_sink_not_configured_stays_pending_without_send()
        {
            var service = Build();
            _sink.IsConfigured = false;

            service.Submit(SurveyTestConfig.MakeResponse());

            Assert.AreEqual(1, _store.PendingCount);
            Assert.AreEqual(0, _sink.SentBodies.Count);
            CollectionAssert.AreEqual(
                new[] { SurveyDeliveryState.Pending },
                _states.ConvertAll(s => s.State),
                "Sin endpoint no hay Sending ni Failed: queda Pending.");
        }

        [Test]
        public void test_submit_sink_throws_marks_failed_and_keeps_pending()
        {
            var service = Build();
            _sink.ThrowOnSend = true;

            service.Submit(SurveyTestConfig.MakeResponse());

            Assert.AreEqual(1, _store.PendingCount);
            Assert.AreEqual(SurveyDeliveryState.Failed, _states[_states.Count - 1].State);
        }

        [Test]
        public void test_submit_store_throws_still_tries_to_send()
        {
            var service = Build();
            _store.ThrowOnWrite = true;
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;

            try
            {
                service.Submit(SurveyTestConfig.MakeResponse());
            }
            finally
            {
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
            }

            Assert.AreEqual(1, _sink.SentBodies.Count, "Sin disco, igual vale intentar la red.");
        }

        [Test]
        public void test_submit_fills_metadata_from_injected_funcs()
        {
            var service = Build(SurveyTestConfig.Make(eventId: "expo-x"));
            var response = SurveyTestConfig.MakeResponse();

            service.Submit(response);

            Assert.IsFalse(string.IsNullOrEmpty(response.response_id));
            Assert.AreEqual("expo-x", response.event_id);
            Assert.AreEqual(FixedNow.ToString("o"), response.created_at);
            Assert.AreEqual("9.9.9", response.app_version);
            Assert.AreEqual("device-A", response.device_id);
        }

        [Test]
        public void test_submit_keeps_explicit_response_id()
        {
            var service = Build();

            service.Submit(SurveyTestConfig.MakeResponse(id: "abc123"));

            Assert.AreEqual("abc123", _states[0].Id);
            StringAssert.EndsWith("_abc123", _store.Sent.Keys.GetEnumerator().MoveNextAndGet());
        }

        [Test]
        public void test_submit_store_key_has_chronological_prefix()
        {
            var key = SurveyService.BuildStoreKey(FixedNow, "abc");

            Assert.AreEqual("20260903-153045_abc", key);
        }

        [Test]
        public void test_submit_email_cleared_when_raffle_opt_out()
        {
            var service = Build();
            var response = SurveyTestConfig.MakeResponse(raffle: false, email: "alguien@mail.com");

            service.Submit(response);

            Assert.AreEqual(string.Empty, response.email);
            StringAssert.DoesNotContain("alguien@mail.com", _sink.SentBodies[0]);
        }

        [Test]
        public void test_submit_email_kept_when_raffle_opt_in()
        {
            var service = Build();

            service.Submit(SurveyTestConfig.MakeResponse(raffle: true, email: "alguien@mail.com"));

            StringAssert.Contains("alguien@mail.com", _sink.SentBodies[0]);
        }

        [Test]
        public void test_submit_wire_carries_secret_but_disk_does_not()
        {
            var service = Build(SurveyTestConfig.Make(secret: "s3cr3t"));
            _sink.NextResult = false;

            service.Submit(SurveyTestConfig.MakeResponse());

            StringAssert.Contains("s3cr3t", _sink.SentBodies[0]);
            foreach (var json in _store.Pending.Values)
            {
                StringAssert.DoesNotContain("s3cr3t", json);
            }
        }

        [Test]
        public void test_submit_emits_pending_sending_sent()
        {
            var service = Build();

            service.Submit(SurveyTestConfig.MakeResponse(id: "r1"));

            CollectionAssert.AreEqual(
                new[] { SurveyDeliveryState.Pending, SurveyDeliveryState.Sending, SurveyDeliveryState.Sent },
                _states.ConvertAll(s => s.State));
            Assert.IsTrue(_states.TrueForAll(s => s.Id == "r1"));
        }

        [Test]
        public void test_submit_null_is_noop()
        {
            var service = Build();

            service.Submit(null);

            Assert.AreEqual(0, _store.WriteCount);
            Assert.AreEqual(0, _sink.SentBodies.Count);
        }

        [Test]
        public void test_submit_disabled_service_still_persists_and_sends()
        {
            // Que la consola pueda mandar un 'survey test' aunque el tick esté apagado.
            var service = Build(SurveyTestConfig.Make(enabled: false));

            service.Submit(SurveyTestConfig.MakeResponse());

            Assert.AreEqual(1, _store.WriteCount);
            Assert.AreEqual(1, _sink.SentBodies.Count);
        }
    }

    internal static class EnumeratorExtensions
    {
        public static string MoveNextAndGet(this IEnumerator<string> enumerator)
        {
            Assert.IsTrue(enumerator.MoveNext(), "Colección vacía.");
            return enumerator.Current;
        }
    }
}
