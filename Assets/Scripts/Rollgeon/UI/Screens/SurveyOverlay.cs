using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Patterns;
using Rollgeon.Localization;
using Rollgeon.Patterns;
using Rollgeon.Phase;
using Rollgeon.Run;
using Rollgeon.Survey;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Screens
{
    /// <summary>
    /// Cuestionario in-game para builds de evento (Feature#0074). Escucha
    /// <c>OnFloorCleared</c> (sale al reclamar la recompensa del boss) y, si
    /// <see cref="ISurveyService.ShouldPrompt"/> dice que sí, se pushea como overlay
    /// no-destructivo un frame después. Mientras está abierto bloquea el gameplay con
    /// <see cref="PhaseOverlay.Pause"/> (como <see cref="PauseMenuOverlay"/>) y un
    /// fondo con raycast tapa el HUD de atrás. Omitir siempre está disponible.
    /// </summary>
    /// <remarks>
    /// [SETUP] Vive en <c>Assets/Prefabs/UI/Canvas/Canvas_Survey.prefab</c>, instanciado
    /// como hijo del <c>ScreenHost</c> de <c>02_Gameplay</c>. Ver
    /// <c>docs/setup/event-survey.md</c>. Las filas se instancian por código desde los
    /// prefabs de fila; cualquier ref faltante degrada a warning, nunca bloquea Omitir.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/Screens/Survey Overlay")]
    public class SurveyOverlay : BaseScreen
    {
        private const string LogPrefix = "[SurveyOverlay] ";
        public const string ScreenId = "Survey";
        private const string DefaultLocale = "es";

        private static readonly Regex EmailPattern = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        // ---- Inspector refs ----
        [Title("Overlay — Survey")]
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _subtitleLabel;

        [Required("Contenedor (con Layout) donde se instancian las filas.")]
        [SerializeField] private RectTransform _rowsContainer;

        [Tooltip("Prefabs de fila por tipo de pregunta. Si falta uno, esas preguntas se saltean con warning.")]
        [SerializeField] private SurveyQuestionRow _ratingRowPrefab;
        [SerializeField] private SurveyQuestionRow _choiceRowPrefab;
        [SerializeField] private SurveyQuestionRow _textRowPrefab;

        [Title("Sorteo")]
        [Tooltip("Bloque completo del sorteo; se oculta si la config no pide email.")]
        [SerializeField] private GameObject _raffleGroup;
        [SerializeField] private Toggle _raffleToggle;
        [SerializeField] private TMP_Text _raffleLabel;
        [SerializeField] private TMP_InputField _emailInput;

        [Title("Pie")]
        [SerializeField] private TMP_Text _statusLabel;
        [Required("Botón Enviar.")]
        [SerializeField] private Button _sendButton;
        [SerializeField] private TMP_Text _sendLabel;
        [Required("Botón Omitir.")]
        [SerializeField] private Button _skipButton;
        [SerializeField] private TMP_Text _skipLabel;

        // ---- State ----
        [ShowInInspector, ReadOnly] private bool _pushed;
        [ShowInInspector, ReadOnly] private bool _phasePushed;
        [ShowInInspector, ReadOnly] private bool _submitted;

        private readonly List<SurveyQuestionRow> _rows = new List<SurveyQuestionRow>();
        private EventManager.EventReceiver _onFloorClearedHandler;
        private Action<string, SurveyDeliveryState> _onDeliveryChanged;
        private string _submittedId;
        private Coroutine _autoClose;

        public override string ScreenStringId => ScreenId;

        /// <summary>Filas vivas (tests / debug).</summary>
        public IReadOnlyList<SurveyQuestionRow> Rows => _rows;

        public bool Submitted => _submitted;

        // ====================================================================
        // Suscripción al bus (patrón DefeatScreen: Awake + OnRegisteredByHost)
        // ====================================================================

        private void Awake() => EnsureSubscribed();

        public override void OnRegisteredByHost() => EnsureSubscribed();

        private void EnsureSubscribed()
        {
            if (_onFloorClearedHandler != null) return;
            _onFloorClearedHandler = HandleFloorCleared;
            EventManager.Subscribe(EventName.OnFloorCleared, _onFloorClearedHandler);
        }

        private void OnDestroy()
        {
            if (_onFloorClearedHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnFloorCleared, _onFloorClearedHandler);
                _onFloorClearedHandler = null;
            }

            DetachDelivery();
        }

        // Schema OnFloorCleared: [Guid runId, int floorIndex]
        private void HandleFloorCleared(params object[] args)
        {
            if (_pushed) return;
            if (args == null || args.Length < 2 || !(args[1] is int floorIndex)) return;

            if (!ServiceLocator.TryGetService<ISurveyService>(out var survey) || survey == null) return;
            if (!survey.ShouldPrompt(floorIndex)) return;

            DeferPush();
        }

        /// <summary>
        /// Un frame después: que termine de cerrarse lo que haya disparado el evento
        /// (pedestal, reward UI). Los tests lo overridean para pushear sincrónico.
        /// </summary>
        protected virtual void DeferPush() => CoroutineHost.Run(PushNextFrame());

        private IEnumerator PushNextFrame()
        {
            yield return null;
            TryPushNow();
        }

        protected void TryPushNow()
        {
            if (_pushed) return;

            if (!ServiceLocator.TryGetService<IScreenManager>(out var screens) || screens == null)
            {
                Debug.LogWarning(LogPrefix + "IScreenManager no registrado — no se puede mostrar la encuesta.", this);
                return;
            }

            if (screens.Current is SurveyOverlay) return;

            if (ServiceLocator.TryGetService<IRunContextService>(out var runCtx) && runCtx != null && !runCtx.IsRunActive)
            {
                return;
            }

            screens.PushOverlay<SurveyOverlay>();
        }

        // ====================================================================
        // Ciclo de vida de screen
        // ====================================================================

        protected override void OnPushed(IScreenPayload payload)
        {
            _pushed = true;
            _submitted = false;
            _submittedId = null;

            var survey = GetSurvey();
            survey?.MarkPrompted();

            PushPhaseOverlay();
            BuildRows(survey?.Config);
            RefreshLabels();
            LocalizationRefresh.Subscribe(RefreshLabels);

            bool askEmail = survey?.Config != null && survey.Config.AskEmailForRaffle;
            if (_raffleGroup != null) _raffleGroup.SetActive(askEmail);
            if (_raffleToggle != null)
            {
                _raffleToggle.isOn = false;
                _raffleToggle.onValueChanged.AddListener(OnRaffleToggled);
            }
            if (_emailInput != null)
            {
                _emailInput.text = string.Empty;
                _emailInput.interactable = false;
            }

            SetStatus(string.Empty);
            SetButtonsInteractable(true);

            if (_sendButton != null) _sendButton.onClick.AddListener(OnSendClicked);
            if (_skipButton != null) _skipButton.onClick.AddListener(OnSkipClicked);
        }

        protected override void OnPopped()
        {
            if (_sendButton != null) _sendButton.onClick.RemoveListener(OnSendClicked);
            if (_skipButton != null) _skipButton.onClick.RemoveListener(OnSkipClicked);
            if (_raffleToggle != null) _raffleToggle.onValueChanged.RemoveListener(OnRaffleToggled);

            LocalizationRefresh.Unsubscribe(RefreshLabels);
            DetachDelivery();
            StopAutoClose();
            ClearRows();
            PopPhaseOverlay();

            _pushed = false;
        }

        // ====================================================================
        // Phase overlay (un solo slot en PhaseService: solo si está libre)
        // ====================================================================

        private void PushPhaseOverlay()
        {
            _phasePushed = false;
            if (!ServiceLocator.TryGetService<IPhaseService>(out var phase) || phase == null)
            {
                Debug.LogWarning(LogPrefix + "IPhaseService no registrado — la encuesta no bloquea el gameplay por fase.", this);
                return;
            }

            if (phase.CurrentOverlay != PhaseOverlay.None) return;

            try
            {
                phase.PushOverlay(PhaseOverlay.Pause);
                _phasePushed = true;
            }
            catch (Exception e)
            {
                // Una build de evento no puede crashear por la matriz de fases.
                Debug.LogWarning(LogPrefix + $"No se pudo pushear PhaseOverlay.Pause: {e.Message}", this);
            }
        }

        private void PopPhaseOverlay()
        {
            if (!_phasePushed) return;
            _phasePushed = false;

            if (ServiceLocator.TryGetService<IPhaseService>(out var phase) && phase != null)
            {
                try
                {
                    phase.PopOverlay();
                }
                catch (Exception e)
                {
                    Debug.LogWarning(LogPrefix + $"No se pudo popear el phase overlay: {e.Message}", this);
                }
            }
        }

        // ====================================================================
        // Filas
        // ====================================================================

        private void BuildRows(SurveyConfigSO config)
        {
            ClearRows();
            if (config == null || !config.HasQuestions)
            {
                Debug.LogWarning(LogPrefix + "Sin config o sin preguntas — el formulario queda vacío.", this);
                return;
            }

            if (_rowsContainer == null)
            {
                Debug.LogWarning(LogPrefix + "_rowsContainer sin cablear — no se pueden mostrar preguntas.", this);
                return;
            }

            string locale = CurrentLocale();
            foreach (var question in config.Questions)
            {
                if (question == null) continue;

                var prefab = PrefabFor(question.Type);
                if (prefab == null)
                {
                    Debug.LogWarning(LogPrefix + $"Sin prefab de fila para {question.Type} — se saltea '{question.Id}'.", this);
                    continue;
                }

                var row = Instantiate(prefab, _rowsContainer);
                row.name = $"Row_{question.Id}";
                row.gameObject.SetActive(true);
                row.Bind(question, locale);
                _rows.Add(row);
            }
        }

        private SurveyQuestionRow PrefabFor(SurveyQuestionType type)
        {
            switch (type)
            {
                case SurveyQuestionType.Rating1to5: return _ratingRowPrefab;
                case SurveyQuestionType.SingleChoice: return _choiceRowPrefab;
                case SurveyQuestionType.FreeText: return _textRowPrefab;
                default: return null;
            }
        }

        private void ClearRows()
        {
            foreach (var row in _rows)
            {
                if (row == null) continue;
                if (Application.isPlaying) Destroy(row.gameObject);
                else DestroyImmediate(row.gameObject);
            }
            _rows.Clear();
        }

        // ====================================================================
        // Labels (code-set, tabla UI)
        // ====================================================================

        private void RefreshLabels()
        {
            if (_titleLabel != null) _titleLabel.text = LocalizedContent.Ui("survey.title", "¡Contanos qué te pareció!");
            if (_subtitleLabel != null)
            {
                _subtitleLabel.text = LocalizedContent.Ui("survey.subtitle",
                    "Un minuto y seguís jugando. Nos ayuda a mejorar el juego.");
            }
            if (_sendLabel != null) _sendLabel.text = LocalizedContent.Ui("survey.send", "Enviar");
            if (_skipLabel != null) _skipLabel.text = LocalizedContent.Ui("survey.skip", "Omitir");
            if (_raffleLabel != null) _raffleLabel.text = LocalizedContent.Ui("survey.raffle_optin", "Quiero participar del sorteo de keys");
            if (_emailInput != null && _emailInput.placeholder is TMP_Text placeholder)
            {
                placeholder.text = LocalizedContent.Ui("survey.email_placeholder", "Tu email (solo para el sorteo)");
            }

            string locale = CurrentLocale();
            foreach (var row in _rows)
            {
                if (row != null) row.Relabel(locale);
            }
        }

        private static string CurrentLocale()
        {
            if (ServiceLocator.TryGetService<ILocalizationService>(out var loc) && loc != null
                && !string.IsNullOrEmpty(loc.CurrentCode))
            {
                return loc.CurrentCode;
            }
            return DefaultLocale;
        }

        // ====================================================================
        // Botones
        // ====================================================================

        private void OnRaffleToggled(bool isOn)
        {
            if (_emailInput != null) _emailInput.interactable = isOn;
        }

        private void OnSkipClicked() => Close();

        private void OnSendClicked()
        {
            if (_submitted) return;

            if (!ValidateForm(out var response, out var errorKey, out var errorFallback))
            {
                SetStatus(LocalizedContent.Ui(errorKey, errorFallback));
                return;
            }

            var survey = GetSurvey();
            if (survey == null)
            {
                Debug.LogWarning(LogPrefix + "ISurveyService no registrado — no se puede enviar.", this);
                SetStatus(LocalizedContent.Ui("survey.status_offline", "¡Gracias! Guardada, se envía cuando haya conexión."));
                StartAutoClose(1.5f);
                return;
            }

            _submitted = true;
            _submittedId = response.response_id = Guid.NewGuid().ToString("N");
            SetButtonsInteractable(false);

            _onDeliveryChanged = OnDeliveryChanged;
            survey.DeliveryChanged += _onDeliveryChanged;
            survey.Submit(response);

            StartAutoClose(survey.Config != null ? survey.Config.AutoCloseSeconds : 1.5f);
        }

        /// <summary>
        /// Arma la respuesta desde las filas. <c>false</c> si falta una requerida o el
        /// email del sorteo no es válido; marca las filas en falta.
        /// </summary>
        public bool ValidateForm(out SurveyResponse response, out string errorKey, out string errorFallback)
        {
            response = null;
            errorKey = null;
            errorFallback = null;

            var answers = new List<SurveyAnswer>();
            bool missing = false;
            foreach (var row in _rows)
            {
                if (row == null) continue;

                bool has = row.TryGetValue(out var value);
                bool invalid = !has && row.Required;
                row.SetInvalid(invalid);
                if (invalid) missing = true;
                if (has) answers.Add(new SurveyAnswer(row.QuestionId, value));
            }

            if (missing)
            {
                errorKey = "survey.required_hint";
                errorFallback = "Faltan responder las preguntas marcadas.";
                return false;
            }

            bool raffle = _raffleGroup != null && _raffleGroup.activeSelf && _raffleToggle != null && _raffleToggle.isOn;
            string email = raffle && _emailInput != null ? _emailInput.text?.Trim() : string.Empty;
            if (raffle && (string.IsNullOrEmpty(email) || !EmailPattern.IsMatch(email)))
            {
                errorKey = "survey.status_invalid_email";
                errorFallback = "Ese email no parece válido.";
                return false;
            }

            response = new SurveyResponse
            {
                raffle_opt_in = raffle,
                email = raffle ? email : string.Empty,
                locale = CurrentLocale(),
                answers = answers,
            };

            if (ServiceLocator.TryGetService<IRunContextService>(out var runCtx) && runCtx != null)
            {
                response.run_id = runCtx.RunId.ToString("N");
                response.floor_index = runCtx.FloorIndex;
                response.hero_id = runCtx.SelectedHero != null ? runCtx.SelectedHero.EntityId : string.Empty;
            }

            return true;
        }

        private void OnDeliveryChanged(string responseId, SurveyDeliveryState state)
        {
            if (!string.Equals(responseId, _submittedId, StringComparison.Ordinal)) return;

            switch (state)
            {
                case SurveyDeliveryState.Pending:
                    SetStatus(LocalizedContent.Ui("survey.status_saved", "¡Gracias! Respuesta guardada."));
                    break;
                case SurveyDeliveryState.Sending:
                    SetStatus(LocalizedContent.Ui("survey.status_sending", "Enviando…"));
                    break;
                case SurveyDeliveryState.Sent:
                    SetStatus(LocalizedContent.Ui("survey.status_sent", "¡Gracias! Respuesta enviada."));
                    break;
                case SurveyDeliveryState.Failed:
                    SetStatus(LocalizedContent.Ui("survey.status_offline", "¡Gracias! Guardada, se envía cuando haya conexión."));
                    break;
            }
        }

        private void DetachDelivery()
        {
            if (_onDeliveryChanged == null) return;
            var survey = GetSurvey();
            if (survey != null) survey.DeliveryChanged -= _onDeliveryChanged;
            _onDeliveryChanged = null;
        }

        // ====================================================================
        // Cierre
        // ====================================================================

        private void StartAutoClose(float seconds)
        {
            StopAutoClose();
            if (seconds <= 0f)
            {
                Close();
                return;
            }

            // CoroutineHost: el pop desactiva este GO y mataría una coroutine propia.
            _autoClose = CoroutineHost.Run(CloseAfter(seconds));
        }

        private IEnumerator CloseAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            _autoClose = null;
            Close();
        }

        private void StopAutoClose()
        {
            if (_autoClose == null) return;
            CoroutineHost.Stop(_autoClose);
            _autoClose = null;
        }

        private void Close()
        {
            if (!ServiceLocator.TryGetService<IScreenManager>(out var screens) || screens == null)
            {
                Debug.LogWarning(LogPrefix + "IScreenManager no registrado — no se puede cerrar la encuesta.", this);
                return;
            }

            if (!ReferenceEquals(screens.Current, this))
            {
                Debug.LogWarning(LogPrefix + "La encuesta no es el top del stack — no se popea nada.", this);
                return;
            }

            screens.PopOverlay();
        }

        // ====================================================================
        // Helpers
        // ====================================================================

        private static ISurveyService GetSurvey()
            => ServiceLocator.TryGetService<ISurveyService>(out var survey) && survey != null ? survey : null;

        private void SetStatus(string text)
        {
            if (_statusLabel != null) _statusLabel.text = text ?? string.Empty;
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (_sendButton != null) _sendButton.interactable = interactable;
            if (_skipButton != null) _skipButton.interactable = interactable;
        }
    }
}
