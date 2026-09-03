using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Patterns.Save;
using Rollgeon.Combat.Rolls;
using Rollgeon.Combos;
using Rollgeon.Combos.Play;
using Rollgeon.Dice;
using Rollgeon.Effects;
using Rollgeon.Heroes;
using Rollgeon.Player;
using Rollgeon.Upgrades.Dice;
using UnityEngine;

namespace Rollgeon.Items.Tests
{
    /// <summary>
    /// Eco Menguante (<see cref="DecayingMultiplierService"/>): multiplica solo ataques, descuenta
    /// con cualquier combo de combate, se rompe al tocar el piso y persiste el contador.
    /// </summary>
    [TestFixture]
    public class DecayingMultiplierServiceTests
    {
        private InventoryService _inventory;
        private ComboPlayService _play;
        private DecayingMultiplierService _service;
        private Guid _player;
        private readonly List<UnityEngine.Object> _created = new();

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            TypedEvent<ComboPlayedPayload>.Clear();
            // El SaveSystem re-hidrata desde su cache al registrar: sin esto el contador
            // del test anterior reaparece en la instancia nueva.
            SaveSystem.ResetForTests();
            _player = Guid.NewGuid();
            ServiceLocator.AddService<IPlayerService>(new StubPlayerService(_player));
            _inventory = new InventoryService(null, 4);
            ServiceLocator.AddService<IInventoryService>(_inventory, ServiceScope.Global);
            _play = new ComboPlayService();
            _play.Register();
            _service = new DecayingMultiplierService();
            ServiceLocator.AddService<IDecayingMultiplierService>(_service, ServiceScope.Global);
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();
            _play?.Dispose();
            _inventory?.Dispose();
            foreach (var o in _created) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _created.Clear();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            TypedEvent<ComboPlayedPayload>.Clear();
            SaveSystem.ResetForTests();
        }

        private ItemSO NewEco(float start = 5f, float decay = 0.2f, float min = 1f, bool breakAtMin = true)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = "eco.menguante";
            item.DisplayName = "Eco Menguante";
            item.Type = ItemType.Passive;
            item.DecayingMultiplier = new DecayingMultiplierDef
            {
                Enabled = true, Start = start, DecayPerCombo = decay, Min = min, BreakAtMin = breakAtMin,
            };
            _created.Add(item);
            return item;
        }

        /// <summary>Abre la ventana de combo jugado (emite ComboPlayed) y devuelve el scratch.</summary>
        private EnchantmentScratch Play(RollActionKind kind = RollActionKind.Attack)
        {
            _play.BeginPlay(new EffectContext
            {
                SourceGuid = _player,
                DiceResult = new[] { 2, 2, 5 },
                ActionKind = kind,
                ComboResult = ComboDetectionResult.Match("combo.pair", baseDamage: 10, countUsed: 2,
                    contributingIndices: new[] { 0, 1 }),
            });
            var scratch = _play.CurrentPlayScratch;
            _play.EndPlay();
            return scratch;
        }

        [Test]
        public void FirstAttack_MultipliesByStart_AndJournalsTheItem()
        {
            var eco = NewEco();
            _inventory.AddItem(eco);

            var scratch = Play(RollActionKind.Attack);

            Assert.AreEqual(5f, scratch.ComboDamageMultiplier, 0.0001f);
            Assert.AreEqual(1, scratch.Journal.Count);
            Assert.AreEqual("eco.menguante", scratch.Journal[0].SourceId);
            Assert.AreSame(eco, scratch.Journal[0].SourceAsset);
            Assert.AreEqual(5f, scratch.Journal[0].MultiplierFactor, 0.0001f);
        }

        [Test]
        public void EachCombatCombo_DecaysTheNextAttack()
        {
            _inventory.AddItem(NewEco());

            Play(RollActionKind.Attack);   // x5.0 → contador 1
            Play(RollActionKind.Defense);  // no multiplica, contador 2
            Play(RollActionKind.Heal);     // contador 3
            var third = Play(RollActionKind.Attack);

            Assert.AreEqual(5f - 3 * 0.2f, third.ComboDamageMultiplier, 0.0001f);
            Assert.AreEqual(4, _service.GetCombosPlayed("eco.menguante"));
        }

        [Test]
        public void DefenseCombo_DoesNotMultiplyTheShield()
        {
            _inventory.AddItem(NewEco());

            var scratch = Play(RollActionKind.Defense);

            Assert.AreEqual(1f, scratch.ComboDamageMultiplier, 0.0001f);
            Assert.IsNull(scratch.Journal);
        }

        [Test]
        public void MovementCombo_DoesNotDecay()
        {
            _inventory.AddItem(NewEco());

            Play(RollActionKind.Movement);

            Assert.AreEqual(0, _service.GetCombosPlayed("eco.menguante"));
        }

        [Test]
        public void ReachingMin_BreaksTheItem_AfterMultiplyingThatHit_AndEmitsEvent()
        {
            // Start 1.4, decay 0.2, min 1: combo 1 → 1.2, combo 2 → 1.0 = piso → se rompe.
            var eco = NewEco(start: 1.4f);
            _inventory.AddItem(eco);
            ItemSO broke = null;
            int brokeAfter = -1;
            EventManager.Subscribe(EventName.OnItemBrokeDown, args =>
            {
                broke = args[1] as ItemSO;
                brokeAfter = (int)args[2];
            });

            var first = Play(RollActionKind.Attack);
            Assert.AreEqual(1.4f, first.ComboDamageMultiplier, 0.0001f);
            Assert.IsTrue(_inventory.HasItem("eco.menguante"), "1.2 todavía está sobre el piso");

            var second = Play(RollActionKind.Attack);

            Assert.AreEqual(1.2f, second.ComboDamageMultiplier, 0.0001f, "el golpe que lo agota pega con el valor previo");
            Assert.IsFalse(_inventory.HasItem("eco.menguante"), "al tocar el piso se rompe");
            Assert.AreSame(eco, broke);
            Assert.AreEqual(2, brokeAfter);
            Assert.AreEqual(0, _service.GetCombosPlayed("eco.menguante"), "el contador se va con el item");
        }

        [Test]
        public void BreakAtMinOff_StaysAtMinForever()
        {
            _inventory.AddItem(NewEco(start: 1.2f, breakAtMin: false));

            Play(RollActionKind.Attack);
            Play(RollActionKind.Attack);
            var third = Play(RollActionKind.Attack);

            Assert.IsTrue(_inventory.HasItem("eco.menguante"));
            Assert.AreEqual(1f, third.ComboDamageMultiplier, 0.0001f);
        }

        [Test]
        public void OtherPlayer_IsIgnored()
        {
            _inventory.AddItem(NewEco());

            _play.BeginPlay(new EffectContext
            {
                SourceGuid = Guid.NewGuid(),
                ActionKind = RollActionKind.Attack,
                ComboResult = ComboDetectionResult.Match("combo.pair", 10, 2, new[] { 0, 1 }),
            });
            var scratch = _play.CurrentPlayScratch;
            _play.EndPlay();

            Assert.AreEqual(1f, scratch.ComboDamageMultiplier, 0.0001f);
            Assert.AreEqual(0, _service.GetCombosPlayed("eco.menguante"));
        }

        [Test]
        public void SaveRoundTrip_KeepsTheCounter()
        {
            _inventory.AddItem(NewEco());
            Play(RollActionKind.Attack);
            Play(RollActionKind.Attack);

            var state = _service.CaptureState();
            var restored = new DecayingMultiplierService();
            try
            {
                restored.RestoreState(state);
                Assert.AreEqual(2, restored.GetCombosPlayed("eco.menguante"));
                Assert.AreEqual(5f - 2 * 0.2f, restored.GetCurrentMultiplier(_created[0] as ItemSO), 0.0001f);
            }
            finally { restored.Dispose(); }
        }

        [Test]
        public void RunStart_ClearsCounters()
        {
            _inventory.AddItem(NewEco());
            Play(RollActionKind.Attack);

            EventManager.Trigger(EventName.OnRunStart, Guid.NewGuid());

            Assert.AreEqual(0, _service.GetCombosPlayed("eco.menguante"));
        }

        [Test]
        public void ItemWithoutKnob_IsUntouched()
        {
            var plain = ScriptableObject.CreateInstance<ItemSO>();
            plain.ItemId = "plain";
            plain.DisplayName = "plain";
            plain.Type = ItemType.Passive;
            _created.Add(plain);
            _inventory.AddItem(plain);

            var scratch = Play(RollActionKind.Attack);

            Assert.AreEqual(1f, scratch.ComboDamageMultiplier, 0.0001f);
            Assert.AreEqual(1f, _service.GetCurrentMultiplier(plain), 0.0001f);
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
#pragma warning disable CS0067
            public event Action<ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;
#pragma warning restore CS0067
        }
    }
}
