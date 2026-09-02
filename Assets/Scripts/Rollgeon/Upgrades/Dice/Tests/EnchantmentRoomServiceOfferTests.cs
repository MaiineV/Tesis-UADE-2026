using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Dice;
using Rollgeon.Economy;
using Rollgeon.Upgrades.Dice.Filters;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Tests
{
    /// <summary>
    /// Cobertura del flujo de oferta paga palanca-primero (Feature#0053):
    /// <see cref="EnchantmentRoomService.RollOffer"/> (pagar → revelar hasta 3
    /// opciones distintas, cada una válida para al menos un dado) +
    /// <see cref="EnchantmentRoomService.ConfirmChoice"/> (opción + dado →
    /// append). Usa un <see cref="DiceEnchantmentService"/> real (no fake) para
    /// que el pre-filtro de coherencia (<c>ValidateApply</c>) sea el mismo que
    /// corre en producción.
    /// </summary>
    [TestFixture]
    public class EnchantmentRoomServiceOfferTests
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
            // El constructor de EnchantmentRoomService se suscribe a OnRoomEntered —
            // Dispose() desuscribe. DiceEnchantmentService también tiene listeners propios.
            _roomSvc?.Dispose();
            _enchSvc?.Dispose();
            _roomSvc = null;
            _enchSvc = null;
            ServiceLocator.Clear();

            foreach (var obj in _created)
            {
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            }
            _created.Clear();
        }

        // ====================================================================
        // Helpers
        // ====================================================================

        private DiceBagSO MakeBag(params DiceType[] dice)
        {
            var bag = ScriptableObject.CreateInstance<DiceBagSO>();
            bag.Dice = new List<DiceType>(dice);
            bag.name = "TestBag";
            _created.Add(bag);
            return bag;
        }

        private EnchantmentSO MakeEnchantment(string id, IFaceFilter filter = null, params DiceType[] allowedTypes)
        {
            var ench = ScriptableObject.CreateInstance<EnchantmentSO>();
            ench.name = id;
            _created.Add(ench);

            typeof(UpgradeSO).GetField("_upgradeId", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(ench, id);
            typeof(EnchantmentSO).GetField("_allowedDiceTypes", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(ench, new List<DiceType>(allowedTypes));
            if (filter != null)
            {
                typeof(EnchantmentSO).GetField("_faceFilter", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(ench, filter);
            }
            return ench;
        }

        private EnchantmentConfigSO MakeConfig(int baseCost, float mult = 1f, int minFacesAfterApply = 1)
        {
            var config = ScriptableObject.CreateInstance<EnchantmentConfigSO>();
            _created.Add(config);
            typeof(EnchantmentConfigSO).GetField("_baseCost", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(config, baseCost);
            typeof(EnchantmentConfigSO).GetField("_reEnchantCostMultiplier", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(config, mult);
            typeof(EnchantmentConfigSO).GetField("_minFacesAfterApply", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(config, minFacesAfterApply);
            return config;
        }

        private EnchantmentPoolSO MakePool(params EnchantmentSO[] enchantments)
        {
            var pool = ScriptableObject.CreateInstance<EnchantmentPoolSO>();
            pool.Entries = enchantments
                .Select(e => new WeightedEnchantment { Enchantment = e, Weight = 1f })
                .ToList();
            _created.Add(pool);
            return pool;
        }

        /// <summary>
        /// Arma el par de servicios reales (mismo patrón que los bootstraps de producción,
        /// que comparten UNA <see cref="EnchantmentConfigSO"/> entre ambos) + un
        /// <see cref="FakeEconomy"/> registrado en el ServiceLocator. Devuelve el fake para
        /// que el test pueda leer/asertar el oro.
        /// </summary>
        private FakeEconomy BuildHarness(EnchantmentConfigSO config, EnchantmentPoolSO pool, DiceBagSO bag,
            int startingGold, int seed = 42)
        {
            _enchSvc = new DiceEnchantmentService(config);
            _enchSvc.InitializeFromBag(bag);
            ServiceLocator.AddService<IDiceEnchantmentService>(_enchSvc, ServiceScope.Global);

            var economy = new FakeEconomy(startingGold);
            ServiceLocator.AddService<IEconomyService>(economy, ServiceScope.Global);

            _roomSvc = new EnchantmentRoomService(config, pool, altarPrefab: null);
            _roomSvc.ConfigureForTests(new System.Random(seed));

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

        // ====================================================================
        // RollOffer — happy path / cobro
        // ====================================================================

        [Test]
        public void RollOffer_EnoughGoldAndWidePool_ReturnsThreeDistinctOptionsAndChargesOnce()
        {
            // Arrange
            var bag = MakeBag(DiceType.D6);
            var config = MakeConfig(baseCost: 10);
            var pool = MakePool(
                MakeEnchantment("ench.a"),
                MakeEnchantment("ench.b"),
                MakeEnchantment("ench.c"),
                MakeEnchantment("ench.d"),
                MakeEnchantment("ench.e"));
            var economy = BuildHarness(config, pool, bag, startingGold: 100);

            // Act
            var result = _roomSvc.RollOffer(Guid.NewGuid());

            // Assert
            Assert.IsTrue(result.Success, result.ErrorMessage);
            Assert.AreEqual(EnchantmentRoomService.OfferSize, result.Offer.Options.Count);
            CollectionAssert.AllItemsAreUnique(result.Offer.Options);
            Assert.AreEqual(10, result.Offer.GoldPaid);
            Assert.AreEqual(90, economy.CurrentGold, "el costo debe cobrarse una única vez");
            Assert.IsTrue(_roomSvc.CurrentOffer.HasValue);
        }

        [Test]
        public void RollOffer_InsufficientGold_FailsWithoutChargingOrSettingOffer()
        {
            // Arrange
            var bag = MakeBag(DiceType.D6);
            var config = MakeConfig(baseCost: 10);
            var pool = MakePool(MakeEnchantment("ench.a"), MakeEnchantment("ench.b"), MakeEnchantment("ench.c"));
            var economy = BuildHarness(config, pool, bag, startingGold: 5);

            // Act
            var result = _roomSvc.RollOffer(Guid.NewGuid());

            // Assert
            Assert.IsFalse(result.Success);
            Assert.AreEqual(5, economy.CurrentGold, "sin oro suficiente no se cobra nada");
            Assert.IsNull(_roomSvc.CurrentOffer);
        }

        [Test]
        public void RollOffer_NoCompatibleCandidatesInPool_FailsWithoutCharging()
        {
            // Arrange — el pool solo tiene un encantamiento exclusivo de D20 y el bag es D6.
            var bag = MakeBag(DiceType.D6);
            var config = MakeConfig(baseCost: 10);
            var pool = MakePool(MakeEnchantment("ench.only_d20", allowedTypes: DiceType.D20));
            var economy = BuildHarness(config, pool, bag, startingGold: 100);

            // Act
            var result = _roomSvc.RollOffer(Guid.NewGuid());

            // Assert
            Assert.IsFalse(result.Success);
            Assert.AreEqual(100, economy.CurrentGold, "sin candidatos no se cobra el roll");
            Assert.IsNull(_roomSvc.CurrentOffer);
        }

        // ====================================================================
        // RollOffer — pre-filtro de coherencia (≥1 dado válido por opción)
        // ====================================================================

        [Test]
        public void RollOffer_CandidateIncoherentWithEveryDie_NeverOffersIt()
        {
            // Arrange — único dado con "solo pares" ya aplicado; "solo impares"
            // dejaría la intersección vacía en TODOS los dados → filtrado antes
            // de mostrarse.
            var bag = MakeBag(DiceType.D6);
            var config = MakeConfig(baseCost: 10);
            var evens = MakeEnchantment("ench.evens", filter: new ParityFilter { Allowed = Parity.Even });
            var odds = MakeEnchantment("ench.odds", filter: new ParityFilter { Allowed = Parity.Odd });
            var universal = MakeEnchantment("ench.universal");
            var pool = MakePool(odds, universal);
            var economy = BuildHarness(config, pool, bag, startingGold: 100);

            _enchSvc.Apply(0, evens); // pre-condición: ya aplicado, fuera del flujo de la oferta.

            // Act
            var result = _roomSvc.RollOffer(Guid.NewGuid());

            // Assert
            Assert.IsTrue(result.Success, result.ErrorMessage);
            CollectionAssert.DoesNotContain(result.Offer.Options, odds);
            CollectionAssert.AreEquivalent(new[] { universal }, result.Offer.Options);
            Assert.AreEqual(90, economy.CurrentGold);
        }

        [Test]
        public void RollOffer_CandidateValidForAtLeastOneDie_IsOffered()
        {
            // Arrange — dado 0 con "solo pares": "solo impares" es inválido ahí,
            // pero el dado 1 está limpio → la opción se ofrece igual (el filtro
            // es "≥1 dado válido"; la UI marca cuáles).
            var bag = MakeBag(DiceType.D6, DiceType.D6);
            var config = MakeConfig(baseCost: 10);
            var evens = MakeEnchantment("ench.evens", filter: new ParityFilter { Allowed = Parity.Even });
            var odds = MakeEnchantment("ench.odds", filter: new ParityFilter { Allowed = Parity.Odd });
            var pool = MakePool(odds);
            BuildHarness(config, pool, bag, startingGold: 100);

            _enchSvc.Apply(0, evens);

            // Act
            var result = _roomSvc.RollOffer(Guid.NewGuid());

            // Assert
            Assert.IsTrue(result.Success, result.ErrorMessage);
            CollectionAssert.Contains(result.Offer.Options, odds);
        }

        // ====================================================================
        // ConfirmChoice
        // ====================================================================

        [Test]
        public void ConfirmChoice_AppliesChosenOptionToChosenDie_KeepsPreviousAndClearsOffer()
        {
            // Arrange
            var bag = MakeBag(DiceType.D6, DiceType.D8);
            var config = MakeConfig(baseCost: 10);
            var existing = MakeEnchantment("ench.existing");
            var optionX = MakeEnchantment("ench.x");
            var optionY = MakeEnchantment("ench.y");
            var optionZ = MakeEnchantment("ench.z");
            var pool = MakePool(optionX, optionY, optionZ);
            BuildHarness(config, pool, bag, startingGold: 100);

            _enchSvc.Apply(1, existing);
            var offerResult = _roomSvc.RollOffer(Guid.NewGuid());
            Assert.IsTrue(offerResult.Success, offerResult.ErrorMessage);
            var chosenOption = offerResult.Offer.Options[1];

            // Act — la opción 1 va al dado 1 (elección explícita del jugador).
            var chooseResult = _roomSvc.ConfirmChoice(optionIndex: 1, bagIndex: 1);

            // Assert
            Assert.IsTrue(chooseResult.Success, chooseResult.ErrorMessage);
            Assert.AreSame(chosenOption, chooseResult.RolledEnchantment);
            Assert.AreEqual(offerResult.Offer.GoldPaid, chooseResult.GoldPaid);
            Assert.AreEqual(2, _enchSvc.Bag.GetEnchantmentCount(1), "append — no debe pisar lo previo");
            Assert.AreSame(existing, _enchSvc.Bag.GetEnchantmentAt(1, 0));
            Assert.AreSame(chosenOption, _enchSvc.Bag.GetEnchantmentAt(1, 1));
            Assert.AreEqual(0, _enchSvc.Bag.GetEnchantmentCount(0), "el dado no elegido queda intacto");
            Assert.IsNull(_roomSvc.CurrentOffer);
        }

        [Test]
        public void ConfirmChoice_NoActiveOffer_Fails()
        {
            // Arrange
            var bag = MakeBag(DiceType.D6);
            var config = MakeConfig(baseCost: 10);
            var pool = MakePool(MakeEnchantment("ench.a"));
            BuildHarness(config, pool, bag, startingGold: 100);

            // Act
            var result = _roomSvc.ConfirmChoice(0, 0);

            // Assert
            Assert.IsFalse(result.Success);
        }

        [Test]
        public void ConfirmChoice_IncoherentDieForOption_FailsAndKeepsOffer()
        {
            // Arrange — la opción "solo impares" es válida para el dado 1 pero no
            // para el 0 (que ya tiene "solo pares"): confirmar sobre el 0 falla y
            // la oferta se conserva para re-elegir.
            var bag = MakeBag(DiceType.D6, DiceType.D6);
            var config = MakeConfig(baseCost: 10);
            var evens = MakeEnchantment("ench.evens", filter: new ParityFilter { Allowed = Parity.Even });
            var odds = MakeEnchantment("ench.odds", filter: new ParityFilter { Allowed = Parity.Odd });
            var pool = MakePool(odds);
            BuildHarness(config, pool, bag, startingGold: 100);
            _enchSvc.Apply(0, evens);

            var offer = _roomSvc.RollOffer(Guid.NewGuid());
            Assert.IsTrue(offer.Success, offer.ErrorMessage);
            int oddsIndex = offer.Offer.Options.ToList().IndexOf(odds);
            Assert.GreaterOrEqual(oddsIndex, 0, "el pool solo tiene odds — debe estar en la oferta");

            // Act
            var badChoice = _roomSvc.ConfirmChoice(oddsIndex, bagIndex: 0);

            // Assert
            Assert.IsFalse(badChoice.Success);
            Assert.IsTrue(_roomSvc.CurrentOffer.HasValue, "la oferta se conserva tras el fallo");
            Assert.AreEqual(1, _enchSvc.Bag.GetEnchantmentCount(0), "el dado 0 no debe recibir nada");

            // El dado válido sí puede confirmar la misma oferta.
            var goodChoice = _roomSvc.ConfirmChoice(oddsIndex, bagIndex: 1);
            Assert.IsTrue(goodChoice.Success, goodChoice.ErrorMessage);
            Assert.AreSame(odds, _enchSvc.Bag.GetEnchantmentAt(1, 0));
        }

        // ====================================================================
        // Re-roll (DoD tarea 3)
        // ====================================================================

        [Test]
        public void RollOffer_CalledAgainWithoutChoosing_ReplacesOfferAndChargesAgain()
        {
            // Arrange
            var bag = MakeBag(DiceType.D6);
            var config = MakeConfig(baseCost: 10);
            var pool = MakePool(
                MakeEnchantment("ench.a"), MakeEnchantment("ench.b"), MakeEnchantment("ench.c"),
                MakeEnchantment("ench.d"), MakeEnchantment("ench.e"), MakeEnchantment("ench.f"));
            var economy = BuildHarness(config, pool, bag, startingGold: 100);
            var roomId = Guid.NewGuid();

            // Act
            var firstOffer = _roomSvc.RollOffer(roomId);
            var secondOffer = _roomSvc.RollOffer(roomId);

            // Assert
            Assert.IsTrue(firstOffer.Success, firstOffer.ErrorMessage);
            Assert.IsTrue(secondOffer.Success, secondOffer.ErrorMessage);
            Assert.AreEqual(80, economy.CurrentGold, "repetible mientras alcance el oro — se cobra cada vez");
            Assert.IsTrue(_roomSvc.CurrentOffer.HasValue);
            CollectionAssert.AreEqual(secondOffer.Offer.Options, _roomSvc.CurrentOffer.Value.Options,
                "la oferta activa debe ser la del segundo roll, no la del primero");
        }

        // ====================================================================
        // Escalado de costo global por run
        // ====================================================================

        [Test]
        public void ResolveCost_AfterEachRoll_ScalesGlobally()
        {
            // Arrange
            var bag = MakeBag(DiceType.D6, DiceType.D6);
            var config = MakeConfig(baseCost: 10, mult: 2f);
            var pool = MakePool(MakeEnchantment("ench.a"), MakeEnchantment("ench.b"), MakeEnchantment("ench.c"));
            BuildHarness(config, pool, bag, startingGold: 100);
            var roomId = Guid.NewGuid();

            // Act + Assert — el contador es global de la run: cada tirada de la
            // palanca encarece la siguiente, sin importar el dado destino.
            Assert.AreEqual(10, _roomSvc.ResolveCost());
            Assert.IsTrue(_roomSvc.RollOffer(roomId).Success);
            Assert.AreEqual(20, _roomSvc.ResolveCost(), "base × mult^1 tras el primer roll");
            Assert.IsTrue(_roomSvc.RollOffer(roomId).Success);
            Assert.AreEqual(40, _roomSvc.ResolveCost(), "base × mult^2 tras el segundo roll");
        }

        // ====================================================================
        // DoD tarea 1: 3 rolls+confirm sobre el mismo dado → 3 encantamientos.
        // ====================================================================

        [Test]
        public void RollOffer_ThreeTimesOnSameDie_AccumulatesThreeEnchantments()
        {
            // Arrange
            var bag = MakeBag(DiceType.D6);
            var config = MakeConfig(baseCost: 5); // sin escalado — solo importa poder pagar 3 veces.
            var pool = MakePool(
                MakeEnchantment("ench.a"), MakeEnchantment("ench.b"), MakeEnchantment("ench.c"),
                MakeEnchantment("ench.d"), MakeEnchantment("ench.e"), MakeEnchantment("ench.f"),
                MakeEnchantment("ench.g"), MakeEnchantment("ench.h"), MakeEnchantment("ench.i"));
            BuildHarness(config, pool, bag, startingGold: 1000);
            var roomId = Guid.NewGuid();
            var applied = new List<EnchantmentSO>();

            // Act
            for (int i = 0; i < 3; i++)
            {
                var offer = _roomSvc.RollOffer(roomId);
                Assert.IsTrue(offer.Success, $"roll #{i} falló: {offer.ErrorMessage}");

                var choice = _roomSvc.ConfirmChoice(0, bagIndex: 0);
                Assert.IsTrue(choice.Success, $"confirm #{i} falló: {choice.ErrorMessage}");
                applied.Add(choice.RolledEnchantment);
            }

            // Assert
            Assert.AreEqual(3, _enchSvc.Bag.GetEnchantmentCount(0));
            CollectionAssert.AreEqual(applied, _enchSvc.Bag.GetEnchantments(0));
        }
        // ====================================================================
        // RollOffer — slot maldito garantizado (Moneda Maldita)
        // ====================================================================

        private sealed class FakeWeightService : IEnchantmentWeightModifierService
        {
            public float Multiplier = 1f;
            public void Register(string sourceId, float cursedWeightMultiplier) { }
            public void Unregister(string sourceId) { }
            public float ResolveCursedMultiplier() => Multiplier;
        }

        private EnchantmentSO MakeCursed(string id, IFaceFilter filter = null)
        {
            var ench = MakeEnchantment(id, filter);
            typeof(EnchantmentSO).GetField("_capabilities", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(ench, new List<IEnchantmentCapability> { new CapCursed() });
            return ench;
        }

        private EnchantmentSO[] MakeManyPlain(int count)
        {
            var result = new EnchantmentSO[count];
            for (int i = 0; i < count; i++) result[i] = MakeEnchantment("ench.plain." + i);
            return result;
        }

        private static bool AnyCursed(IReadOnlyList<EnchantmentSO> options)
        {
            for (int i = 0; i < options.Count; i++)
                if (EnchantmentPoolSO.IsCursedForPool(options[i])) return true;
            return false;
        }

        [Test]
        public void RollOffer_CursedMultiplierActive_EveryOfferIncludesACursedOption()
        {
            // Arrange — 8 normales + 1 maldito: sin garantía, 2 de cada 3 ofertas
            // saldrían sin maldito aun con el ×3.
            var bag = MakeBag(DiceType.D6);
            var config = MakeConfig(baseCost: 10);
            var plain = MakeManyPlain(8);
            var cursed = MakeCursed("ench.cursed");
            var pool = MakePool(plain.Concat(new[] { cursed }).ToArray());
            BuildHarness(config, pool, bag, startingGold: 100000);
            ServiceLocator.AddService<IEnchantmentWeightModifierService>(
                new FakeWeightService { Multiplier = 3f }, ServiceScope.Global);

            for (int seed = 1; seed <= 40; seed++)
            {
                _roomSvc.ConfigureForTests(new System.Random(seed));
                _roomSvc.ClearOffer();

                // Act
                var result = _roomSvc.RollOffer(Guid.NewGuid());

                // Assert
                Assert.IsTrue(result.Success, result.ErrorMessage);
                Assert.AreEqual(EnchantmentRoomService.OfferSize, result.Offer.Options.Count, "seed " + seed);
                CollectionAssert.AllItemsAreUnique(result.Offer.Options, "seed " + seed);
                Assert.IsTrue(AnyCursed(result.Offer.Options), "seed " + seed + ": oferta sin maldito con Moneda Maldita activa");
            }
        }

        [Test]
        public void RollOffer_NoCursedMultiplier_OffersCanComeWithoutCursed()
        {
            // Arrange — mismo pool, sin item: la garantía NO debe activarse.
            var bag = MakeBag(DiceType.D6);
            var config = MakeConfig(baseCost: 10);
            var plain = MakeManyPlain(8);
            var cursed = MakeCursed("ench.cursed");
            var pool = MakePool(plain.Concat(new[] { cursed }).ToArray());
            BuildHarness(config, pool, bag, startingGold: 100000);
            ServiceLocator.AddService<IEnchantmentWeightModifierService>(
                new FakeWeightService { Multiplier = 1f }, ServiceScope.Global);

            int withoutCursed = 0;
            for (int seed = 1; seed <= 40; seed++)
            {
                _roomSvc.ConfigureForTests(new System.Random(seed));
                _roomSvc.ClearOffer();
                var result = _roomSvc.RollOffer(Guid.NewGuid());
                Assert.IsTrue(result.Success, result.ErrorMessage);
                if (!AnyCursed(result.Offer.Options)) withoutCursed++;
            }

            Assert.Greater(withoutCursed, 0, "sin multiplicador la oferta no debe forzar malditos");
        }

        [Test]
        public void RollOffer_CursedGuarantee_CursedIncoherentWithEveryDie_LeavesOfferIntact()
        {
            // Arrange — el único maldito es "solo impares" y el único dado ya tiene
            // "solo pares": no hay maldito válido → la oferta queda con los 3 normales.
            var bag = MakeBag(DiceType.D6);
            var config = MakeConfig(baseCost: 10);
            var evens = MakeEnchantment("ench.evens", filter: new ParityFilter { Allowed = Parity.Even });
            var plain = MakeManyPlain(5);
            var cursedOdds = MakeCursed("ench.cursed.odds", filter: new ParityFilter { Allowed = Parity.Odd });
            var pool = MakePool(plain.Concat(new[] { cursedOdds }).ToArray());
            BuildHarness(config, pool, bag, startingGold: 100000);
            ServiceLocator.AddService<IEnchantmentWeightModifierService>(
                new FakeWeightService { Multiplier = 3f }, ServiceScope.Global);
            _enchSvc.Apply(0, evens);

            // Act
            var result = _roomSvc.RollOffer(Guid.NewGuid());

            // Assert
            Assert.IsTrue(result.Success, result.ErrorMessage);
            Assert.AreEqual(EnchantmentRoomService.OfferSize, result.Offer.Options.Count);
            CollectionAssert.DoesNotContain(result.Offer.Options, cursedOdds);
            CollectionAssert.AllItemsAreUnique(result.Offer.Options);
        }
    }
}
