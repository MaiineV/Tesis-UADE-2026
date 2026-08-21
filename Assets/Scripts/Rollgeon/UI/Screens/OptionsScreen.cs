using Patterns;
using Patterns.Save;
using PrimeTween;
using Rollgeon.Analytics;
using Rollgeon.Audio;
using Rollgeon.Localization;
using Rollgeon.Meta;
using Rollgeon.Timing;
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

        [Title("Tabs")]
        [SerializeField, Optional] private Button _generalTabButton;
        [SerializeField, Optional] private TMP_Text _generalTabLabel;
        [SerializeField, Optional] private Button _audioTabButton;
        [SerializeField, Optional] private TMP_Text _audioTabLabel;
        [SerializeField, Optional] private GameObject _generalTabRoot;
        [SerializeField, Optional] private GameObject _audioTabRoot;

        [Title("Audio")]
        [SerializeField, Optional] private TMP_Text _masterLabel;
        [SerializeField, Optional] private Slider _masterSlider;
        [SerializeField, Optional] private Button _masterMuteButton;
        [SerializeField, Optional] private TMP_Text _masterMuteLabel;
        [SerializeField, Optional] private JuicyMenuButton _masterMuteJuice;
        [SerializeField, Optional] private TMP_Text _musicLabel;
        [SerializeField, Optional] private Slider _musicSlider;
        [SerializeField, Optional] private Button _musicMuteButton;
        [SerializeField, Optional] private TMP_Text _musicMuteLabel;
        [SerializeField, Optional] private JuicyMenuButton _musicMuteJuice;
        [SerializeField, Optional] private TMP_Text _sfxLabel;
        [SerializeField, Optional] private Slider _sfxSlider;
        [SerializeField, Optional] private Button _sfxMuteButton;
        [SerializeField, Optional] private TMP_Text _sfxMuteLabel;
        [SerializeField, Optional] private JuicyMenuButton _sfxMuteJuice;

        [Title("Toggles")]
        [SerializeField, Optional] private Button _tutorialToggleButton;
        [SerializeField, Optional] private TMP_Text _tutorialToggleLabel;
        [SerializeField, Optional] private Button _analyticsToggleButton;
        [SerializeField, Optional] private TMP_Text _analyticsToggleLabel;

        [Title("Velocidad")]
        [SerializeField, Optional] private Button _gameSpeedButton;
        [SerializeField, Optional] private TMP_Text _gameSpeedLabel;

        [Title("Reroll")]
        [SerializeField, Optional] private Button _rerollModeButton;
        [SerializeField, Optional] private TMP_Text _rerollModeLabel;

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
            if (_gameSpeedButton != null) _gameSpeedButton.onClick.AddListener(OnGameSpeedClicked);
            if (_rerollModeButton != null) _rerollModeButton.onClick.AddListener(OnRerollModeClicked);
            if (_resetSaveButton != null) _resetSaveButton.onClick.AddListener(OnResetSaveClicked);
            if (_backButton != null) _backButton.onClick.AddListener(OnBackClicked);

            if (_generalTabButton != null) _generalTabButton.onClick.AddListener(OnGeneralTabClicked);
            if (_audioTabButton != null) _audioTabButton.onClick.AddListener(OnAudioTabClicked);
            if (_masterSlider != null) _masterSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            if (_musicSlider != null) _musicSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            if (_sfxSlider != null) _sfxSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
            if (_masterMuteButton != null) _masterMuteButton.onClick.AddListener(OnMasterMuteClicked);
            if (_musicMuteButton != null) _musicMuteButton.onClick.AddListener(OnMusicMuteClicked);
            if (_sfxMuteButton != null) _sfxMuteButton.onClick.AddListener(OnSfxMuteClicked);

            // Los botones ES/EN viven en este mismo panel: sin esto, cambiar el idioma
            // dejaba todo el panel con el texto del idioma anterior hasta reabrirlo.
            LocalizationRefresh.Subscribe(RefreshLabels);

            DisarmReset();
            ShowTab(audio: false);
            SyncAudioControls();
            RefreshLabels();
            PlayEntrance();
        }

        protected override void OnPopped()
        {
            if (_tutorialToggleButton != null) _tutorialToggleButton.onClick.RemoveListener(OnTutorialToggleClicked);
            if (_analyticsToggleButton != null) _analyticsToggleButton.onClick.RemoveListener(OnAnalyticsToggleClicked);
            if (_gameSpeedButton != null) _gameSpeedButton.onClick.RemoveListener(OnGameSpeedClicked);
            if (_rerollModeButton != null) _rerollModeButton.onClick.RemoveListener(OnRerollModeClicked);
            if (_resetSaveButton != null) _resetSaveButton.onClick.RemoveListener(OnResetSaveClicked);
            if (_backButton != null) _backButton.onClick.RemoveListener(OnBackClicked);

            if (_generalTabButton != null) _generalTabButton.onClick.RemoveListener(OnGeneralTabClicked);
            if (_audioTabButton != null) _audioTabButton.onClick.RemoveListener(OnAudioTabClicked);
            if (_masterSlider != null) _masterSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
            if (_musicSlider != null) _musicSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
            if (_sfxSlider != null) _sfxSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
            if (_masterMuteButton != null) _masterMuteButton.onClick.RemoveListener(OnMasterMuteClicked);
            if (_musicMuteButton != null) _musicMuteButton.onClick.RemoveListener(OnMusicMuteClicked);
            if (_sfxMuteButton != null) _sfxMuteButton.onClick.RemoveListener(OnSfxMuteClicked);

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

            if (_generalTabLabel != null)
            {
                _generalTabLabel.text = LocalizedContent.Ui("menu.tab_general", "General");
            }

            if (_audioTabLabel != null)
            {
                _audioTabLabel.text = LocalizedContent.Ui("menu.tab_audio", "Audio");
            }

            RefreshTutorialToggleLabel();
            RefreshAnalyticsToggleLabel();
            RefreshGameSpeedLabel();
            RefreshRerollModeLabel();
            RefreshResetSaveLabel();
            RefreshAudioLabels();
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
        // Velocidad del juego
        // ================================================================

        /// <summary>
        /// Cicla x1→x2→x4 vía <see cref="GameSpeedPrefs"/> (persiste al toque — la
        /// pausa es soft, así que el cambio se siente sin salir del menú).
        /// </summary>
        private void OnGameSpeedClicked()
        {
            GameSpeedPrefs.CycleNext();
            RefreshGameSpeedLabel();
        }

        private void RefreshGameSpeedLabel()
        {
            if (_gameSpeedLabel == null) return;

            _gameSpeedLabel.text = string.Format(
                LocalizedContent.Ui("menu.game_speed", "Velocidad: x{0}"),
                GameSpeedPrefs.Multiplier);
        }

        // ================================================================
        // Modo de reroll
        // ================================================================

        /// <summary>
        /// Alterna la semántica de selección del reroll vía
        /// <see cref="Rollgeon.Dice.RerollSelectionPrefs"/> (persiste al toque):
        /// default = los seleccionados vuelan (Balatro); alternativo = los
        /// seleccionados se quedan (clásico).
        /// </summary>
        private void OnRerollModeClicked()
        {
            Rollgeon.Dice.RerollSelectionPrefs.KeepSelected =
                !Rollgeon.Dice.RerollSelectionPrefs.KeepSelected;
            RefreshRerollModeLabel();
        }

        private void RefreshRerollModeLabel()
        {
            if (_rerollModeLabel == null) return;

            _rerollModeLabel.text = Rollgeon.Dice.RerollSelectionPrefs.KeepSelected
                ? LocalizedContent.Ui("menu.reroll_keep", "Reroll: se quedan los elegidos")
                : LocalizedContent.Ui("menu.reroll_discard", "Reroll: vuelan los elegidos");
        }

        // ================================================================
        // Tabs (General | Audio)
        // ================================================================

        private void OnGeneralTabClicked() => ShowTab(audio: false);
        private void OnAudioTabClicked() => ShowTab(audio: true);

        /// <summary>
        /// Alterna el contenido del panel. Título, tabs y Volver son comunes;
        /// el resto vive en uno de los dos roots.
        /// </summary>
        private void ShowTab(bool audio)
        {
            if (_generalTabRoot != null) _generalTabRoot.SetActive(!audio);
            if (_audioTabRoot != null) _audioTabRoot.SetActive(audio);

            // El tab activo se lee por opacidad — sin esto los dos botones
            // parecen igualmente "apretables" y no se sabe dónde estás parado.
            SetTabLabelEmphasis(_generalTabLabel, selected: !audio);
            SetTabLabelEmphasis(_audioTabLabel, selected: audio);
        }

        private static void SetTabLabelEmphasis(TMP_Text label, bool selected)
        {
            if (label == null) return;
            var color = label.color;
            color.a = selected ? 1f : 0.45f;
            label.color = color;
        }

        // ================================================================
        // Audio — sliders + mutes
        // ================================================================

        private static IAudioService AudioService =>
            ServiceLocator.TryGetService<IAudioService>(out IAudioService audio) ? audio : null;

        /// <summary>
        /// Pone sliders y mutes en el estado actual del servicio sin disparar los
        /// handlers (<c>SetValueWithoutNotify</c>) — abrir el panel no re-setea nada.
        /// </summary>
        private void SyncAudioControls()
        {
            var audio = AudioService;
            if (audio == null) return;

            if (_masterSlider != null) _masterSlider.SetValueWithoutNotify(audio.GetVolume(AudioChannel.Master));
            if (_musicSlider != null) _musicSlider.SetValueWithoutNotify(audio.GetVolume(AudioChannel.Music));
            if (_sfxSlider != null) _sfxSlider.SetValueWithoutNotify(audio.GetVolume(AudioChannel.Sfx));
        }

        private void OnMasterVolumeChanged(float value) => AudioService?.SetVolume(AudioChannel.Master, value);

        private void OnMusicVolumeChanged(float value) => AudioService?.SetVolume(AudioChannel.Music, value);

        /// <summary>El slider de SFX gobierna Sfx y Ui — un solo control para todos los efectos.</summary>
        private void OnSfxVolumeChanged(float value)
        {
            var audio = AudioService;
            if (audio == null) return;

            audio.SetVolume(AudioChannel.Sfx, value);
            audio.SetVolume(AudioChannel.Ui, value);
        }

        private void OnMasterMuteClicked() => ToggleMute(AudioChannel.Master);

        private void OnMusicMuteClicked() => ToggleMute(AudioChannel.Music);

        private void OnSfxMuteClicked()
        {
            var audio = AudioService;
            if (audio == null) return;

            bool muted = !audio.IsMuted(AudioChannel.Sfx);
            audio.SetMuted(AudioChannel.Sfx, muted);
            audio.SetMuted(AudioChannel.Ui, muted);
            RefreshAudioLabels();
        }

        private void ToggleMute(AudioChannel channel)
        {
            var audio = AudioService;
            if (audio == null) return;

            audio.SetMuted(channel, !audio.IsMuted(channel));
            RefreshAudioLabels();
        }

        private void RefreshAudioLabels()
        {
            if (_masterLabel != null) _masterLabel.text = LocalizedContent.Ui("menu.audio_master", "Master");
            if (_musicLabel != null) _musicLabel.text = LocalizedContent.Ui("menu.audio_music", "Música");
            if (_sfxLabel != null) _sfxLabel.text = LocalizedContent.Ui("menu.audio_sfx", "Efectos");

            var audio = AudioService;
            RefreshMuteVisual(_masterMuteLabel, _masterMuteJuice, audio != null && audio.IsMuted(AudioChannel.Master));
            RefreshMuteVisual(_musicMuteLabel, _musicMuteJuice, audio != null && audio.IsMuted(AudioChannel.Music));
            RefreshMuteVisual(_sfxMuteLabel, _sfxMuteJuice, audio != null && audio.IsMuted(AudioChannel.Sfx));
        }

        /// <summary>
        /// El botón de mute muestra el estado ("Sonando"/"Muteado") y cuando está
        /// muteado usa el color de alerta del settings — mismo lenguaje visual que
        /// el borrar partida armado.
        /// </summary>
        private void RefreshMuteVisual(TMP_Text label, JuicyMenuButton juice, bool muted)
        {
            if (label != null)
            {
                label.text = muted
                    ? LocalizedContent.Ui("menu.audio_muted", "Muteado")
                    : LocalizedContent.Ui("menu.audio_unmuted", "Sonando");
            }

            if (juice != null && _settings != null)
            {
                if (muted) juice.SetColorOverride(_settings.AlertColor);
                else juice.ClearColorOverride();
            }
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

            // El flag de "ya vi la guía de armado de bolsa" vive en PlayerPrefs, así que
            // no lo alcanza el borrado de saves — lo limpiamos a mano para que un jugador
            // que empieza de cero la vuelva a ver.
            Rollgeon.UI.Help.BuildHelpPrefs.ClearSeen();

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
