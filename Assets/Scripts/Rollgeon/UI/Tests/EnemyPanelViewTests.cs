using System;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.UI.HUD;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Verifica <see cref="EnemyPanelView"/>: cambio de target, filtrado por guid en
    /// <see cref="HealthChangedPayload"/>, ocultamiento al recibir <c>OnEntityDestroyed</c>
    /// del target. Plan §3.10.
    /// </summary>
    [TestFixture]
    public class EnemyPanelViewTests
    {
        private GameObject _go;
        private EnemyPanelView _view;
        private GameObject _panelRoot;
        private Slider _hpSlider;
        private Guid _playerGuid;

        [SetUp]
        public void Setup()
        {
            _playerGuid = Guid.NewGuid();

            _go = new GameObject("EnemyPanel");
            _view = _go.AddComponent<EnemyPanelView>();

            _panelRoot = new GameObject("PanelRoot");
            _panelRoot.transform.SetParent(_go.transform, false);

            var sliderGO = new GameObject("HpSlider");
            sliderGO.transform.SetParent(_panelRoot.transform, false);
            _hpSlider = sliderGO.AddComponent<Slider>();
            _hpSlider.minValue = 0f;
            _hpSlider.maxValue = 1f;

            AssignPrivate(_view, "_panelRoot", _panelRoot);
            AssignPrivate(_view, "_hpSlider", _hpSlider);
        }

        [TearDown]
        public void Teardown()
        {
            EventManager.ResetEventDictionary();
            TypedEvent<HealthChangedPayload>.Clear();
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
        }

        [Test]
        public void SetTarget_WithValidGuid_ShowsPanel()
        {
            _view.Bind(_playerGuid);
            _view.SetTarget(Guid.NewGuid());

            Assert.IsTrue(_panelRoot.activeSelf, "Panel debe mostrarse al setear target.");
        }

        [Test]
        public void SetTarget_GuidEmpty_HidesPanel()
        {
            _view.Bind(_playerGuid);
            _view.SetTarget(Guid.NewGuid());
            _view.SetTarget(Guid.Empty);

            Assert.IsFalse(_panelRoot.activeSelf, "Panel debe ocultarse con Guid.Empty.");
        }

        [Test]
        public void HealthChangedPayload_ForCurrentTarget_UpdatesSlider()
        {
            _view.Bind(_playerGuid);
            var enemyGuid = Guid.NewGuid();
            _view.SetTarget(enemyGuid);

            var payload = new HealthChangedPayload
            {
                EntityGuid = enemyGuid,
                Current = 30,
                Max = 60,
            };
            TypedEvent<HealthChangedPayload>.Raise(payload);

            Assert.AreEqual(0.5f, _hpSlider.value, 0.001f);
        }

        [Test]
        public void HealthChangedPayload_OtherEntity_IsIgnored()
        {
            _view.Bind(_playerGuid);
            var enemyGuid = Guid.NewGuid();
            _view.SetTarget(enemyGuid);
            _hpSlider.value = 0f;

            var payload = new HealthChangedPayload
            {
                EntityGuid = Guid.NewGuid(),
                Current = 100,
                Max = 100,
            };
            TypedEvent<HealthChangedPayload>.Raise(payload);

            Assert.AreEqual(0f, _hpSlider.value, 0.001f,
                "Eventos de otra entidad no deben mutar el slider.");
        }

        [Test]
        public void OnEntityDestroyed_ForTarget_HidesPanel()
        {
            _view.Bind(_playerGuid);
            var enemyGuid = Guid.NewGuid();
            _view.SetTarget(enemyGuid);
            Assert.IsTrue(_panelRoot.activeSelf);

            EventManager.Trigger(EventName.OnEntityDestroyed, enemyGuid, Guid.Empty);

            Assert.IsFalse(_panelRoot.activeSelf);
        }

        [Test]
        public void Unbind_StopsProcessingEvents()
        {
            _view.Bind(_playerGuid);
            var enemyGuid = Guid.NewGuid();
            _view.SetTarget(enemyGuid);

            _view.Unbind();
            _hpSlider.value = 0f;

            var payload = new HealthChangedPayload { EntityGuid = enemyGuid, Current = 10, Max = 10 };
            TypedEvent<HealthChangedPayload>.Raise(payload);

            Assert.AreEqual(0f, _hpSlider.value, 0.001f,
                "Tras Unbind, el HP slider no debe actualizarse.");
        }

        private static void AssignPrivate(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' no encontrado.");
            field.SetValue(target, value);
        }
    }
}
