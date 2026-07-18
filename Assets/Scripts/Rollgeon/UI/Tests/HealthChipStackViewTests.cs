using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Heroes;
using Rollgeon.Player;
using Rollgeon.UI.HUD;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Pila de vida+escudo: composición de fichas, label rich-text con color de
    /// escudo, filtro por guid y bursts de absorción sin doble conteo. El rig
    /// queda inactivo → SetChips toma el path instantáneo (sin tweens).
    /// </summary>
    [TestFixture]
    public class HealthChipStackViewTests
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

        private GameObject _go;
        private HealthChipStackView _view;
        private ChipStackView _stack;
        private TextMeshProUGUI _label;
        private ChipStackSettingsSO _settings;
        private AttributesManager _attrs;
        private ClassHeroSO _hero;
        private FakePlayerService _playerService;
        private Guid _playerGuid;

        [SetUp]
        public void Setup()
        {
            _playerGuid = Guid.NewGuid();

            _hero = ScriptableObject.CreateInstance<ClassHeroSO>();
            _hero.BaseMaxHp = 10;
            _playerService = new FakePlayerService { PlayerGuid = _playerGuid, CurrentHero = _hero };
            ServiceLocator.AddService<IPlayerService>(_playerService);

            _attrs = new AttributesManager();
            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(10));
            attrs.SetAttribute<Shield>(new Shield(0));
            _attrs.Register(_playerGuid, attrs);
            ServiceLocator.AddService<AttributesManager>(_attrs);

            _settings = ScriptableObject.CreateInstance<ChipStackSettingsSO>();

            // Con RectTransform: ChipStackView castea su transform a RectTransform
            // (en runtime siempre vive en un canvas).
            _go = new GameObject("HealthChips", typeof(RectTransform));
            _go.SetActive(false);
            _stack = _go.AddComponent<ChipStackView>();
            _view = _go.AddComponent<HealthChipStackView>();

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
            TypedEvent<DamageResolvedPayload>.Clear();
            TypedEvent<HealResolvedPayload>.Clear();
            ServiceLocator.RemoveService<IPlayerService>();
            ServiceLocator.RemoveService<AttributesManager>();
            if (_attrs != null) { _attrs.Dispose(); _attrs = null; }
            if (_hero != null) UnityEngine.Object.DestroyImmediate(_hero);
            if (_settings != null) UnityEngine.Object.DestroyImmediate(_settings);
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
        }

        [Test]
        public void Bind_InitialState_TenChipsAndPlainLabel()
        {
            _view.Bind(_playerGuid);

            Assert.AreEqual(10, _stack.DisplayedCount);
            Assert.AreEqual("10/10", _label.text);
        }

        [Test]
        public void DamageEvent_RemovesChipsAndUpdatesLabel()
        {
            _view.Bind(_playerGuid);

            _attrs.SetAttributeValue<Health, int>(_playerGuid, 5);
            TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                SourceGuid = Guid.NewGuid(),
                TargetGuid = _playerGuid,
                FinalDamage = 5,
            });

            Assert.AreEqual(5, _stack.DisplayedCount);
            Assert.AreEqual("5/10", _label.text);
        }

        [Test]
        public void ShieldEvent_AddsChipsOnTopAndColorsLabel()
        {
            _view.Bind(_playerGuid);

            _attrs.SetAttributeValue<Shield, int>(_playerGuid, 2);
            EventManager.Trigger(EventName.OnShieldChanged, _playerGuid, 2);

            Assert.AreEqual(12, _stack.DisplayedCount, "10 de vida + 2 de escudo apiladas.");
            Assert.AreEqual("<color=#A3B3B1>12</color>/10", _label.text);
        }

        [Test]
        public void AbsorbBurst_ShieldEventThenDamagePayload_NoDoubleCount()
        {
            _view.Bind(_playerGuid);
            _attrs.SetAttributeValue<Shield, int>(_playerGuid, 2);
            EventManager.Trigger(EventName.OnShieldChanged, _playerGuid, 2);

            // Golpe de 4: 2 absorbidos por escudo, 2 a la vida. El pipeline emite
            // OnShieldChanged y DamageResolvedPayload en el mismo burst síncrono.
            _attrs.SetAttributeValue<Shield, int>(_playerGuid, 0);
            _attrs.SetAttributeValue<Health, int>(_playerGuid, 8);
            EventManager.Trigger(EventName.OnShieldChanged, _playerGuid, 0);
            TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                SourceGuid = Guid.NewGuid(),
                TargetGuid = _playerGuid,
                FinalDamage = 2,
                ShieldAbsorbed = 2,
                ShieldBroken = true,
            });

            Assert.AreEqual(8, _stack.DisplayedCount);
            Assert.AreEqual("8/10", _label.text);
        }

        [Test]
        public void DamageEvent_OtherEntity_IgnoresAndKeepsState()
        {
            _view.Bind(_playerGuid);

            _attrs.SetAttributeValue<Health, int>(_playerGuid, 5);
            TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                SourceGuid = Guid.NewGuid(),
                TargetGuid = Guid.NewGuid(),
                FinalDamage = 5,
            });

            Assert.AreEqual(10, _stack.DisplayedCount,
                "Un evento de otra entidad no debe refrescar la pila del jugador.");
            Assert.AreEqual("10/10", _label.text);
        }

        [Test]
        public void LethalDamage_EmptiesStackAndShowsZero()
        {
            _view.Bind(_playerGuid);

            _attrs.SetAttributeValue<Health, int>(_playerGuid, 0);
            TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                SourceGuid = Guid.NewGuid(),
                TargetGuid = _playerGuid,
                FinalDamage = 10,
                WasLethal = true,
            });

            Assert.AreEqual(0, _stack.DisplayedCount);
            Assert.AreEqual("0/10", _label.text);
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
