using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Rolls;
using Rollgeon.Combos;
using Rollgeon.Combos.Play;
using Rollgeon.Dice;
using Rollgeon.Economy;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Readers;
using Rollgeon.Heroes;
using Rollgeon.Player;
using Rollgeon.Upgrades.Dice;
using UnityEngine;

namespace Rollgeon.Items.Tests
{
    /// <summary>
    /// Regresión del playtest 2026-09-04 sobre "Bolsa del Impar": pagaba +3 por cada impar de
    /// CADA tirada (hook de <c>OnRollResolved</c>), con o sin combo. La regla es que pague por
    /// los dados impares que PARTICIPAN del combo jugado. Replica la autoría del asset: hook
    /// ComboPlayed + <see cref="EffModifyGold"/> con <see cref="ReadDiceCountByParity"/> en
    /// alcance <see cref="DiceParityScope.ComboDice"/>, y verifica el feedback
    /// (<see cref="EventName.OnItemGoldGranted"/>, el toast sobre la pila de oro).
    /// </summary>
    [TestFixture]
    public class BolsaDelImparHookTests
    {
        private InventoryService _service;
        private ComboPlayService _play;
        private FakeEconomy _economy;
        private Guid _playerGuid;
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();
        private readonly List<(string itemId, int amount)> _granted = new List<(string, int)>();
        private EventManager.EventReceiver _onGranted;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            TypedEvent<ComboPlayedPayload>.Clear();
            _playerGuid = Guid.NewGuid();
            ServiceLocator.AddService<IPlayerService>(new StubPlayerService(_playerGuid));
            _economy = new FakeEconomy();
            ServiceLocator.AddService<IEconomyService>(_economy, ServiceScope.Global);
            _service = new InventoryService(null, 4);
            _play = new ComboPlayService();
            _play.Register();

            _granted.Clear();
            _onGranted = args => _granted.Add((args[1] as string, (int)args[2]));
            EventManager.Subscribe(EventName.OnItemGoldGranted, _onGranted);
        }

        [TearDown]
        public void TearDown()
        {
            EventManager.UnSubscribe(EventName.OnItemGoldGranted, _onGranted);
            _play?.Dispose();
            _play = null;
            _service?.Dispose();
            _service = null;

            foreach (var o in _created)
            {
                if (o != null) UnityEngine.Object.DestroyImmediate(o);
            }
            _created.Clear();

            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            TypedEvent<ComboPlayedPayload>.Clear();
        }

        // ================================================================
        // Helpers
        // ================================================================

        private ItemSO NewBolsaDelImpar()
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = "bolsa.del.impar";
            item.DisplayName = "Bolsa del Impar";
            item.Type = ItemType.Passive;

            var hook = new PassiveItemHook
            {
                Kind = PassiveHookKind.ComboPlayed,
                ComboFilter = new ComboFilter { Mode = ComboFilterMode.AnyCombo },
            };
            hook.Effect.Effects.Add(new EffModifyGold
            {
                Operation = GoldOperation.Add,
                Amount = new ReadDiceCountByParity
                {
                    Parity = DiceParity.Odd,
                    Scope = DiceParityScope.ComboDice,
                    PerDieAmount = 3,
                },
            });
            item.PassiveHooks.Add(hook);

            _created.Add(item);
            return item;
        }

        private void PlayCombo(string comboId, int[] dice, int[] contributingIndices,
            int[] kept = null, int[] keptOriginalIndices = null)
        {
            _play.BeginPlay(new EffectContext
            {
                SourceGuid = _playerGuid,
                ActionKind = RollActionKind.Attack,
                DiceResult = dice,
                KeptDice = kept,
                KeptDiceOriginalIndices = keptOriginalIndices,
                ComboResult = ComboDetectionResult.Match(comboId, baseDamage: 8,
                    countUsed: contributingIndices.Length, contributingIndices: contributingIndices),
            });
            _play.EndPlay();
        }

        // ================================================================
        // Tests
        // ================================================================

        [Test]
        public void test_bolsa_pays_three_gold_per_odd_die_that_forms_the_combo()
        {
            // Arrange
            _service.AddItem(NewBolsaDelImpar());

            // Act — par de 3 con 3-3-5-4-6: los dos 3 forman el combo; el 5 es impar pero sobra.
            PlayCombo("combo.pair", new[] { 3, 3, 5, 4, 6 }, new[] { 0, 1 });

            // Assert
            Assert.AreEqual(6, _economy.CurrentGold);
        }

        [Test]
        public void test_bolsa_emits_item_gold_feedback_with_the_amount_paid()
        {
            // Arrange
            _service.AddItem(NewBolsaDelImpar());

            // Act
            PlayCombo("combo.pair", new[] { 3, 3, 5, 4, 6 }, new[] { 0, 1 });

            // Assert — el toast "Bolsa del Impar: +6 de oro" sale de este evento.
            Assert.AreEqual(1, _granted.Count);
            Assert.AreEqual("bolsa.del.impar", _granted[0].itemId);
            Assert.AreEqual(6, _granted[0].amount);
        }

        [Test]
        public void test_bolsa_ignores_odd_dice_outside_the_combo()
        {
            // Arrange
            _service.AddItem(NewBolsaDelImpar());

            // Act — par de 4: los impares 5, 1 y 3 no participan del combo.
            PlayCombo("combo.pair", new[] { 4, 4, 5, 1, 3 }, new[] { 0, 1 });

            // Assert — ni oro ni toast.
            Assert.AreEqual(0, _economy.CurrentGold);
            Assert.AreEqual(0, _granted.Count);
        }

        [Test]
        public void test_bolsa_does_not_pay_a_roll_without_combo()
        {
            // Arrange
            _service.AddItem(NewBolsaDelImpar());

            // Act — tirada llena de impares pero sin combo: la ventana no se abre.
            _play.BeginPlay(new EffectContext
            {
                SourceGuid = _playerGuid,
                ActionKind = RollActionKind.Attack,
                DiceResult = new[] { 1, 3, 5, 7, 9 },
                ComboResult = ComboDetectionResult.NoMatch(),
            });
            _play.EndPlay();

            // Assert
            Assert.AreEqual(0, _economy.CurrentGold);
            Assert.AreEqual(0, _granted.Count);
        }

        [Test]
        public void test_bolsa_reads_combo_dice_in_the_kept_subset_index_space()
        {
            // Arrange — bolsa 4-1-5-5-2, holdeados los slots 2, 3 y 4 (5-5-2): el par son los 5.
            _service.AddItem(NewBolsaDelImpar());

            // Act
            PlayCombo("combo.pair", new[] { 4, 1, 5, 5, 2 }, new[] { 0, 1 },
                kept: new[] { 5, 5, 2 }, keptOriginalIndices: new[] { 2, 3, 4 });

            // Assert — dos 5 impares en el combo; el 1 del slot 1 no cuenta (no fue holdeado).
            Assert.AreEqual(6, _economy.CurrentGold);
        }

        private sealed class FakeEconomy : IEconomyService
        {
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
            public StubPlayerService(Guid guid) { PlayerGuid = guid; }
            public Guid PlayerGuid { get; }
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
