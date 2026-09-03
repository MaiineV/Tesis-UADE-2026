using NUnit.Framework;
using Rollgeon.UI.Screens;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    /// <summary>Decisión de la tecla de pausa según el top del stack (Feature#0074).</summary>
    [TestFixture]
    public class PauseHotkeyRuleTests
    {
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("screens");
            _go.SetActive(false);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        [Test]
        public void test_rule_pause_on_top_pops()
        {
            var pause = _go.AddComponent<PauseMenuOverlay>();

            Assert.AreEqual(PauseHotkeyAction.Pop, PauseHotkeyRule.Resolve(pause));
        }

        [Test]
        public void test_rule_options_on_top_pops()
        {
            var options = _go.AddComponent<OptionsScreen>();

            Assert.AreEqual(PauseHotkeyAction.Pop, PauseHotkeyRule.Resolve(options));
        }

        [Test]
        public void test_rule_survey_on_top_ignores()
        {
            var survey = _go.AddComponent<SurveyOverlay>();

            Assert.AreEqual(PauseHotkeyAction.Ignore, PauseHotkeyRule.Resolve(survey));
        }

        [Test]
        public void test_rule_other_screen_pushes()
        {
            var defeat = _go.AddComponent<DefeatScreen>();

            Assert.AreEqual(PauseHotkeyAction.Push, PauseHotkeyRule.Resolve(defeat));
        }

        [Test]
        public void test_rule_empty_stack_pushes()
        {
            Assert.AreEqual(PauseHotkeyAction.Push, PauseHotkeyRule.Resolve(null));
        }
    }
}
