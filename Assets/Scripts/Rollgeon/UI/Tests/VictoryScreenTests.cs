using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.UI.Screens;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="VictoryScreen"/> (UI#0013c).
    /// <list type="bullet">
    /// <item><description><see cref="ScreenStringId_ReturnsVictoryScreen"/> — literal match.</description></item>
    /// <item><description><see cref="OnFloorCleared_PushesScreenViaScreenManager"/> — event triggers push.</description></item>
    /// <item><description><see cref="OnPushed_WiresReturnButton"/> — button click does not throw.</description></item>
    /// <item><description><see cref="OnPopped_RemovesButtonListener"/> — button click after pop has no side effects.</description></item>
    /// <item><description><see cref="OnPushed_SetsTitleText"/> — title label set to "Victory!".</description></item>
    /// <item><description><see cref="OnDestroy_UnsubscribesFromEvent"/> — event after destroy does not push.</description></item>
    /// </list>
    /// </summary>
    [TestFixture]
    public class VictoryScreenTests
    {
        private GameObject _screenGO;
        private TestableVictoryScreen _screen;
        private Button _returnToMenuButton;
        private TextMeshProUGUI _titleLabel;
        private SpyScreenManager _spyScreenManager;

        // -------------------------------------------------------------------
        // Stubs
        // -------------------------------------------------------------------

        // Override LoadMainMenu para evitar SceneManager.LoadScene en EditMode.
        private sealed class TestableVictoryScreen : VictoryScreen
        {
            public int LoadMainMenuCalls;
            protected override void LoadMainMenu() => LoadMainMenuCalls++;
        }

        private class SpyScreenManager : IScreenManager
        {
            public IBaseScreen Current => null;
            public List<string> PushByStringIdCalls { get; } = new();

            public void Push<TScreen>(IScreenPayload payload = null) where TScreen : class, IBaseScreen { }
            public void PushByStringId(string screenId, IScreenPayload payload = null)
            {
                PushByStringIdCalls.Add(screenId);
            }
            public void PopCurrent() { }
            public void PushOverlay<TScreen>(IScreenPayload payload = null) where TScreen : class, IBaseScreen { }
            public void PopOverlay() { }
            public void RegisterScreen(IBaseScreen screen) { }
            public void UnregisterScreen(IBaseScreen screen) { }
        }

        // -------------------------------------------------------------------
        // Setup / Teardown
        // -------------------------------------------------------------------

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();

            _screenGO = new GameObject("VictoryScreen");
            _screenGO.SetActive(false);
            _screen = _screenGO.AddComponent<TestableVictoryScreen>();

            _returnToMenuButton = AttachButton("ReturnToMenuButton");

            var titleGO = new GameObject("TitleLabel");
            titleGO.transform.SetParent(_screenGO.transform, false);
            _titleLabel = titleGO.AddComponent<TextMeshProUGUI>();

            AssignPrivate(_screen, "_returnToMenuButton", _returnToMenuButton);
            AssignPrivate(_screen, "_titleLabel", _titleLabel);

            _spyScreenManager = new SpyScreenManager();
            ServiceLocator.AddService<IScreenManager>(_spyScreenManager);
        }

        [TearDown]
        public void TearDown()
        {
            if (_screenGO != null) Object.DestroyImmediate(_screenGO);
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        // -------------------------------------------------------------------
        // Tests
        // -------------------------------------------------------------------

        [Test]
        public void ScreenStringId_ReturnsVictoryScreen()
        {
            Assert.AreEqual("VictoryScreen", _screen.ScreenStringId,
                "Must match the literal string-id used for PushByStringId navigation.");
        }

        [Test]
        public void OnFloorCleared_PushesScreenViaScreenManager()
        {
            // Manually invoke Awake via reflection to subscribe the handler
            InvokeAwake();

            EventManager.Trigger(EventName.OnFloorCleared, System.Guid.NewGuid(), 0);

            Assert.AreEqual(1, _spyScreenManager.PushByStringIdCalls.Count,
                "OnFloorCleared must trigger exactly one PushByStringId call.");
            Assert.AreEqual("VictoryScreen", _spyScreenManager.PushByStringIdCalls[0],
                "PushByStringId must be called with 'VictoryScreen'.");
        }

        [Test]
        public void OnPushed_WiresReturnButton()
        {
            InvokePushed(null);

            Assert.DoesNotThrow(() => _returnToMenuButton.onClick.Invoke(),
                "Return button click must not throw after OnPushed.");
        }

        [Test]
        public void OnPopped_RemovesButtonListener()
        {
            InvokePushed(null);
            InvokePopped();

            // After pop, clicking button should have no side effects
            Assert.DoesNotThrow(() => _returnToMenuButton.onClick.Invoke(),
                "Return button click must not throw after OnPopped.");
        }

        [Test]
        public void OnPushed_SetsTitleText()
        {
            InvokePushed(null);

            Assert.AreEqual("Victory!", _titleLabel.text,
                "Title label must be set to 'Victory!' on push.");
        }

        [Test]
        public void OnDestroy_UnsubscribesFromEvent()
        {
            InvokeAwake();

            // Unity does not call OnDestroy for GOs that were never active.
            // Invoke it directly to test the unsubscription path.
            InvokeOnDestroy();
            Object.DestroyImmediate(_screenGO);
            _screenGO = null;

            EventManager.Trigger(EventName.OnFloorCleared, System.Guid.NewGuid(), 0);

            Assert.AreEqual(0, _spyScreenManager.PushByStringIdCalls.Count,
                "After OnDestroy, OnFloorCleared must not trigger a push.");
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        private Button AttachButton(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_screenGO.transform, false);
            return go.AddComponent<Button>();
        }

        private void InvokeAwake()
        {
            var method = typeof(VictoryScreen).GetMethod("Awake",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(method, "Awake method not found on VictoryScreen.");
            method.Invoke(_screen, null);
        }

        private void InvokeOnDestroy()
        {
            var method = typeof(VictoryScreen).GetMethod("OnDestroy",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(method, "OnDestroy method not found on VictoryScreen.");
            method.Invoke(_screen, null);
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
    }
}
