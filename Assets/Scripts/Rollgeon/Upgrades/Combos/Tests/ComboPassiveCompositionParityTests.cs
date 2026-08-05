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
    /// Oráculo del canal combos migrado: las composiciones ExecuteEffectsOnEvent +
    /// EffModifyGold / EffModifyIntAttribute que reemplazaron a los concretes legacy
    /// (AddGoldOnComboMatch / AddShieldOnTurnStart), con los valores observables que el
    /// legacy producía (verificados con ambas implementaciones vivas antes del borrado
    /// — Feature#0035), despachadas por el <see cref="ComboPassiveService"/> real.
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
        // Gold on combo match (ComboPassive_GoldOnLadder migrada)
        // ================================================================

        [Test]
        public void GoldOnComboMatch_TargetCombo_AddsGold()
        {
            OwnPassive(MakePassive("gold-on-ladder", "combo.escalera",
                Bridge(ComboPassiveHookEvent.ComboMatched,
                    new EffModifyGold { Operation = GoldOperation.Add, Amount = new ReadConstantInt { Value = 3 } })));
            _economy.ResetTo(0);

            RaiseComboMatched("combo.escalera");

            Assert.AreEqual(3, _economy.CurrentGold);
        }

        [Test]
        public void GoldOnComboMatch_OtherCombo_NoGold()
        {
            OwnPassive(MakePassive("gold-on-ladder", "combo.escalera",
                Bridge(ComboPassiveHookEvent.ComboMatched,
                    new EffModifyGold { Operation = GoldOperation.Add, Amount = new ReadConstantInt { Value = 3 } })));
            _economy.ResetTo(0);

            RaiseComboMatched("combo.par");

            Assert.AreEqual(0, _economy.CurrentGold, "El scope por TargetComboId debe respetarse.");
        }

        // ================================================================
        // Shield on turn start (AddShieldOnTurnStart migrado)
        // ================================================================

        [Test]
        public void ShieldOnTurnStart_AddsShieldToPlayer()
        {
            OwnPassive(MakePassive("shield-per-turn", null,
                Bridge(ComboPassiveHookEvent.TurnStarted, ShieldAdd(2))));

            EventManager.Trigger(EventName.OnTurnStarted, _player.PlayerGuid);

            Assert.AreEqual(2, PlayerShield());
        }

        [Test]
        public void ShieldOnTurnStart_EnemyTurn_DoesNothing()
        {
            OwnPassive(MakePassive("shield-per-turn", null,
                Bridge(ComboPassiveHookEvent.TurnStarted, ShieldAdd(2))));

            EventManager.Trigger(EventName.OnTurnStarted, Guid.NewGuid());

            Assert.AreEqual(0, PlayerShield());
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
