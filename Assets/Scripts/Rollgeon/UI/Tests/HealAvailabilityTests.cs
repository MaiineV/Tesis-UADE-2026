using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Heroes;
using Rollgeon.Player;
using Rollgeon.UI.HUD;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Regresion BUG-017: gate del slot/boton de Healing con la vida llena.
    /// <see cref="HealAvailability.CanHealMore"/> devuelve false SOLO cuando
    /// AttributesManager + IPlayerService.CurrentHero resuelven y HP &gt;= max;
    /// ante cualquier wiring incompleto degrada a true (nunca lockear de mas).
    /// </summary>
    [TestFixture]
    public class HealAvailabilityTests
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

        private Guid _playerGuid;
        private FakePlayerService _playerService;
        private AttributesManager _attrManager;
        private ClassHeroSO _hero;

        [SetUp]
        public void Setup()
        {
            _playerGuid = Guid.NewGuid();

            // Clean slate: el ServiceLocator es estatico y otro fixture pudo dejar
            // servicios colgados — el gate depende de su ausencia en el caso (a).
            ServiceLocator.RemoveService<IPlayerService>();
            ServiceLocator.RemoveService<AttributesManager>();
        }

        [TearDown]
        public void Teardown()
        {
            ServiceLocator.RemoveService<IPlayerService>();
            ServiceLocator.RemoveService<AttributesManager>();
            if (_attrManager != null) { _attrManager.Dispose(); _attrManager = null; }
            if (_hero != null) { UnityEngine.Object.DestroyImmediate(_hero); _hero = null; }
            _playerService = null;
        }

        [Test]
        public void should_return_true_when_services_not_registered()
        {
            // Arrange — sin AttributesManager ni IPlayerService (bootstrap incompleto).

            // Act
            bool canHeal = HealAvailability.CanHealMore(_playerGuid);

            // Assert
            Assert.IsTrue(canHeal,
                "Sin servicios registrados el gate degrada a true (nunca lockear de mas).");
        }

        [Test]
        public void should_return_true_when_hp_below_max()
        {
            // Arrange
            RegisterServices(currentHp: 50, maxHp: 100);

            // Act
            bool canHeal = HealAvailability.CanHealMore(_playerGuid);

            // Assert
            Assert.IsTrue(canHeal, "Con headroom (50/100) el heal aporta — gate abierto.");
        }

        [Test]
        public void should_return_false_when_hp_at_max()
        {
            // Arrange
            RegisterServices(currentHp: 100, maxHp: 100);

            // Act
            bool canHeal = HealAvailability.CanHealMore(_playerGuid);

            // Assert
            Assert.IsFalse(canHeal,
                "Con la vida llena HealPipeline clampea a 0 — gate cerrado (BUG-017).");
        }

        [Test]
        public void should_return_true_when_current_hero_is_null()
        {
            // Arrange — servicios registrados pero sin hero: no hay max HP conocido.
            RegisterServices(currentHp: 100, maxHp: 100);
            _playerService.CurrentHero = null;

            // Act
            bool canHeal = HealAvailability.CanHealMore(_playerGuid);

            // Assert
            Assert.IsTrue(canHeal, "Sin CurrentHero no se conoce el max — gate degrada a true.");
        }

        // ---------------- helpers ----------------

        private void RegisterServices(int currentHp, int maxHp)
        {
            _hero = ScriptableObject.CreateInstance<ClassHeroSO>();
            _hero.BaseMaxHp = maxHp;

            _playerService = new FakePlayerService
            {
                PlayerGuid = _playerGuid,
                CurrentHero = _hero
            };
            ServiceLocator.AddService<IPlayerService>(_playerService);

            _attrManager = new AttributesManager();
            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(currentHp));
            _attrManager.Register(_playerGuid, attrs);
            ServiceLocator.AddService<AttributesManager>(_attrManager);
        }
    }
}
