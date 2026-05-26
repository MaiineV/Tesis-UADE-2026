using System;
using System.Reflection;
using NUnit.Framework;
using Rollgeon.Heroes;
using Rollgeon.UI.HUD;
using Rollgeon.UI.Screens;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="BuildSelectionScreen"/> (UI#0013a).
    /// <list type="bullet">
    /// <item><description><see cref="ScreenStringId_Returns_BuildSelectionScreen"/> — literal match.</description></item>
    /// <item><description><see cref="OnPushed_WithPayload_PopulatesHeroName"/> — hero name label populated.</description></item>
    /// <item><description><see cref="OnPushed_WithNullPayload_DoesNotCrash"/> — graceful degradation.</description></item>
    /// <item><description><see cref="OnPushed_WithNullDiceBag_ShowsFallbackLabel"/> — fallback label active.</description></item>
    /// <item><description><see cref="OnPopped_CleansUpState"/> — listeners removed and state null.</description></item>
    /// </list>
    /// </summary>
    [TestFixture]
    public class BuildSelectionScreenTests
    {
        private GameObject _screenGO;
        private BuildSelectionScreen _screen;
        private Button _confirmButton;
        private Button _backButton;
        private Transform _diceContainer;
        private ClassHeroSO _hero;

        // TMP labels created via GameObject (no TMP_Settings needed in EditMode
        // if we only check .text — the component exists but won't render).
        private GameObject _heroNameGO;
        private GameObject _heroDescGO;
        private GameObject _fallbackGO;
        private GameObject _diceSlotPrefabGO;

        [SetUp]
        public void SetUp()
        {
            _screenGO = new GameObject("BuildSelectionScreen");
            _screenGO.SetActive(false);
            _screen = _screenGO.AddComponent<BuildSelectionScreen>();

            // Buttons
            _confirmButton = AttachButton("ConfirmButton");
            _backButton = AttachButton("BackButton");

            // Hero name label
            _heroNameGO = new GameObject("HeroNameLabel");
            _heroNameGO.transform.SetParent(_screenGO.transform, false);
            var heroNameLabel = _heroNameGO.AddComponent<TextMeshProUGUI>();

            // Hero description label
            _heroDescGO = new GameObject("HeroDescLabel");
            _heroDescGO.transform.SetParent(_screenGO.transform, false);
            var heroDescLabel = _heroDescGO.AddComponent<TextMeshProUGUI>();

            // Dice container
            var diceContainerGO = new GameObject("DiceContainer");
            diceContainerGO.transform.SetParent(_screenGO.transform, false);
            _diceContainer = diceContainerGO.transform;

            // Dice slot prefab (inactive template)
            _diceSlotPrefabGO = new GameObject("DiceSlotPrefab");
            _diceSlotPrefabGO.SetActive(false);
            var diceSlotPrefab = _diceSlotPrefabGO.AddComponent<DiceSlotView>();

            // Fallback label
            _fallbackGO = new GameObject("FallbackLabel");
            _fallbackGO.transform.SetParent(_screenGO.transform, false);
            var fallbackLabel = _fallbackGO.AddComponent<TextMeshProUGUI>();

            // Hero SO
            _hero = ScriptableObject.CreateInstance<ClassHeroSO>();
            _hero.EntityId = "hero.warrior";
            _hero.DisplayName = "Guerrero";
            _hero.Description = "Un guerrero valiente.";
            // StartingDiceBagRef left null for most tests

            // Wire serialized fields via reflection
            AssignPrivate(_screen, "_heroNameLabel", heroNameLabel);
            AssignPrivate(_screen, "_heroDescriptionLabel", heroDescLabel);
            AssignPrivate(_screen, "_diceContainer", _diceContainer);
            AssignPrivate(_screen, "_diceSlotPrefab", diceSlotPrefab);
            AssignPrivate(_screen, "_diceBagFallbackLabel", fallbackLabel);
            AssignPrivate(_screen, "_confirmButton", _confirmButton);
            AssignPrivate(_screen, "_backButton", _backButton);
        }

        [TearDown]
        public void TearDown()
        {
            if (_screenGO != null) UnityEngine.Object.DestroyImmediate(_screenGO);
            if (_hero != null) UnityEngine.Object.DestroyImmediate(_hero);
            if (_diceSlotPrefabGO != null) UnityEngine.Object.DestroyImmediate(_diceSlotPrefabGO);
        }

        [Test]
        public void ScreenStringId_Returns_BuildSelectionScreen()
        {
            Assert.AreEqual("BuildSelectionScreen", _screen.ScreenStringId,
                "Must match the literal string-id that ClassSelectionScreen navigates to.");
        }

        [Test]
        public void OnPushed_WithPayload_PopulatesHeroName()
        {
            var payload = new BuildSelectionPayload
            {
                SelectedHero = _hero,
                RunId = Guid.NewGuid(),
                RulesetId = "default"
            };

            InvokePushed(payload);

            var label = GetPrivate<TextMeshProUGUI>(_screen, "_heroNameLabel");
            Assert.AreEqual("Guerrero", label.text,
                "Hero name label must be populated from payload.");
        }

        [Test]
        public void OnPushed_WithNullPayload_DoesNotCrash()
        {
            // Should not throw — graceful degradation with a warning log.
            Assert.DoesNotThrow(() => InvokePushed(null),
                "Null payload must not throw.");
        }

        [Test]
        public void OnPushed_WithNullDiceBag_ShowsFallbackLabel()
        {
            // Hero has no StartingDiceBagRef — fallback should be active.
            var payload = new BuildSelectionPayload
            {
                SelectedHero = _hero,
                RunId = Guid.NewGuid(),
                RulesetId = "default"
            };

            InvokePushed(payload);

            var fallback = GetPrivate<TextMeshProUGUI>(_screen, "_diceBagFallbackLabel");
            Assert.IsTrue(fallback.gameObject.activeSelf,
                "Fallback label must be active when StartingDiceBagRef is null.");
            Assert.AreEqual(0, _diceContainer.childCount,
                "No dice slots should be instantiated without a dice bag.");
        }

        [Test]
        public void OnPopped_CleansUpState()
        {
            var payload = new BuildSelectionPayload
            {
                SelectedHero = _hero,
                RunId = Guid.NewGuid(),
                RulesetId = "default"
            };

            InvokePushed(payload);
            InvokePopped();

            var selectedHero = GetPrivateValue(_screen, "_selectedHero");
            Assert.IsNull(selectedHero, "_selectedHero must be null after OnPopped.");
            Assert.AreEqual(0, _diceContainer.childCount,
                "Dice slots must be cleared on pop.");
        }

        // ---------------- helpers ----------------

        private Button AttachButton(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_screenGO.transform, false);
            return go.AddComponent<Button>();
        }

        private void InvokePushed(IScreenPayload payload)
        {
            ((IBaseScreen)_screen)._Internal_OnPushed(payload);
        }

        private void InvokePopped()
        {
            ((IBaseScreen)_screen)._Internal_OnPopped();
        }

        private static void AssignPrivate(object target, string fieldName, object value)
        {
            FieldInfo field = null;
            var type = target.GetType();
            while (type != null && field == null)
            {
                field = type.GetField(fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                type = type.BaseType;
            }
            Assert.IsNotNull(field, $"Field '{fieldName}' not found in {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static T GetPrivate<T>(object target, string fieldName) where T : class
        {
            FieldInfo field = null;
            var type = target.GetType();
            while (type != null && field == null)
            {
                field = type.GetField(fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                type = type.BaseType;
            }
            Assert.IsNotNull(field, $"Field '{fieldName}' not found in {target.GetType().Name}.");
            return field.GetValue(target) as T;
        }

        private static object GetPrivateValue(object target, string fieldName)
        {
            FieldInfo field = null;
            var type = target.GetType();
            while (type != null && field == null)
            {
                field = type.GetField(fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                type = type.BaseType;
            }
            Assert.IsNotNull(field, $"Field '{fieldName}' not found in {target.GetType().Name}.");
            return field.GetValue(target);
        }
    }
}
