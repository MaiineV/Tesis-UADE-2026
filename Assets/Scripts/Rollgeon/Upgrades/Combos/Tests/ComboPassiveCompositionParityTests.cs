using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Patterns.Save;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Dice;
using Rollgeon.Economy;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Readers;
using Rollgeon.Heroes;
using Rollgeon.Player;
using Rollgeon.Upgrades.Combos.Triggers.Concretes;
using UnityEngine;

namespace Rollgeon.Upgrades.Combos.Tests
{
    /// <summary>
    /// Paridad observable del canal combos: los concretes de scratch legacy
    /// (AddGoldOnComboMatch, AddShieldOnTurnStart) contra sus composiciones
    /// <c>ExecuteEffectsOnEvent</c> + EffModifyGold / EffModifyIntAttribute,
    /// despachadas por el <see cref="ComboPassiveService"/> real.
    /// </summary>
    [TestFixture]
    public class ComboPassiveCompositionParityTests
    {
        private ComboPassiveService _svc;
        private FakeEconomy _economy;
        private StubPlayerService _player;
        private AttributesManager _attrs;
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            TypedEvent<ComboMatchedPayload>.Clear();
            SaveSystem.ResetForTests();

            _svc = new ComboPassiveService();
            _svc.SubscribeEventsForTests();

            _economy = new FakeEconomy(0);
            ServiceLocator.AddService<IEconomyService>(_economy, ServiceScope.Global);

            _player = new StubPlayerService();
            ServiceLocator.AddService<IPlayerService>(_player, ServiceScope.Global);

            _attrs = new AttributesManager();
            var a = new ModifiableAttributes();
            a.SetAttribute<Shield>(new Shield(0));
            _attrs.Register(_player.PlayerGuid, a);
            ServiceLocator.AddService<AttributesManager>(_attrs, ServiceScope.Global);

            SaveSystem.Clear();
            EventManager.Trigger(EventName.OnRunStart, Guid.NewGuid(), "test-ruleset");
        }

        [TearDown]
        public void TearDown()
        {
            _svc?.UnsubscribeEventsForTests();
            _svc = null;
            _attrs?.Dispose();

            foreach (var obj in _created)
            {
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            }
            _created.Clear();

            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            TypedEvent<ComboMatchedPayload>.Clear();
            SaveSystem.ResetForTests();
        }

        // ================================================================
        // Helpers
        // ================================================================

        private ComboPassiveSO MakePassive(string id, string targetComboId, params IComboPassiveTrigger[] triggers)
        {
            var passive = ScriptableObject.CreateInstance<ComboPassiveSO>();
            passive.name = id;
            _created.Add(passive);
            typeof(Rollgeon.Upgrades.UpgradeSO).GetField("_upgradeId", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(passive, id);
            typeof(ComboPassiveSO).GetField("_targetComboId", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(passive, targetComboId);
            typeof(ComboPassiveSO).GetField("_extraTriggers", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(passive, new List<IComboPassiveTrigger>(triggers));
            return passive;
        }

        private void OwnPassive(ComboPassiveSO passive) =>
            ServiceLocator.GetService<RunComboPassivesState>().Add(passive);

        private void ClearPassives()
        {
            // Estado fresco por variante: re-crea el run state (mismo camino que OnRunStart).
            SaveSystem.Clear();
            EventManager.Trigger(EventName.OnRunStart, Guid.NewGuid(), "test-ruleset");
        }

        private static EffModifyIntAttribute ShieldAdd(int amount)
        {
            var eff = new EffModifyIntAttribute
            {
                TargetStat = StatType.Shield,
                Operation = IntOperation.Add,
            };
            typeof(EffModifyIntAttribute).GetField("_baseAmount", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(eff, amount);
            return eff;
        }

        private static ExecuteEffectsOnEvent Bridge(ComboPassiveHookEvent evt, params IEffect[] effects)
        {
            var group = new EffectData();
            foreach (var eff in effects) group.Effects.Add(eff);
            return new ExecuteEffectsOnEvent
            {
                Event = evt,
                Effects = new List<EffectData> { group },
            };
        }

        private void RaiseComboMatched(string comboId) =>
            TypedEvent<ComboMatchedPayload>.Raise(new ComboMatchedPayload
            {
                SourceGuid = _player.PlayerGuid,
                ComboId = comboId,
                BaseDamage = 10,
            });

        private int PlayerShield() => _attrs.GetAttributeValue<Shield, int>(_player.PlayerGuid);

        // ================================================================
        // AddGoldOnComboMatch → ExecuteEffectsOnEvent + EffModifyGold
        // ================================================================

        [Test]
        public void Parity_GoldOnComboMatch_SameFinalGold()
        {
            // Legacy — scratch diferido, aplicado por el service.
            OwnPassive(MakePassive("legacy", "combo.escalera",
                new AddGoldOnComboMatch { Amount = new ReadConstantInt { Value = 3 } }));
            _economy.ResetTo(0);
            RaiseComboMatched("combo.escalera");
            int legacyGold = _economy.CurrentGold;

            // Composición — apply inmediato del EffModifyGold.
            ClearPassives();
            OwnPassive(MakePassive("composed", "combo.escalera",
                Bridge(ComboPassiveHookEvent.ComboMatched,
                    new EffModifyGold { Operation = GoldOperation.Add, Amount = new ReadConstantInt { Value = 3 } })));
            _economy.ResetTo(0);
            RaiseComboMatched("combo.escalera");

            Assert.AreEqual(legacyGold, _economy.CurrentGold);
            Assert.AreEqual(3, _economy.CurrentGold);
        }

        [Test]
        public void Parity_GoldOnComboMatch_OtherCombo_NoGold()
        {
            OwnPassive(MakePassive("composed", "combo.escalera",
                Bridge(ComboPassiveHookEvent.ComboMatched,
                    new EffModifyGold { Operation = GoldOperation.Add, Amount = new ReadConstantInt { Value = 3 } })));
            _economy.ResetTo(0);

            RaiseComboMatched("combo.par");

            Assert.AreEqual(0, _economy.CurrentGold, "El scope por TargetComboId debe respetarse.");
        }

        // ================================================================
        // AddShieldOnTurnStart → ExecuteEffectsOnEvent + EffModifyIntAttribute
        // ================================================================

        [Test]
        public void Parity_ShieldOnTurnStart_SameFinalShield()
        {
            // Legacy — scratch.BonusShield aplicado por el applier sobre AttributesManager.
            OwnPassive(MakePassive("legacy", null,
                new AddShieldOnTurnStart { Amount = new ReadConstantInt { Value = 2 } }));
            EventManager.Trigger(EventName.OnTurnStarted, _player.PlayerGuid);
            int legacyShield = PlayerShield();

            // Reset de shield + pasivas para la variante composicional.
            _attrs.SetAttributeValue<Shield, int>(_player.PlayerGuid, 0);
            ClearPassives();
            OwnPassive(MakePassive("composed", null,
                Bridge(ComboPassiveHookEvent.TurnStarted, ShieldAdd(2))));
            EventManager.Trigger(EventName.OnTurnStarted, _player.PlayerGuid);

            Assert.AreEqual(legacyShield, PlayerShield());
            Assert.AreEqual(2, PlayerShield());
        }

        // ================================================================
        // Stubs
        // ================================================================

        private sealed class FakeEconomy : IEconomyService
        {
            public FakeEconomy(int gold) { CurrentGold = gold; }
            public int CurrentGold { get; private set; }
            public void Add(int amount) { if (amount > 0) CurrentGold += amount; }
            public bool Spend(int amount)
            {
                if (amount > CurrentGold) return false;
                CurrentGold -= amount;
                return true;
            }
            public bool CanAfford(int amount) => amount <= 0 || CurrentGold >= amount;
            public void ResetTo(int amount) => CurrentGold = amount;
        }

        private sealed class StubPlayerService : IPlayerService
        {
            public Guid PlayerGuid { get; } = Guid.NewGuid();
            public Guid RunId => Guid.Empty;
            public ClassHeroSO CurrentHero => null;
            public DiceBagSO DiceBag => null;
            public void SetPlayer(ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(DiceBagSO bag) { }
            public void ClearPlayer() { }
            public event Action<ClassHeroSO> OnPlayerSet { add { } remove { } }
            public event Action OnPlayerCleared { add { } remove { } }
        }
    }
}
