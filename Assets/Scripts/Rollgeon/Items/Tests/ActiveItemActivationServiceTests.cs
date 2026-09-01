using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Actions;
using Rollgeon.Combat.Rolls;
using Rollgeon.Dice;
using Rollgeon.Effects;
using Rollgeon.Heroes;
using Rollgeon.Items.Active;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Items.Tests
{
    /// <summary>
    /// Gating (§6/§7) y secuencia de activacion (§22) del item activo: tocar es gratis,
    /// confirmar cobra 1 roll, la tirada va inmediatamente despues y la banda decide que
    /// efecto corre.
    /// </summary>
    [TestFixture]
    public sealed class ActiveItemActivationServiceTests
    {
        private readonly List<UnityEngine.Object> _spawned = new List<UnityEngine.Object>();
        private EquippedActiveItemService _equipped;
        private ActiveItemActivationService _service;
        private FakeRollPool _rolls;
        private FakeDieRoller _roller;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _player = Guid.NewGuid();
            ServiceLocator.AddService<IPlayerService>(new FakePlayerService(_player));

            _rolls = new FakeRollPool { InCombat = true };
            _rolls.Current[_player] = 5;
            ServiceLocator.AddService<IRollPoolService>(_rolls);

            _equipped = new EquippedActiveItemService(catalog: null);
            _roller = new FakeDieRoller();
            _service = new ActiveItemActivationService(_equipped, _roller);

            Eff_Tag.Log.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _spawned.Clear();
            Eff_Tag.Log.Clear();
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        // ------------------------------------------------------------------
        // Gating
        // ------------------------------------------------------------------

        [Test]
        public void test_canActivate_withEmptySlot_reportsNoItemEquipped()
        {
            // Act + Assert — PRE-02.
            Assert.AreEqual(ActiveItemBlock.NoItemEquipped, _service.CanActivate());
        }

        [Test]
        public void test_canActivate_outOfCombat_reportsNotInCombat()
        {
            // Arrange — el GDD: "no existe ni se acumula durante la exploración".
            _equipped.Equip(NewItem("item.a", DiceType.D6));
            _rolls.InCombat = false;

            // Act + Assert
            Assert.AreEqual(ActiveItemBlock.NotInCombat, _service.CanActivate());
        }

        [Test]
        public void test_canActivate_withNoRolls_reportsNotEnoughRolls()
        {
            // Arrange — PRE-03.
            _equipped.Equip(NewItem("item.a", DiceType.D6));
            _rolls.Current[_player] = 0;

            // Act + Assert
            Assert.AreEqual(ActiveItemBlock.NotEnoughRolls, _service.CanActivate());
        }

        [Test]
        public void test_canActivate_withExactlyOneRoll_isAllowed()
        {
            // Arrange — edge case del GDD: con 1 roll se puede, el pool queda en 0.
            _equipped.Equip(NewItem("item.a", DiceType.D6));
            _rolls.Current[_player] = 1;

            // Act + Assert
            Assert.AreEqual(ActiveItemBlock.None, _service.CanActivate());
        }

        [Test]
        public void test_canActivate_outsideYourTurn_reportsNotYourTurn()
        {
            // Arrange — PRE-01.
            _equipped.Equip(NewItem("item.a", DiceType.D6));
            var turns = new TurnManager();
            turns.ConfigureForTests(_rolls, actions: null, ruleset: null);
            turns.SetActingGuidForTests(Guid.NewGuid());
            ServiceLocator.AddService<TurnManager>(turns);

            // Act + Assert
            Assert.AreEqual(ActiveItemBlock.NotYourTurn, _service.CanActivate());
        }

        [Test]
        public void test_canActivate_isPure_doesNotSpendRolls()
        {
            // Arrange — el HUD lo llama en cada refresh.
            _equipped.Equip(NewItem("item.a", DiceType.D6));

            // Act
            _service.CanActivate();
            _service.CanActivate();

            // Assert
            Assert.AreEqual(5, _rolls.Current[_player]);
            Assert.AreEqual(0, _rolls.SpendCalls);
        }

        // ------------------------------------------------------------------
        // Confirmacion: cobro, tirada y banda
        // ------------------------------------------------------------------

        [Test]
        public void test_confirm_spendsExactlyOneRoll()
        {
            // Arrange — "1 roll, fijo, igual para todos los ítems activos".
            _equipped.Equip(NewItem("item.a", DiceType.D6));
            _roller.Next = 5;

            // Act
            _service.Confirm(selection: null);

            // Assert
            Assert.AreEqual(4, _rolls.Current[_player]);
        }

        [TestCase(1, ActiveItemBand.Negative, "neg")]
        [TestCase(3, ActiveItemBand.Mixed, "mix")]
        [TestCase(6, ActiveItemBand.Positive, "pos")]
        public void test_confirm_runsOnlyTheEffectsOfTheRolledBand(int roll, ActiveItemBand band, string tag)
        {
            // Arrange
            _equipped.Equip(NewItem("item.a", DiceType.D6));
            _roller.Next = roll;

            // Act
            var result = _service.Confirm(selection: null);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(roll, result.Value.Roll);
            Assert.AreEqual(band, result.Value.Band);
            CollectionAssert.AreEqual(new[] { tag }, Eff_Tag.Log,
                "solo tiene que correr el grupo de la banda que salio");
        }

        [Test]
        public void test_confirm_whenBlocked_neitherSpendsNorRolls()
        {
            // Arrange
            _equipped.Equip(NewItem("item.a", DiceType.D6));
            _rolls.Current[_player] = 0;

            // Act
            var result = _service.Confirm(selection: null);

            // Assert
            Assert.IsNull(result);
            Assert.AreEqual(0, _roller.Calls, "no se tira el dado si la activacion esta bloqueada");
            CollectionAssert.IsEmpty(Eff_Tag.Log);
        }

        [Test]
        public void test_confirm_raisesOnResolvedWithTheRollAndBand()
        {
            // Arrange — el HUD lo usa para mostrar la cara dentro del slot.
            _equipped.Equip(NewItem("item.a", DiceType.D6));
            _roller.Next = 2;

            ActiveItemActivationResult? seen = null;
            _service.OnResolved += r => seen = r;

            // Act
            _service.Confirm(selection: null);

            // Assert
            Assert.IsNotNull(seen);
            Assert.AreEqual(2, seen.Value.Roll);
            Assert.AreEqual(ActiveItemBand.Negative, seen.Value.Band);
        }

        [Test]
        public void test_confirm_canBeRepeatedWhileRollsLast()
        {
            // Arrange — el GDD no pone tope de usos por turno ni por combate.
            _equipped.Equip(NewItem("item.a", DiceType.D6));
            _roller.Next = 6;
            _rolls.Current[_player] = 2;

            // Act
            Assert.IsNotNull(_service.Confirm(null));
            Assert.IsNotNull(_service.Confirm(null));

            // Assert — al tercer intento el pool esta en 0.
            Assert.AreEqual(0, _rolls.Current[_player]);
            Assert.IsNull(_service.Confirm(null));
        }

        [Test]
        public void test_confirm_theRollIsSpentEvenIfTheBandEffectsFail()
        {
            // Arrange — no hay reembolso: el GDD dice que no existe ventana para uno.
            var item = NewItem("item.a", DiceType.D6);
            item.OnPositiveBand.Effects.Clear();
            item.OnPositiveBand.Effects.Add(new Eff_Fail());
            _roller.Next = 6;

            // Act
            var result = _service.Confirm(selection: null);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsFalse(result.Value.EffectsSucceeded);
            Assert.AreEqual(4, _rolls.Current[_player], "el roll ya se habia cobrado");
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private ItemSO NewItem(string id, DiceType die)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = id;
            item.DisplayName = id;
            item.Type = ItemType.Active;
            item.ActiveDie = die;
            item.Family = ActiveItemFamily.Potencia;
            item.OnNegativeBand = new EffectData();
            item.OnNegativeBand.Effects.Add(new Eff_Tag { Tag = "neg" });
            item.OnMixedBand = new EffectData();
            item.OnMixedBand.Effects.Add(new Eff_Tag { Tag = "mix" });
            item.OnPositiveBand = new EffectData();
            item.OnPositiveBand.Effects.Add(new Eff_Tag { Tag = "pos" });
            _spawned.Add(item);
            _equipped.Equip(item);
            return item;
        }

        /// <summary>Registra que corrio, para saber que banda se ejecuto.</summary>
        [Serializable]
        private sealed class Eff_Tag : BaseEffect
        {
            public static readonly List<string> Log = new List<string>();
            public string Tag;

            public override string GetEffectName() => "Tag";
            public override bool ApplyEffect(EffectContext context)
            {
                Log.Add(Tag);
                return true;
            }
        }

        [Serializable]
        private sealed class Eff_Fail : BaseEffect
        {
            public override string GetEffectName() => "Fail";
            public override bool ApplyEffect(EffectContext context) => false;
        }

        private sealed class FakeDieRoller : IActiveItemDieRoller
        {
            public int Next = 1;
            public int Calls { get; private set; }

            public int Roll(DiceType die)
            {
                Calls++;
                return Next;
            }
        }

        private sealed class FakeRollPool : IRollPoolService
        {
            public readonly Dictionary<Guid, int> Current = new Dictionary<Guid, int>();
            public bool InCombat = true;
            public int SpendCalls { get; private set; }

            public bool IsCombatActive => InCombat;

            public void InitializeForEntity(Guid entityId) => Current[entityId] = 5;

            public bool TrySpendRolls(Guid entityId, int count)
            {
                SpendCalls++;
                if (!Current.TryGetValue(entityId, out var have) || count > have) return false;
                Current[entityId] = have - count;
                return true;
            }

            public int Drain(Guid entityId, int amount) => 0;
            public void AddRolls(Guid entityId, int amount) { }
            public int GetCurrent(Guid entityId) => Current.TryGetValue(entityId, out var v) ? v : 0;
            public int GetMax(Guid entityId) => 15;
            public int GetRollsPerTurn(Guid entityId) => 5;
            public void AddRollPoolBonus(int amount) { }
            public void RestoreCurrent(Guid entityId, int value) => Current[entityId] = value;
        }

        private sealed class FakePlayerService : IPlayerService
        {
            public FakePlayerService(Guid guid) { PlayerGuid = guid; }

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
