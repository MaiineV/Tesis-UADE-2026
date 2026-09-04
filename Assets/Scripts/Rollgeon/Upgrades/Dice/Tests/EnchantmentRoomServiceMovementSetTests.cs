using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Dice;
using Rollgeon.Economy;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Tests
{
    /// <summary>
    /// Oferta por set visible (carousel del altar): con <see cref="EnchantmentTargetSet.MovementDie"/>
    /// la palanca ofrece SOLO encantamientos de Movimiento y la confirmación va al carril del
    /// dado de Movimiento; con <see cref="EnchantmentTargetSet.CombatDice"/> nunca ofrece uno de
    /// Movimiento. Servicios reales (mismo harness que <see cref="EnchantmentRoomServiceOfferTests"/>).
    /// </summary>
    [TestFixture]
    public class EnchantmentRoomServiceMovementSetTests
    {
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();
        private DiceEnchantmentService _enchSvc;
        private EnchantmentRoomService _roomSvc;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            _roomSvc?.Dispose();
            _enchSvc?.Dispose();
            _roomSvc = null;
            _enchSvc = null;
            ServiceLocator.Clear();
            foreach (var obj in _created) if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            _created.Clear();
        }

        private EnchantmentSO MakeEnchantment(string id, EnchantmentCategory category)
        {
            var ench = ScriptableObject.CreateInstance<EnchantmentSO>();
            ench.name = id;
            _created.Add(ench);
            typeof(UpgradeSO).GetField("_upgradeId", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(ench, id);
            typeof(EnchantmentSO).GetField("_category", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(ench, category);
            return ench;
        }

        private FakeEconomy BuildHarness(int startingGold, params EnchantmentSO[] poolEntries)
        {
            var bag = ScriptableObject.CreateInstance<DiceBagSO>();
            bag.Dice = new List<DiceType> { DiceType.D6, DiceType.D8 };
            _created.Add(bag);

            var config = ScriptableObject.CreateInstance<EnchantmentConfigSO>();
            _created.Add(config);
            typeof(EnchantmentConfigSO).GetField("_baseCost", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(config, 10);

            var pool = ScriptableObject.CreateInstance<EnchantmentPoolSO>();
            pool.Entries = poolEntries.Select(e => new WeightedEnchantment { Enchantment = e, Weight = 1f }).ToList();
            _created.Add(pool);

            _enchSvc = new DiceEnchantmentService(config);
            _enchSvc.InitializeFromBag(bag);
            ServiceLocator.AddService<IDiceEnchantmentService>(_enchSvc, ServiceScope.Global);

            var economy = new FakeEconomy(startingGold);
            ServiceLocator.AddService<IEconomyService>(economy, ServiceScope.Global);

            _roomSvc = new EnchantmentRoomService(config, pool, altarPrefab: null);
            _roomSvc.ConfigureForTests(new System.Random(7));
            return economy;
        }

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

        private EnchantmentSO[] MixedPool()
        {
            return new[]
            {
                MakeEnchantment("ench.combat.a", EnchantmentCategory.Ataque),
                MakeEnchantment("ench.combat.b", EnchantmentCategory.Control),
                MakeEnchantment("ench.combat.c", EnchantmentCategory.Recursos),
                MakeEnchantment("ench.combat.d", EnchantmentCategory.Caos),
                MakeEnchantment("ench.move.a", EnchantmentCategory.Movimiento),
                MakeEnchantment("ench.move.b", EnchantmentCategory.Movimiento),
                MakeEnchantment("ench.move.c", EnchantmentCategory.Movimiento),
                MakeEnchantment("ench.move.d", EnchantmentCategory.Movimiento),
            };
        }

        [Test]
        public void RollOffer_MovementSet_OffersOnlyMovementEnchantmentsAndCharges()
        {
            var economy = BuildHarness(100, MixedPool());

            var result = _roomSvc.RollOffer(Guid.NewGuid(), EnchantmentTargetSet.MovementDie);

            Assert.IsTrue(result.Success, result.ErrorMessage);
            Assert.AreEqual(EnchantmentRoomService.OfferSize, result.Offer.Options.Count);
            Assert.AreEqual(EnchantmentTargetSet.MovementDie, result.Offer.TargetSet);
            Assert.IsTrue(result.Offer.Options.All(o => o.Category == EnchantmentCategory.Movimiento));
            Assert.AreEqual(90, economy.CurrentGold);
        }

        [Test]
        public void RollOffer_CombatSet_NeverOffersMovementEnchantments()
        {
            BuildHarness(100, MixedPool());

            for (int i = 0; i < 10; i++)
            {
                var result = _roomSvc.RollOffer(Guid.NewGuid());
                Assert.IsTrue(result.Success, result.ErrorMessage);
                Assert.AreEqual(EnchantmentTargetSet.CombatDice, result.Offer.TargetSet);
                Assert.IsTrue(result.Offer.Options.All(o => o.Category != EnchantmentCategory.Movimiento),
                    $"Roll {i} ofreció uno de Movimiento en el set de combate.");
            }
        }

        [Test]
        public void RollOffer_MovementSet_WithoutMovementEntries_FailsWithoutCharging()
        {
            var economy = BuildHarness(100,
                MakeEnchantment("ench.combat.a", EnchantmentCategory.Ataque),
                MakeEnchantment("ench.combat.b", EnchantmentCategory.Control));

            var result = _roomSvc.RollOffer(Guid.NewGuid(), EnchantmentTargetSet.MovementDie);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(100, economy.CurrentGold);
        }

        [Test]
        public void ConfirmChoice_MovementOffer_AppendsToTheMovementLaneIgnoringBagIndex()
        {
            BuildHarness(100, MixedPool());
            var offer = _roomSvc.RollOffer(Guid.NewGuid(), EnchantmentTargetSet.MovementDie);
            Assert.IsTrue(offer.Success);

            var confirm = _roomSvc.ConfirmChoice(0, bagIndex: 1);

            Assert.IsTrue(confirm.Success, confirm.ErrorMessage);
            Assert.AreEqual(1, _enchSvc.Bag.GetEnchantmentCount(EnchantmentSlotRef.MovementDieSlot));
            Assert.AreEqual(0, _enchSvc.Bag.GetEnchantmentCount(1));
            Assert.AreSame(offer.Offer.Options[0], _enchSvc.Bag.GetEnchantmentAt(EnchantmentSlotRef.MovementDieSlot, 0));
            Assert.IsNull(_roomSvc.CurrentOffer);
        }

        [Test]
        public void ConfirmChoice_CombatOffer_RejectsTheMovementSentinel()
        {
            BuildHarness(100, MixedPool());
            Assert.IsTrue(_roomSvc.RollOffer(Guid.NewGuid()).Success);

            var confirm = _roomSvc.ConfirmChoice(0, EnchantmentSlotRef.MovementDieSlot);

            Assert.IsFalse(confirm.Success);
            Assert.IsNotNull(_roomSvc.CurrentOffer, "La oferta se conserva cuando el dado es inválido.");
        }
    }
}
