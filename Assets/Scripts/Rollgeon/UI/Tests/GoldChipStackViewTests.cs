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

            AssignPrivate(_view, "_stack", _stack);
            AssignPrivate(_view, "_label", _label);
            AssignPrivate(_view, "_settings", _settings);
            AssignPrivate(_view, "_tiltedChip", _tilted);

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

        // ---------------- helpers ----------------

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
