using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Cashier;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// El overlay que pinta el lado del mostrador. Es el único consumidor de UI que tiene el peaje:
    /// hasta ahora el servicio cobraba y animaba el golpe <b>después</b> de cobrar, así que el
    /// jugador aprendía la regla perdiendo vida.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Acá sí se carga la sala con <c>LoadRoom</c> —a diferencia de <c>CajeroCounterTollTests</c>—
    /// porque lo que se prueba es <b>qué casillas</b> se pintan, y eso necesita que la sala tenga
    /// extensión real.
    /// </para>
    /// <para>
    /// <b>Coordenadas 0..10 y no centradas en (0,0):</b> <c>NavGraph.Rect(11,11)</c> arranca en el
    /// origen, así que el mostrador va en la fila 5 (el medio) y no en la 0. La regla que se prueba
    /// es relativa a <c>CounterRow</c>, no absoluta — la sala real lo pone en <c>Y = 0</c> porque su
    /// grafo está centrado, y al peaje eso le da igual.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class CajeroCounterTollOverlayTests
    {
        private const int CounterRow = 5;
        private const int TollDamage = 10;

        /// <summary>Del lado de arriba del mostrador — el lado del jefe.</summary>
        private static readonly GridCoord BossCoord = new GridCoord(5, 7);

        /// <summary>Del lado de abajo — donde entra el jugador.</summary>
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

        // =====================================================================
        // Qué se pinta
        // =====================================================================

        [Test]
        public void Side_WithoutAnArmedToll_PaintsNothing()
        {
            // Arrange — el peaje se arma en el primer tick del jefe; antes de eso no hay regla que
            // anunciar y pintar sería mentirle al jugador.

            // Act
            bool resolved = CashierCounterTollOverlay.TryResolveSide(out _, out _);

            // Assert
            Assert.IsFalse(resolved);
        }

        [Test]
        public void Side_PaintsTheBossHalf_AndOnlyTheBossHalf()
        {
            // Arrange
            Arm();

            // Act
            bool resolved = CashierCounterTollOverlay.TryResolveSide(out var bossGuid, out var tiles);

            // Assert
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
            // Arrange — parado en una abertura estás en la puerta, no de un lado, y no pagás:
            // IsSameSide devuelve false con side == 0.
            Arm();

            // Act
            CashierCounterTollOverlay.TryResolveSide(out _, out var tiles);

            // Assert
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
            // Arrange — un lado a medio pintar se lee como "estas casillas sí y estas no", que es
            // una regla que el peaje no tiene.
            Arm();

            // Act
            CashierCounterTollOverlay.TryResolveSide(out _, out var tiles);

            // Assert
            int expected = 0;
            foreach (var cell in ThreatAreaShape.RoomTiles(_grid))
                if (cell.Y > CounterRow) expected++;

            Assert.AreEqual(expected, tiles.Count);
        }

        [Test]
        public void Side_FollowsTheBoss_WhenKitingCrossesHimToTheOtherHalf()
        {
            // Arrange — el peaje lee posiciones vivas: si el jefe cruza por una abertura, el lado
            // que cobra es el de abajo. Un overlay horneado al armar mentiría a partir de acá.
            Arm();
            Assert.IsTrue(_grid.Move(_boss, new GridCoord(5, 2)), "El jefe tenía que poder cruzar.");

            // Act
            CashierCounterTollOverlay.TryResolveSide(out _, out var tiles);

            // Assert
            foreach (var cell in tiles)
                Assert.Less(cell.Y, CounterRow, $"{cell} quedó pintada del lado equivocado.");
        }

        [Test]
        public void Side_WithTheBossOffTheGrid_PaintsNothing()
        {
            // Arrange — CombatDeathWatcher lo saca de la grilla al morir. Sin coordenada del jefe no
            // hay lado, y es también lo que apaga el overlay solo al terminar la pelea.
            Arm();
            _grid.Unregister(_boss);

            // Act / Assert
            Assert.IsFalse(CashierCounterTollOverlay.TryResolveSide(out _, out _));
        }

        // =====================================================================
        // Ciclo de vida del pintado
        // =====================================================================

        [Test]
        public void Repaint_PaintsUnderADerivedSource_NotUnderTheBossItself()
        {
            // Arrange — el jefe marca su columna bajo su propio guid; compartir fuente haría que
            // una cosa borrara a la otra.
            Arm();

            // Act
            _overlay.Repaint();

            // Assert
            var overlay = (ThreatTelegraphOverlay)ThreatTelegraphOverlay.ResolveOrCreate();
            Assert.IsNotEmpty(overlay.ActiveQuadsOf(CashierCounterTollOverlay.OverlayGuid(_boss)));
            CollectionAssert.IsEmpty(overlay.ActiveQuadsOf(_boss),
                "El overlay del peaje no puede pisar la fuente que usa el jefe para su columna.");
        }

        [Test]
        public void Repaint_AfterDisarm_TakesTheOverlayDown()
        {
            // Arrange
            Arm();
            _overlay.Repaint();
            var overlay = (ThreatTelegraphOverlay)ThreatTelegraphOverlay.ResolveOrCreate();
            Assume.That(overlay.ActiveQuadsOf(CashierCounterTollOverlay.OverlayGuid(_boss)), Is.Not.Empty);

            // Act
            _toll.Disarm();
            _overlay.Repaint();

            // Assert
            CollectionAssert.IsEmpty(overlay.ActiveQuadsOf(CashierCounterTollOverlay.OverlayGuid(_boss)));
        }

        [Test]
        public void CombatEnd_TakesTheOverlayDown()
        {
            // Arrange
            Arm();
            _overlay.Repaint();

            // Act
            EventManager.Trigger(EventName.OnCombatEnd);

            // Assert
            var overlay = (ThreatTelegraphOverlay)ThreatTelegraphOverlay.ResolveOrCreate();
            CollectionAssert.IsEmpty(overlay.ActiveQuadsOf(CashierCounterTollOverlay.OverlayGuid(_boss)),
                "El lado pintado no se filtra a la pelea siguiente.");
        }

        [Test]
        public void OverlayGuid_IsStableAndDifferentFromTheBossGuid()
        {
            // Arrange / Act
            var derived = CashierCounterTollOverlay.OverlayGuid(_boss);

            // Assert
            Assert.AreNotEqual(_boss, derived);
            Assert.AreEqual(derived, CashierCounterTollOverlay.OverlayGuid(_boss),
                "Tiene que ser determinístico o cada repintado dejaría el anterior colgado.");
            Assert.AreEqual(Guid.Empty, CashierCounterTollOverlay.OverlayGuid(Guid.Empty));
        }
    }
}
