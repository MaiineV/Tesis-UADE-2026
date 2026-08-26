using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Cashier;
using Rollgeon.Combat.Threat;
using Rollgeon.Economy;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>Tabla compartida por los tres fixtures; el builder vive en Editor.</summary>
    internal static class CashierFicha
    {
        public static List<CashierGoldTier> Tiers() => new List<CashierGoldTier>
        {
            new CashierGoldTier { MinGold = 0,   ColumnSize = 1, Damage = 14 },
            new CashierGoldTier { MinGold = 40,  ColumnSize = 3, Damage = 28 },
            new CashierGoldTier { MinGold = 120, ColumnSize = 3, Damage = 35 },
        };
    }

    [TestFixture]
    public class CashierRakeTierTableTests
    {

        [TestCase(0,   14)]
        [TestCase(39,  14)]  // borde: un oro menos que el umbral sigue siendo el escalón pobre.
        [TestCase(40,  28)]  // borde: el umbral es inclusive.
        [TestCase(67,  28)]  // el oro con el que se entra de verdad al piso 2 ya paga el medio.
        [TestCase(119, 28)]
        [TestCase(120, 35)]  // borde: el umbral es inclusive.
        public void test_goldTiers_realFloorTwoWallet_landsAboveTheCheapestTier(int gold, int expectedDamage)
        {
            var tiers = CashierFicha.Tiers();

            var tier = CashierGoldTierTable.Resolve(tiers, gold, stepDown: 0);

            Assert.AreEqual(expectedDamage, tier.Damage, $"Daño con {gold} de oro.");
        }

        [TestCase(0,  0, 14)]  // sin rastrillo, el pobre paga lo de siempre…
        [TestCase(0,  1, 28)]  // …y tres rondas después paga como si tuviera 40 de oro.
        [TestCase(0,  2, 35)]
        [TestCase(40, 1, 35)]  // el que ya está en el medio salta al techo.
        public void test_stepUp_raisesTheTierWithoutLookingAtGold(int gold, int stepUp, int expectedDamage)
        {
            var tiers = CashierFicha.Tiers();

            var tier = CashierGoldTierTable.Resolve(tiers, gold, stepDown: 0, stepUp: stepUp);

            Assert.AreEqual(expectedDamage, tier.Damage);
        }

        [Test]
        public void test_stepUpBeyondTheTable_clampsToTheRichestTier()
        {
            var tiers = CashierFicha.Tiers();

            var tier = CashierGoldTierTable.Resolve(tiers, gold: 0, stepDown: 0, stepUp: 99, out int rank);

            Assert.AreEqual(2, rank, "El rastrillo no puede inventar escalones que la tabla no tiene.");
            Assert.AreEqual(35, tier.Damage, "El techo de daño de piso 2 sigue siendo 35.");
        }

        [Test]
        public void test_bribeAppliesAfterTheRakeIsClamped_soItNeverStopsWorking()
        {
            // El reloj lleva 10 escalones sobre una tabla de 3.
            var tiers = CashierFicha.Tiers();

            var tier = CashierGoldTierTable.Resolve(tiers, gold: 0, stepDown: 1, stepUp: 10, out int rank);

            Assert.AreEqual(1, rank,
                "Si el soborno se restara del rastrillo crudo (0+10-1) el descuento sería invisible " +
                "para siempre y pagar 35 de oro no compraría nada.");
            Assert.AreEqual(28, tier.Damage);
        }

        [Test]
        public void test_bribeCancelsExactlyOneRakeStep()
        {
            var tiers = CashierFicha.Tiers();

            var unpaid = CashierGoldTierTable.Resolve(tiers, gold: 0, stepDown: 0, stepUp: 1);
            var paid = CashierGoldTierTable.Resolve(tiers, gold: 0, stepDown: 1, stepUp: 1);

            Assert.AreEqual(28, unpaid.Damage, "Sin pagar, el reloj ya le subió un escalón.");
            Assert.AreEqual(14, paid.Damage, "El soborno devuelve el escalón que puso el reloj, no más.");
        }

        [Test]
        public void test_negativeStepUp_isIgnored_neverDemotesTheBoss()
        {
            var tiers = CashierFicha.Tiers();

            var tier = CashierGoldTierTable.Resolve(tiers, gold: 120, stepDown: 0, stepUp: -5, out int rank);

            Assert.AreEqual(2, rank);
            Assert.AreEqual(35, tier.Damage);
        }

        [Test]
        public void test_resolveWithoutStepUp_behavesLikeBefore()
        {
            // El overload sin rastrillo lo siguen usando otros call sites.
            var tiers = CashierFicha.Tiers();

            var tier = CashierGoldTierTable.Resolve(tiers, gold: 120, stepDown: 1, out int rank);

            Assert.AreEqual(1, rank, "Sin rastrillo el resultado es el de siempre.");
            Assert.AreEqual(28, tier.Damage);
        }
    }

    [TestFixture]
    public class CashierRakeClockTests
    {
        private CashierLedgerService _ledger;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            TypedEvent<DamageResolvedPayload>.Clear();
            ServiceLocator.Clear();

            // 200 de oro: dos sobornos de 35, lo que pide el test de renovación de la cuota.
            ServiceLocator.AddService<IEconomyService>(new FakeEconomyService(200));
            _ledger = new CashierLedgerService();
        }

        [TearDown]
        public void TearDown()
        {
            _ledger.Dispose();
            TypedEvent<DamageResolvedPayload>.Clear();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        /// <summary><c>TurnOrderService</c> dispara el evento con el índice de ronda 0-based.</summary>
        private static void FireRound(int roundIndex) =>
            EventManager.Trigger(EventName.OnTurnQueueBuilt, new List<Guid>(), roundIndex);

        private void FireRoundsUpTo(int roundIndex)
        {
            for (int round = 0; round <= roundIndex; round++) FireRound(round);
        }

        private int Net() => _ledger.DamageStepUp - _ledger.DamageStepDown;

        [Test]
        public void test_rake_startsAtZero_soTheFirstRoundsAreTheAuthoredTier()
        {
            Assert.AreEqual(0, _ledger.DamageStepUp);
        }

        [TestCase(0, 0)]
        [TestCase(1, 0)]
        [TestCase(2, 0)]
        [TestCase(3, 1)]  // primera subida: tres rondas cumplidas.
        [TestCase(5, 1)]
        [TestCase(6, 2)]
        [TestCase(9, 3)]
        public void test_rake_climbsOneStepEveryThreeRounds(int roundIndex, int expectedStepUp)
        {
            FireRoundsUpTo(roundIndex);

            Assert.AreEqual(expectedStepUp, _ledger.DamageStepUp,
                $"En la ronda {roundIndex} el rastrillo debería llevar {expectedStepUp} escalón(es).");
        }

        [Test]
        public void test_rake_readsTheAbsoluteRoundIndex_soALateCreatedLedgerIsNotBehind()
        {
            // El servicio es lazy: nace en el primer tick del jefe, tarde.
            FireRound(7);

            Assert.AreEqual(2, _ledger.DamageStepUp,
                "El rastrillo se deriva del índice de ronda, no de cuántos eventos escuchó.");
        }

        [Test]
        public void test_rake_disabledWhenCadenceIsZeroOrLess()
        {
            _ledger.RakeRoundsPerStep = 0;

            FireRoundsUpTo(12);

            Assert.AreEqual(0, _ledger.DamageStepUp, "Cadencia <= 0 apaga el reloj (modo test/debug).");
        }

        [Test]
        public void test_rakeCadenceMatchesTheBribeWindow_soThePayoffIsAQuotaNotInsurance()
        {
            Assert.AreEqual(3, _ledger.RakeRoundsPerStep);
            Assert.AreEqual(3, _ledger.BribeRounds);
            Assert.AreEqual(35, _ledger.BribeCost);
        }

        [Test]
        public void test_bribingEveryThreeRounds_holdsTheTierFlat()
        {
            FireRoundsUpTo(3);
            Assert.AreEqual(1, _ledger.DamageStepUp);

            Assert.IsTrue(_ledger.TryBribe());

            Assert.AreEqual(0, Net(), "Pagar en la ronda 3 devuelve el escalón que puso el reloj.");
            FireRound(4);
            Assert.AreEqual(0, Net());
            FireRound(5);
            Assert.AreEqual(0, Net());
        }

        [Test]
        public void test_skippingTheBribe_letsTheClockGainGround()
        {
            FireRoundsUpTo(3);
            Assert.IsTrue(_ledger.TryBribe());

            // La ventana se cae justo cuando el reloj vuelve a sumar.
            FireRound(4);
            FireRound(5);
            FireRound(6);

            Assert.AreEqual(0, _ledger.DamageStepDown, "Tres rondas y la cuota venció.");
            Assert.AreEqual(2, Net(),
                "Sin renovar, el jefe queda dos escalones arriba de lo que dice el oro.");
            Assert.IsTrue(_ledger.TryBribe());
            Assert.AreEqual(1, Net(), "Renovar tarde compra un escalón, no los dos perdidos.");
        }

        [Test]
        public void test_rake_resetsOnCombatEnd_soItDoesNotLeakIntoTheNextFight()
        {
            FireRoundsUpTo(6);
            Assert.AreEqual(2, _ledger.DamageStepUp);

            EventManager.Trigger(EventName.OnCombatEnd);

            Assert.AreEqual(0, _ledger.DamageStepUp, "La pelea siguiente arranca con el reloj en cero.");
        }
    }

    [TestFixture]
    public class CashierRakeColumnNodeTests
    {
        private const int RoomSize = 9;

        private GridManager _grid;
        private ThreatenedAreaService _threat;
        private FakeEconomyService _economy;
        private FakeCashierLedgerService _ledger;
        private Guid _boss;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(RoomSize, RoomSize));
            ServiceLocator.AddService<IGridManager>(_grid);

            _threat = new ThreatenedAreaService();
            _threat.Register();

            _player = Guid.NewGuid();
            _boss = Guid.NewGuid();
            _grid.Register(_player, new GridCoord(4, 4));
            _grid.Register(_boss, new GridCoord(8, 4));

            _economy = new FakeEconomyService();
            ServiceLocator.AddService<IEconomyService>(_economy);

            _ledger = new FakeCashierLedgerService();
            ServiceLocator.AddService<ICashierLedgerService>(_ledger);
        }

        [TearDown]
        public void TearDown()
        {
            // El nodo de marca crea el GameObject del overlay: sin limpiarlo queda huérfano.
            if (ServiceLocator.TryGetService<IThreatOverlayService>(out var overlay)
                && overlay is IDisposable disposable)
            {
                disposable.Dispose();
            }
            var leftover = GameObject.Find("ThreatTelegraphOverlay");
            if (leftover != null) UnityEngine.Object.DestroyImmediate(leftover);

            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private AINode_TelegraphMarkGoldScaled NewNode() => new AINode_TelegraphMarkGoldScaled
        {
            Shape = ThreatShape.Column,
            Tiers = CashierFicha.Tiers(),
        };

        private AIContext NewContext() => new AIContext
        {
            SelfGuid = _boss,
            PlayerGuid = _player,
            Grid = _grid,
            SelfMaxHp = 190,
            Rng = new System.Random(7),
        };

        private int MarkedWidth()
        {
            var xs = new HashSet<int>();
            foreach (var coord in _threat.GetPendingTiles(_boss)) xs.Add(coord.X);
            return xs.Count;
        }

        private int MarkedDamage()
        {
            Assert.IsTrue(_threat.TryConsume(_boss, out var area), "El jefe no dejó área marcada.");
            return area.Damage;
        }

        [Test]
        public void test_brokePlayerWithTheClockRunning_stillGetsThreatened()
        {
            // Jugador sin un peso, ronda 3.
            _economy.ResetTo(0);
            _ledger.DamageStepUp = 1;

            var result = NewNode().Tick(NewContext());

            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(3, MarkedWidth(), "El escalón que puso el reloj también ensancha la franja.");
            Assert.AreEqual(28, MarkedDamage(),
                "Sin rastrillo un jugador pobre dejaba al Cajero clavado en 14 toda la pelea.");
        }

        [Test]
        public void test_rakeAndBribeCancelOut()
        {
            _economy.ResetTo(0);
            _ledger.DamageStepUp = 1;
            _ledger.DamageStepDown = 1;

            NewNode().Tick(NewContext());

            Assert.AreEqual(14, MarkedDamage(), "Pagar la cuota devuelve el escalón que puso el reloj.");
        }

        [Test]
        public void test_rakeDisabled_ignoresTheLedgerClock()
        {
            _economy.ResetTo(0);
            _ledger.DamageStepUp = 2;
            var node = NewNode();
            node.ApplyRakeStepUp = false;

            node.Tick(NewContext());

            Assert.AreEqual(14, MarkedDamage(), "Apagado, el escalón vuelve a salir sólo del oro.");
        }

        [Test]
        public void test_exposesTheRakeSeparatelyFromTheResolvedTier()
        {
            _economy.ResetTo(40);
            _ledger.DamageStepUp = 1;

            var node = NewNode();
            node.Tick(NewContext());

            Assert.AreEqual(1, node.LastStepUp, "Cuánto del escalón lo puso el reloj…");
            Assert.AreEqual(2, node.LastRank, "…y en cuál terminó parado.");
            Assert.AreEqual(40, node.LastGold);
        }

        [Test]
        public void test_withoutLedger_fallsBackToTheGoldTier_insteadOfNotAttacking()
        {
            ServiceLocator.RemoveService<ICashierLedgerService>();
            _economy.ResetTo(40);

            var result = NewNode().Tick(NewContext());

            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(28, MarkedDamage(),
                "Sin ledger no hay reloj ni soborno, pero el jefe amenaza igual.");
        }
    }
}
