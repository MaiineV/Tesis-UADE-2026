using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Initiative;
using Rollgeon.Combat.Threat;
using Rollgeon.Dice;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// Qué pinta (y qué limpia) el toggle de ALT que muestra el alcance de todos los
    /// enemigos del combate a la vez.
    /// </summary>
    [TestFixture]
    public sealed class AllEnemyRangesOverlayTests
    {
        private AllEnemyRangesOverlay _ranges;
        private PerEnemyReachService _intents;
        private SpyThreatOverlay _overlay;
        private StubPlayerService _players;

        private Guid _melee;
        private Guid _archer;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            // Arrange compartido: locator y eventos limpios, servicios fake registrados.
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();

            _melee = Guid.NewGuid();
            _archer = Guid.NewGuid();
            _player = Guid.NewGuid();

            _players = new StubPlayerService { PlayerGuid = _player };
            ServiceLocator.AddService<IPlayerService>(_players);

            _intents = new PerEnemyReachService();
            ServiceLocator.AddService<IEnemyIntentService>(_intents);

            _overlay = new SpyThreatOverlay();
            ServiceLocator.AddService<IThreatOverlayService>(_overlay, ServiceScope.Global);

            _ranges = new AllEnemyRangesOverlay();
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        [Test]
        public void SetOn_PintaUnCanalPorEnemigoConAlcance_EnElAmarilloDelReach()
        {
            // Arrange
            _intents.Reach[_melee] = Cells((1, 1), (2, 1));
            _intents.Reach[_archer] = Cells((5, 5));

            // Act
            _ranges.SetOn(true);
            _ranges.Repaint(new[] { _melee, _archer, _player });

            // Assert
            Assert.AreEqual(2, _overlay.Painted.Count,
                "El jugador no tiene reach (TryReadReach le devuelve false) y no debe pintarse.");
            CollectionAssert.AreEquivalent(
                new[] { AllEnemyRangesOverlay.Source(_melee), AllEnemyRangesOverlay.Source(_archer) },
                _overlay.Painted.Select(p => p.Source),
                "Cada enemigo pinta en SU canal derivado, para no pisarse con el hover.");
            Assert.IsTrue(_overlay.Painted.All(p => p.Tint == ThreatTelegraphOverlay.ReachTint),
                "El toggle usa el mismo amarillo del reach del hover.");
            CollectionAssert.AreEquivalent(Cells((1, 1), (2, 1)), _overlay.Painted
                .First(p => p.Source == AllEnemyRangesOverlay.Source(_melee)).Cells);
        }

        [Test]
        public void SetOn_False_LimpiaExactamenteLosCanalesQuePinto()
        {
            // Arrange
            _intents.Reach[_melee] = Cells((1, 1));
            _intents.Reach[_archer] = Cells((5, 5));
            _ranges.SetOn(true);
            _ranges.Repaint(new[] { _melee, _archer });

            // Act
            _ranges.SetOn(false);

            // Assert
            CollectionAssert.AreEquivalent(
                new[] { AllEnemyRangesOverlay.Source(_melee), AllEnemyRangesOverlay.Source(_archer) },
                _overlay.Cleared);
        }

        [Test]
        public void Repaint_ConToggleApagado_NoPintaNada()
        {
            // Arrange
            _intents.Reach[_melee] = Cells((1, 1));

            // Act
            _ranges.Repaint(new[] { _melee });

            // Assert
            Assert.IsEmpty(_overlay.Painted,
                "Repaint sin toggle prendido pintaría el overlay que el jugador nunca pidió.");
        }

        [Test]
        public void EnemigoSinAlcance_NoDejaCanalPintadoNiPorLimpiar()
        {
            // Arrange
            _intents.Reach[_melee] = Cells((1, 1));
            _ranges.SetOn(true);
            _ranges.Repaint(new[] { _melee, _archer });

            // Act
            _ranges.SetOn(false);

            // Assert: _archer nunca se pintó, así que tampoco hay que limpiarlo.
            CollectionAssert.AreEquivalent(
                new[] { AllEnemyRangesOverlay.Source(_melee) }, _overlay.Cleared);
        }

        [Test]
        public void TurnoDeUnEnemigo_SuspendeElPintado_SinApagarElToggle()
        {
            // Arrange
            _intents.Reach[_melee] = Cells((1, 1));
            _ranges.Bind();
            _ranges.SetOn(true);
            _ranges.Repaint(new[] { _melee });

            // Act: arranca el turno del enemigo.
            EventManager.Trigger(EventName.OnTurnStarted, _melee);

            // Assert
            CollectionAssert.Contains(_overlay.Cleared, AllEnemyRangesOverlay.Source(_melee));
            Assert.IsTrue(_ranges.IsOn,
                "El toggle es una preferencia del jugador: el turno enemigo suspende el " +
                "pintado, no la preferencia.");

            _ranges.Unbind();
        }

        [Test]
        public void TurnoDelJugador_ConToggleGuardado_RepintaContraElOrdenDeTurnos()
        {
            // Arrange: TurnOrderService real (initiative stub plano) para el camino completo.
            ServiceLocator.AddService<IInitiativeProvider>(new FlatInitiative());
            var turnOrder = new TurnOrderService();
            turnOrder.BuildForCombat(new[] { _melee, _player });
            ServiceLocator.AddService<TurnOrderService>(turnOrder);

            _intents.Reach[_melee] = Cells((3, 3));
            _ranges.Bind();
            _ranges.SetOn(true);
            _ranges.Repaint(new[] { _melee });
            _overlay.Painted.Clear();

            // Act: turno enemigo (suspende) y vuelta al jugador (repinta).
            EventManager.Trigger(EventName.OnTurnStarted, _melee);
            EventManager.Trigger(EventName.OnTurnStarted, _player);

            // Assert
            Assert.AreEqual(1, _overlay.Painted.Count,
                "Al volver el turno del jugador, el toggle guardado repinta solo.");
            Assert.AreEqual(AllEnemyRangesOverlay.Source(_melee), _overlay.Painted[0].Source);

            _ranges.Unbind();
        }

        [Test]
        public void EnemigoDestruido_ConElTogglePrendido_SuCanalSeLimpiaSolo()
        {
            // Arrange
            _intents.Reach[_melee] = Cells((1, 1));
            _intents.Reach[_archer] = Cells((5, 5));
            _ranges.Bind();
            _ranges.SetOn(true);
            _ranges.Repaint(new[] { _melee, _archer });

            // Act
            EventManager.Trigger(EventName.OnEntityDestroyed, _melee);

            // Assert: solo el muerto se limpia; el otro sigue pintado.
            CollectionAssert.AreEquivalent(
                new[] { AllEnemyRangesOverlay.Source(_melee) }, _overlay.Cleared);

            _ranges.Unbind();
        }

        [Test]
        public void FinDeCombate_ApagaElToggleYLimpia()
        {
            // Arrange
            _intents.Reach[_melee] = Cells((1, 1));
            _ranges.Bind();
            _ranges.SetOn(true);
            _ranges.Repaint(new[] { _melee });

            // Act
            EventManager.Trigger(EventName.OnCombatEnd);

            // Assert
            Assert.IsFalse(_ranges.IsOn,
                "El próximo combate arranca con el toggle apagado, no heredado.");
            CollectionAssert.Contains(_overlay.Cleared, AllEnemyRangesOverlay.Source(_melee));

            _ranges.Unbind();
        }

        // ==================================================================
        // Fixtures
        // ==================================================================

        private static List<GridCoord> Cells(params (int x, int y)[] coords)
            => coords.Select(c => new GridCoord(c.x, c.y)).ToList();

        /// <summary>Reach por enemigo; guids sin entrada devuelven false — como el jugador
        /// o un prop sin árbol contra el servicio real.</summary>
        private sealed class PerEnemyReachService : IEnemyIntentService
        {
            public readonly Dictionary<Guid, List<GridCoord>> Reach = new();

            public bool TryRead(Guid enemyId, List<AIIntent> standing, List<AIIntent> next,
                                List<AIIntent> options = null)
            {
                standing?.Clear();
                next?.Clear();
                options?.Clear();
                return false;
            }

            public bool TryReadReach(Guid enemyId, HashSet<GridCoord> into)
            {
                into?.Clear();
                if (into == null || !Reach.TryGetValue(enemyId, out var cells)) return false;

                foreach (var cell in cells) into.Add(cell);
                return true;
            }
        }

        private sealed class SpyThreatOverlay : IThreatOverlayService
        {
            public readonly List<(Guid Source, List<GridCoord> Cells, ThreatOverlayState State, Color? Tint)> Painted = new();
            public readonly List<Guid> Cleared = new();

            public void Show(Guid sourceGuid, IEnumerable<GridCoord> tiles)
                => Record(sourceGuid, tiles, ThreatOverlayState.Marked, null);

            public void Show(Guid sourceGuid, IEnumerable<GridCoord> tiles, Color tint)
                => Record(sourceGuid, tiles, ThreatOverlayState.Marked, tint);

            public void Show(Guid sourceGuid, IEnumerable<GridCoord> tiles, ThreatOverlayState state,
                             Color? tint = null)
                => Record(sourceGuid, tiles, state, tint);

            public void Clear(Guid sourceGuid) => Cleared.Add(sourceGuid);
            public void ClearAll() => Painted.Clear();

            private void Record(Guid source, IEnumerable<GridCoord> tiles, ThreatOverlayState state,
                                Color? tint)
                => Painted.Add((source, new List<GridCoord>(tiles), state, tint));
        }

        private sealed class FlatInitiative : IInitiativeProvider
        {
            public int RollInitiative(Guid entityGuid) => 1;
        }

        private sealed class StubPlayerService : IPlayerService
        {
            public Guid PlayerGuid { get; set; } = Guid.NewGuid();
            public Guid RunId { get; set; } = Guid.NewGuid();
            public ClassHeroSO CurrentHero { get; set; }
            public DiceBagSO DiceBag { get; set; }
            public void SetPlayer(ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(DiceBagSO bag) { DiceBag = bag; }
            public void ClearPlayer() { }
            public event Action<ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;
        }
    }
}
