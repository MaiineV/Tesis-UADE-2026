using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Patterns.Save;
using Rollgeon.Analytics;
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
