using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Cashier;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Combat.AI.Tests
{
    // Necesita LoadRoom porque lo que se prueba es qué casillas se pintan. NavGraph.Rect(11,11)
    // arranca en el origen, así que el mostrador va en la fila 5 y no en Y = 0 como en la sala
    // real: la regla es relativa a CounterRow.
    [TestFixture]
    public class CajeroCounterTollOverlayTests
    {
        private const int CounterRow = 5;
        private const int TollDamage = 10;

        private static readonly GridCoord BossCoord = new GridCoord(5, 7);
        private static readonly GridCoord PlayerCoord = new GridCoord(5, 3);

        private GridManager _grid;
        private CashierCounterTollService _toll;
        private CashierCounterTollOverlay _overlay;
        private Guid _boss;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(11, 11));
            ServiceLocator.AddService<IGridManager>(_grid);

            _boss = Guid.NewGuid();
            _player = Guid.NewGuid();
            _grid.Register(_boss, BossCoord);
            _grid.Register(_player, PlayerCoord);

            _toll = new CashierCounterTollService();
            ServiceLocator.AddService<ICashierCounterTollService>(_toll);

            _overlay = new CashierCounterTollOverlay();
            ServiceLocator.AddService<CashierCounterTollOverlay>(_overlay);
        }

        [TearDown]
        public void TearDown()
        {
            _overlay.Dispose();
            _toll.Dispose();

            if (ServiceLocator.TryGetService<IThreatOverlayService>(out var painted) && painted is IDisposable d)
                d.Dispose();
            var leftover = GameObject.Find("ThreatTelegraphOverlay");
            if (leftover != null) UnityEngine.Object.DestroyImmediate(leftover);

            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private void Arm() => _toll.Arm(_boss, _player, CounterRow, TollDamage);

        private void ArmIntermittent() => _toll.Arm(_boss, _player, CounterRow, TollDamage, 2);

        /// <summary><paramref name="round"/> es 1-based; <c>RoundIndex</c> es 0-based.</summary>
        private void PutPlayerInRound(int round)
        {
            var turnOrder = new TurnOrderService();
            turnOrder.RestoreState(new[] { _player, _boss }, cursor: 0, roundIndex: round - 1);
            ServiceLocator.AddService<TurnOrderService>(turnOrder);
        }

        [Test]
        public void Side_WithoutAnArmedToll_PaintsNothing()
        {
            // El peaje se arma en el primer tick del jefe.

            bool resolved = CashierCounterTollOverlay.TryResolveSide(out _, out _);

            Assert.IsFalse(resolved);
        }

        [Test]
        public void Side_OnTheFreeRound_PaintsNothing()
        {
            // Cobra una ronda de cada dos; la impar es franca.
            ArmIntermittent();
            PutPlayerInRound(1);

            bool resolved = CashierCounterTollOverlay.TryResolveSide(out _, out _);

            // Se apaga entero en vez de atenuarse.
            Assert.IsFalse(resolved,
                "En la ronda franca cruzar es gratis, y el overlay es lo único que lo dice.");
        }

        [Test]
        public void Side_OnTheChargingRound_PaintsItAgain()
        {
            ArmIntermittent();
            PutPlayerInRound(2);

            bool resolved = CashierCounterTollOverlay.TryResolveSide(out _, out var tiles);

            Assert.IsTrue(resolved);
            CollectionAssert.IsNotEmpty(tiles);
        }

        [Test]
        public void Side_PaintsTheBossHalf_AndOnlyTheBossHalf()
        {
            Arm();

            bool resolved = CashierCounterTollOverlay.TryResolveSide(out var bossGuid, out var tiles);

            Assert.IsTrue(resolved);
            Assert.AreEqual(_boss, bossGuid);

            foreach (var cell in tiles)
            {
                Assert.Greater(cell.Y, CounterRow,
                    $"{cell} está del lado del jugador (o en el mostrador) y no debería pintarse: " +
                    "pintar de más convierte casillas seguras en zona de peligro.");
            }
        }

        [Test]
        public void Side_NeverPaintsTheCounterRow_BecauseStandingInAnOpeningIsFree()
        {
            // IsSameSide devuelve false con side == 0.
            Arm();

            CashierCounterTollOverlay.TryResolveSide(out _, out var tiles);

            for (int x = 0; x <= 10; x++)
            {
                CollectionAssert.DoesNotContain(tiles, new GridCoord(x, CounterRow),
                    "La fila del mostrador es el único lugar neutral de la sala — pintarla borra " +
                    "la lectura de 'acá no me cobran'.");
            }
        }

        [Test]
        public void Side_CoversEveryWalkableCellOfTheBossHalf()
        {
            Arm();

            CashierCounterTollOverlay.TryResolveSide(out _, out var tiles);

            int expected = 0;
            foreach (var cell in ThreatAreaShape.RoomTiles(_grid))
                if (cell.Y > CounterRow) expected++;

            Assert.AreEqual(expected, tiles.Count);
        }

        [Test]
        public void Side_FollowsTheBoss_WhenKitingCrossesHimToTheOtherHalf()
        {
            // El peaje lee posiciones vivas, no las que tenía al armarse.
            Arm();
            Assert.IsTrue(_grid.Move(_boss, new GridCoord(5, 2)), "El jefe tenía que poder cruzar.");

            CashierCounterTollOverlay.TryResolveSide(out _, out var tiles);

            foreach (var cell in tiles)
                Assert.Less(cell.Y, CounterRow, $"{cell} quedó pintada del lado equivocado.");
        }

        [Test]
        public void Side_WithTheBossOffTheGrid_PaintsNothing()
        {
            // CombatDeathWatcher lo saca de la grilla al morir.
            Arm();
            _grid.Unregister(_boss);

            Assert.IsFalse(CashierCounterTollOverlay.TryResolveSide(out _, out _));
        }

        [Test]
        public void Repaint_PaintsUnderADerivedSource_NotUnderTheBossItself()
        {
            // El jefe ya marca su columna bajo su propio guid.
            Arm();

            _overlay.Repaint();

            var overlay = (ThreatTelegraphOverlay)ThreatTelegraphOverlay.ResolveOrCreate();
            Assert.IsNotEmpty(overlay.ActiveQuadsOf(CashierCounterTollOverlay.OverlayGuid(_boss)));
            CollectionAssert.IsEmpty(overlay.ActiveQuadsOf(_boss),
                "El overlay del peaje no puede pisar la fuente que usa el jefe para su columna.");
        }

        [Test]
        public void Repaint_AfterDisarm_TakesTheOverlayDown()
        {
            Arm();
            _overlay.Repaint();
            var overlay = (ThreatTelegraphOverlay)ThreatTelegraphOverlay.ResolveOrCreate();
            Assume.That(overlay.ActiveQuadsOf(CashierCounterTollOverlay.OverlayGuid(_boss)), Is.Not.Empty);

            _toll.Disarm();
            _overlay.Repaint();

            CollectionAssert.IsEmpty(overlay.ActiveQuadsOf(CashierCounterTollOverlay.OverlayGuid(_boss)));
        }

        [Test]
        public void CombatEnd_TakesTheOverlayDown()
        {
            Arm();
            _overlay.Repaint();

            EventManager.Trigger(EventName.OnCombatEnd);

            var overlay = (ThreatTelegraphOverlay)ThreatTelegraphOverlay.ResolveOrCreate();
            CollectionAssert.IsEmpty(overlay.ActiveQuadsOf(CashierCounterTollOverlay.OverlayGuid(_boss)),
                "El lado pintado no se filtra a la pelea siguiente.");
        }

        [Test]
        public void OverlayGuid_IsStableAndDifferentFromTheBossGuid()
        {
            var derived = CashierCounterTollOverlay.OverlayGuid(_boss);

            Assert.AreNotEqual(_boss, derived);
            Assert.AreEqual(derived, CashierCounterTollOverlay.OverlayGuid(_boss),
                "Tiene que ser determinístico o cada repintado dejaría el anterior colgado.");
            Assert.AreEqual(Guid.Empty, CashierCounterTollOverlay.OverlayGuid(Guid.Empty));
        }
    }
}
