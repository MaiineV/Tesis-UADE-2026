using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combos;
using Rollgeon.Combos.Concretes;
using Rollgeon.Combos.Tests;
using Rollgeon.Heroes;
using Rollgeon.Localization;
using Rollgeon.Meta;
using Rollgeon.UI.HUD;
using Rollgeon.UI.Screens;
using Rollgeon.UI.Tooltips;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Cobertura del state-machine del <see cref="ClassSelectionScreen"/> (brief §12):
    /// <list type="bullet">
    /// <item><description><see cref="ScreenStringId_Is_ClassSelectionScreen_Literal"/> — matchea el string que pushea MainMenuScreen.</description></item>
    /// <item><description><see cref="OnPushed_DisablesConfirmButton_AndLocksMagoPicaro"/> — estado inicial sin seleccion.</description></item>
    /// <item><description><see cref="WarriorClick_EnablesConfirm_AndPopulatesPanel"/> — el click del Guerrero activa Confirm + puebla el panel.</description></item>
    /// <item><description><see cref="ConfirmClick_FiresOnRunStart_WithRulesetId"/> — el confirm dispara OnRunStart con schema correcto.</description></item>
    /// </list>
    /// EditMode puro — GameObjects en memoria, sin assets ni escenas.
    /// </summary>
    [TestFixture]
    public class ClassSelectionScreenTests
    {
        private class SpyScreenManager : IScreenManager
        {
            public IBaseScreen Current { get; set; }
            public int PopCurrentCallCount { get; private set; }

            public void Push<TScreen>(IScreenPayload payload = null) where TScreen : class, IBaseScreen { }
            public void PushByStringId(string screenId, IScreenPayload payload = null) { }
            public void PopCurrent() => PopCurrentCallCount++;
            public void PushOverlay<TScreen>(IScreenPayload payload = null) where TScreen : class, IBaseScreen { }
            public void PopOverlay() { }
            public void RegisterScreen(IBaseScreen screen) { }
            public void UnregisterScreen(IBaseScreen screen) { }
        }

        private GameObject _screenGO;
        private ClassSelectionScreen _screen;
        private Button _warriorButton;
        private Button _magoButton;
        private Button _picaroButton;
        private Button _confirmButton;
        private Button _backButton;
        private GameObject _indicator;
        private Image _portrait;
        private ContractDisplayView _contractDisplay;
        private ClassHeroSO _warriorHero;

        private Combo_Par _par;
        private Combo_DoblePar _doblePar;
        private Combo_SumaX _sumaX;
        private Combo_Trio _trio;
        private Combo_Escalera _escalera;
        private Combo_FullHouse _fullHouse;
        private Combo_Poker _poker;
        private Combo_Generala _generala;

        [SetUp]
        public void SetUp()
        {
            _screenGO = new GameObject("ClassSelectionScreen");
            _screenGO.SetActive(false);
            _screen = _screenGO.AddComponent<ClassSelectionScreen>();

            _warriorButton = AttachButton("WarriorButton");
            _magoButton = AttachButton("MagoButton");
            _picaroButton = AttachButton("PicaroButton");
            _confirmButton = AttachButton("ConfirmButton");
            _backButton = AttachButton("BackButton");

            _indicator = new GameObject("WarriorIndicator");
            _indicator.transform.SetParent(_screenGO.transform, false);
            _indicator.SetActive(true); // se apaga en OnPushed

            var portraitGO = new GameObject("Portrait");
            portraitGO.transform.SetParent(_screenGO.transform, false);
            _portrait = portraitGO.AddComponent<Image>();

            // ContractDisplayView con sus refs cableados (rows container + prefab sencillo).
            var contractGO = new GameObject("ContractDisplayView");
            contractGO.transform.SetParent(_screenGO.transform, false);
            _contractDisplay = contractGO.AddComponent<ContractDisplayView>();

            var rowsContainer = new GameObject("RowsContainer");
            rowsContainer.transform.SetParent(contractGO.transform, false);

            // Prefab "virtual": un GameObject con ComboRowView — se usa como template.
            var rowPrefabGO = new GameObject("ComboRowPrefab");
            rowPrefabGO.SetActive(false);
            var rowPrefab = rowPrefabGO.AddComponent<ComboRowView>();

            AssignPrivate(_contractDisplay, "_rowsContainer", rowsContainer.transform);
            AssignPrivate(_contractDisplay, "_rowPrefab", rowPrefab);

            // Warrior hero con 8 combos poblados (priorities ascendentes — matchea §5.4).
            _par = ComboTestUtils.CreateCombo<Combo_Par>(ComboId.Par, 10);
            _doblePar = ComboTestUtils.CreateCombo<Combo_DoblePar>(ComboId.DoublePair, 18);
            _sumaX = ComboTestUtils.CreateCombo<Combo_SumaX>(ComboId.HigherNumber, 25);
            _trio = ComboTestUtils.CreateCombo<Combo_Trio>(ComboId.Triple, 28);
            _escalera = ComboTestUtils.CreateCombo<Combo_Escalera>(ComboId.Straight, 35);
            _fullHouse = ComboTestUtils.CreateCombo<Combo_FullHouse>(ComboId.FullHouse, 40);
            _poker = ComboTestUtils.CreateCombo<Combo_Poker>(ComboId.Poker, 60);
            _generala = ComboTestUtils.CreateCombo<Combo_Generala>(ComboId.Generala, 100);

            _warriorHero = ScriptableObject.CreateInstance<ClassHeroSO>();
            _warriorHero.EntityId = "hero.warrior";
            _warriorHero.DisplayName = "Guerrero";
            _warriorHero.Sheet = new ContractSheet
            {
                Combos = new List<BaseComboSO>
                {
                    _par, _doblePar, _sumaX, _trio, _escalera, _fullHouse, _poker, _generala,
                },
            };

            // Wirear los fields serializados del screen via reflection.
            AssignPrivate(_screen, "_warriorHero", _warriorHero);
            AssignPrivate(_screen, "_warriorButton", _warriorButton);
            AssignPrivate(_screen, "_magoButton", _magoButton);
            AssignPrivate(_screen, "_picaroButton", _picaroButton);
            AssignPrivate(_screen, "_confirmButton", _confirmButton);
            AssignPrivate(_screen, "_backButton", _backButton);
            AssignPrivate(_screen, "_contractDisplay", _contractDisplay);
            AssignPrivate(_screen, "_portraitDisplay", _portrait);
            // _passiveDisplay se deja null — el screen tiene null-check (TMP requiere TMP_Settings).
            AssignPrivate(_screen, "_warriorSelectionIndicator", _indicator);
        }

        [TearDown]
        public void TearDown()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.RemoveService<IScreenManager>();
            ServiceLocator.RemoveService<IMetaProgressionService>();
            if (_screenGO != null) UnityEngine.Object.DestroyImmediate(_screenGO);
            if (_warriorHero != null) UnityEngine.Object.DestroyImmediate(_warriorHero);
            if (_par != null) UnityEngine.Object.DestroyImmediate(_par);
            if (_doblePar != null) UnityEngine.Object.DestroyImmediate(_doblePar);
            if (_sumaX != null) UnityEngine.Object.DestroyImmediate(_sumaX);
            if (_trio != null) UnityEngine.Object.DestroyImmediate(_trio);
            if (_escalera != null) UnityEngine.Object.DestroyImmediate(_escalera);
            if (_fullHouse != null) UnityEngine.Object.DestroyImmediate(_fullHouse);
            if (_poker != null) UnityEngine.Object.DestroyImmediate(_poker);
            if (_generala != null) UnityEngine.Object.DestroyImmediate(_generala);
        }

        [Test]
        public void ScreenStringId_Is_ClassSelectionScreen_Literal()
        {
            Assert.AreEqual("ClassSelectionScreen", _screen.ScreenStringId,
                "Debe matchear literal el string-id que MainMenuScreen pushea.");
        }

        [Test]
        public void OnPushed_AutoSelectsWarrior_AndLocksMagoPicaro()
        {
            InvokePushed(null);

            // Default del mock: el Guerrero arranca seleccionado.
            Assert.IsTrue(_confirmButton.interactable,
                "Confirm arranca habilitado — el Guerrero se auto-selecciona al pushear.");
            Assert.IsFalse(_magoButton.interactable, "Mago bloqueado en MVP.");
            Assert.IsFalse(_picaroButton.interactable, "Picaro bloqueado en MVP.");
            Assert.IsTrue(_warriorButton.interactable, "Guerrero esta disponible.");
            Assert.IsTrue(_indicator.activeSelf, "El indicador del Guerrero arranca prendido.");
        }

        [Test]
        public void WarriorClick_EnablesConfirm_AndPopulatesPanel()
        {
            InvokePushed(null);

            // Simular click del boton — el listener fue cableado en OnPushed.
            _warriorButton.onClick.Invoke();

            Assert.IsTrue(_confirmButton.interactable, "Confirm habilitado tras seleccionar al Guerrero.");
            Assert.IsTrue(_indicator.activeSelf, "El indicador de seleccion se prende.");
            // El ContractDisplayView debe haber instanciado 8 rows (una por combo).
            var rowsContainer = GetPrivate<Transform>(_contractDisplay, "_rowsContainer");
            Assert.AreEqual(8, rowsContainer.childCount,
                "ContractDisplayView.Bind debe instanciar una row por combo (8 Warrior).");
        }

        [Test]
        public void ConfirmClick_DoesNotFireOnRunStart_DefersToBuildSelectionScreen()
        {
            // Since UI#0013a, OnRunStart is fired by BuildSelectionScreen via
            // RunBootstrapper.StartRun — ClassSelectionScreen only navigates.
            InvokePushed(null);
            _warriorButton.onClick.Invoke();

            int receivedCount = 0;
            EventManager.EventReceiver handler = args => { receivedCount++; };
            EventManager.Subscribe(EventName.OnRunStart, handler);

            try
            {
                // No IScreenManager registered — confirm logs warning but does not fire event.
                _confirmButton.onClick.Invoke();

                Assert.AreEqual(0, receivedCount,
                    "OnRunStart must NOT fire from ClassSelectionScreen — it is now " +
                    "BuildSelectionScreen's responsibility via RunBootstrapper.");
            }
            finally
            {
                EventManager.UnSubscribe(EventName.OnRunStart, handler);
            }
        }

        [Test]
        public void BackClick_CallsPopCurrent_OnScreenManager()
        {
            // Arrange
            var spy = new SpyScreenManager();
            ServiceLocator.AddService<IScreenManager>(spy);
            InvokePushed(null);

            // Act
            _backButton.onClick.Invoke();

            // Assert
            Assert.AreEqual(1, spy.PopCurrentCallCount,
                "Atrás debe popear la screen actual para volver al menú.");
        }

        [Test]
        public void BackClick_WithoutScreenManager_DoesNotThrow()
        {
            InvokePushed(null);

            Assert.DoesNotThrow(() => _backButton.onClick.Invoke(),
                "Sin IScreenManager registrado, Atrás loggea warning y no explota.");
        }

        [Test]
        public void OnPopped_RemovesBackListener()
        {
            // Arrange: push + pop deja el listener removido.
            InvokePushed(null);
            InvokePopped();

            var spy = new SpyScreenManager();
            ServiceLocator.AddService<IScreenManager>(spy);

            // Act: un click posterior al pop no debe invocar nada.
            _backButton.onClick.Invoke();

            // Assert
            Assert.AreEqual(0, spy.PopCurrentCallCount,
                "Tras OnPopped el listener de Atrás debe estar removido.");
        }

        [Test]
        public void OnPushed_UnlockableEntryWithoutHero_LocksButtonAndShowsLockIndicator()
        {
            // Arrange: Mago gestionado por _unlockableClasses, sin ClassHeroSO
            // (clase no implementada). Sin IMetaProgressionService el gate degrada
            // a "disponible", pero Hero==null debe mantenerlo bloqueado igual.
            var lockGO = new GameObject("LockIcon");
            lockGO.transform.SetParent(_screenGO.transform, false);
            lockGO.SetActive(false);
            var selectionGO = new GameObject("MagoUnderline");
            selectionGO.transform.SetParent(_screenGO.transform, false);

            AssignPrivate(_screen, "_unlockableClasses",
                new List<ClassSelectionScreen.SelectableClassEntry>
                {
                    new ClassSelectionScreen.SelectableClassEntry
                    {
                        Hero = null,
                        ClassId = "Mage",
                        Button = _magoButton,
                        SelectionIndicator = selectionGO,
                        LockIndicator = lockGO,
                    },
                });

            // Act
            InvokePushed(null);
            _magoButton.onClick.Invoke();

            // Assert
            Assert.IsFalse(_magoButton.interactable, "Sin ClassHeroSO la clase queda bloqueada.");
            Assert.IsTrue(lockGO.activeSelf, "El candado se muestra mientras está bloqueada.");
            Assert.IsFalse(selectionGO.activeSelf,
                "El click en un botón bloqueado no debe seleccionar la clase (sin listener).");
            Assert.IsTrue(_indicator.activeSelf, "El Guerrero sigue auto-seleccionado.");
        }

        [Test]
        public void OnPushed_LockedEntry_AddsTooltipTriggerWithFallbackText()
        {
            // Arrange
            AssignPrivate(_screen, "_unlockableClasses",
                new List<ClassSelectionScreen.SelectableClassEntry>
                {
                    new ClassSelectionScreen.SelectableClassEntry
                    {
                        Hero = null,
                        ClassId = "Mage",
                        Button = _magoButton,
                    },
                });

            // Act
            InvokePushed(null);

            // Assert: sin IMetaProgressionService el provider usa el texto genérico de la
            // tabla UI. Se resuelve por el mismo camino que la producción para no depender del
            // locale activo (sale de un PlayerPref y no siempre es español).
            var trigger = _magoButton.GetComponent<UITooltipTrigger>();
            Assert.IsNotNull(trigger, "El botón bloqueado debe tener UITooltipTrigger.");
            Assert.IsNotNull(trigger.TextProvider, "El trigger debe tener TextProvider cableado.");
            Assert.AreEqual(GenericLockedTooltip, trigger.TextProvider(),
                "Sin servicio meta el tooltip usa el fallback genérico.");
        }

        [Test]
        public void ResolveLockedTooltip_WithHeroClassDefinition_ReturnsDefinitionHint()
        {
            // Arrange
            // UnlockId sintético a propósito: "unlock.class.mage" SÍ existe en la tabla Content
            // (con el placeholder "Coming soon"/"Próximamente", que coincide con el tooltip
            // genérico), así que usarlo hacía indistinguible "usó el hint de la definición" de
            // "cayó al genérico". Con una key inexistente el hint degrada al HintText autorado.
            var def = ScriptableObject.CreateInstance<UnlockDefinitionSO>();
            def.UnlockId = "unlock.class.test_only_mage";
            def.Category = UnlockableCategory.HeroClass;
            def.TargetId = "Mage";
            def.HintText = "Hint de prueba del Mago";
            ServiceLocator.AddService<IMetaProgressionService>(new StubMetaService(def));

            try
            {
                // Act
                string tooltip = ClassSelectionScreen.ResolveLockedTooltip("Mage");

                // Assert: usa el hint DE ESA definición, no el genérico. El valor se resuelve
                // por el mismo camino que la producción (si la tabla no tiene la key, degrada al
                // HintText autorado) para que el test no dependa del locale activo.
                Assert.AreEqual(LocalizedContent.Hint(def.UnlockId, def.HintText), tooltip);
                Assert.AreNotEqual(GenericLockedTooltip, tooltip,
                    "Con definición registrada no debe caer al tooltip genérico.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(def);
            }
        }

        [Test]
        public void ResolveLockedTooltip_WithoutService_ReturnsUiFallback()
        {
            // Arrange: sin IMetaProgressionService registrado (TearDown lo garantiza).

            // Act
            string tooltip = ClassSelectionScreen.ResolveLockedTooltip("Mage");

            // Assert
            Assert.AreEqual(GenericLockedTooltip, tooltip,
                "Sin servicio meta se usa el fallback de la tabla UI.");
        }

        /// <summary>Texto genérico de clase bloqueada, resuelto igual que en producción
        /// (<c>ClassSelectionScreen.ResolveLockedTooltip</c>) para no atarse a un idioma.</summary>
        private static string GenericLockedTooltip =>
            LocalizedContent.Ui("class_select.locked_tooltip", "Próximamente");

        private sealed class StubMetaService : IMetaProgressionService
        {
            private readonly List<UnlockDefinitionSO> _definitions;

            public StubMetaService(params UnlockDefinitionSO[] definitions)
                => _definitions = new List<UnlockDefinitionSO>(definitions);

            public bool IsAvailable(UnlockableCategory category, string targetId) => false;
            public bool IsDefinitionCompleted(UnlockDefinitionSO definition) => false;
            public IReadOnlyList<UnlockDefinitionSO> Definitions => _definitions;
            public bool TryUnlock(UnlockDefinitionSO definition, bool duringRun) => false;
            public int ConsecutiveWins => 0;
            public IReadOnlyCollection<string> ClassesPlayed => Array.Empty<string>();
            public void RecordRunCompleted(bool won, string classId) { }
            public bool IsTutorialCompleted => true;
            public void MarkTutorialCompleted() { }
            public bool IsTutorialEnabled => true;
            public void SetTutorialEnabled(bool enabled) { }
            public void SaveNow() { }
            public void ResetProgression() { }
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
            // OnPushed es protected — usamos el forwarder explicito de IBaseScreen.
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
            Assert.IsNotNull(field, $"Field '{fieldName}' no encontrado en {target.GetType().Name}.");
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
            Assert.IsNotNull(field, $"Field '{fieldName}' no encontrado en {target.GetType().Name}.");
            return field.GetValue(target) as T;
        }
    }
}
