using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.AI.Readers;
using Rollgeon.Grid;
using Rollgeon.Movement;
using UnityEngine;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// Fase B: <see cref="AINode_Move"/> / <see cref="AINode_KeepDistance"/> con un self
    /// multi-celda — distancia desde la celda más cercana del rectángulo y candidatos donde
    /// el rectángulo entero cabe. Sin planner en el contexto: ejercita el fallback BFS
    /// (<c>GetReachableAnchors</c>); la paridad del planner se cubre en
    /// <c>AIPathPlannerFootprintTests</c>.
    /// </summary>
    [TestFixture]
    public class AINodeFootprintMoveTests
    {
        static readonly Vector2Int Two = new Vector2Int(2, 2);

        private GridManager _grid;
        private MovementService _movement;
        private Guid _self;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(10, 10));
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

        private GridCoord Anchor(Guid g)
        {
            _grid.TryGetPosition(g, out var c);
            return c;
        }

        [Test]
        public void Move_2x2_StopsWhenNearestCellAdjacent()
        {
            _grid.TryRegister(_self, new GridCoord(0, 0), Two);
            _grid.Register(_player, new GridCoord(6, 0));
            var node = new AINode_Move { MaxSteps = Const(10), DesiredRange = Const(1) };

            Assert.AreEqual(AIResult.Succeeded, node.Tick(Ctx()));

            // El rect (ancla+1 en X cubre hasta x+1): la celda más cercana debe quedar a 1
            // del player — ancla (4,0) cubre (4,0)-(5,1), y (5,0) está pegada a (6,0).
            var anchor = Anchor(_self);
            Assert.AreEqual(1, GridFootprint.ManhattanDistance(anchor, Two, new GridCoord(6, 0)));
        }

        [Test]
        public void Move_2x2_AdjacentByNonAnchorCell_DoesNotMove()
        {
            // Ancla (4,4) cubre (4,4)-(5,5); player en (6,4): pegado a (5,4), ancla a dist 2.
            _grid.TryRegister(_self, new GridCoord(4, 4), Two);
            _grid.Register(_player, new GridCoord(6, 4));
            var node = new AINode_Move { MaxSteps = Const(5), DesiredRange = Const(1) };

            Assert.AreEqual(AIResult.Succeeded, node.Tick(Ctx()));
            Assert.AreEqual(new GridCoord(4, 4), Anchor(_self), "ya está adyacente: no se mueve");
        }

        [Test]
        public void Move_2x2_DoesNotEnterOneWideCorridor()
        {
            // Pared vertical en x=3 con un hueco de 1 celda en y=0: el 2×2 no puede cruzar.
            var walkable = new bool[100];
            for (int y = 0; y < 10; y++)
                for (int x = 0; x < 10; x++)
                    walkable[y * 10 + x] = x != 3 || y == 0;
            _grid.LoadRoom(NavGraph.FromSnapshot(new GridSnapshot(10, 10, walkable)));

            _grid.TryRegister(_self, new GridCoord(0, 4), Two);
            _grid.Register(_player, new GridCoord(8, 4));
            var node = new AINode_Move { MaxSteps = Const(20), DesiredRange = Const(1) };

            Assert.AreEqual(AIResult.Succeeded, node.Tick(Ctx()));
            // Puede acomodarse de este lado de la pared, pero nunca cruzar x=3.
            Assert.LessOrEqual(Anchor(_self).X, 1, "el rect no cabe del otro lado ni en el hueco");
        }

        [Test]
        public void KeepDistance_2x2_KitesMeasuringNearestCell()
        {
            // Ancla (2,2) cubre (2,2)-(3,3); player en (4,2) → dist rect 1, kitea a ideal 3.
            _grid.TryRegister(_self, new GridCoord(2, 2), Two);
            _grid.Register(_player, new GridCoord(4, 2));
            var node = new AINode_KeepDistance { MaxSteps = Const(4), IdealDistance = Const(3) };

            Assert.AreEqual(AIResult.Succeeded, node.Tick(Ctx()));
            int dist = GridFootprint.ManhattanDistance(Anchor(_self), Two, new GridCoord(4, 2));
            Assert.GreaterOrEqual(dist, 3, "la celda más cercana quedó a la distancia ideal");
        }

        [Test]
        public void KeepDistance_2x2_AlreadyAtIdealByNearestCell_DoesNotMove()
        {
            // Ancla (0,0) cubre (0,0)-(1,1); player (4,1) → dist rect 3 == ideal.
            _grid.TryRegister(_self, new GridCoord(0, 0), Two);
            _grid.Register(_player, new GridCoord(4, 1));
            var node = new AINode_KeepDistance { MaxSteps = Const(4), IdealDistance = Const(3) };

            Assert.AreEqual(AIResult.Succeeded, node.Tick(Ctx()));
            Assert.AreEqual(new GridCoord(0, 0), Anchor(_self));
        }
    }
}
