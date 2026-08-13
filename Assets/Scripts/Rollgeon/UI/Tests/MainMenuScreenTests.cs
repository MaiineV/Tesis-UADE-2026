using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using Patterns;
using Patterns.Save;
using Rollgeon.UI.Screens;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Botón Continue del main menu: prendido sí y solo sí hay un save de run en
    /// curso; un save ilegible al clickear apaga el botón sin navegar.
    /// </summary>
    [TestFixture]
    public class MainMenuScreenTests
    {
        private GameObject _screenGO;
        private MainMenuScreen _screen;
        private Button _playButton;
        private Button _continueButton;
        private Button _quitButton;
        private InMemoryStore _store;
        private SaveSettingsSO _settings;

        // Copia local — los test asmdefs no se referencian entre sí.
        private sealed class InMemoryStore : ISaveFileStore
        {
            public readonly Dictionary<string, byte[]> Files = new();
            public bool Exists(string path) => Files.ContainsKey(path);
            public byte[] Read(string path) => Files[path];
            public void Write(string path, byte[] bytes) => Files[path] = bytes;
            public void Delete(string path) => Files.Remove(path);
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

            _screenGO = new GameObject("MainMenuScreen_Test");
            _screenGO.SetActive(false);
            _screen = _screenGO.AddComponent<MainMenuScreen>();

            _playButton = AttachButton("PlayButton");
            _continueButton = AttachButton("ContinueButton");
            _quitButton = AttachButton("QuitButton");
            AssignPrivate(_screen, "_playButton", _playButton);
            AssignPrivate(_screen, "_continueButton", _continueButton);
            AssignPrivate(_screen, "_quitButton", _quitButton);
        }

        [TearDown]
        public void TearDown()
        {
            if (_screenGO != null) Object.DestroyImmediate(_screenGO);
            if (_settings != null) Object.DestroyImmediate(_settings);
            Rollgeon.Run.PendingRunRequest.Clear();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            SaveSystem.ResetForTests();
        }

        [Test]
        public void ContinueButton_NoSave_DisabledOnEnable()
        {
            ActivateAndEnable();

            Assert.IsFalse(_continueButton.interactable);
        }

        [Test]
        public void ContinueButton_SaveExists_EnabledOnEnable()
        {
            _store.Files[_settings.GetSavePath()] = new byte[] { 1 };

            ActivateAndEnable();

            Assert.IsTrue(_continueButton.interactable);
        }

        [Test]
        public void ContinueClicked_UnreadableSave_DisablesButtonAndDoesNotNavigate()
        {
            _store.Files[_settings.GetSavePath()] = Encoding.UTF8.GetBytes("{ basura ]");
            ActivateAndEnable();
            Assert.IsTrue(_continueButton.interactable);

            // Odin puede loguear errores propios al deserializar basura — el contrato
            // observable es: botón apagado, sin PendingRunRequest, sin scene load.
            LogAssert.ignoreFailingMessages = true;
            try
            {
                _continueButton.onClick.Invoke();
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }

            Assert.IsFalse(_continueButton.interactable);
            Assert.IsFalse(Rollgeon.Run.PendingRunRequest.HasRequest);
        }

        [Test]
        public void ContinueClicked_SaveWithoutRunIdentity_DisablesButton()
        {
            // Save legible pero sin unlock_tracker (identidad de run): flush de un
            // cache con una key ajena.
            var seeder = new IdentitylessSeeder();
            SaveSystem.Register(seeder);
            SaveSystem.CaptureAll();
            SaveSystem.Flush(SaveTrigger.Manual);
            SaveSystem.ResetForTests();
            SaveSystem.SetStoreForTests(_store);

            ActivateAndEnable();
            Assert.IsTrue(_continueButton.interactable);

            _continueButton.onClick.Invoke();

            Assert.IsFalse(_continueButton.interactable);
            Assert.IsFalse(Rollgeon.Run.PendingRunRequest.HasRequest);
        }

        [Test]
        public void OnGainFocus_AfterSaveDeleted_DisablesContinueButton()
        {
            // Arrange: hay una run guardada → Continue arranca prendido. El panel
            // de opciones (overlay) puede borrar el save; al pop, el menú recupera
            // foco sin re-enable y debe refrescar el Continue.
            _store.Files[_settings.GetSavePath()] = new byte[] { 1 };
            ActivateAndEnable();
            Assert.IsTrue(_continueButton.interactable,
                "Precondición: con save presente el Continue debe estar prendido.");

            // Act: el save desaparece (lo borró el panel) y el menú recupera foco.
            SaveSystem.DeleteSave();
            typeof(MainMenuScreen)
                .GetMethod("OnGainFocus", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_screen, null);

            // Assert
            Assert.IsFalse(_continueButton.interactable,
                "Tras recuperar foco sin save, el Continue debe quedar apagado.");
        }

        // ---------------- helpers ----------------

        /// <summary>
        /// Activa el GO e invoca <c>OnEnable</c> por reflection: en EditMode Unity no
        /// dispara los lifecycle messages sin [ExecuteInEditMode].
        /// </summary>
        private void ActivateAndEnable()
        {
            _screenGO.SetActive(true);
            typeof(MainMenuScreen)
                .GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_screen, null);
        }

        private sealed class IdentitylessSeeder : ISaveable
        {
            public string SaveKey => "run.gold";
            public object CaptureState() => 42;
            public void RestoreState(object state) { }
        }

        private Button AttachButton(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_screenGO.transform, false);
            return go.AddComponent<Button>();
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
    }
}
