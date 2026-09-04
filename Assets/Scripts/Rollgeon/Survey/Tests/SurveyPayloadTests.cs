using System.Collections.Generic;
using NUnit.Framework;

namespace Rollgeon.Survey.Tests
{
    /// <summary>Forma del JSON (Feature#0074): wire vs disco, escape, respuesta del Apps Script.</summary>
    [TestFixture]
    public class SurveyPayloadTests
    {
        private static SurveyResponse Sample()
        {
            return new SurveyResponse
            {
                response_id = "r1",
                event_id = "expo",
                created_at = "2026-09-03T15:30:45.0000000Z",
                app_version = "0.4.0",
                run_id = "run",
                floor_index = 2,
                hero_id = "hero.warrior",
                locale = "es",
                device_id = "dev",
                raffle_opt_in = true,
                email = "a@b.c",
                answers = new List<SurveyAnswer>
                {
                    new SurveyAnswer("fun", "5"),
                    new SurveyAnswer("change", "nada"),
                },
            };
        }

        [Test]
        public void test_payload_wire_contains_secret_stored_does_not()
        {
            var response = Sample();

            string wire = SurveyPayload.ToWireJson(response, "s3cr3t");
            string stored = SurveyPayload.ToStoredJson(response);

            StringAssert.Contains("\"secret\":\"s3cr3t\"", wire);
            StringAssert.DoesNotContain("secret", stored);
        }

        [Test]
        public void test_payload_wire_keeps_every_field_at_top_level()
        {
            string wire = SurveyPayload.ToWireJson(Sample(), "x");

            StringAssert.Contains("\"response_id\":\"r1\"", wire);
            StringAssert.Contains("\"event_id\":\"expo\"", wire);
            StringAssert.Contains("\"floor_index\":2", wire);
            StringAssert.Contains("\"raffle_opt_in\":true", wire);
            StringAssert.Contains("\"email\":\"a@b.c\"", wire);
        }

        [Test]
        public void test_payload_answers_serialize_as_array_of_id_value()
        {
            string json = SurveyPayload.ToStoredJson(Sample());

            StringAssert.Contains("\"answers\":[{\"id\":\"fun\",\"value\":\"5\"},{\"id\":\"change\",\"value\":\"nada\"}]", json);
        }

        [Test]
        public void test_payload_round_trip_escapes_special_chars()
        {
            var response = Sample();
            response.answers[1].value = "dijo \"hola\"\ncon barra \\ y tildes áéíóú y emoji 🎲";

            var back = SurveyPayload.FromStoredJson(SurveyPayload.ToStoredJson(response));

            Assert.IsNotNull(back);
            Assert.AreEqual(response.answers[1].value, back.answers[1].value);
            Assert.AreEqual("r1", back.response_id);
            Assert.AreEqual(2, back.floor_index);
            Assert.IsTrue(back.raffle_opt_in);
        }

        [Test]
        public void test_payload_null_secret_serializes_empty_string()
        {
            string wire = SurveyPayload.ToWireJson(Sample(), null);

            StringAssert.Contains("\"secret\":\"\"", wire);
        }

        [Test]
        public void test_payload_from_stored_invalid_json_returns_null()
        {
            Assert.IsNull(SurveyPayload.FromStoredJson("no es json"));
            Assert.IsNull(SurveyPayload.FromStoredJson(null));
            Assert.IsNull(SurveyPayload.FromStoredJson("   "));
        }

        [Test]
        public void test_sink_accepted_requires_200_and_ok_true()
        {
            Assert.IsTrue(AppsScriptSurveySink.Accepted(new SurveyPostResult(true, 200, "{\"ok\":true}", null)));
            Assert.IsFalse(AppsScriptSurveySink.Accepted(new SurveyPostResult(true, 200, "{\"ok\":false,\"error\":\"unauthorized\"}", null)));
            Assert.IsFalse(AppsScriptSurveySink.Accepted(new SurveyPostResult(true, 302, "", null)));
            Assert.IsFalse(AppsScriptSurveySink.Accepted(new SurveyPostResult(false, 0, null, "timeout")));
            Assert.IsFalse(AppsScriptSurveySink.Accepted(new SurveyPostResult(true, 200, "<html>login</html>", null)));
            Assert.IsFalse(AppsScriptSurveySink.Accepted(new SurveyPostResult(true, 200, "", null)));
        }

        [Test]
        public void test_sink_not_configured_reports_false_without_posting()
        {
            var transport = new FakeSurveyTransport();
            var sink = new AppsScriptSurveySink("  ", 10, transport);
            bool? result = null;

            sink.Send("{}", ok => result = ok);

            Assert.IsFalse(sink.IsConfigured);
            Assert.AreEqual(false, result);
            Assert.AreEqual(0, transport.Posts.Count);
        }

        [Test]
        public void test_sink_posts_body_to_url_with_timeout()
        {
            var transport = new FakeSurveyTransport();
            var sink = new AppsScriptSurveySink("https://script.google.com/macros/s/x/exec", 7, transport);
            bool? result = null;

            sink.Send("{\"a\":1}", ok => result = ok);

            Assert.AreEqual(true, result);
            Assert.AreEqual(1, transport.Posts.Count);
            Assert.AreEqual("https://script.google.com/macros/s/x/exec", transport.Posts[0].Url);
            Assert.AreEqual("{\"a\":1}", transport.Posts[0].Body);
            Assert.AreEqual(7, transport.Posts[0].Timeout);
        }
    }
}
