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
        /// <summary>&lt;40 ⇒ Size 1 / 14, 40-119 ⇒ Size 3 / 28, ≥120 ⇒ Size 3 / 35.</summary>
        public static List<CashierGoldTier> Tiers() => new List<CashierGoldTier>
        {
            new CashierGoldTier { MinGold = 0,   ColumnSize = 1, Damage = 14 },
            new CashierGoldTier { MinGold = 40,  ColumnSize = 3, Damage = 28 },
            new CashierGoldTier { MinGold = 120, ColumnSize = 3, Damage = 35 },
        };
    }

    /// <summary>El escalón sube +1 cada 3 rondas sin mirar el oro; sólo el soborno lo baja.</summary>
    [TestFixture]
    public class CashierRakeTierTableTests
    {
        // ---- Umbrales nuevos ---------------------------------------------

        [TestCase(0,   14)]
        [TestCase(39,  14)]  // borde: un oro menos que el umbral sigue siendo el escalón pobre.
        [TestCase(40,  28)]  // borde: el umbral es inclusive.
        [TestCase(67,  28)]  // el oro con el que se entra de verdad al piso 2 ya paga el medio.
        [TestCase(119, 28)]
        [TestCase(120, 35)]  // borde: el umbral es inclusive.
        public void test_goldTiers_realFloorTwoWallet_landsAboveTheCheapestTier(int gold, int expectedDamage)
        {
            // Arrange
            var tiers = CashierFicha.Tiers();

            // Act
            var tier = CashierGoldTierTable.Resolve(tiers, gold, stepDown: 0);

            // Assert
            Assert.AreEqual(expectedDamage, tier.Damage, $"Daño con {gold} de oro.");
        }

        // ---- El rastrillo contra el soborno --------------------------------

        [TestCase(0,  0, 14)]  // sin rastrillo, el pobre paga lo de siempre…
        [TestCase(0,  1, 28)]  // …y tres rondas después paga como si tuviera 40 de oro.
        [TestCase(0,  2, 35)]
        [TestCase(40, 1, 35)]  // el que ya está en el medio salta al techo.
        public void test_stepUp_raisesTheTierWithoutLookingAtGold(int gold, int stepUp, int expectedDamage)
        {
            // Arrange
            var tiers = CashierFicha.Tiers();

            // Act
            var tier = CashierGoldTierTable.Resolve(tiers, gold, stepDown: 0, stepUp: stepUp);

            // Assert
            Assert.AreEqual(expectedDamage, tier.Damage);
        }

        [Test]
        public void test_stepUpBeyondTheTable_clampsToTheRichestTier()
        {
            // Arrange
            var tiers = CashierFicha.Tiers();

            // Act
            var tier = CashierGoldTierTable.Resolve(tiers, gold: 0, stepDown: 0, stepUp: 99, out int rank);

            // Assert
            Assert.AreEqual(2, rank, "El rastrillo no puede inventar escalones que la tabla no tiene.");
            Assert.AreEqual(35, tier.Damage, "El techo de daño de piso 2 sigue siendo 35.");
        }

        [Test]
        public void test_bribeAppliesAfterTheRakeIsClamped_soItNeverStopsWorking()
        {
            // Arrange — el reloj lleva 10 escalones sobre una tabla de 3.
            var tiers = CashierFicha.Tiers();

            // Act
            var tier = CashierGoldTierTable.Resolve(tiers, gold: 0, stepDown: 1, stepUp: 10, out int rank);

            // Assert
            Assert.AreEqual(1, rank,
                "Si el soborno se restara del rastrillo crudo (0+10-1) el descuento sería invisible " +
                "para siempre y pagar 35 de oro no compraría nada.");
            Assert.AreEqual(28, tier.Damage);
        }

        [Test]
        public void test_bribeCancelsExactlyOneRakeStep()
        {
            // Arrange
            var tiers = CashierFicha.Tiers();

            // Act
            var unpaid = CashierGoldTierTable.Resolve(tiers, gold: 0, stepDown: 0, stepUp: 1);
            var paid = CashierGoldTierTable.Resolve(tiers, gold: 0, stepDown: 1, stepUp: 1);

            // Assert
            Assert.AreEqual(28, unpaid.Damage, "Sin pagar, el reloj ya le subió un escalón.");
            Assert.AreEqual(14, paid.Damage, "El soborno devuelve el escalón que puso el reloj, no más.");
        }

        [Test]
        public void test_negativeStepUp_isIgnored_neverDemotesTheBoss()
        {
            // Arrange
            var tiers = CashierFicha.Tiers();

            // Act
            var tier = CashierGoldTierTable.Resolve(tiers, gold: 120, stepDown: 0, stepUp: -5, out int rank);

            // Assert
            Assert.AreEqual(2, rank);
            Assert.AreEqual(35, tier.Damage);
        }

        [Test]
        public void test_resolveWithoutStepUp_behavesLikeBefore()
        {
            // Arrange — el overload sin rastrillo lo siguen usando otros call sites.
            var tiers = CashierFicha.Tiers();

            // Act
            var tier = CashierGoldTierTable.Resolve(tiers, gold: 120, stepDown: 1, out int rank);

            // Assert
            Assert.AreEqual(1, rank, "Sin rastrillo el resultado es el de siempre.");
            Assert.AreEqual(28, tier.Damage);
        }
    }

    /// <summary><see cref="CashierLedgerService"/> contando rondas, y la cuota que lo compensa.</summary>
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

        /// <summary>Escalones netos que el jefe se corrió respecto de lo que dice el oro.</summary>
        private int Net() => _ledger.DamageStepUp - _ledger.DamageStepDown;

        [Test]
        public void test_rake_startsAtZero_soTheFirstRoundsAreTheAuthoredTier()
        {
            // Assert — sin ninguna ronda todavía.
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
            // Act
            FireRoundsUpTo(roundIndex);

            // Assert
            Assert.AreEqual(expectedStepUp, _ledger.DamageStepUp,
                $"En la ronda {roundIndex} el rastrillo debería llevar {expectedStepUp} escalón(es).");
        }

        [Test]
        public void test_rake_readsTheAbsoluteRoundIndex_soALateCreatedLedgerIsNotBehind()
        {
            // Arrange — el servicio es lazy: nace en el primer tick del jefe, tarde.
            FireRound(7);

            // Assert
            Assert.AreEqual(2, _ledger.DamageStepUp,
                "El rastrillo se deriva del índice de ronda, no de cuántos eventos escuchó.");
        }

        [Test]
        public void test_rake_disabledWhenCadenceIsZeroOrLess()
        {
            // Arrange
            _ledger.RakeRoundsPerStep = 0;

            // Act
            FireRoundsUpTo(12);

            // Assert
            Assert.AreEqual(0, _ledger.DamageStepUp, "Cadencia <= 0 apaga el reloj (modo test/debug).");
        }

        [Test]
        public void test_rakeCadenceMatchesTheBribeWindow_soThePayoffIsAQuotaNotInsurance()
        {
            // Assert
            Assert.AreEqual(3, _ledger.RakeRoundsPerStep);
            Assert.AreEqual(3, _ledger.BribeRounds);
            Assert.AreEqual(35, _ledger.BribeCost);
        }

        [Test]
        public void test_bribingEveryThreeRounds_holdsTheTierFlat()
        {
            // Arrange — ronda 3: el reloj acaba de subir un escalón.
            FireRoundsUpTo(3);
            Assert.AreEqual(1, _ledger.DamageStepUp);

            // Act
            Assert.IsTrue(_ledger.TryBribe());

            // Assert
            Assert.AreEqual(0, Net(), "Pagar en la ronda 3 devuelve el escalón que puso el reloj.");
            FireRound(4);
            Assert.AreEqual(0, Net());
            FireRound(5);
            Assert.AreEqual(0, Net());
        }

        [Test]
        public void test_skippingTheBribe_letsTheClockGainGround()
        {
            // Arrange
            FireRoundsUpTo(3);
            Assert.IsTrue(_ledger.TryBribe());

            // Act — la ventana se cae justo cuando el reloj vuelve a sumar.
            FireRound(4);
            FireRound(5);
            FireRound(6);

            // Assert
            Assert.AreEqual(0, _ledger.DamageStepDown, "Tres rondas y la cuota venció.");
            Assert.AreEqual(2, Net(),
                "Sin renovar, el jefe queda dos escalones arriba de lo que dice el oro.");
            Assert.IsTrue(_ledger.TryBribe());
            Assert.AreEqual(1, Net(), "Renovar tarde compra un escalón, no los dos perdidos.");
        }

        [Test]
        public void test_rake_resetsOnCombatEnd_soItDoesNotLeakIntoTheNextFight()
        {
            // Arrange
            FireRoundsUpTo(6);
            Assert.AreEqual(2, _ledger.DamageStepUp);

            // Act
            EventManager.Trigger(EventName.OnCombatEnd);

            // Assert
            Assert.AreEqual(0, _ledger.DamageStepUp, "La pelea siguiente arranca con el reloj en cero.");
        }
    }

    /// <summary>El nodo de la columna leyendo el rastrillo.</summary>
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

        /// <summary>Columnas distintas (X) que cubre el área marcada = ancho de la franja.</summary>
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
            // Arrange — jugador sin un peso, ronda 3.
            _economy.ResetTo(0);
            _ledger.DamageStepUp = 1;

            // Act
            var result = NewNode().Tick(NewContext());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(3, MarkedWidth(), "El escalón que puso el reloj también ensancha la franja.");
            Assert.AreEqual(28, MarkedDamage(),
                "Sin rastrillo un jugador pobre dejaba al Cajero clavado en 14 toda la pelea.");
        }

        [Test]
        public void test_rakeAndBribeCancelOut()
        {
            // Arrange
            _economy.ResetTo(0);
            _ledger.DamageStepUp = 1;
            _ledger.DamageStepDown = 1;

            // Act
            NewNode().Tick(NewContext());

            // Assert
            Assert.AreEqual(14, MarkedDamage(), "Pagar la cuota devuelve el escalón que puso el reloj.");
        }

        [Test]
        public void test_rakeDisabled_ignoresTheLedgerClock()
        {
            // Arrange
            _economy.ResetTo(0);
            _ledger.DamageStepUp = 2;
            var node = NewNode();
            node.ApplyRakeStepUp = false;

            // Act
            node.Tick(NewContext());

            // Assert
            Assert.AreEqual(14, MarkedDamage(), "Apagado, el escalón vuelve a salir sólo del oro.");
        }

        [Test]
        public void test_exposesTheRakeSeparatelyFromTheResolvedTier()
        {
            // Arrange
            _economy.ResetTo(40);
            _ledger.DamageStepUp = 1;

            // Act
            var node = NewNode();
            node.Tick(NewContext());

            // Assert
            Assert.AreEqual(1, node.LastStepUp, "Cuánto del escalón lo puso el reloj…");
            Assert.AreEqual(2, node.LastRank, "…y en cuál terminó parado.");
            Assert.AreEqual(40, node.LastGold);
        }

        [Test]
        public void test_withoutLedger_fallsBackToTheGoldTier_insteadOfNotAttacking()
        {
            // Arrange
            ServiceLocator.RemoveService<ICashierLedgerService>();
            _economy.ResetTo(40);

            // Act
            var result = NewNode().Tick(NewContext());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(28, MarkedDamage(),
                "Sin ledger no hay reloj ni soborno, pero el jefe amenaza igual.");
        }
    }
}
