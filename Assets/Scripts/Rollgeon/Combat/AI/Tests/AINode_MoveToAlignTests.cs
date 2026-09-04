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
    /// El nodo de alineación fila/columna (Charger, Sniper). El foco es el BUG de playtest de las
    /// bolas de pool: "ya llegué" miraba SÓLO la alineación, así que un enemigo que compartía
    /// columna con el jugador se creía en posición a cualquier distancia, no se movía nunca más y
    /// sus gates de ataque y de telegraph le fallaban por estar lejos — hasta que el jugador se
    /// movía y rompía la alineación.
    /// </summary>
    [TestFixture]
    public sealed class AINode_MoveToAlignTests
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

        private int DistToPlayer()
        {
            _grid.TryGetPosition(_player, out var p);
            return SelfCoord().Manhattan(p);
        }

        [Test]
        public void AlignedButBeyondDesiredRange_KeepsClosingIn()
        {
            // Arrange — misma columna que el jugador pero a 12 tiles, con DesiredRange 5
            // (la config real del ED_Charger). Antes: Succeeded sin moverse, para siempre.
            _grid.Register(_player, new GridCoord(5, 0));
            _grid.Register(_self, new GridCoord(5, 12));
            var node = new AINode_MoveToAlign
            {
                MaxSteps = Const(3),
                DesiredRange = Const(5),
                RequireLineOfSight = true,
            };

            // Act
            var result = node.Tick(Ctx());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.Less(DistToPlayer(), 12, "Alineado pero lejos: tiene que seguir acercándose.");
            Assert.AreEqual(5, SelfCoord().X, "Y sin perder la alineación de columna.");
        }

        [Test]
        public void AlignedAndWithinDesiredRange_DoesNotMove()
        {
            // Arrange — ya en posición de disparo.
            _grid.Register(_player, new GridCoord(5, 0));
            _grid.Register(_self, new GridCoord(5, 4));
            var node = new AINode_MoveToAlign
            {
                MaxSteps = Const(3),
                DesiredRange = Const(5),
                RequireLineOfSight = true,
            };

            // Act
            var result = node.Tick(Ctx());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(new GridCoord(5, 4), SelfCoord());
        }

        [Test]
        public void AlignedBehindAnAlly_StillClosesIn()
        {
            // Arrange — la forma típica de llegar al bug: otro enemigo tomó la casilla alineada
            // buena y éste quedó atrás sobre la misma columna. Los aliados NO cortan LoS
            // (GridLineOfSight.Blocks), así que se creía perfectamente en posición.
            _grid.Register(_player, new GridCoord(5, 0));
            _grid.Register(Guid.NewGuid(), new GridCoord(5, 5));
            _grid.Register(_self, new GridCoord(5, 11));
            var node = new AINode_MoveToAlign
            {
                MaxSteps = Const(3),
                DesiredRange = Const(5),
                RequireLineOfSight = true,
            };

            // Act
            var result = node.Tick(Ctx());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.Less(DistToPlayer(), 11, "Detrás de un aliado tampoco puede quedarse clavado.");
        }

        [Test]
        public void ConvergesToTheBandOverSuccessiveTurns()
        {
            // Arrange
            _grid.Register(_player, new GridCoord(5, 0));
            _grid.Register(_self, new GridCoord(5, 14));
            var node = new AINode_MoveToAlign
            {
                MaxSteps = Const(3),
                DesiredRange = Const(5),
                RequireLineOfSight = true,
            };

            // Act — sin oscilar: cada turno debe acercar o quedarse ya en banda.
            for (int turn = 0; turn < 10; turn++) node.Tick(Ctx());

            // Assert
            Assert.LessOrEqual(DistToPlayer(), 5);
        }

        [Test]
        public void NullDesiredRange_KeepsLegacyBehaviourOfStoppingOnAlignment()
        {
            // Arrange — sin DesiredRange autorado no hay tope: se conserva el comportamiento viejo
            // para no re-interpretar árboles que dejaron el campo en blanco.
            _grid.Register(_player, new GridCoord(5, 0));
            _grid.Register(_self, new GridCoord(5, 12));
            var node = new AINode_MoveToAlign { MaxSteps = Const(3), RequireLineOfSight = true };

            // Act
            var result = node.Tick(Ctx());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(new GridCoord(5, 12), SelfCoord());
        }

        [Test]
        public void NotAligned_MovesToAnAlignedTile()
        {
            // Arrange — comportamiento base del nodo, que no debe haber cambiado.
            _grid.Register(_player, new GridCoord(5, 0));
            _grid.Register(_self, new GridCoord(8, 3));
            var node = new AINode_MoveToAlign
            {
                MaxSteps = Const(3),
                DesiredRange = Const(5),
                RequireLineOfSight = true,
            };

            // Act
            node.Tick(Ctx());

            // Assert
            var c = SelfCoord();
            Assert.IsTrue(c.X == 5 || c.Y == 0, $"Debería haber quedado alineado; quedó en {c}.");
        }
    }
}
