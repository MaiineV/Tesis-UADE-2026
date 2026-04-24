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
        private HealthBarView _hp;
        private EnergyBarView _energy;
        private GoldCounterView _gold;
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

            _hp = AttachChild<HealthBarView>("HealthBar", _hudGO);
            AttachFillImage(_hp, "_fillImage");

            _energy = AttachChild<EnergyBarView>("EnergyBar", _hudGO);
            AttachFillImage(_energy, "_fillImage");

            _gold = AttachChild<GoldCounterView>("Gold", _hudGO);

            _items = AttachChild<ActiveItemsView>("ActiveItems", _hudGO);

            _minimap = AttachChild<MinimapView>("Minimap", _hudGO);

            _roomNavigation = AttachChild<RoomNavigationView>("RoomNavigation", _hudGO);

            AssignPrivate(_hud, "_healthBar", _hp);
            AssignPrivate(_hud, "_energyBar", _energy);
            AssignPrivate(_hud, "_goldCounter", _gold);
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
            if (_hudGO != null) UnityEngine.Object.DestroyImmediate(_hudGO);
        }

        [Test]
        public void BindAll_SubscribesHealthBar_DamageUpdatesFill()
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

            var fill = GetPrivate<Image>(_hp, "_fillImage");
            Assert.IsNotNull(fill);
            Assert.AreEqual(0.5f, fill.fillAmount, 0.001f);
        }

        [Test]
        public void BindAll_SubscribesEnergyBar_EventUpdatesFill()
        {
            _hud.BindAll(_playerGuid);

            var fill = GetPrivate<Image>(_energy, "_fillImage");
            EventManager.Trigger(EventName.OnPlayerEnergyChanged, _playerGuid, 3, 4);

            Assert.AreEqual(0.75f, fill.fillAmount, 0.001f);
        }

        [Test]
        public void HealthBar_FiltersByGuid_IgnoresOtherEntities()
        {
            _hud.BindAll(_playerGuid);
            var fill = GetPrivate<Image>(_hp, "_fillImage");

            var otherGuid = Guid.NewGuid();
            TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                SourceGuid = Guid.NewGuid(),
                TargetGuid = otherGuid,
                FinalDamage = 50,
                WeaknessHit = false
            });

            Assert.AreEqual(1f, fill.fillAmount, 0.001f,
                "HealthBar debe filtrar por playerGuid — un evento de otra entidad no debe mutar la UI.");
        }

        [Test]
        public void UnbindAll_StopsReceivingEvents()
        {
            _hud.BindAll(_playerGuid);
            var fill = GetPrivate<Image>(_hp, "_fillImage");

            _attrManager.SetAttributeValue<Health, int>(_playerGuid, 50);
            TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                SourceGuid = Guid.NewGuid(),
                TargetGuid = _playerGuid,
                FinalDamage = 50,
                WeaknessHit = false
            });
            Assert.AreEqual(0.5f, fill.fillAmount, 0.001f);

            _hud.UnbindAll();

            _attrManager.SetAttributeValue<Health, int>(_playerGuid, 100);
            TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                SourceGuid = Guid.NewGuid(),
                TargetGuid = _playerGuid,
                FinalDamage = 0,
                WeaknessHit = false
            });

            Assert.AreEqual(0.5f, fill.fillAmount, 0.001f,
                "Despues de UnbindAll, nuevos eventos no deben mutar la UI.");
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

            _hud.UnbindAll();
            var fill = GetPrivate<Image>(_hp, "_fillImage");
            float afterUnbind = fill.fillAmount;

            _attrManager.SetAttributeValue<Health, int>(_playerGuid, 100);
            TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                SourceGuid = Guid.NewGuid(),
                TargetGuid = _playerGuid,
                FinalDamage = 0,
                WeaknessHit = false
            });
            Assert.AreEqual(afterUnbind, fill.fillAmount, 0.001f,
                "Un solo UnbindAll debe bastar — si BindAll hubiera duplicado subs, uno quedaria vivo.");
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

        private static void AttachFillImage(Component host, string fieldName)
        {
            var go = new GameObject(fieldName + "_image");
            go.transform.SetParent(host.transform, false);
            var img = go.AddComponent<Image>();
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillAmount = 1f;
            AssignPrivate(host, fieldName, img);
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
