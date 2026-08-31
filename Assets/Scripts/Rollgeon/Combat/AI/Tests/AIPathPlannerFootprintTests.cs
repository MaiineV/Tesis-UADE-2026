using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Combat.AI.Pathing;
using Rollgeon.Grid;
using Rollgeon.Tiles;
using UnityEngine;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// Fase B: <see cref="AIPathPlanner"/> con self multi-celda — candidatos donde el
    /// rectángulo cabe, distancias desde la celda más cercana, y con casillas en sala planea
    /// footprint-aware pero ciego a hazards (la activación bajo un rectángulo es Fase C).
    /// </summary>
    [TestFixture]
    public class AIPathPlannerFootprintTests
    {
        static readonly Vector2Int Two = new Vector2Int(2, 2);

        private Guid _self;

        [SetUp]
        public void SetUp() => _self = Guid.NewGuid();

        private sealed class FakeTileQuery : ISpecialTileAIQuery
        {
            public readonly Dictionary<GridCoord, SpecialTileAIView> Tiles =
                new Dictionary<GridCoord, SpecialTileAIView>();

            public bool HasAnySpecialTiles => Tiles.Count > 0;
            public bool AnyActiveDangerTelegraph => false;

            public bool TryGetTileFor(GridCoord coord, Guid entity, Cardinal entryDirection,
                out SpecialTileAIView view)
                => Tiles.TryGetValue(coord, out view);
        }

        private static SpecialTileAIView DamageView(int enter)
            => new SpecialTileAIView(enter, 0, 0, BeneficialTileKind.None,
                false, false, default, false, 0, false);

        private GridManager MakeGridWithBig(int w, int h, GridCoord anchor)
        {
            var grid = new GridManager();
            grid.LoadRoom(NavGraph.Rect(w, h));
            Assert.IsTrue(grid.TryRegister(_self, anchor, Two), "setup: el 2×2 tiene que caber");
            return grid;
        }

        private AIPathRequest Request(GridCoord origin, GridCoord target, int maxSteps, int desiredRange,
            MoveIntent intent = MoveIntent.Approach)
            => new AIPathRequest
            {
                SelfGuid = _self,
                Origin = origin,
                TargetCoord = target,
                MaxSteps = maxSteps,
                DesiredRange = desiredRange,
                Intent = intent,
                CurrentHp = 100,
                MaxHp = 100,
                AttackRange = 1,
                TargetHpPct = -1,
                Personality = AIPersonalityProfile.Default,
            };

        [Test]
        public void Approach_2x2_StopsWithNearestCellAtDesiredRange()
        {
            var grid = MakeGridWithBig(10, 10, new GridCoord(0, 0));
            var planner = new AIPathPlanner(grid);

            var plan = planner.PlanMove(Request(new GridCoord(0, 0), new GridCoord(6, 0), 10, 1));

            Assert.IsTrue(plan.HasMove);
            Assert.AreEqual(1, GridFootprint.ManhattanDistance(plan.Destination, Two, new GridCoord(6, 0)));
        }

        [Test]
        public void Approach_2x2_NearestCellAlreadyAdjacent_NoMove()
        {
            // Ancla (4,0) cubre (4,0)-(5,1); target (6,0) pegado a (5,0).
            var grid = MakeGridWithBig(10, 10, new GridCoord(4, 0));
            var planner = new AIPathPlanner(grid);

            var plan = planner.PlanMove(Request(new GridCoord(4, 0), new GridCoord(6, 0), 5, 1));
            Assert.IsFalse(plan.HasMove);
        }

        [Test]
        public void Approach_2x2_DoesNotCrossOneWideGap()
        {
            var walkable = new bool[100];
            for (int y = 0; y < 10; y++)
                for (int x = 0; x < 10; x++)
                    walkable[y * 10 + x] = x != 4 || y == 0;
            var grid = new GridManager();
            grid.LoadRoom(NavGraph.FromSnapshot(new GridSnapshot(10, 10, walkable)));
            Assert.IsTrue(grid.TryRegister(_self, new GridCoord(0, 5), Two));
            var planner = new AIPathPlanner(grid);

            var plan = planner.PlanMove(Request(new GridCoord(0, 5), new GridCoord(8, 5), 30, 1));

            // Puede acomodarse pero jamás elegir un ancla del otro lado de la pared.
            if (plan.HasMove)
                Assert.LessOrEqual(plan.Destination.X, 2, "ningún ancla válida cruza x=4");
        }

        [Test]
        public void MultiCell_WithSpecialTilesInRoom_StillPlansFootprintAware()
        {
            // Con casillas en sala el 1×1 va por LabelPlan; el multi-celda planea igual
            // (LegacyPlan footprint-aware, ciego a hazards — Fase C).
            var grid = MakeGridWithBig(10, 10, new GridCoord(0, 0));
            var query = new FakeTileQuery();
            query.Tiles[new GridCoord(2, 5)] = DamageView(10);
            var planner = new AIPathPlanner(grid, query);

            var plan = planner.PlanMove(Request(new GridCoord(0, 0), new GridCoord(6, 0), 10, 1));

            Assert.IsTrue(plan.HasMove);
            Assert.AreEqual(1, GridFootprint.ManhattanDistance(plan.Destination, Two, new GridCoord(6, 0)));
        }

        [Test]
        public void Kite_2x2_MeasuresNearestCell()
        {
            // Ancla (2,2) cubre (2,2)-(3,3); target (4,2) a dist rect 1 → kitear a ideal 3.
            var grid = MakeGridWithBig(10, 10, new GridCoord(2, 2));
            var planner = new AIPathPlanner(grid);

            var plan = planner.PlanMove(Request(new GridCoord(2, 2), new GridCoord(4, 2), 4, 3, MoveIntent.Kite));

            Assert.IsTrue(plan.HasMove);
            Assert.GreaterOrEqual(
                GridFootprint.ManhattanDistance(plan.Destination, Two, new GridCoord(4, 2)), 3);
        }
    }
}
