using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.AI.Readers;
using Rollgeon.Grid;
using Rollgeon.Movement;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// El hermano diagonal de <see cref="AINode_MoveToAlign"/> (Skirmisher). Tenía el mismo BUG:
    /// "en diagonal exacta" alcanzaba para creerse en posición a cualquier distancia. Acá la
    /// métrica de banda es Chebyshev, como dice el tooltip de <c>DesiredRange</c>.
    /// </summary>
    [TestFixture]
    public sealed class AINode_MoveToDiagonalTests
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
            _grid.LoadRoom(NavGraph.Rect(15, 15));
            _movement = new MovementService(_grid);
            _self = Guid.NewGuid();
            _player = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown() => ServiceLocator.Clear();

        private AIContext Ctx() => new AIContext
        {
            SelfGuid = _self,
            PlayerGuid = _player,
            Grid = _grid,
            Movement = _movement,
        };

        private static AIIntReader Const(int v) => new AIConstantInt { Value = v };

        private GridCoord SelfCoord()
        {
            _grid.TryGetPosition(_self, out var c);
            return c;
        }

        private int ChebyshevToPlayer()
        {
            _grid.TryGetPosition(_player, out var p);
            return SelfCoord().Chebyshev(p);
        }

        [Test]
        public void DiagonalButBeyondDesiredRange_KeepsClosingIn()
        {
            // Arrange — diagonal exacta a Chebyshev 6, con DesiredRange 2 (config del Skirmisher).
            _grid.Register(_player, new GridCoord(5, 5));
            _grid.Register(_self, new GridCoord(11, 11));
            var node = new AINode_MoveToDiagonal { MaxSteps = Const(3), DesiredRange = Const(2) };

            // Act
            var result = node.Tick(Ctx());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.Less(ChebyshevToPlayer(), 6, "En diagonal pero lejos: tiene que acercarse.");
        }

        [Test]
        public void DiagonalAndWithinDesiredRange_DoesNotMove()
        {
            // Arrange
            _grid.Register(_player, new GridCoord(5, 5));
            _grid.Register(_self, new GridCoord(7, 7));
            var node = new AINode_MoveToDiagonal { MaxSteps = Const(3), DesiredRange = Const(2) };

            // Act
            var result = node.Tick(Ctx());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(new GridCoord(7, 7), SelfCoord());
        }

        [Test]
        public void ConvergesToTheBandOverSuccessiveTurns()
        {
            // Arrange
            _grid.Register(_player, new GridCoord(5, 5));
            _grid.Register(_self, new GridCoord(13, 13));
            var node = new AINode_MoveToDiagonal { MaxSteps = Const(3), DesiredRange = Const(2) };

            // Act
            for (int turn = 0; turn < 10; turn++) node.Tick(Ctx());

            // Assert
            Assert.LessOrEqual(ChebyshevToPlayer(), 2);
        }

        [Test]
        public void NullDesiredRange_KeepsLegacyBehaviourOfStoppingOnDiagonal()
        {
            // Arrange
            _grid.Register(_player, new GridCoord(5, 5));
            _grid.Register(_self, new GridCoord(11, 11));
            var node = new AINode_MoveToDiagonal { MaxSteps = Const(3) };

            // Act
            var result = node.Tick(Ctx());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(new GridCoord(11, 11), SelfCoord());
        }

        [Test]
        public void NotDiagonal_MovesToADiagonalTile()
        {
            // Arrange — comportamiento base, que no debe haber cambiado.
            _grid.Register(_player, new GridCoord(5, 5));
            _grid.Register(_self, new GridCoord(9, 6));
            var node = new AINode_MoveToDiagonal { MaxSteps = Const(3), DesiredRange = Const(2) };

            // Act
            node.Tick(Ctx());

            // Assert
            var c = SelfCoord();
            int dx = 5 - c.X, dy = 5 - c.Y;
            Assert.IsTrue(dx != 0 && Math.Abs(dx) == Math.Abs(dy),
                $"Debería haber quedado en diagonal exacta; quedó en {c}.");
        }

        // ------------------------------------------------------------------
        // RequireLineOfSight — deadlock del Skirmisher reportado en playtest
        // ------------------------------------------------------------------

        /// <summary>
        /// La geometría exacta del playtest, trasladada al origen: el Skirmisher en diagonal
        /// exacta a Chebyshev 2, dentro de su DesiredRange y de su AttackRange, pero con los DOS
        /// flancos del segundo paso diagonal bloqueados — la regla de no cortar esquinas de
        /// <c>GridLineOfSight</c> le niega el tiro. Su gate de ataque pide LoS y dice que no;
        /// el nodo de movimiento decía "ya llegué" y no se movía. Deadlock.
        /// </summary>
        private void ArrangeCorneredDiagonal()
        {
            var graph = NavGraph.Rect(15, 15);
            graph.RemoveNode(new GridCoord(5, 6)); // flanco A
            graph.RemoveNode(new GridCoord(6, 5)); // flanco B
            _grid.LoadRoom(graph);
            _grid.Register(_player, new GridCoord(5, 5));
            _grid.Register(_self, new GridCoord(7, 7));
        }

        [Test]
        public void DiagonalWithoutLineOfSight_RequireLos_RepositionsInsteadOfFreezing()
        {
            // Arrange
            ArrangeCorneredDiagonal();
            Assert.IsFalse(
                GridLineOfSight.HasClearLine(_grid, new GridCoord(7, 7), new GridCoord(5, 5), _self, _player),
                "Premisa del test: desde (7,7) NO hay tiro a (5,5).");
            var node = new AINode_MoveToDiagonal
            {
                MaxSteps = Const(3),
                DesiredRange = Const(2),
                RequireLineOfSight = true,
            };

            // Act
            var result = node.Tick(Ctx());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreNotEqual(new GridCoord(7, 7), SelfCoord(),
                "En diagonal pero sin tiro: tiene que reposicionarse, no quedarse clavado.");
        }

        [Test]
        public void DiagonalWithoutLineOfSight_RequireLos_EndsUpWithAClearShot()
        {
            // Arrange
            ArrangeCorneredDiagonal();
            var node = new AINode_MoveToDiagonal
            {
                MaxSteps = Const(3),
                DesiredRange = Const(2),
                RequireLineOfSight = true,
            };

            // Act
            for (int turn = 0; turn < 10; turn++) node.Tick(Ctx());

            // Assert — el objetivo real del nodo: quedar en diagonal Y con tiro.
            var c = SelfCoord();
            int dx = 5 - c.X, dy = 5 - c.Y;
            Assert.IsTrue(dx != 0 && Math.Abs(dx) == Math.Abs(dy),
                $"Debería estar en diagonal exacta; quedó en {c}.");
            Assert.IsTrue(
                GridLineOfSight.HasClearLine(_grid, c, new GridCoord(5, 5), _self, _player),
                $"Y con línea de visión limpia; quedó en {c}.");
        }

        [Test]
        public void DiagonalWithoutLineOfSight_RequireLosOff_KeepsLegacyBehaviour()
        {
            // Arrange — con el flag apagado el nodo se comporta como antes: la LoS no le importa.
            // Default false para no re-interpretar árboles ya serializados que no lo autoraron.
            ArrangeCorneredDiagonal();
            var node = new AINode_MoveToDiagonal { MaxSteps = Const(3), DesiredRange = Const(2) };

            // Act
            var result = node.Tick(Ctx());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(new GridCoord(7, 7), SelfCoord());
        }
    }
}
