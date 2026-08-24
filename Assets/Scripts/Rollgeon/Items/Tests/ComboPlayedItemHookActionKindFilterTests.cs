using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.Rolls;
using Rollgeon.Combos;
using Rollgeon.Combos.Play;
using Rollgeon.Dice;
using Rollgeon.Economy;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Heroes;
using Rollgeon.Player;
using Rollgeon.Upgrades.Dice;
using Rollgeon.Upgrades.Dice.Readers;
using UnityEngine;

namespace Rollgeon.Items.Tests
{
    /// <summary>
    /// BUG-060/BUG-080: <see cref="PassiveItemHook.ActionKindFilter"/> — un ítem con
    /// bonus de daño (El Egoísta) no debe leakear a Heal/Movement, que comparten el
    /// mismo play scratch que <c>PlayerComboDamage</c>/<c>PlayerComboHeal</c> leen.
    /// También cubre BUG-080: el bono nuevo (<see cref="ReadCurrentGoldSqrtScaled"/> vía
    /// <see cref="EffAddComboBonus"/>) nunca muta el Attack BASE, a diferencia del diseño
    /// viejo (<c>EffModifyIntAttribute</c> sobre <c>OnDamageOutgoing</c>).
    /// </summary>
    [TestFixture]
    public class ComboPlayedItemHookActionKindFilterTests
    {
        private InventoryService _inventory;
        private ComboPlayService _play;
        private AttributesManager _attrs;
        private FakeEconomy _economy;
        private Guid _playerGuid;
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            TypedEvent<ComboPlayedPayload>.Clear();
            _playerGuid = Guid.NewGuid();

            ServiceLocator.AddService<IPlayerService>(new StubPlayerService(_playerGuid));

            _economy = new FakeEconomy(gold: 5); // sqrt(5 × 5) = 5
            ServiceLocator.AddService<IEconomyService>(_economy);

            _attrs = new AttributesManager();
            _attrs.Register(_playerGuid, BuildAttrsWithAttack(baseAttack: 12));
            ServiceLocator.AddService<AttributesManager>(_attrs);

            _play = new ComboPlayService();
            _play.Register();

            _inventory = new InventoryService(null, 4);
        }

        [TearDown]
        public void TearDown()
        {
            _inventory?.Dispose();
            _inventory = null;
            _play?.Dispose();
            _play = null;
            _attrs?.Dispose();
            _attrs = null;

            foreach (var o in _created)
            {
                if (o != null) UnityEngine.Object.DestroyImmediate(o);
            }
            _created.Clear();

            ServiceLocator.Clear();
            TypedEvent<ComboPlayedPayload>.Clear();
        }

        // ---- Helpers ------------------------------------------------------

        private static ModifiableAttributes BuildAttrsWithAttack(int baseAttack)
        {
            var attrs = new ModifiableAttributes();
            attrs.SetAttribute<Attack>(new Attack(baseAttack));
            return attrs;
        }

        // Réplica del diseño nuevo de El Egoísta: ComboPlayed, restringido a Attack,
        // EffAddComboBonus alimentado por ReadCurrentGoldSqrtScaled(factor=5).
        private ItemSO NewEgoistaStyleItem(RollActionKind actionKindFilter)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = "item.egoista-repro";
            item.DisplayName = "item.egoista-repro";
            item.Type = ItemType.Passive;

            var hook = new PassiveItemHook
            {
                Kind = PassiveHookKind.ComboPlayed,
                ComboFilter = new ComboFilter { Mode = ComboFilterMode.AnyCombo },
                ActionKindFilter = actionKindFilter,
            };
            hook.Effect.Effects.Add(new EffAddComboBonus
            {
                Amount = new ReadCurrentGoldSqrtScaled { Factor = 5f },
            });
            item.PassiveHooks.Add(hook);

            _created.Add(item);
            return item;
        }

        private void PlayCombo(RollActionKind kind)
        {
            var combo = ComboDetectionResult.Match("combo.trio", baseDamage: 10, countUsed: 3,
                contributingIndices: new[] { 0, 1, 2 });
            _play.BeginPlay(new EffectContext
            {
                SourceGuid = _playerGuid,
                DiceResult = new[] { 3, 3, 3 },
                ComboResult = combo,
                ActionKind = kind,
            });
            _play.EndPlay();
        }

        // ---- Tests: ActionKindFilter ---------------------------------------

        [Test]
        public void ActionKindFilter_Attack_FiresOnAttackCombo_WritesGoldSqrtBonus()
        {
            // Arrange — oro=5, factor=5 ⇒ bono = floor(sqrt(25)) = 5.
            _inventory.AddItem(NewEgoistaStyleItem(RollActionKind.Attack));

            // Act
            PlayCombo(RollActionKind.Attack);

            // Assert
            Assert.AreEqual(5, _play.LastPlayScratch.BonusComboDamage);
        }

        [Test]
        public void ActionKindFilter_Attack_DoesNotFireOnHealCombo()
        {
            // Arrange — un item de daño no debe leakear a Curarse EN combate, aunque
            // comparta el mismo play scratch que PlayerComboHeal lee.
            _inventory.AddItem(NewEgoistaStyleItem(RollActionKind.Attack));

            // Act
            PlayCombo(RollActionKind.Heal);

            // Assert — sin bono escrito (LastPlayScratch queda null: BeginPlay solo lo
            // crea si algún trigger corrió, y acá el hook no debió correr).
            Assert.IsTrue(_play.LastPlayScratch == null || _play.LastPlayScratch.BonusComboDamage == 0);
        }

        [Test]
        public void ActionKindFilter_Attack_DoesNotFireOnMovementCombo()
        {
            // Arrange — BUG-060: un trío tirado para MOVERSE no debe disparar el bono.
            _inventory.AddItem(NewEgoistaStyleItem(RollActionKind.Attack));

            // Act
            PlayCombo(RollActionKind.Movement);

            // Assert
            Assert.IsTrue(_play.LastPlayScratch == null || _play.LastPlayScratch.BonusComboDamage == 0);
        }

        [Test]
        public void ActionKindFilter_Unknown_FiresRegardlessOfKind_PreservesLegacyBehavior()
        {
            // Arrange — default (Unknown) = sin restricción, comportamiento previo a
            // BUG-080 preservado para hooks que no opinan sobre el kind.
            _inventory.AddItem(NewEgoistaStyleItem(RollActionKind.Unknown));

            // Act
            PlayCombo(RollActionKind.Heal);

            // Assert
            Assert.AreEqual(5, _play.LastPlayScratch.BonusComboDamage);
        }

        // ---- Tests: BUG-080 — Attack base nunca se muta -------------------

        [Test]
        public void EgoistaStyleItem_MultipleAttacks_NeverMutatesBaseAttackAttribute()
        {
            // Arrange — diseño viejo (EffModifyIntAttribute) sumaba el oro DIRECTO al
            // Attack base en cada golpe, de forma permanente. El nuevo diseño es de solo
            // lectura: el bono vive en el scratch, nunca en el atributo.
            _inventory.AddItem(NewEgoistaStyleItem(RollActionKind.Attack));
            int baseAttackBefore = _attrs.GetAttributeValue<Attack, int>(_playerGuid);

            // Act — 3 "golpes" con oro creciente entre cada uno.
            PlayCombo(RollActionKind.Attack);
            _economy.ResetTo(20);
            PlayCombo(RollActionKind.Attack);
            _economy.ResetTo(45);
            PlayCombo(RollActionKind.Attack);

            // Assert
            int baseAttackAfter = _attrs.GetAttributeValue<Attack, int>(_playerGuid);
            Assert.AreEqual(baseAttackBefore, baseAttackAfter,
                "El Attack BASE no debe cambiar entre golpes — el bono es de solo lectura.");
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

        private sealed class FakeEconomy : IEconomyService
        {
            public FakeEconomy(int gold) { CurrentGold = gold; }
            public int CurrentGold { get; private set; }
            public void Add(int amount) { CurrentGold += amount; }
            public bool Spend(int amount)
            {
                if (amount > CurrentGold) return false;
                CurrentGold -= amount;
                return true;
            }
            public bool CanAfford(int amount) => amount <= 0 || CurrentGold >= amount;
            public void ResetTo(int amount) => CurrentGold = amount;
        }
    }
}
