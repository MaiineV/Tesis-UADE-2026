using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combos;
using Rollgeon.Combos.Concretes;
using Rollgeon.Combos.Tests;
using Rollgeon.Heroes;
using Rollgeon.UI.HUD;
using Rollgeon.UI.Screens;
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
        private GameObject _screenGO;
        private ClassSelectionScreen _screen;
        private Button _warriorButton;
        private Button _magoButton;
        private Button _picaroButton;
        private Button _confirmButton;
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
            _sumaX = ComboTestUtils.CreateCombo<Combo_SumaX>(ComboId.SumX, 25);
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
            AssignPrivate(_screen, "_contractDisplay", _contractDisplay);
            AssignPrivate(_screen, "_portraitDisplay", _portrait);
            // _passiveDisplay se deja null — el screen tiene null-check (TMP requiere TMP_Settings).
            AssignPrivate(_screen, "_warriorSelectionIndicator", _indicator);
        }

        [TearDown]
        public void TearDown()
        {
            EventManager.ResetEventDictionary();
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
        public void OnPushed_DisablesConfirmButton_AndLocksMagoPicaro()
        {
            InvokePushed(null);

            Assert.IsFalse(_confirmButton.interactable,
                "Confirm arranca deshabilitado hasta que el usuario seleccione al Guerrero.");
            Assert.IsFalse(_magoButton.interactable, "Mago bloqueado en MVP.");
            Assert.IsFalse(_picaroButton.interactable, "Picaro bloqueado en MVP.");
            Assert.IsTrue(_warriorButton.interactable, "Guerrero esta disponible.");
            Assert.IsFalse(_indicator.activeSelf, "El indicador arranca apagado — aun no hay seleccion.");
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
        public void ConfirmClick_FiresOnRunStart_WithRulesetId()
        {
            InvokePushed(null);
            _warriorButton.onClick.Invoke();

            Guid receivedRunId = Guid.Empty;
            string receivedRulesetId = null;
            int receivedCount = 0;
            EventManager.EventReceiver handler = args =>
            {
                receivedCount++;
                Assert.IsNotNull(args, "args no puede ser null.");
                Assert.AreEqual(2, args.Length, "Schema OnRunStart: [Guid runId, string rulesetId].");
                Assert.IsInstanceOf<Guid>(args[0], "args[0] debe ser Guid runId.");
                Assert.IsInstanceOf<string>(args[1], "args[1] debe ser string rulesetId.");
                receivedRunId = (Guid)args[0];
                receivedRulesetId = (string)args[1];
            };
            EventManager.Subscribe(EventName.OnRunStart, handler);

            try
            {
                _confirmButton.onClick.Invoke();

                Assert.AreEqual(1, receivedCount, "OnRunStart debe dispararse exactamente una vez.");
                Assert.AreNotEqual(Guid.Empty, receivedRunId, "runId debe ser un Guid no-vacio.");
                Assert.AreEqual("default", receivedRulesetId,
                    "rulesetId default del screen es 'default' (plan §4.1).");
            }
            finally
            {
                EventManager.UnSubscribe(EventName.OnRunStart, handler);
            }
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
