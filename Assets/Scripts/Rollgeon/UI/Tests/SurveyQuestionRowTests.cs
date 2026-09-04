using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rollgeon.Survey;
using Rollgeon.UI.Screens;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    /// <summary>Filas del cuestionario (Feature#0074): opciones clonadas, valores, texto, idioma.</summary>
    [TestFixture]
    public class SurveyQuestionRowTests
    {
        private GameObject _rowGO;
        private SurveyQuestionRow _row;
        private TextMeshProUGUI _label;
        private Toggle _template;
        private Transform _container;
        private TMP_InputField _input;
        private Image _invalidFrame;

        [SetUp]
        public void SetUp()
        {
            _rowGO = new GameObject("Row");
            _row = _rowGO.AddComponent<SurveyQuestionRow>();

            _label = Child<TextMeshProUGUI>("Label");
            _invalidFrame = Child<Image>("Invalid");

            var containerGO = new GameObject("Options");
            containerGO.transform.SetParent(_rowGO.transform, false);
            _container = containerGO.transform;

            var templateGO = new GameObject("Template");
            templateGO.transform.SetParent(_container, false);
            _template = templateGO.AddComponent<Toggle>();
            var templateLabel = new GameObject("Label");
            templateLabel.transform.SetParent(templateGO.transform, false);
            templateLabel.AddComponent<TextMeshProUGUI>();
            templateGO.SetActive(false);

            var inputGO = new GameObject("Input");
            inputGO.transform.SetParent(_rowGO.transform, false);
            _input = inputGO.AddComponent<TMP_InputField>();
            var inputText = new GameObject("Text");
            inputText.transform.SetParent(inputGO.transform, false);
            _input.textComponent = inputText.AddComponent<TextMeshProUGUI>();

            AssignPrivate(_row, "_questionLabel", _label);
            AssignPrivate(_row, "_invalidFrame", _invalidFrame);
            AssignPrivate(_row, "_optionTemplate", _template);
            AssignPrivate(_row, "_optionContainer", _container);
            AssignPrivate(_row, "_textInput", _input);
        }

        [TearDown]
        public void TearDown()
        {
            if (_rowGO != null) Object.DestroyImmediate(_rowGO);
        }

        private T Child<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(_rowGO.transform, false);
            return go.AddComponent<T>();
        }

        private static SurveyQuestion Rating() => new SurveyQuestion
        {
            Id = "fun", Type = SurveyQuestionType.Rating1to5, TextEs = "¿Diversión?", TextEn = "Fun?", Required = true,
        };

        private static SurveyQuestion Choice() => new SurveyQuestion
        {
            Id = "fav", Type = SurveyQuestionType.SingleChoice, TextEs = "¿Favorito?", TextEn = "Favorite?",
            OptionsEs = new List<string> { "Dados", "Combos", "Arte" },
            OptionsEn = new List<string> { "Dice", "Combos", "Art" },
        };

        private static SurveyQuestion Text() => new SurveyQuestion
        {
            Id = "change", Type = SurveyQuestionType.FreeText, TextEs = "¿Qué cambiarías?", Required = false, MaxLength = 20,
        };

        [Test]
        public void test_row_rating_builds_five_options_and_no_value()
        {
            _row.Bind(Rating(), "es");

            Assert.AreEqual(5, _row.OptionCount);
            Assert.IsFalse(_row.TryGetValue(out _));
            Assert.AreEqual("¿Diversión?", _label.text);
            Assert.IsFalse(_template.gameObject.activeSelf, "El template queda apagado.");
        }

        [Test]
        public void test_row_rating_third_option_returns_three()
        {
            _row.Bind(Rating(), "es");

            _row.SelectOption(2);

            Assert.IsTrue(_row.TryGetValue(out var value));
            Assert.AreEqual("3", value);
        }

        [Test]
        public void test_row_choice_returns_zero_based_index()
        {
            _row.Bind(Choice(), "es");

            _row.SelectOption(1);

            Assert.IsTrue(_row.TryGetValue(out var value));
            Assert.AreEqual("1", value);
            Assert.AreEqual(3, _row.OptionCount);
        }

        [Test]
        public void test_row_choice_uses_english_labels_when_complete()
        {
            _row.Bind(Choice(), "en");

            Assert.AreEqual("Favorite?", _label.text);
            Assert.AreEqual("Dice", OptionLabel(0));
        }

        [Test]
        public void test_row_relabel_changes_texts_without_losing_selection()
        {
            _row.Bind(Choice(), "es");
            _row.SelectOption(2);

            _row.Relabel("en");

            Assert.AreEqual("Art", OptionLabel(2));
            Assert.IsTrue(_row.TryGetValue(out var value));
            Assert.AreEqual("2", value);
        }

        [Test]
        public void test_row_rebind_clears_previous_selection()
        {
            _row.Bind(Rating(), "es");
            _row.SelectOption(4);

            _row.Bind(Rating(), "es");

            Assert.IsFalse(_row.TryGetValue(out _));
            Assert.AreEqual(5, _row.OptionCount, "No se acumulan clones al rebindear.");
        }

        [Test]
        public void test_row_text_whitespace_is_no_value()
        {
            _row.Bind(Text(), "es");

            _row.SetText("   ");

            Assert.IsFalse(_row.TryGetValue(out _));
        }

        [Test]
        public void test_row_text_trims_and_applies_max_length()
        {
            _row.Bind(Text(), "es");

            _row.SetText("  hola  ");

            Assert.IsTrue(_row.TryGetValue(out var value));
            Assert.AreEqual("hola", value);
            Assert.AreEqual(20, _input.characterLimit);
        }

        [Test]
        public void test_row_set_invalid_toggles_frame()
        {
            _row.Bind(Rating(), "es");

            _row.SetInvalid(true);
            Assert.IsTrue(_invalidFrame.enabled);

            _row.SetInvalid(false);
            Assert.IsFalse(_invalidFrame.enabled);
        }

        [Test]
        public void test_row_without_template_does_not_throw()
        {
            AssignPrivate(_row, "_optionTemplate", null);
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;

            try
            {
                Assert.DoesNotThrow(() => _row.Bind(Rating(), "es"));
            }
            finally
            {
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
            }

            Assert.AreEqual(0, _row.OptionCount);
            Assert.IsFalse(_row.TryGetValue(out _));
        }

        private string OptionLabel(int index)
        {
            var option = _container.Find($"Option_{index}");
            Assert.IsNotNull(option, $"Option_{index} no existe.");
            return option.GetComponentInChildren<TMP_Text>(true).text;
        }

        private static void AssignPrivate(object target, string fieldName, object value)
        {
            FieldInfo field = null;
            var type = target.GetType();
            while (type != null && field == null)
            {
                field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                type = type.BaseType;
            }
            Assert.IsNotNull(field, $"Field '{fieldName}' not found in {target.GetType().Name}.");
            field.SetValue(target, value);
        }
    }
}
