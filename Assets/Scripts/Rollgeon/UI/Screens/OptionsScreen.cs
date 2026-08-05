using Patterns;
using Patterns.Save;
using PrimeTween;
using Rollgeon.Analytics;
using Rollgeon.Localization;
using Rollgeon.Meta;
using Rollgeon.UI.Menu;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Screens
{
    /// <summary>
    /// Panel de opciones del menú principal (overlay no-destructivo, mismo
    /// esquema que <see cref="AnalyticsConsentOverlay"/>). Concentra los
    /// controles que antes vivían sueltos en el menú: toggle de tutorial,
    /// toggle de telemetría, selección de idioma y borrado de partida — este
    /// último con confirmación de dos clicks (armado con ventana de 3s).
    /// </summary>
    /// <remarks>
    /// [SETUP] GameObject hijo del Canvas en <c>01_MainMenu</c>, arranca
    /// desactivado. Lo cablea el installer <c>Rollgeon → Juicy Menu → 4</c>.
    /// El <c>LanguageSelector</c> vive en este mismo GameObject y maneja los
    /// botones de idioma por su cuenta.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/Screens/Options Screen")]
    public class OptionsScreen : BaseScreen
    {
        private const string LogPrefix = "[OptionsScreen] ";
        public const string ScreenId = "Options";

        // Ventana del estado "armado" del borrar partida; pasada, se desarma solo.
        private const float ArmWindowSeconds = 3f;

        [Title("Panel")]
        [Tooltip("Título del panel. Opcional — el texto se setea por código (tabla UI).")]
        [SerializeField] private TMP_Text _titleLabel;

        [SerializeField, Optional] private RectTransform _panel;
        [SerializeField, Optional] private CanvasGroup _rootCanvasGroup;

        [Title("Toggles")]
        [SerializeField, Optional] private Button _tutorialToggleButton;
        [SerializeField, Optional] private TMP_Text _tutorialToggleLabel;
        [SerializeField, Optional] private Button _analyticsToggleButton;
        [SerializeField, Optional] private TMP_Text _analyticsToggleLabel;

        [Title("Idioma")]
        [Tooltip("Label de la fila de idioma. Los botones ES/EN los maneja LanguageSelector.")]
        [SerializeField, Optional] private TMP_Text _languageLabel;

        [Title("Borrar partida")]
        [SerializeField, Optional] private Button _resetSaveButton;
        [SerializeField, Optional] private TMP_Text _resetSaveLabel;
        [SerializeField, Optional] private JuicyMenuButton _resetSaveJuice;

        [Title("Volver")]
        [SerializeField, Optional] private Button _backButton;
        [SerializeField, Optional] private TMP_Text _backLabel;

        [Title("Juice")]
        [SerializeField, Optional] private MenuJuiceSettingsSO _settings;

        [SerializeField] private float _entranceFadeDuration = 0.2f;
        [SerializeField] private float _entrancePopDuration = 0.28f;

        private bool _resetArmed;
        private float _armedAtUnscaled;

        public override string ScreenStringId => ScreenId;

        protected override void OnPushed(IScreenPayload payload)
        {
            if (_tutorialToggleButton != null) _tutorialToggleButton.onClick.AddListener(OnTutorialToggleClicked);
            if (_analyticsToggleButton != null) _analyticsToggleButton.onClick.AddListener(OnAnalyticsToggleClicked);
            if (_resetSaveButton != null) _resetSaveButton.onClick.AddListener(OnResetSaveClicked);
            if (_backButton != null) _backButton.onClick.AddListener(OnBackClicked);

            // Los botones ES/EN viven en este mismo panel: sin esto, cambiar el idioma
            // dejaba todo el panel con el texto del idioma anterior hasta reabrirlo.
            LocalizationRefresh.Subscribe(RefreshLabels);

            DisarmReset();
            RefreshLabels();
            PlayEntrance();
        }

        protected override void OnPopped()
        {
            if (_tutorialToggleButton != null) _tutorialToggleButton.onClick.RemoveListener(OnTutorialToggleClicked);
            if (_analyticsToggleButton != null) _analyticsToggleButton.onClick.RemoveListener(OnAnalyticsToggleClicked);
            if (_resetSaveButton != null) _resetSaveButton.onClick.RemoveListener(OnResetSaveClicked);
            if (_backButton != null) _backButton.onClick.RemoveListener(OnBackClicked);

            LocalizationRefresh.Unsubscribe(RefreshLabels);
            DisarmReset();
        }

        private void Update() => TickDisarm(Time.unscaledTime);

        // Separado de Update para poder testearlo con un reloj fake (EditMode).
        private void TickDisarm(float nowUnscaled)
        {
            if (_resetArmed && nowUnscaled - _armedAtUnscaled >= ArmWindowSeconds)
            {
                DisarmReset();
            }
        }

        private void PlayEntrance()
        {
            // El stagger de los botones lo dispara solo el JuicyMenuGroup de este
            // GO al activarse; acá va la capa de panel: fade del scrim + pop.
            if (_rootCanvasGroup != null)
            {
                _rootCanvasGroup.alpha = 0f;
                Tween.Alpha(_rootCanvasGroup, 1f, _entranceFadeDuration,
                    Ease.OutQuad, useUnscaledTime: true);
            }

            if (_panel != null)
            {
                _panel.localScale = Vector3.one * 0.92f;
                Tween.Scale(_panel, 1f, _entrancePopDuration, Ease.OutBack, useUnscaledTime: true);
            }
        }

        private void RefreshLabels()
        {
            if (_titleLabel != null)
            {
                // Tags de Text Animator (Febucci) — si el componente no está en
                // el label, TMP los ignora silenciosamente.
                _titleLabel.text = "<wave>" + LocalizedContent.Ui("pause.settings", "Opciones") + "</wave>";
            }

            if (_languageLabel != null)
            {
                _languageLabel.text = LocalizedContent.Ui("menu.language", "Idioma");
            }

            if (_backLabel != null)
            {
                _backLabel.text = LocalizedContent.Ui("menu.back", "Volver");
            }

            RefreshTutorialToggleLabel();
            RefreshAnalyticsToggleLabel();
            RefreshResetSaveLabel();
        }

        /// <summary>
        /// El label de borrar partida tiene dos textos según el estado de la
        /// confirmación de dos clicks, así que su refresco depende de <c>_resetArmed</c>
        /// y no puede vivir suelto en <see cref="RefreshLabels"/>.
        /// </summary>
        private void RefreshResetSaveLabel()
        {
            if (_resetSaveLabel == null) return;

            _resetSaveLabel.text = _resetArmed
                ? LocalizedContent.Ui("menu.reset_confirm", "¿Seguro?")
                : LocalizedContent.Ui("menu.delete", "Borrar partida");
        }

        // ================================================================
        // Toggles (movidos de MainMenuScreen)
        // ================================================================

        /// <summary>
        /// Invierte <see cref="IMetaProgressionService.IsTutorialEnabled"/> y
        /// persiste — gatea el auto-launch del tutorial en la primera run.
        /// </summary>
        private void OnTutorialToggleClicked()
        {
            if (!ServiceLocator.TryGetService<IMetaProgressionService>(out var meta) || meta == null)
            {
                Debug.LogWarning(LogPrefix + "IMetaProgressionService no esta registrado — no se puede togglear el tutorial.", this);
                return;
            }

            meta.SetTutorialEnabled(!meta.IsTutorialEnabled);
            RefreshTutorialToggleLabel();
        }

        private void RefreshTutorialToggleLabel()
        {
            if (_tutorialToggleLabel == null) return;

            bool enabled = !ServiceLocator.TryGetService<IMetaProgressionService>(out var meta) || meta == null || meta.IsTutorialEnabled;
            _tutorialToggleLabel.text = enabled
                ? LocalizedContent.Ui("menu.tutorial_on", "Tutorial: ON")
                : LocalizedContent.Ui("menu.tutorial_off", "Tutorial: OFF");
        }

        /// <summary>
        /// Invierte el consentimiento de telemetría vía
        /// <see cref="IAnalyticsConsentService"/> (persiste en PlayerPrefs).
        /// </summary>
        private void OnAnalyticsToggleClicked()
        {
            if (!ServiceLocator.TryGetService<IAnalyticsConsentService>(out var consent) || consent == null)
            {
                Debug.LogWarning(LogPrefix + "IAnalyticsConsentService no esta registrado — no se puede togglear la telemetría.", this);
                return;
            }

            consent.SetConsent(!consent.IsGranted);
            RefreshAnalyticsToggleLabel();
        }

        private void RefreshAnalyticsToggleLabel()
        {
            if (_analyticsToggleLabel == null) return;

            bool granted = ServiceLocator.TryGetService<IAnalyticsConsentService>(out var consent)
                           && consent != null && consent.IsGranted;
            _analyticsToggleLabel.text = granted
                ? LocalizedContent.Ui("menu.analytics_on", "Telemetría: ON")
                : LocalizedContent.Ui("menu.analytics_off", "Telemetría: OFF");
        }

        // ================================================================
        // Borrar partida — confirmación de dos clicks
        // ================================================================

        private void OnResetSaveClicked()
        {
            if (!_resetArmed)
            {
                ArmReset();
            }
            else
            {
                ConfirmReset();
            }
        }

        private void ArmReset()
        {
            _resetArmed = true;
            _armedAtUnscaled = Time.unscaledTime;
            RefreshResetSaveLabel();

            if (_resetSaveLabel != null)
            {
                // Shake extra al del click del JuicyMenuButton: el estado armado
                // tiene que leerse más fuerte que un click normal.
                if (_settings != null)
                {
                    Tween.ShakeLocalPosition(_resetSaveLabel.transform,
                        strength: new Vector3(_settings.ShakeStrength * 2f, _settings.ShakeStrength * 2f, 0f),
                        duration: _settings.ShakeDuration, frequency: 25f, useUnscaledTime: true);
                }
            }

            if (_resetSaveJuice != null && _settings != null)
            {
                _resetSaveJuice.SetColorOverride(_settings.AlertColor);
            }
        }

        /// <summary>
        /// Borra TODO el progreso: (1) resetea la meta-progresión al estado
        /// inicial y (2) elimina la run en curso guardada. Sin el paso 2 el
        /// Continue del menú reanudaría una partida recién borrada (los dos
        /// saves son independientes). El menú refresca su Continue al recuperar
        /// foco (<c>MainMenuScreen.OnGainFocus</c>).
        /// </summary>
        private void ConfirmReset()
        {
            if (ServiceLocator.TryGetService<IMetaProgressionService>(out var meta) && meta != null)
            {
                meta.ResetProgression();
            }
            else
            {
                Debug.LogWarning(LogPrefix + "IMetaProgressionService no esta registrado — " +
                                 "se borra solo la run en curso, no la meta-progresion.", this);
            }

            SaveSystem.DeleteSave();
            DisarmReset();

            Debug.Log(LogPrefix + "Partida borrada — meta-progresion en estado inicial y run en curso eliminada.", this);
        }

        private void DisarmReset()
        {
            _resetArmed = false;
            RefreshResetSaveLabel();

            if (_resetSaveJuice != null)
            {
                _resetSaveJuice.ClearColorOverride();
            }
        }

        private void OnBackClicked() => Close();

        private void Close()
        {
            if (ServiceLocator.TryGetService<IScreenManager>(out var screens))
            {
                screens.PopOverlay();
            }
            else
            {
                Debug.LogWarning(LogPrefix + "IScreenManager no está registrado — no se puede cerrar el overlay.", this);
            }
        }
    }
}
