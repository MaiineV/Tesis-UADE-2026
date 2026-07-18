using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.EnergyLib;
using Rollgeon.UI.HUD;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Pila de energía: fetch inicial vía IEnergyService, updates por
    /// OnPlayerEnergyChanged y filtro por guid. Rig inactivo → path instantáneo.
    /// </summary>
    [TestFixture]
    public class EnergyChipStackViewTests
    {
        private sealed class FakeEnergyService : IEnergyService
        {
            public int Current = 4;
            public int Max = 4;
            public void InitializeForEntity(Guid entityId) { }
            public bool SpendEnergy(Guid entityId, int cost) => false;
            public void RegenerateAtTurnEnd(Guid entityId) { }
            public int GetCurrent(Guid entityId) => Current;
            public int GetMax(Guid entityId) => Max;
        }

        private GameObject _go;
        private EnergyChipStackView _view;
        private ChipStackView _stack;
        private TextMeshProUGUI _label;
        private ChipStackSettingsSO _settings;
        private FakeEnergyService _energy;
        private Guid _playerGuid;

        [SetUp]
        public void Setup()
        {
            _playerGuid = Guid.NewGuid();
            _energy = new FakeEnergyService();
            ServiceLocator.AddService<IEnergyService>(_energy);

            _settings = ScriptableObject.CreateInstance<ChipStackSettingsSO>();

            // Con RectTransform: ChipStackView castea su transform a RectTransform.
            _go = new GameObject("EnergyChips", typeof(RectTransform));
            _go.SetActive(false);
            _stack = _go.AddComponent<ChipStackView>();
            _view = _go.AddComponent<EnergyChipStackView>();

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
            ServiceLocator.RemoveService<IEnergyService>();
            if (_settings != null) UnityEngine.Object.DestroyImmediate(_settings);
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
        }

        [Test]
        public void Bind_FetchesInitialStateFromService()
        {
            _energy.Current = 4;
            _energy.Max = 4;

            _view.Bind(_playerGuid);

            Assert.AreEqual(4, _stack.DisplayedCount);
            Assert.AreEqual("4/4", _label.text);
        }

        [Test]
        public void EnergyEvent_UpdatesChipsAndLabel()
        {
            _view.Bind(_playerGuid);

            EventManager.Trigger(EventName.OnPlayerEnergyChanged, _playerGuid, 1, 4);

            Assert.AreEqual(1, _stack.DisplayedCount);
            Assert.AreEqual("1/4", _label.text);
        }

        [Test]
        public void EnergyEvent_OtherGuid_Ignored()
        {
            _view.Bind(_playerGuid);

            EventManager.Trigger(EventName.OnPlayerEnergyChanged, Guid.NewGuid(), 0, 4);

            Assert.AreEqual(4, _stack.DisplayedCount);
            Assert.AreEqual("4/4", _label.text);
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
