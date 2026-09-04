using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Economy;
using Rollgeon.UI.HUD;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Pila de oro: fichas planas hasta 4, ficha inclinada desde 5, régimen
    /// shake-only con ambos valores ≥5 y label con el valor real. La vista es
    /// autónoma (OnGoldChanged sin guid) — el rig invoca OnEnable por reflexión.
    /// </summary>
    [TestFixture]
    public class GoldChipStackViewTests
    {
        private sealed class FakeEconomyService : IEconomyService
        {
            public int CurrentGold { get; set; }
            public void Add(int amount) { }
            public bool Spend(int amount) => false;
            public bool CanAfford(int amount) => false;
            public void ResetTo(int amount) { }
        }

        private GameObject _go;
        private GoldChipStackView _view;
        private ChipStackView _stack;
        private TextMeshProUGUI _label;
        private Image _tilted;
        private Image _debt;
        private ChipStackSettingsSO _settings;
        private FakeEconomyService _economy;

        [SetUp]
        public void Setup()
        {
            _economy = new FakeEconomyService { CurrentGold = 0 };
            ServiceLocator.AddService<IEconomyService>(_economy);

            _settings = ScriptableObject.CreateInstance<ChipStackSettingsSO>();

            // Con RectTransform: ChipStackView castea su transform a RectTransform.
            _go = new GameObject("GoldChips", typeof(RectTransform));
            _go.SetActive(false);
            _stack = _go.AddComponent<ChipStackView>();
            _view = _go.AddComponent<GoldChipStackView>();

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(_go.transform, false);
            _label = labelGo.AddComponent<TextMeshProUGUI>();

            var tiltedGo = new GameObject("TiltedChip");
            tiltedGo.transform.SetParent(_go.transform, false);
            _tilted = tiltedGo.AddComponent<Image>();
            tiltedGo.SetActive(false);

            var debtGo = new GameObject("DebtChip", typeof(RectTransform));
            debtGo.transform.SetParent(_go.transform, false);
            _debt = debtGo.AddComponent<Image>();
            debtGo.SetActive(false);

            AssignPrivate(_view, "_stack", _stack);
            AssignPrivate(_view, "_label", _label);
            AssignPrivate(_view, "_settings", _settings);
            AssignPrivate(_view, "_tiltedChip", _tilted);
            AssignPrivate(_view, "_debtChip", _debt);

            // El GO nunca se activa en el fixture — OnEnable se invoca directo
            // (mismo patrón que ExplorationHUDViewTests con OnDisable).
            InvokeNonPublic(_view, "OnEnable");
        }

        [TearDown]
        public void Teardown()
        {
            InvokeNonPublic(_view, "OnDisable");
            EventManager.ResetEventDictionary();
            ServiceLocator.RemoveService<IEconomyService>();
            if (_settings != null) UnityEngine.Object.DestroyImmediate(_settings);
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
        }

        [Test]
        public void GoldBelowFive_ShowsOneChipPerGold_NoTilted()
        {
            EventManager.Trigger(EventName.OnGoldChanged, 3, 3);

            Assert.AreEqual(3, _stack.DisplayedCount);
            Assert.IsFalse(_tilted.gameObject.activeSelf);
            Assert.AreEqual("3", _label.text);
        }

        [Test]
        public void GoldReachesFive_FourFlatChipsPlusTilted()
        {
            EventManager.Trigger(EventName.OnGoldChanged, 3, 3);
            EventManager.Trigger(EventName.OnGoldChanged, 5, 2);

            Assert.AreEqual(4, _stack.DisplayedCount);
            Assert.IsTrue(_tilted.gameObject.activeSelf);
            Assert.AreEqual("5", _label.text);
        }

        [Test]
        public void GoldAboveFive_ChipsUnchanged_LabelTracksValue()
        {
            EventManager.Trigger(EventName.OnGoldChanged, 5, 5);
            EventManager.Trigger(EventName.OnGoldChanged, 12, 7);

            Assert.AreEqual(4, _stack.DisplayedCount, "≥5 no agrega fichas — solo shake + número.");
            Assert.IsTrue(_tilted.gameObject.activeSelf);
            Assert.AreEqual("12", _label.text);
        }

        [Test]
        public void GoldDropsBelowFive_TiltedHiddenAndChipsShrink()
        {
            EventManager.Trigger(EventName.OnGoldChanged, 12, 12);
            EventManager.Trigger(EventName.OnGoldChanged, 2, -10);

            Assert.AreEqual(2, _stack.DisplayedCount);
            Assert.IsFalse(_tilted.gameObject.activeSelf);
            Assert.AreEqual("2", _label.text);
        }

        [Test]
        public void InitialFetch_UsesEconomyService()
        {
            // Re-crear el enable con oro preexistente en el servicio.
            InvokeNonPublic(_view, "OnDisable");
            _economy.CurrentGold = 7;
            InvokeNonPublic(_view, "OnEnable");

            Assert.AreEqual(4, _stack.DisplayedCount);
            Assert.IsTrue(_tilted.gameObject.activeSelf);
            Assert.AreEqual("7", _label.text);
        }

        // ---------------- deuda (Tarjeta de Crédito) ----------------

        [Test]
        public void test_gold_hud_negative_gold_shows_debt_chip_and_tints_label()
        {
            // Arrange
            var restColor = _label.color;
            var debtTint = (Color)GetPrivate(_view, "_debtTint");

            // Act
            EventManager.Trigger(EventName.OnGoldChanged, -12, -12);

            // Assert
            Assert.AreEqual(0, _stack.DisplayedCount, "No hay fichas negativas.");
            Assert.IsFalse(_tilted.gameObject.activeSelf);
            Assert.IsTrue(_debt.gameObject.activeSelf, "La ficha de deuda marca el oro negativo.");
            Assert.AreEqual(debtTint, _debt.color);
            Assert.AreEqual(debtTint, _label.color);
            Assert.AreNotEqual(restColor, _label.color);
            Assert.AreEqual("-12", _label.text);
        }

        [Test]
        public void test_gold_hud_back_to_non_negative_hides_debt_chip_and_restores_label_color()
        {
            // Arrange
            var restColor = _label.color;
            EventManager.Trigger(EventName.OnGoldChanged, -12, -12);

            // Act
            EventManager.Trigger(EventName.OnGoldChanged, 0, 12);

            // Assert
            Assert.IsFalse(_debt.gameObject.activeSelf);
            Assert.AreEqual(restColor, _label.color);
            Assert.AreEqual("0", _label.text);
        }

        [Test]
        public void test_gold_hud_non_negative_gold_never_shows_debt_chip()
        {
            // Act
            EventManager.Trigger(EventName.OnGoldChanged, 3, 3);
            EventManager.Trigger(EventName.OnGoldChanged, 9, 6);

            // Assert
            Assert.IsFalse(_debt.gameObject.activeSelf);
        }

        // ---------------- helpers ----------------

        private static object GetPrivate(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' no encontrado en {target.GetType().Name}.");
            return field.GetValue(target);
        }

        private static void AssignPrivate(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' no encontrado en {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static void InvokeNonPublic(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Method '{methodName}' no encontrado en {target.GetType().Name}.");
            method.Invoke(target, null);
        }
    }
}
