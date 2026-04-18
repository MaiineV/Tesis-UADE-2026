using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Player;
using Rollgeon.UI.HUD;
using Rollgeon.UI.Screens;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Smoke test del <see cref="ExplorationHUDView"/>: verifica que tras
    /// <c>BindAll(guid)</c> las sub-views reaccionan a los eventos del bus
    /// (§17.D.4 "regla de oro"). EditMode puro — sin escenas, sin prefabs.
    /// Plan §3 (tests opcionales, smoke test).
    /// </summary>
    [TestFixture]
    public class ExplorationHUDViewTests
    {
        // Fake service para que ExplorationHUDView pueda resolver un playerGuid sin
        // depender de una implementacion real de IPlayerService.
        private sealed class FakePlayerService : IPlayerService
        {
            public Guid PlayerGuid { get; set; }
        }

        private GameObject _hudGO;
        private ExplorationHUDView _hud;
        private HealthBarView _hp;
        private EnergyBarView _energy;
        private GoldCounterView _gold;
        private ActiveItemsView _items;
        private MinimapView _minimap;
        private FakePlayerService _playerService;
        private Guid _playerGuid;

        [SetUp]
        public void Setup()
        {
            _playerGuid = Guid.NewGuid();
            _playerService = new FakePlayerService { PlayerGuid = _playerGuid };
            ServiceLocator.AddService<IPlayerService>(_playerService);

            // Construir la jerarquia a mano — no hay prefab. Root con ExplorationHUDView
            // + 5 hijos, uno por sub-view. Los widgets graficos se crean en memoria
            // (Slider/Image); para TMP evitamos TextMeshProUGUI (requiere TMP_Settings
            // en ProjectSettings) — las sub-views tienen null-checks en _text.
            _hudGO = new GameObject("ExplorationHUDView");
            _hudGO.SetActive(false);
            _hud = _hudGO.AddComponent<ExplorationHUDView>();

            _hp = AttachChild<HealthBarView>("HealthBar", _hudGO);
            AttachSlider(_hp, "_slider");

            _energy = AttachChild<EnergyBarView>("EnergyBar", _hudGO);
            AttachSlider(_energy, "_slider");

            _gold = AttachChild<GoldCounterView>("Gold", _hudGO);

            _items = AttachChild<ActiveItemsView>("ActiveItems", _hudGO);

            _minimap = AttachChild<MinimapView>("Minimap", _hudGO);

            AssignPrivate(_hud, "_healthBar", _hp);
            AssignPrivate(_hud, "_energyBar", _energy);
            AssignPrivate(_hud, "_goldCounter", _gold);
            AssignPrivate(_hud, "_activeItems", _items);
            AssignPrivate(_hud, "_minimap", _minimap);
        }

        [TearDown]
        public void Teardown()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.RemoveService<IPlayerService>();
            if (_hudGO != null) UnityEngine.Object.DestroyImmediate(_hudGO);
        }

        [Test]
        public void BindAll_SubscribesHealthBar_EventUpdatesSlider()
        {
            _hud.BindAll(_playerGuid);

            var slider = GetPrivate<Slider>(_hp, "_slider");
            Assert.IsNotNull(slider);

            EventManager.Trigger(EventName.OnPlayerHealthChanged, _playerGuid, 50, 100);

            Assert.AreEqual(0.5f, slider.value, 0.001f);
        }

        [Test]
        public void BindAll_SubscribesEnergyBar_EventUpdatesSlider()
        {
            _hud.BindAll(_playerGuid);

            var slider = GetPrivate<Slider>(_energy, "_slider");
            EventManager.Trigger(EventName.OnPlayerEnergyChanged, _playerGuid, 3, 4);

            Assert.AreEqual(0.75f, slider.value, 0.001f);
        }

        [Test]
        public void HealthBar_FiltersByGuid_IgnoresOtherEntities()
        {
            _hud.BindAll(_playerGuid);
            var slider = GetPrivate<Slider>(_hp, "_slider");
            slider.value = 0f;

            var otherGuid = Guid.NewGuid();
            EventManager.Trigger(EventName.OnPlayerHealthChanged, otherGuid, 50, 100);

            Assert.AreEqual(0f, slider.value, 0.001f,
                "HealthBar debe filtrar por playerGuid — un evento de otra entidad no debe mutar la UI.");
        }

        [Test]
        public void UnbindAll_StopsReceivingEvents()
        {
            _hud.BindAll(_playerGuid);
            var slider = GetPrivate<Slider>(_hp, "_slider");
            EventManager.Trigger(EventName.OnPlayerHealthChanged, _playerGuid, 50, 100);
            Assert.AreEqual(0.5f, slider.value, 0.001f);

            _hud.UnbindAll();
            EventManager.Trigger(EventName.OnPlayerHealthChanged, _playerGuid, 100, 100);

            Assert.AreEqual(0.5f, slider.value, 0.001f,
                "Despues de UnbindAll, nuevos eventos no deben mutar la UI.");
        }

        [Test]
        public void BindAll_IsIdempotent_NoDoubleSubscription()
        {
            _hud.BindAll(_playerGuid);
            _hud.BindAll(_playerGuid); // re-bind — no debe duplicar handlers

            var slider = GetPrivate<Slider>(_hp, "_slider");
            EventManager.Trigger(EventName.OnPlayerHealthChanged, _playerGuid, 50, 100);

            // Si hubiera doble subscripcion, SetValue corre 2x — pero el resultado es idempotente.
            // Cambio: tras un Unbind manual, el valor debe volver a no actualizarse.
            _hud.UnbindAll();
            slider.value = 0f;
            EventManager.Trigger(EventName.OnPlayerHealthChanged, _playerGuid, 100, 100);
            Assert.AreEqual(0f, slider.value, 0.001f,
                "Un solo UnbindAll debe bastar — si BindAll hubiera duplicado subs, uno quedaria vivo.");
        }

        // ---------------- helpers ----------------

        private static T AttachChild<T>(string name, GameObject parent) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go.AddComponent<T>();
        }

        private static void AttachSlider(Component host, string fieldName)
        {
            var sliderGO = new GameObject(fieldName + "_slider");
            sliderGO.transform.SetParent(host.transform, false);
            var slider = sliderGO.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            AssignPrivate(host, fieldName, slider);
        }

        private static void AssignPrivate(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' no encontrado en {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static T GetPrivate<T>(object target, string fieldName) where T : class
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' no encontrado en {target.GetType().Name}.");
            return field.GetValue(target) as T;
        }
    }
}
