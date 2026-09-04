using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.AI.Pathing;
using Rollgeon.Combat.AI.Readers;
using Rollgeon.Grid;
using Rollgeon.Movement;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// La fase de desbloqueo de <c>AINode_Move</c> / <see cref="AIPathPlanner"/>: el scoring de
    /// Manhattan se queda en mínimos locales y devuelve "quedate quieto" apenas algo tapa la línea
    /// recta — una pared, una mesa u otro enemigo. Como nada cambia solo, el enemigo se congelaba
    /// PARA SIEMPRE hasta que el jugador se movía (BUGs de playtest del Guardian y del Charger).
    /// </summary>
    /// <remarks>
    /// Cada escenario corre por los DOS caminos que el proyecto mantiene en paridad: con planner
    /// (<see cref="AIPathPlanner"/>) y sin planner (el fallback propio del nodo).
    /// </remarks>
    [TestFixture]
    public sealed class AIMoveUnstickTests
    {
        private GridManager _grid;
        private MovementService _movement;
        private Guid _self;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _grid = new GridManager();
            _movement = new MovementService(_grid);
            _self = Guid.NewGuid();
            _player = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown() => ServiceLocator.Clear();

        private AIContext Ctx(bool withPlanner) => new AIContext
        {
            SelfGuid = _self,
            PlayerGuid = _player,
            SelfMaxHp = 10,
            Grid = _grid,
            Movement = _movement,
            PathPlanner = withPlanner ? new AIPathPlanner(_grid) : null,
        };

        private static AIIntReader Const(int v) => new AIConstantInt { Value = v };

        private GridCoord SelfCoord()
        {
            _grid.TryGetPosition(_self, out var c);
            return c;
        }

        // ------------------------------------------------------------------
        // Rodear una pared exige ALEJARSE en línea recta un turno
        // ------------------------------------------------------------------

        /// <summary>
        /// Sala 11×11 con una pared en x=5 que va de y=0 a y=7: el único paso está por el sur.
        /// Con el jugador en (0,4) y el enemigo en (6,4), TODA casilla alcanzable empeora la
        /// distancia Manhattan — el enemigo se congela. La única casilla que acorta el camino REAL
        /// es (6,5), que en línea recta lo ALEJA: es justo el caso que un filtro de "que no empeore
        /// la Manhattan" descartaría.
        /// </summary>
        private void ArrangeWallScenario()
        {
            var graph = NavGraph.Rect(11, 11);
            for (int y = 0; y <= 7; y++) graph.RemoveNode(new GridCoord(5, y));
            _grid.LoadRoom(graph);
            _grid.Register(_player, new GridCoord(0, 4));
            _grid.Register(_self, new GridCoord(6, 4));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void WallBetween_StepsAwayInStraightLineToStartGoingAround(bool withPlanner)
        {
            // Arrange
            ArrangeWallScenario();
            var node = new AINode_Move { MaxSteps = Const(1), DesiredRange = Const(1) };

            // Act
            var result = node.Tick(Ctx(withPlanner));

            // Assert — (6,5) está a Manhattan 7 del jugador (el origen estaba a 6): se aleja en
            // recta y sin embargo acorta el camino real de 14 a 13.
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(new GridCoord(6, 5), SelfCoord(),
                "Tiene que empezar a bordear hacia el hueco del sur, aunque suba la Manhattan.");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void WallBetween_ConvergesTowardThePlayerOverSuccessiveTurns(bool withPlanner)
        {
            // Arrange
            ArrangeWallScenario();
            var node = new AINode_Move { MaxSteps = Const(1), DesiredRange = Const(1) };

            // Act — 20 turnos alcanzan de sobra para los ~14 tiles de ruta real.
            for (int turn = 0; turn < 20; turn++)
            {
                var ctx = Ctx(withPlanner);
                node.Tick(ctx);
            }

            // Assert — el criterio es que LLEGUE, no que oscile cerca de la pared.
            _grid.TryGetPosition(_player, out var playerCoord);
            Assert.AreEqual(1, SelfCoord().Manhattan(playerCoord),
                $"Debería haber rodeado la pared y quedado adyacente; quedó en {SelfCoord()}.");
        }

        // ------------------------------------------------------------------
        // Un ALIADO tapando la ruta (Charger / bolas de pool)
        // ------------------------------------------------------------------

        [TestCase(true)]
        [TestCase(false)]
        public void AllyBlockingTheLane_SidestepsAroundIt(bool withPlanner)
        {
            // Arrange — pasillo de 3 de alto: jugador(0,1), aliado(2,1), self(3,1) pegado atrás.
            // Manhattan: quedarse = 3, cualquier casilla alcanzable = 4 ⇒ hoy NoMove.
            _grid.LoadRoom(NavGraph.Rect(9, 3));
            _grid.Register(_player, new GridCoord(0, 1));
            _grid.Register(Guid.NewGuid(), new GridCoord(2, 1));
            _grid.Register(_self, new GridCoord(3, 1));

            var node = new AINode_Move { MaxSteps = Const(1), DesiredRange = Const(1) };

            // Act
            var result = node.Tick(Ctx(withPlanner));

            // Assert — se corre a un costado para empezar a rodear al aliado.
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreNotEqual(1, SelfCoord().Y,
                $"Tenía que salir de la fila del aliado para rodearlo; quedó en {SelfCoord()}.");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void AllyBlockingWithNoWayAround_StaysPut(bool withPlanner)
        {
            // Arrange — mismo apilamiento pero en pasillo de 1: no hay rodeo posible.
            _grid.LoadRoom(NavGraph.Rect(9, 1));
            _grid.Register(_player, new GridCoord(0, 0));
            _grid.Register(Guid.NewGuid(), new GridCoord(2, 0));
            _grid.Register(_self, new GridCoord(3, 0));

            var node = new AINode_Move { MaxSteps = Const(1), DesiredRange = Const(1) };

            // Act
            var result = node.Tick(Ctx(withPlanner));

            // Assert — el desbloqueo exige mejora ESTRICTA: sin ruta mejor se queda esperando
            // detrás de su aliado, no retrocede ni oscila.
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(new GridCoord(3, 0), SelfCoord());
        }

        // ------------------------------------------------------------------
        // Guarda de banda: no destrabar a quien ya está en posición
        // ------------------------------------------------------------------

        [TestCase(true)]
        [TestCase(false)]
        public void AlreadyInBand_DoesNotWanderAroundTheWall(bool withPlanner)
        {
            // Arrange — ranged a Manhattan 4 del jugador (su DesiredRange) con una pared en el
            // medio: el camino real es larguísimo, pero ya está en banda. Reposicionar por falta
            // de línea de visión es trabajo del nodo (RequireLineOfSight), no del desbloqueo.
            var graph = NavGraph.Rect(11, 11);
            for (int y = 0; y <= 7; y++) graph.RemoveNode(new GridCoord(5, y));
            _grid.LoadRoom(graph);
            _grid.Register(_player, new GridCoord(3, 4));
            _grid.Register(_self, new GridCoord(6, 5)); // Manhattan 4

            var node = new AINode_Move { MaxSteps = Const(1), DesiredRange = Const(4) };

            // Act
            var result = node.Tick(Ctx(withPlanner));

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(new GridCoord(6, 5), SelfCoord(),
                "Ya está en banda: el desbloqueo no debe dispararse.");
        }
    }
}
