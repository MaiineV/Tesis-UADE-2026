using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Heroes;
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
#pragma warning disable 67
        private sealed class FakePlayerService : IPlayerService
        {
            public Guid PlayerGuid { get; set; }
            public Guid RunId { get; set; }
            public ClassHeroSO CurrentHero { get; set; }
            public Rollgeon.Dice.DiceBagSO DiceBag { get; set; }
            public void SetPlayer(ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(Rollgeon.Dice.DiceBagSO bag) { DiceBag = bag; }
            public void ClearPlayer() { }
            public event Action<ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;
        }
#pragma warning restore 67

        private GameObject _hudGO;
        private ExplorationHUDView _hud;
        private HealthChipStackView _hp;
        private ChipStackView _hpStack;
        private TMPro.TextMeshProUGUI _hpLabel;
        private ChipStackSettingsSO _chipSettings;
        private ActiveItemsView _items;
        private MinimapView _minimap;
        private RoomNavigationView _roomNavigation;
        private FakePlayerService _playerService;
        private AttributesManager _attrManager;
        private ClassHeroSO _hero;
        private Guid _playerGuid;

        [SetUp]
        public void Setup()
        {
            _playerGuid = Guid.NewGuid();

            _hero = ScriptableObject.CreateInstance<ClassHeroSO>();
            _hero.BaseMaxHp = 100;

            _playerService = new FakePlayerService
            {
                PlayerGuid = _playerGuid,
                CurrentHero = _hero
            };
            ServiceLocator.AddService<IPlayerService>(_playerService);

            _attrManager = new AttributesManager();
            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(100));
            _attrManager.Register(_playerGuid, attrs);
            ServiceLocator.AddService<AttributesManager>(_attrManager);

            _hudGO = new GameObject("ExplorationHUDView");
            _hudGO.SetActive(false);
            _hud = _hudGO.AddComponent<ExplorationHUDView>();

            _chipSettings = ScriptableObject.CreateInstance<ChipStackSettingsSO>();

            _hp = AttachChipStackView<HealthChipStackView>("HealthChips", _hudGO,
                out _hpStack, out _hpLabel);

            _items = AttachChild<ActiveItemsView>("ActiveItems", _hudGO);

            _minimap = AttachChild<MinimapView>("Minimap", _hudGO);

            _roomNavigation = AttachChild<RoomNavigationView>("RoomNavigation", _hudGO);

            AssignPrivate(_hud, "_healthChips", _hp);
            AssignPrivate(_hud, "_activeItems", _items);
            AssignPrivate(_hud, "_minimap", _minimap);
            AssignPrivate(_hud, "_roomNavigation", _roomNavigation);
        }

        [TearDown]
        public void Teardown()
        {
            EventManager.ResetEventDictionary();
            TypedEvent<DamageResolvedPayload>.Clear();
            TypedEvent<HealResolvedPayload>.Clear();
            ServiceLocator.RemoveService<IPlayerService>();
            ServiceLocator.RemoveService<AttributesManager>();
            if (_attrManager != null) { _attrManager.Dispose(); _attrManager = null; }
            if (_hero != null) UnityEngine.Object.DestroyImmediate(_hero);
            if (_chipSettings != null) UnityEngine.Object.DestroyImmediate(_chipSettings);
            if (_hudGO != null) UnityEngine.Object.DestroyImmediate(_hudGO);
        }

        [Test]
        public void BindAll_SubscribesHealthChips_DamageUpdatesLabel()
        {
            _hud.BindAll(_playerGuid);

            _attrManager.SetAttributeValue<Health, int>(_playerGuid, 50);
            TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                SourceGuid = Guid.NewGuid(),
                TargetGuid = _playerGuid,
                FinalDamage = 50,
                WeaknessHit = false
            });

            Assert.AreEqual("50/100", _hpLabel.text);
        }

        [Test]
        public void HealthChips_FiltersByGuid_IgnoresOtherEntities()
        {
            _hud.BindAll(_playerGuid);

            var otherGuid = Guid.NewGuid();
            TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                SourceGuid = Guid.NewGuid(),
                TargetGuid = otherGuid,
                FinalDamage = 50,
                WeaknessHit = false
            });

            Assert.AreEqual("100/100", _hpLabel.text,
                "La pila de vida debe filtrar por playerGuid — un evento de otra entidad no debe mutar la UI.");
        }

        [Test]
        public void UnbindAll_ThenDisable_StopsReceivingEvents()
        {
            _hud.BindAll(_playerGuid);

            _attrManager.SetAttributeValue<Health, int>(_playerGuid, 50);
            TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                SourceGuid = Guid.NewGuid(),
                TargetGuid = _playerGuid,
                FinalDamage = 50,
                WeaknessHit = false
            });
            Assert.AreEqual("50/100", _hpLabel.text);

            // HealthChipStackView.Unbind es no-op deliberado: su ciclo de vida lo
            // controla OnEnable/OnDisable (sin eso, la pila de exploration quedaba
            // stale al reactivarse post-combate). El teardown real es la
            // desactivacion del GameObject; como este fixture nunca activa la
            // jerarquia, OnDisable no dispara solo y lo invocamos directo.
            _hud.UnbindAll();
            InvokeNonPublic(_hp, "OnDisable");

            _attrManager.SetAttributeValue<Health, int>(_playerGuid, 100);
            TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                SourceGuid = Guid.NewGuid(),
                TargetGuid = _playerGuid,
                FinalDamage = 0,
                WeaknessHit = false
            });

            Assert.AreEqual("50/100", _hpLabel.text,
                "Despues del teardown (UnbindAll + OnDisable), nuevos eventos no deben mutar la UI.");
        }

        [Test]
        public void BindAll_IsIdempotent_NoDoubleSubscription()
        {
            _hud.BindAll(_playerGuid);
            _hud.BindAll(_playerGuid);

            _attrManager.SetAttributeValue<Health, int>(_playerGuid, 50);
            TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                SourceGuid = Guid.NewGuid(),
                TargetGuid = _playerGuid,
                FinalDamage = 50,
                WeaknessHit = false
            });

            // El teardown de la pila es OnDisable (Unbind es no-op); un solo
            // OnDisable debe bastar — si el doble BindAll hubiera duplicado la
            // suscripcion, el handler extra quedaria vivo y el evento mutaria la UI.
            _hud.UnbindAll();
            InvokeNonPublic(_hp, "OnDisable");
            string afterTeardown = _hpLabel.text;

            _attrManager.SetAttributeValue<Health, int>(_playerGuid, 100);
            TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                SourceGuid = Guid.NewGuid(),
                TargetGuid = _playerGuid,
                FinalDamage = 0,
                WeaknessHit = false
            });
            Assert.AreEqual(afterTeardown, _hpLabel.text,
                "Un solo teardown debe bastar — si BindAll hubiera duplicado subs, uno quedaria vivo.");
        }

        [Test]
        public void BindAll_BindsRoomNavigation()
        {
            _hud.BindAll(_playerGuid);

            var bound = GetPrivateValue<bool>(_roomNavigation, "_bound");
            Assert.IsTrue(bound, "RoomNavigationView must be bound after BindAll.");
        }

        [Test]
        public void UnbindAll_UnbindsRoomNavigation()
        {
            _hud.BindAll(_playerGuid);
            _hud.UnbindAll();

            var bound = GetPrivateValue<bool>(_roomNavigation, "_bound");
            Assert.IsFalse(bound, "RoomNavigationView must be unbound after UnbindAll.");
        }

        // ---------------- helpers ----------------

        private static T AttachChild<T>(string name, GameObject parent) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go.AddComponent<T>();
        }

        private T AttachChipStackView<T>(string name, GameObject parent,
            out ChipStackView stack, out TMPro.TextMeshProUGUI label) where T : Component
        {
            // Con RectTransform: ChipStackView castea su transform a RectTransform.
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            stack = go.AddComponent<ChipStackView>();
            var view = go.AddComponent<T>();

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            label = labelGo.AddComponent<TMPro.TextMeshProUGUI>();

            AssignPrivate(view, "_stack", stack);
            AssignPrivate(view, "_label", label);
            AssignPrivate(view, "_settings", _chipSettings);
            return view;
        }

        private static void AssignPrivate(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' no encontrado en {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static void InvokeNonPublic(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Method '{methodName}' no encontrado en {target.GetType().Name}.");
            method.Invoke(target, null);
        }

        private static T GetPrivate<T>(object target, string fieldName) where T : class
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' no encontrado en {target.GetType().Name}.");
            return field.GetValue(target) as T;
        }

        private static T GetPrivateValue<T>(object target, string fieldName) where T : struct
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' no encontrado en {target.GetType().Name}.");
            return (T)field.GetValue(target);
        }
    }
}
