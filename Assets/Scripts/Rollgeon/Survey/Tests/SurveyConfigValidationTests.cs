using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Rollgeon.Survey.Tests
{
    /// <summary>Validación de <see cref="SurveyConfigSO"/> y textos por idioma (Feature#0074).</summary>
    [TestFixture]
    public class SurveyConfigValidationTests
    {
        private static SurveyConfigSO Defaults()
        {
            var config = ScriptableObject.CreateInstance<SurveyConfigSO>();
            SurveyConfigDefaults.Populate(config);
            return config;
        }

        private static List<string> Errors(SurveyConfigSO config, List<string> warnings = null)
        {
            var errors = new List<string>();
            config.Validate(errors, warnings);
            return errors;
        }

        [Test]
        public void test_config_defaults_are_valid_with_offline_warning()
        {
            var config = Defaults();
            var warnings = new List<string>();

            var errors = Errors(config, warnings);

            Assert.IsEmpty(errors, string.Join(" | ", errors));
            Assert.AreEqual(1, warnings.Count, "Sin endpoint tiene que avisar, no fallar.");
        }

        [Test]
        public void test_config_with_https_endpoint_has_no_warning()
        {
            var config = Defaults();
            config.EndpointUrl = "https://script.google.com/macros/s/abc/exec";
            var warnings = new List<string>();

            Assert.IsTrue(config.Validate(new List<string>(), warnings));
            Assert.IsEmpty(warnings);
        }

        [Test]
        public void test_config_http_endpoint_is_error()
        {
            var config = Defaults();
            config.EndpointUrl = "http://script.google.com/macros/s/abc/exec";

            Assert.AreEqual(1, Errors(config).Count);
        }

        [Test]
        public void test_config_duplicate_ids_case_insensitive_is_error()
        {
            var config = Defaults();
            config.Questions[1].Id = "FUN";

            StringAssert.Contains("duplicado", string.Join(" ", Errors(config)));
        }

        [Test]
        public void test_config_empty_id_is_error()
        {
            var config = Defaults();
            config.Questions[0].Id = " ";

            StringAssert.Contains("Id vacío", string.Join(" ", Errors(config)));
        }

        [Test]
        public void test_config_id_with_dash_is_error()
        {
            var config = Defaults();
            config.Questions[0].Id = "fun-level";

            StringAssert.Contains("letras, números y _", string.Join(" ", Errors(config)));
        }

        [Test]
        public void test_config_choice_with_one_option_is_error()
        {
            var config = Defaults();
            var choice = config.Questions[2];
            choice.OptionsEs = new List<string> { "solo una" };
            choice.OptionsEn = new List<string>();

            StringAssert.Contains("al menos 2 opciones", string.Join(" ", Errors(config)));
        }

        [Test]
        public void test_config_options_en_mismatch_is_error()
        {
            var config = Defaults();
            config.Questions[2].OptionsEn.RemoveAt(0);

            StringAssert.Contains("OptionsEn", string.Join(" ", Errors(config)));
        }

        [Test]
        public void test_config_options_en_empty_is_ok()
        {
            var config = Defaults();
            config.Questions[2].OptionsEn = new List<string>();

            Assert.IsEmpty(Errors(config));
        }

        [Test]
        public void test_config_event_id_with_slash_is_error()
        {
            var config = Defaults();
            config.EventId = "expo/2026";

            StringAssert.Contains("EventId", string.Join(" ", Errors(config)));
        }

        [Test]
        public void test_config_event_id_empty_is_error()
        {
            var config = Defaults();
            config.EventId = "";

            StringAssert.Contains("EventId vacío", string.Join(" ", Errors(config)));
        }

        [Test]
        public void test_config_event_id_too_long_is_error()
        {
            var config = Defaults();
            config.EventId = new string('a', SurveyConfigSO.MaxEventIdLength + 1);

            StringAssert.Contains("caracteres", string.Join(" ", Errors(config)));
        }

        [Test]
        public void test_config_no_questions_is_error()
        {
            var config = Defaults();
            config.Questions.Clear();

            StringAssert.Contains("No hay preguntas", string.Join(" ", Errors(config)));
            Assert.IsFalse(config.HasQuestions);
        }

        [Test]
        public void test_config_freetext_zero_maxlength_is_error()
        {
            var config = Defaults();
            config.Questions[3].MaxLength = 0;

            StringAssert.Contains("MaxLength", string.Join(" ", Errors(config)));
        }

        [Test]
        public void test_config_empty_text_es_is_error()
        {
            var config = Defaults();
            config.Questions[0].TextEs = "";

            StringAssert.Contains("TextEs vacío", string.Join(" ", Errors(config)));
        }

        [Test]
        public void test_question_get_text_en_falls_back_to_es_when_empty()
        {
            var q = new SurveyQuestion { TextEs = "Hola", TextEn = "" };

            Assert.AreEqual("Hola", q.GetText("en"));
            Assert.AreEqual("Hola", q.GetText("es"));
            Assert.AreEqual("Hola", q.GetText(null));
        }

        [Test]
        public void test_question_get_text_en_uses_english_when_present()
        {
            var q = new SurveyQuestion { TextEs = "Hola", TextEn = "Hello" };

            Assert.AreEqual("Hello", q.GetText("en"));
            Assert.AreEqual("Hello", q.GetText("en-US"));
            Assert.AreEqual("Hola", q.GetText("es-AR"));
        }

        [Test]
        public void test_question_get_options_en_requires_same_count()
        {
            var q = new SurveyQuestion
            {
                OptionsEs = new List<string> { "a", "b" },
                OptionsEn = new List<string> { "A" },
            };

            CollectionAssert.AreEqual(new[] { "a", "b" }, q.GetOptions("en"));

            q.OptionsEn.Add("B");
            CollectionAssert.AreEqual(new[] { "A", "B" }, q.GetOptions("en"));
            CollectionAssert.AreEqual(new[] { "a", "b" }, q.GetOptions("es"));
        }
    }
}
