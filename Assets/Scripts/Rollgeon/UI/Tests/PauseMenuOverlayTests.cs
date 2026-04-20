using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Phase;
using Rollgeon.UI.Screens;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="PauseMenuOverlay"/> (UI#0014c).
    /// <list type="bullet">
    /// <item><description><see cref="ScreenStringId_Returns_PauseMenu"/> — literal match.</description></item>
    /// <item><description><see cref="OnPushed_CallsPhaseServicePushOverlay"/> — overlay set to Pause.</description></item>
    /// <item><description><see cref="OnPopped_CallsPhaseServicePopOverlay"/> — overlay reset to None.</description></item>
    /// <item><description><see cref="OnPushed_WithoutPhaseService_DoesNotThrow"/> — graceful degradation.</description></item>
    /// <item><description><see cref="OnPopped_RemovesButtonListeners"/> — no side effects after pop.</description></item>
    /// <item><description><see cref="ResumeButton_DoesNotThrow"/> — click without crash.</description></item>
    /// <item><description><see cref="SettingsButton_DoesNotThrow"/> — stub click without crash.</description></item>
    /// </list>
    /// </summary>
    [TestFixture]
    public class PauseMenuOverlayTests
    {
        private GameObject _screenGO;
        private PauseMenuOverlay _screen;
        private Button _resumeButton;
        private Button _settingsButton;
        private Button _quitRunButton;
        private StubPhaseService _stubPhase;

        // -------------------------------------------------------------------
        // Stub
        // -------------------------------------------------------------------

        private class StubPhaseService : IPhaseService
        {
            public GamePhase CurrentBase { get; private set; }
            public PhaseOverlay CurrentOverlay { get; private set; }
            public List<PhaseOverlay> PushOverlayCalls { get; } = new();
            public int PopOverlayCount { get; private set; }

            public void ReplacePhase(GamePhase next) => CurrentBase = next;

            public void PushOverlay(PhaseOverlay overlay)
            {
                CurrentOverlay = overlay;
                PushOverlayCalls.Add(overlay);
            }

            public void PopOverlay()
            {
                CurrentOverlay = PhaseOverlay.None;
                PopOverlayCount++;
            }
        }

        // -------------------------------------------------------------------
        // Setup / Teardown
        // -------------------------------------------------------------------

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();

            _screenGO = new GameObject("PauseMenuOverlay");
            _screenGO.SetActive(false);
            _screen = _screenGO.AddComponent<PauseMenuOverlay>();

            _resumeButton = AttachButton("ResumeButton");
            _settingsButton = AttachButton("SettingsButton");
            _quitRunButton = AttachButton("QuitRunButton");

            AssignPrivate(_screen, "_resumeButton", _resumeButton);
            AssignPrivate(_screen, "_settingsButton", _settingsButton);
            AssignPrivate(_screen, "_quitRunButton", _quitRunButton);

            _stubPhase = new StubPhaseService();
            ServiceLocator.AddService<IPhaseService>(_stubPhase);
        }

        [TearDown]
        public void TearDown()
        {
            if (_screenGO != null) Object.DestroyImmediate(_screenGO);
            ServiceLocator.Clear();
        }

        // -------------------------------------------------------------------
        // Tests
        // -------------------------------------------------------------------

        [Test]
        public void ScreenStringId_Returns_PauseMenu()
        {
            Assert.AreEqual("PauseMenu", _screen.ScreenStringId,
                "Must match the literal string-id used for PushOverlay navigation.");
        }

        [Test]
        public void OnPushed_CallsPhaseServicePushOverlay()
        {
            InvokePushed(null);

            Assert.AreEqual(PhaseOverlay.Pause, _stubPhase.CurrentOverlay,
                "OnPushed must push PhaseOverlay.Pause.");
            Assert.AreEqual(1, _stubPhase.PushOverlayCalls.Count,
                "PushOverlay must be called exactly once.");
        }

        [Test]
        public void OnPopped_CallsPhaseServicePopOverlay()
        {
            InvokePushed(null);
            InvokePopped();

            Assert.AreEqual(PhaseOverlay.None, _stubPhase.CurrentOverlay,
                "OnPopped must pop the overlay back to None.");
            Assert.AreEqual(1, _stubPhase.PopOverlayCount,
                "PopOverlay must be called exactly once.");
        }

        [Test]
        public void OnPushed_WithoutPhaseService_DoesNotThrow()
        {
            ServiceLocator.RemoveService<IPhaseService>();

            Assert.DoesNotThrow(() => InvokePushed(null),
                "Must not throw when IPhaseService is not registered.");
        }

        [Test]
        public void OnPopped_RemovesButtonListeners()
        {
            InvokePushed(null);
            InvokePopped();

            // Invoking button clicks after pop should have no side effects.
            // If listeners were not removed, this could cause errors or
            // unexpected state changes. We verify no extra PopOverlay calls.
            int popCountBefore = _stubPhase.PopOverlayCount;
            _resumeButton.onClick.Invoke();
            _settingsButton.onClick.Invoke();

            Assert.AreEqual(popCountBefore, _stubPhase.PopOverlayCount,
                "Buttons must not trigger handlers after OnPopped.");
        }

        [Test]
        public void ResumeButton_DoesNotThrow()
        {
            InvokePushed(null);

            Assert.DoesNotThrow(() => _resumeButton.onClick.Invoke(),
                "Resume button click must not throw.");
        }

        [Test]
        public void SettingsButton_DoesNotThrow()
        {
            InvokePushed(null);

            Assert.DoesNotThrow(() => _settingsButton.onClick.Invoke(),
                "Settings button click must not throw (stub).");
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
