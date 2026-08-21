using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Patterns.Save;
using Rollgeon.Analytics;
using Rollgeon.Audio;
using Rollgeon.Localization;
using Rollgeon.Timing;
using Rollgeon.UI.Screens;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Panel de opciones: confirmación de dos clicks del "Borrar partida"
    /// (armado / confirmación / auto-desarme) y toggle de telemetría.
    /// </summary>
    [TestFixture]
    public class OptionsScreenTests
    {
        private GameObject _screenGO;
        private OptionsScreen _screen;
        private Button _resetButton;
        private TMP_Text _resetLabel;
        private Button _analyticsButton;
        private TMP_Text _analyticsLabel;
        private Button _speedButton;
        private TMP_Text _speedLabel;
        private Button _rerollModeButton;
        private TMP_Text _rerollModeLabel;
        private GameObject _generalTabRoot;
        private GameObject _audioTabRoot;
        private Button _generalTabButton;
        private Button _audioTabButton;
        private Slider _masterSlider;
        private Slider _musicSlider;
        private Slider _sfxSlider;
        private Button _musicMuteButton;
        private TMP_Text _musicMuteLabel;
        private Button _sfxMuteButton;
        private TMP_Text _sfxMuteLabel;
        private FakeAudioService _audio;
        private InMemoryStore _store;
        private SaveSettingsSO _settings;
        private int _savedSpeed;
        private bool _savedKeepSelected;

        // Copia local — los test asmdefs no se referencian entre sí.
        private sealed class InMemoryStore : ISaveFileStore
        {
            public readonly Dictionary<string, byte[]> Files = new();
            public bool Exists(string path) => Files.ContainsKey(path);
            public byte[] Read(string path) => Files[path];
            public void Write(string path, byte[] bytes) => Files[path] = bytes;
            public void Delete(string path) => Files.Remove(path);
        }

        private sealed class FakeAudioService : IAudioService
        {
            public readonly Dictionary<AudioChannel, float> Volumes = new();
            public readonly HashSet<AudioChannel> Muted = new();
            public readonly List<(AudioChannel channel, float value)> SetVolumeCalls = new();

            public void PlaySfx(AudioClip clip, Vector3 worldPos, float volume = 1f, float pitch = 1f, bool isImportant = false) { }
            public void PlaySfx2D(AudioClip clip, float volume = 1f, float pitch = 1f, bool isImportant = false) { }
            public void PlayMusic(AudioClip clip, float fadeSeconds = 1f) { }
            public void PlayMusicForBiome(string biomeId, float fadeSeconds = 1f) { }
            public void StopMusic(float fadeSeconds = 1f) { }
            public void PauseMusic() { }
            public void ResumeMusic() { }

            public void SetVolume(AudioChannel channel, float value)
            {
                Volumes[channel] = value;
                SetVolumeCalls.Add((channel, value));
            }

            public float GetVolume(AudioChannel channel) =>
                Volumes.TryGetValue(channel, out var v) ? v : 1f;

            public void SetMuted(AudioChannel channel, bool muted)
            {
                if (muted) Muted.Add(channel);
                else Muted.Remove(channel);
            }

            public bool IsMuted(AudioChannel channel) => Muted.Contains(channel);
        }

        private sealed class FakeConsent : IAnalyticsConsentService
        {
            public bool HasDecision => true;
            public bool IsGranted { get; private set; }
            public void SetConsent(bool granted) => IsGranted = granted;
            public string PrivacyUrl => "https://example.test";
            public bool TryRequestDataDeletion() => true;
        }

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            SaveSystem.ResetForTests();

            _store = new InMemoryStore();
            SaveSystem.SetStoreForTests(_store);
            _settings = ScriptableObject.CreateInstance<SaveSettingsSO>();
            ServiceLocator.AddService<SaveSettingsSO>(_settings, ServiceScope.Global);

            _screenGO = new GameObject("OptionsScreen_Test");
            _screenGO.SetActive(false);
            _screen = _screenGO.AddComponent<OptionsScreen>();

            _resetButton = AttachButton("DeleteProgressButton", out _resetLabel);
            _analyticsButton = AttachButton("AnalyticsToggleButton", out _analyticsLabel);
            _speedButton = AttachButton("GameSpeedButton", out _speedLabel);
            _rerollModeButton = AttachButton("RerollModeButton", out _rerollModeLabel);
            AssignPrivate(_screen, "_resetSaveButton", _resetButton);
            AssignPrivate(_screen, "_resetSaveLabel", _resetLabel);
            AssignPrivate(_screen, "_analyticsToggleButton", _analyticsButton);
            AssignPrivate(_screen, "_analyticsToggleLabel", _analyticsLabel);
            AssignPrivate(_screen, "_gameSpeedButton", _speedButton);
            AssignPrivate(_screen, "_gameSpeedLabel", _speedLabel);
            AssignPrivate(_screen, "_rerollModeButton", _rerollModeButton);
            AssignPrivate(_screen, "_rerollModeLabel", _rerollModeLabel);

            _audio = new FakeAudioService();
            ServiceLocator.AddService<IAudioService>(_audio, ServiceScope.Global);

            _generalTabRoot = new GameObject("GeneralTab");
            _generalTabRoot.transform.SetParent(_screenGO.transform, false);
            _audioTabRoot = new GameObject("AudioTab");
            _audioTabRoot.transform.SetParent(_screenGO.transform, false);
            _generalTabButton = AttachButton("GeneralTabButton", out _);
            _audioTabButton = AttachButton("AudioTabButton", out _);
            _masterSlider = AttachSlider("MasterVolumeSlider");
            _musicSlider = AttachSlider("MusicVolumeSlider");
            _sfxSlider = AttachSlider("SfxVolumeSlider");
            _musicMuteButton = AttachButton("MusicMuteButton", out _musicMuteLabel);
            _sfxMuteButton = AttachButton("SfxMuteButton", out _sfxMuteLabel);
            AssignPrivate(_screen, "_generalTabRoot", _generalTabRoot);
            AssignPrivate(_screen, "_audioTabRoot", _audioTabRoot);
            AssignPrivate(_screen, "_generalTabButton", _generalTabButton);
            AssignPrivate(_screen, "_audioTabButton", _audioTabButton);
            AssignPrivate(_screen, "_masterSlider", _masterSlider);
            AssignPrivate(_screen, "_musicSlider", _musicSlider);
            AssignPrivate(_screen, "_sfxSlider", _sfxSlider);
            AssignPrivate(_screen, "_musicMuteButton", _musicMuteButton);
            AssignPrivate(_screen, "_musicMuteLabel", _musicMuteLabel);
            AssignPrivate(_screen, "_sfxMuteButton", _sfxMuteButton);
            AssignPrivate(_screen, "_sfxMuteLabel", _sfxMuteLabel);

            // Los setters de GameSpeedPrefs / RerollSelectionPrefs escriben
            // PlayerPrefs reales incluso en EditMode — backup acá, restore en TearDown.
            _savedSpeed = GameSpeedPrefs.Multiplier;
            _savedKeepSelected = Rollgeon.Dice.RerollSelectionPrefs.KeepSelected;
        }

        [TearDown]
        public void TearDown()
        {
            if (_screenGO != null) Object.DestroyImmediate(_screenGO);
            if (_settings != null) Object.DestroyImmediate(_settings);
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            SaveSystem.ResetForTests();

            GameSpeedPrefs.Multiplier = _savedSpeed;
            Rollgeon.Dice.RerollSelectionPrefs.KeepSelected = _savedKeepSelected;
        }

        [Test]
        public void ResetSave_FirstClick_ArmsWithoutDeleting()
        {
            // Arrange
            _store.Files[_settings.GetSavePath()] = new byte[] { 1 };
            Push();

            // Act
            _resetButton.onClick.Invoke();

            // Assert: nada borrado, label en modo confirmación.
            Assert.IsTrue(_store.Exists(_settings.GetSavePath()),
                "El primer click solo arma — no debe borrar nada.");
            Assert.IsTrue(SaveSystem.HasSave());
            Assert.AreEqual(LocalizedContent.Ui("menu.reset_confirm", "¿Seguro?"), _resetLabel.text);
        }

        [Test]
        public void ResetSave_SecondClick_DeletesRunSaveAndRestoresLabel()
        {
            // Arrange
            _store.Files[_settings.GetSavePath()] = new byte[] { 1 };
            Push();

            // Act: armar + confirmar (sin IMetaProgressionService — borra solo la
            // run y loguea un warning inocuo, mismo contrato que el flujo viejo).
            _resetButton.onClick.Invoke();
            _resetButton.onClick.Invoke();

            // Assert
            Assert.IsFalse(_store.Exists(_settings.GetSavePath()),
                "Confirmar debe eliminar el save file de la run en curso.");
            Assert.IsFalse(SaveSystem.HasSave());
            Assert.AreEqual(LocalizedContent.Ui("menu.delete", "Borrar partida"), _resetLabel.text,
                "Tras confirmar, el label vuelve al estado de reposo.");
        }

        [Test]
        public void ResetSave_TickPastWindow_DisarmsWithoutDeleting()
        {
            // Arrange: armado.
            _store.Files[_settings.GetSavePath()] = new byte[] { 1 };
            Push();
            _resetButton.onClick.Invoke();
            float armedAt = GetPrivateFloat(_screen, "_armedAtUnscaled");

            // Act: pasa la ventana de 3s (reloj fake vía TickDisarm).
            InvokePrivate(_screen, "TickDisarm", armedAt + 3.01f);

            // Assert: desarmado sin borrar; el próximo click vuelve a armar.
            Assert.IsTrue(_store.Exists(_settings.GetSavePath()),
                "El auto-desarme no debe borrar nada.");
            Assert.AreEqual(LocalizedContent.Ui("menu.delete", "Borrar partida"), _resetLabel.text);

            _resetButton.onClick.Invoke();
            Assert.IsTrue(_store.Exists(_settings.GetSavePath()),
                "Tras desarmarse, un click vuelve a armar en vez de confirmar.");
            Assert.AreEqual(LocalizedContent.Ui("menu.reset_confirm", "¿Seguro?"), _resetLabel.text);
        }

        [Test]
        public void AnalyticsToggle_Click_FlipsConsentAndLabel()
        {
            // Arrange
            var consent = new FakeConsent();
            ServiceLocator.AddService<IAnalyticsConsentService>(consent, ServiceScope.Global);
            Push();
            Assert.IsFalse(consent.IsGranted);

            // Act
            _analyticsButton.onClick.Invoke();

            // Assert
            Assert.IsTrue(consent.IsGranted);
            Assert.AreEqual(LocalizedContent.Ui("menu.analytics_on", "Telemetría: ON"), _analyticsLabel.text);

            // Act: segundo click vuelve a OFF.
            _analyticsButton.onClick.Invoke();

            // Assert
            Assert.IsFalse(consent.IsGranted);
            Assert.AreEqual(LocalizedContent.Ui("menu.analytics_off", "Telemetría: OFF"), _analyticsLabel.text);
        }

        [Test]
        public void GameSpeed_Clicks_CycleMultiplierAndLabel()
        {
            // Arrange
            GameSpeedPrefs.Multiplier = 1;
            Push();
            string Expected(int speed) => string.Format(
                LocalizedContent.Ui("menu.game_speed", "Velocidad: x{0}"), speed);
            Assert.AreEqual(Expected(1), _speedLabel.text,
                "OnPushed debe reflejar el speed persistido.");

            // Act + Assert: el ciclo completo, wrap incluido.
            _speedButton.onClick.Invoke();
            Assert.AreEqual(2, GameSpeedPrefs.Multiplier);
            Assert.AreEqual(Expected(2), _speedLabel.text);

            _speedButton.onClick.Invoke();
            Assert.AreEqual(4, GameSpeedPrefs.Multiplier);

            _speedButton.onClick.Invoke();
            Assert.AreEqual(1, GameSpeedPrefs.Multiplier,
                "Tras x4 el ciclo vuelve a x1.");
            Assert.AreEqual(Expected(1), _speedLabel.text);
        }

        [Test]
        public void RerollMode_Clicks_FlipPrefAndLabel()
        {
            // Arrange — modo default (invertido: los seleccionados vuelan).
            Rollgeon.Dice.RerollSelectionPrefs.KeepSelected = false;
            Push();
            Assert.AreEqual(
                LocalizedContent.Ui("menu.reroll_discard", "Reroll: vuelan los elegidos"),
                _rerollModeLabel.text,
                "OnPushed debe reflejar el modo persistido.");

            // Act + Assert — un click pasa a clásico.
            _rerollModeButton.onClick.Invoke();
            Assert.IsTrue(Rollgeon.Dice.RerollSelectionPrefs.KeepSelected);
            Assert.AreEqual(
                LocalizedContent.Ui("menu.reroll_keep", "Reroll: se quedan los elegidos"),
                _rerollModeLabel.text);

            // Segundo click vuelve al default.
            _rerollModeButton.onClick.Invoke();
            Assert.IsFalse(Rollgeon.Dice.RerollSelectionPrefs.KeepSelected);
            Assert.AreEqual(
                LocalizedContent.Ui("menu.reroll_discard", "Reroll: vuelan los elegidos"),
                _rerollModeLabel.text);
        }

        [Test]
        public void AudioSliders_OnPushed_SyncFromServiceWithoutWritingBack()
        {
            // Arrange
            _audio.Volumes[AudioChannel.Master] = 0.7f;
            _audio.Volumes[AudioChannel.Music] = 0.3f;
            _audio.Volumes[AudioChannel.Sfx] = 0.5f;

            // Act
            Push();

            // Assert: sliders reflejan el servicio y abrir el panel no re-setea nada.
            Assert.AreEqual(0.7f, _masterSlider.value);
            Assert.AreEqual(0.3f, _musicSlider.value);
            Assert.AreEqual(0.5f, _sfxSlider.value);
            Assert.IsEmpty(_audio.SetVolumeCalls,
                "SyncAudioControls debe usar SetValueWithoutNotify — sin writes al abrir.");
        }

        [Test]
        public void MusicSlider_Change_SetsMusicChannelVolume()
        {
            // Arrange
            Push();

            // Act
            _musicSlider.value = 0.25f;

            // Assert
            Assert.AreEqual(0.25f, _audio.GetVolume(AudioChannel.Music));
        }

        [Test]
        public void SfxSlider_Change_SetsSfxAndUiChannels()
        {
            // Arrange
            Push();

            // Act
            _sfxSlider.value = 0.4f;

            // Assert: un solo control gobierna todos los efectos (Sfx + Ui).
            Assert.AreEqual(0.4f, _audio.GetVolume(AudioChannel.Sfx));
            Assert.AreEqual(0.4f, _audio.GetVolume(AudioChannel.Ui));
        }

        [Test]
        public void MusicMute_Clicks_ToggleMuteAndLabel()
        {
            // Arrange
            Push();

            // Act
            _musicMuteButton.onClick.Invoke();

            // Assert
            Assert.IsTrue(_audio.IsMuted(AudioChannel.Music));
            Assert.AreEqual(LocalizedContent.Ui("menu.audio_muted", "Muteado"), _musicMuteLabel.text);

            // Act: segundo click desmutea.
            _musicMuteButton.onClick.Invoke();

            // Assert
            Assert.IsFalse(_audio.IsMuted(AudioChannel.Music));
            Assert.AreEqual(LocalizedContent.Ui("menu.audio_unmuted", "Sonando"), _musicMuteLabel.text);
        }

        [Test]
        public void SfxMute_Click_MutesSfxAndUiChannels()
        {
            // Arrange
            Push();

            // Act
            _sfxMuteButton.onClick.Invoke();

            // Assert
            Assert.IsTrue(_audio.IsMuted(AudioChannel.Sfx));
            Assert.IsTrue(_audio.IsMuted(AudioChannel.Ui));
        }

        [Test]
        public void Tabs_Clicks_SwitchVisibleRoot()
        {
            // Arrange: el panel abre en General.
            Push();
            Assert.IsTrue(_generalTabRoot.activeSelf);
            Assert.IsFalse(_audioTabRoot.activeSelf);

            // Act
            _audioTabButton.onClick.Invoke();

            // Assert
            Assert.IsFalse(_generalTabRoot.activeSelf);
            Assert.IsTrue(_audioTabRoot.activeSelf);

            // Act: volver a General.
            _generalTabButton.onClick.Invoke();

            // Assert
            Assert.IsTrue(_generalTabRoot.activeSelf);
            Assert.IsFalse(_audioTabRoot.activeSelf);
        }

        // ---------------- helpers ----------------

        /// <summary>
        /// Activa el GO e invoca <c>OnPushed</c> por reflection — el wiring del
        /// panel vive ahí (contrato BaseScreen), no en OnEnable.
        /// </summary>
        private void Push()
        {
            _screenGO.SetActive(true);
            typeof(OptionsScreen)
                .GetMethod("OnPushed", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_screen, new object[] { null });
        }

        private Button AttachButton(string name, out TMP_Text label)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_screenGO.transform, false);
            var button = go.AddComponent<Button>();
            var labelGo = new GameObject("Text");
            labelGo.transform.SetParent(go.transform, false);
            label = labelGo.AddComponent<TextMeshProUGUI>();
            return button;
        }

        private Slider AttachSlider(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_screenGO.transform, false);
            var slider = go.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            return slider;
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
            Assert.IsNotNull(field, $"Field '{fieldName}' no encontrado en {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static float GetPrivateFloat(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' no encontrado.");
            return (float)field.GetValue(target);
        }

        private static void InvokePrivate(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Método '{methodName}' no encontrado.");
            method.Invoke(target, args);
        }
    }
}
