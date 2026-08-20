using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Rolls;
using Rollgeon.UI.HUD;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Pila del Pool de Rolls: fetch inicial vía IRollPoolService, updates por
    /// OnPlayerRollsChanged, filtro por guid y ocultamiento fuera de combate.
    /// Rig inactivo → path instantáneo.
    /// </summary>
    [TestFixture]
    public class RollPoolChipStackViewTests
    {
        private sealed class FakeRollPoolService : IRollPoolService
        {
            public int Current = 5;
            public int Max = 15;
            public bool InCombat = true;

            public bool IsCombatActive => InCombat;
            public void InitializeForEntity(Guid entityId) { }
            public bool TrySpendRolls(Guid entityId, int count) => false;
            public int Drain(Guid entityId, int amount) => 0;
            public void AddRolls(Guid entityId, int amount) { }
            public int GetCurrent(Guid entityId) => Current;
            public int GetMax(Guid entityId) => Max;
            public int GetRollsPerTurn(Guid entityId) => 5;
            public void AddPerTurnGrantBonus(int amount) { }
            public void RestoreCurrent(Guid entityId, int value) { }
        }

        private GameObject _go;
        private RollPoolChipStackView _view;
        private ChipStackView _stack;
        private TextMeshProUGUI _label;
        private ChipStackSettingsSO _settings;
        private FakeRollPoolService _rolls;
        private Guid _playerGuid;

        [SetUp]
        public void Setup()
        {
            _playerGuid = Guid.NewGuid();
            _rolls = new FakeRollPoolService();
            ServiceLocator.AddService<IRollPoolService>(_rolls);

            _settings = ScriptableObject.CreateInstance<ChipStackSettingsSO>();

            // Con RectTransform: ChipStackView castea su transform a RectTransform.
            _go = new GameObject("RollPoolChips", typeof(RectTransform));
            _go.SetActive(false);
            _stack = _go.AddComponent<ChipStackView>();
            _view = _go.AddComponent<RollPoolChipStackView>();

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(_go.transform, false);
            _label = labelGo.AddComponent<TextMeshProUGUI>();

            AssignPrivate(_view, "_stack", _stack);
            AssignPrivate(_view, "_label", _label);
            AssignPrivate(_view, "_settings", _settings);
        }

        [TearDown]
        public void Teardown()
        {
            InvokeNonPublic(_view, "OnDisable");
            EventManager.ResetEventDictionary();
            ServiceLocator.RemoveService<IRollPoolService>();
            if (_settings != null) UnityEngine.Object.DestroyImmediate(_settings);
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
        }

        [Test]
        public void Bind_FetchesInitialStateFromService()
        {
            _rolls.Current = 5;
            _rolls.Max = 15;

            _view.Bind(_playerGuid);

            Assert.AreEqual(5, _stack.DisplayedCount);
            Assert.AreEqual("5/15", _label.text);
        }

        [Test]
        public void RollsEvent_UpdatesChipsAndLabel()
        {
            _view.Bind(_playerGuid);

            EventManager.Trigger(EventName.OnPlayerRollsChanged, _playerGuid, 1, 15);

            Assert.AreEqual(1, _stack.DisplayedCount);
            Assert.AreEqual("1/15", _label.text);
        }

        [Test]
        public void RollsEvent_OtherGuid_Ignored()
        {
            _view.Bind(_playerGuid);

            EventManager.Trigger(EventName.OnPlayerRollsChanged, Guid.NewGuid(), 0, 15);

            Assert.AreEqual(5, _stack.DisplayedCount);
            Assert.AreEqual("5/15", _label.text);
        }

        [Test]
        public void OutsideCombat_HidesStackAndLabel()
        {
            // El pool es combat-only: en exploración la pila y el número se ocultan.
            _rolls.InCombat = false;

            _view.Bind(_playerGuid);

            Assert.IsFalse(_stack.gameObject.activeSelf,
                "Fuera de combate la pila de rolls debe ocultarse.");
            Assert.IsFalse(_label.gameObject.activeSelf,
                "Fuera de combate el número del pool debe ocultarse.");
        }

        [Test]
        public void ReenteringCombat_ShowsStackAgain()
        {
            _rolls.InCombat = false;
            _view.Bind(_playerGuid);

            _rolls.InCombat = true;
            EventManager.Trigger(EventName.OnPlayerRollsChanged, _playerGuid, 5, 15);

            Assert.IsTrue(_stack.gameObject.activeSelf);
            Assert.AreEqual(5, _stack.DisplayedCount);
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
