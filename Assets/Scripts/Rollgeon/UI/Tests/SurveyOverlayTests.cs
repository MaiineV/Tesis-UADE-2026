using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Heroes;
using Rollgeon.Phase;
using Rollgeon.Run;
using Rollgeon.Survey;
using Rollgeon.UI.Screens;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// EditMode tests de <see cref="SurveyOverlay"/> (Feature#0074): disparo por
    /// OnFloorCleared, bloqueo por fase, filas desde config, validación y envío.
    /// </summary>
    [TestFixture]
    public class SurveyOverlayTests
    {
        // -------------------------------------------------------------------
        // Stubs
        // -------------------------------------------------------------------

        private sealed class TestableSurveyOverlay : SurveyOverlay
        {
            public int DeferPushCalls;
            protected override void DeferPush()
            {
                DeferPushCalls++;
                TryPushNow();
            }
        }

        // Modela lo que hace el ScreenManager real: Current + hooks internos.
        private sealed class SpyScreenManager : IScreenManager
        {
            private readonly IBaseScreen _overlay;
            public IBaseScreen Current { get; set; }
            public int PushOverlayCalls;
            public int PopOverlayCalls;

            public SpyScreenManager(IBaseScreen overlay) => _overlay = overlay;

            public void Push<TScreen>(IScreenPayload payload = null) where TScreen : class, IBaseScreen { }
            public void PushByStringId(string screenId, IScreenPayload payload = null) { }
            public void PopCurrent() => PopOverlay();

            public void PushOverlay<TScreen>(IScreenPayload payload = null) where TScreen : class, IBaseScreen
            {
                PushOverlayCalls++;
                if (typeof(TScreen) != typeof(SurveyOverlay)) return;
                Current = _overlay;
                _overlay._Internal_OnPushed(payload);
            }

            public void PopOverlay()
            {
                PopOverlayCalls++;
                if (!ReferenceEquals(Current, _overlay)) return;
                _overlay._Internal_OnPopped();
                Current = null;
            }

            public void RegisterScreen(IBaseScreen screen) { }
            public void UnregisterScreen(IBaseScreen screen) { }
        }

        private sealed class StubPhaseService : IPhaseService
        {
            public GamePhase CurrentBase { get; private set; }
            public PhaseOverlay CurrentOverlay { get; set; }
            public int PushCount;
            public int PopCount;

            public void ReplacePhase(GamePhase next) => CurrentBase = next;
            public void PushOverlay(PhaseOverlay overlay) { CurrentOverlay = overlay; PushCount++; }
            public void PopOverlay() { CurrentOverlay = PhaseOverlay.None; PopCount++; }
        }

        private sealed class FakeSurveyService : ISurveyService
        {
            public bool ShouldPromptResult = true;
            public int MarkPromptedCount;
            public readonly List<SurveyResponse> Submitted = new List<SurveyResponse>();
            public SurveyConfigSO Config { get; set; }

            public bool IsEnabled => true;
            public bool IsEventBuild => false;
            public bool PromptedThisRun => MarkPromptedCount > 0;
            public int PendingCount => 0;
            public IReadOnlyList<string> PendingKeys => Array.Empty<string>();

            public bool ShouldPrompt(int floorIndex) => ShouldPromptResult;
            public void MarkPrompted() => MarkPromptedCount++;
            public void ResetPromptGuard() { }
            public void Submit(SurveyResponse response) => Submitted.Add(response);
            public void FlushPending() { }
            public event Action<string, SurveyDeliveryState> DeliveryChanged;

            public void Raise(string id, SurveyDeliveryState state) => DeliveryChanged?.Invoke(id, state);
        }

        private sealed class FakeRunContext : IRunContextService
        {
            public Guid RunId { get; set; } = Guid.NewGuid();
            public int FloorIndex { get; set; } = 1;
            public ClassHeroSO SelectedHero { get; set; }
            public bool IsRunActive { get; set; } = true;
            public void AdvanceFloor() => FloorIndex++;
        }

        // -------------------------------------------------------------------
        // Fixture
        // -------------------------------------------------------------------

        private GameObject _screenGO;
        private TestableSurveyOverlay _screen;
        private SpyScreenManager _screens;
        private StubPhaseService _phase;
        private FakeSurveyService _survey;
        private FakeRunContext _runCtx;
        private SurveyConfigSO _config;
        private ClassHeroSO _hero;
        private GameObject _ratingPrefab;
        private GameObject _textPrefab;
        private Button _sendButton;
        private Button _skipButton;
        private TextMeshProUGUI _statusLabel;
        private GameObject _raffleGroup;
        private Toggle _raffleToggle;
        private TMP_InputField _emailInput;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();

            _screenGO = new GameObject("SurveyOverlay");
            _screenGO.SetActive(false);
            _screen = _screenGO.AddComponent<TestableSurveyOverlay>();

            var container = new GameObject("Rows", typeof(RectTransform));
            container.transform.SetParent(_screenGO.transform, false);

            _ratingPrefab = BuildRowPrefab("RatingRow", withInput: false);
            _textPrefab = BuildRowPrefab("TextRow", withInput: true);

            _sendButton = AttachButton("SendButton");
            _skipButton = AttachButton("SkipButton");
            _statusLabel = Attach<TextMeshProUGUI>("Status");

            _raffleGroup = new GameObject("Raffle");
            _raffleGroup.transform.SetParent(_screenGO.transform, false);
            _raffleToggle = _raffleGroup.AddComponent<Toggle>();
            var emailGO = new GameObject("Email");
            emailGO.transform.SetParent(_raffleGroup.transform, false);
            _emailInput = emailGO.AddComponent<TMP_InputField>();
            var emailText = new GameObject("Text");
            emailText.transform.SetParent(emailGO.transform, false);
            _emailInput.textComponent = emailText.AddComponent<TextMeshProUGUI>();

            AssignPrivate(_screen, "_rowsContainer", container.GetComponent<RectTransform>());
            AssignPrivate(_screen, "_ratingRowPrefab", _ratingPrefab.GetComponent<SurveyQuestionRow>());
            AssignPrivate(_screen, "_textRowPrefab", _textPrefab.GetComponent<SurveyQuestionRow>());
            AssignPrivate(_screen, "_sendButton", _sendButton);
            AssignPrivate(_screen, "_skipButton", _skipButton);
            AssignPrivate(_screen, "_statusLabel", _statusLabel);
            AssignPrivate(_screen, "_raffleGroup", _raffleGroup);
            AssignPrivate(_screen, "_raffleToggle", _raffleToggle);
            AssignPrivate(_screen, "_emailInput", _emailInput);

            _config = ScriptableObject.CreateInstance<SurveyConfigSO>();
            _config.EventId = "test";
            _config.AskEmailForRaffle = true;
            _config.AutoCloseSeconds = 0f;
            _config.Questions = new List<SurveyQuestion>
            {
                new SurveyQuestion { Id = "fun", Type = SurveyQuestionType.Rating1to5, TextEs = "¿Diversión?", Required = true },
                new SurveyQuestion { Id = "change", Type = SurveyQuestionType.FreeText, TextEs = "¿Qué cambiarías?", Required = false },
            };

            _hero = ScriptableObject.CreateInstance<ClassHeroSO>();
            _hero.EntityId = "hero.test";

            _survey = new FakeSurveyService { Config = _config };
            _screens = new SpyScreenManager(_screen);
            _phase = new StubPhaseService();
            _runCtx = new FakeRunContext { SelectedHero = _hero };

            ServiceLocator.AddService<ISurveyService>(_survey);
            ServiceLocator.AddService<IScreenManager>(_screens);
            ServiceLocator.AddService<IPhaseService>(_phase);
            ServiceLocator.AddService<IRunContextService>(_runCtx);
        }

        [TearDown]
        public void TearDown()
        {
            if (_screenGO != null) Object.DestroyImmediate(_screenGO);
            if (_ratingPrefab != null) Object.DestroyImmediate(_ratingPrefab);
            if (_textPrefab != null) Object.DestroyImmediate(_textPrefab);
            if (_config != null) Object.DestroyImmediate(_config);
            if (_hero != null) Object.DestroyImmediate(_hero);
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        // -------------------------------------------------------------------
        // Tests — disparo
        // -------------------------------------------------------------------

        [Test]
        public void test_overlay_screen_id_is_survey()
        {
            Assert.AreEqual("Survey", _screen.ScreenStringId);
        }

        [Test]
        public void test_overlay_floor_cleared_should_prompt_false_no_push()
        {
            InvokeAwake();
            _survey.ShouldPromptResult = false;

            EventManager.Trigger(EventName.OnFloorCleared, Guid.NewGuid(), 0);

            Assert.AreEqual(0, _screens.PushOverlayCalls);
        }

        [Test]
        public void test_overlay_floor_cleared_should_prompt_true_pushes_once()
        {
            InvokeAwake();

            EventManager.Trigger(EventName.OnFloorCleared, Guid.NewGuid(), 0);
            EventManager.Trigger(EventName.OnFloorCleared, Guid.NewGuid(), 0);

            Assert.AreEqual(1, _screens.PushOverlayCalls, "El segundo evento no re-pushea mientras está abierta.");
            Assert.AreSame(_screen, _screens.Current);
        }

        [Test]
        public void test_overlay_floor_cleared_run_inactive_no_push()
        {
            InvokeAwake();
            _runCtx.IsRunActive = false;

            EventManager.Trigger(EventName.OnFloorCleared, Guid.NewGuid(), 0);

            Assert.AreEqual(0, _screens.PushOverlayCalls);
        }

        [Test]
        public void test_overlay_floor_cleared_bad_args_ignored()
        {
            InvokeAwake();

            Assert.DoesNotThrow(() => EventManager.Trigger(EventName.OnFloorCleared, Guid.NewGuid()));
            Assert.DoesNotThrow(() => EventManager.Trigger(EventName.OnFloorCleared, Guid.NewGuid(), "no-int"));
            Assert.AreEqual(0, _screens.PushOverlayCalls);
        }

        [Test]
        public void test_overlay_registered_by_host_subscribes_without_awake()
        {
            _screen.OnRegisteredByHost();

            EventManager.Trigger(EventName.OnFloorCleared, Guid.NewGuid(), 0);

            Assert.AreEqual(1, _screens.PushOverlayCalls);
        }

        // -------------------------------------------------------------------
        // Tests — push / pop
        // -------------------------------------------------------------------

        [Test]
        public void test_overlay_on_pushed_pushes_pause_and_marks_prompted()
        {
            Push();

            Assert.AreEqual(PhaseOverlay.Pause, _phase.CurrentOverlay);
            Assert.AreEqual(1, _phase.PushCount);
            Assert.AreEqual(1, _survey.MarkPromptedCount);
        }

        [Test]
        public void test_overlay_on_pushed_with_phase_overlay_busy_does_not_touch_it()
        {
            _phase.CurrentOverlay = PhaseOverlay.Pause;

            Push();
            Pop();

            Assert.AreEqual(0, _phase.PushCount);
            Assert.AreEqual(0, _phase.PopCount, "No popea un overlay que no pusheó.");
        }

        [Test]
        public void test_overlay_on_pushed_builds_rows_from_config()
        {
            Push();

            Assert.AreEqual(2, _screen.Rows.Count);
            Assert.AreEqual("fun", _screen.Rows[0].QuestionId);
            Assert.AreEqual("change", _screen.Rows[1].QuestionId);
            Assert.AreEqual(5, _screen.Rows[0].OptionCount);
        }

        [Test]
        public void test_overlay_on_pushed_hides_raffle_when_config_says_so()
        {
            _config.AskEmailForRaffle = false;

            Push();

            Assert.IsFalse(_raffleGroup.activeSelf);
        }

        [Test]
        public void test_overlay_on_popped_clears_rows_and_pops_phase()
        {
            Push();

            Pop();

            Assert.AreEqual(0, _screen.Rows.Count);
            Assert.AreEqual(PhaseOverlay.None, _phase.CurrentOverlay);
            Assert.AreEqual(1, _phase.PopCount);
        }

        [Test]
        public void test_overlay_skip_pops_without_submit()
        {
            Push();

            _skipButton.onClick.Invoke();

            Assert.AreEqual(1, _screens.PopOverlayCalls);
            Assert.AreEqual(0, _survey.Submitted.Count);
            Assert.IsNull(_screens.Current);
        }

        // -------------------------------------------------------------------
        // Tests — enviar
        // -------------------------------------------------------------------

        [Test]
        public void test_overlay_send_with_required_missing_does_not_submit()
        {
            Push();

            _sendButton.onClick.Invoke();

            Assert.AreEqual(0, _survey.Submitted.Count);
            Assert.IsFalse(string.IsNullOrEmpty(_statusLabel.text), "Tiene que decir qué falta.");
            Assert.IsFalse(_screen.Submitted);
        }

        [Test]
        public void test_overlay_send_valid_submits_answers_with_run_data()
        {
            Push();
            _screen.Rows[0].SelectOption(4);
            _screen.Rows[1].SetText("más dados");

            _sendButton.onClick.Invoke();

            Assert.AreEqual(1, _survey.Submitted.Count);
            var response = _survey.Submitted[0];
            Assert.AreEqual(2, response.answers.Count);
            Assert.AreEqual("fun", response.answers[0].id);
            Assert.AreEqual("5", response.answers[0].value);
            Assert.AreEqual("más dados", response.answers[1].value);
            Assert.AreEqual(_runCtx.RunId.ToString("N"), response.run_id);
            Assert.AreEqual(1, response.floor_index);
            Assert.AreEqual("hero.test", response.hero_id);
            Assert.IsFalse(response.raffle_opt_in);
            Assert.AreEqual(string.Empty, response.email);
            Assert.IsFalse(string.IsNullOrEmpty(response.response_id));
        }

        [Test]
        public void test_overlay_send_optional_unanswered_is_omitted()
        {
            Push();
            _screen.Rows[0].SelectOption(0);

            _sendButton.onClick.Invoke();

            Assert.AreEqual(1, _survey.Submitted.Count);
            Assert.AreEqual(1, _survey.Submitted[0].answers.Count);
        }

        [Test]
        public void test_overlay_send_auto_close_zero_pops_immediately()
        {
            Push();
            _screen.Rows[0].SelectOption(0);

            _sendButton.onClick.Invoke();

            Assert.AreEqual(1, _screens.PopOverlayCalls);
        }

        [Test]
        public void test_overlay_send_twice_submits_once()
        {
            _config.AutoCloseSeconds = 5f;
            Push();
            _screen.Rows[0].SelectOption(0);

            _sendButton.onClick.Invoke();
            _sendButton.onClick.Invoke();

            Assert.AreEqual(1, _survey.Submitted.Count);
            Assert.IsFalse(_sendButton.interactable);
            Assert.IsFalse(_skipButton.interactable);
        }

        [Test]
        public void test_overlay_send_raffle_on_invalid_email_blocks()
        {
            Push();
            _screen.Rows[0].SelectOption(0);
            _raffleToggle.isOn = true;
            _emailInput.text = "no-es-mail";

            _sendButton.onClick.Invoke();

            Assert.AreEqual(0, _survey.Submitted.Count);
        }

        [Test]
        public void test_overlay_send_raffle_on_valid_email_included()
        {
            Push();
            _screen.Rows[0].SelectOption(0);
            _raffleToggle.isOn = true;
            _emailInput.text = " alguien@mail.com ";

            _sendButton.onClick.Invoke();

            Assert.AreEqual(1, _survey.Submitted.Count);
            Assert.IsTrue(_survey.Submitted[0].raffle_opt_in);
            Assert.AreEqual("alguien@mail.com", _survey.Submitted[0].email);
        }

        [Test]
        public void test_overlay_send_raffle_off_ignores_email_text()
        {
            Push();
            _screen.Rows[0].SelectOption(0);
            _raffleToggle.isOn = false;
            _emailInput.text = "alguien@mail.com";

            _sendButton.onClick.Invoke();

            Assert.IsFalse(_survey.Submitted[0].raffle_opt_in);
            Assert.AreEqual(string.Empty, _survey.Submitted[0].email);
        }

        [Test]
        public void test_overlay_raffle_toggle_enables_email_input()
        {
            Push();

            Assert.IsFalse(_emailInput.interactable);
            _raffleToggle.isOn = true;
            Assert.IsTrue(_emailInput.interactable);
        }

        [Test]
        public void test_overlay_delivery_sent_updates_status_for_own_response()
        {
            _config.AutoCloseSeconds = 5f;
            Push();
            _screen.Rows[0].SelectOption(0);
            _sendButton.onClick.Invoke();
            string id = _survey.Submitted[0].response_id;

            _survey.Raise("otro", SurveyDeliveryState.Failed);
            string beforeOwn = _statusLabel.text;
            _survey.Raise(id, SurveyDeliveryState.Sent);

            Assert.AreNotEqual(beforeOwn, _statusLabel.text, "Solo reacciona a su propia respuesta.");
            Assert.IsFalse(string.IsNullOrEmpty(_statusLabel.text));
        }

        [Test]
        public void test_overlay_on_popped_detaches_delivery_listener()
        {
            _config.AutoCloseSeconds = 5f;
            Push();
            _screen.Rows[0].SelectOption(0);
            _sendButton.onClick.Invoke();
            string id = _survey.Submitted[0].response_id;
            Pop();
            _statusLabel.text = "sentinel";

            _survey.Raise(id, SurveyDeliveryState.Sent);

            Assert.AreEqual("sentinel", _statusLabel.text);
        }

        [Test]
        public void test_overlay_on_destroy_unsubscribes_from_floor_cleared()
        {
            InvokeAwake();
            // EditMode no dispara OnDestroy solo: se invoca a mano, como el Awake.
            InvokeLifecycle("OnDestroy");
            Object.DestroyImmediate(_screenGO);
            _screenGO = null;

            Assert.DoesNotThrow(() => EventManager.Trigger(EventName.OnFloorCleared, Guid.NewGuid(), 0));
            Assert.AreEqual(0, _screens.PushOverlayCalls);
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        private void Push() => _screens.PushOverlay<SurveyOverlay>();

        private void Pop() => _screens.PopOverlay();

        private void InvokeAwake() => InvokeLifecycle("Awake");

        private void InvokeLifecycle(string methodName)
        {
            var method = typeof(SurveyOverlay).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(method, $"{methodName} no encontrado en SurveyOverlay.");
            method.Invoke(_screen, null);
        }

        private Button AttachButton(string name) => Attach<Button>(name);

        private T Attach<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(_screenGO.transform, false);
            return go.AddComponent<T>();
        }

        private static GameObject BuildRowPrefab(string name, bool withInput)
        {
            var root = new GameObject(name, typeof(RectTransform));
            var row = root.AddComponent<SurveyQuestionRow>();

            var label = new GameObject("Label");
            label.transform.SetParent(root.transform, false);
            AssignPrivate(row, "_questionLabel", label.AddComponent<TextMeshProUGUI>());

            if (withInput)
            {
                var inputGO = new GameObject("Input");
                inputGO.transform.SetParent(root.transform, false);
                var input = inputGO.AddComponent<TMP_InputField>();
                var text = new GameObject("Text");
                text.transform.SetParent(inputGO.transform, false);
                input.textComponent = text.AddComponent<TextMeshProUGUI>();
                AssignPrivate(row, "_textInput", input);
            }
            else
            {
                var container = new GameObject("Options");
                container.transform.SetParent(root.transform, false);
                var template = new GameObject("Template");
                template.transform.SetParent(container.transform, false);
                var toggle = template.AddComponent<Toggle>();
                var tLabel = new GameObject("Label");
                tLabel.transform.SetParent(template.transform, false);
                tLabel.AddComponent<TextMeshProUGUI>();
                template.SetActive(false);
                AssignPrivate(row, "_optionTemplate", toggle);
                AssignPrivate(row, "_optionContainer", container.transform);
            }

            root.SetActive(false);
            return root;
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
