using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Grid;
using Rollgeon.PreConditions.Concretes;

namespace Rollgeon.PreConditions.Tests
{
    [TestFixture]
    public class PcTargetInRangeTests
    {
        private GridManager _grid;
        private Guid _ownerId;
        private Guid _opponentId;

        [SetUp]
        public void Setup()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            ServiceLocator.AddService<IGridManager>(_grid);
            _ownerId = Guid.NewGuid();
            _opponentId = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private static PreConditionContext Ctx(Guid owner, Guid opp) =>
            new PreConditionContext { OwnerGuid = owner, OpponentGuid = opp };

        [Test]
        public void Evaluate_Manhattan_PassesWithinRange()
        {
            _grid.Register(_ownerId, new GridCoord(0, 0));
            _grid.Register(_opponentId, new GridCoord(2, 1)); // Manhattan = 3
            Assert.IsTrue(new PcTargetInRange { Range = 3 }.Evaluate(Ctx(_ownerId, _opponentId)));
        }

        [Test]
        public void Evaluate_Manhattan_FailsBeyondRange()
        {
            _grid.Register(_ownerId, new GridCoord(0, 0));
            _grid.Register(_opponentId, new GridCoord(2, 2)); // Manhattan = 4
            Assert.IsFalse(new PcTargetInRange { Range = 3 }.Evaluate(Ctx(_ownerId, _opponentId)));
        }

        [Test]
        public void Evaluate_Chebyshev_DiagonalCountsAsOne()
        {
            _grid.Register(_ownerId, new GridCoord(0, 0));
            _grid.Register(_opponentId, new GridCoord(2, 2));
            var manhattan = new PcTargetInRange { Range = 2, Metric = DistanceMetric.Manhattan };
            var chebyshev = new PcTargetInRange { Range = 2, Metric = DistanceMetric.Chebyshev };
            Assert.IsFalse(manhattan.Evaluate(Ctx(_ownerId, _opponentId)));
            Assert.IsTrue(chebyshev.Evaluate(Ctx(_ownerId, _opponentId)));
        }

        [Test]
        public void Evaluate_MultiCellOwner_AdjacentByNonAnchorCell_Passes()
        {
            // Owner 2×2 en (0,0) cubre (0,0)-(1,1); el opponent en (2,1) queda pegado a la
            // celda (1,1) — desde el ANCLA la Manhattan sería 3 y fallaba.
            _grid.LoadRoom(NavGraph.Rect(6, 6));
            _grid.TryRegister(_ownerId, new GridCoord(0, 0), new UnityEngine.Vector2Int(2, 2));
            _grid.Register(_opponentId, new GridCoord(2, 1));
            Assert.IsTrue(new PcTargetInRange { Range = 1 }.Evaluate(Ctx(_ownerId, _opponentId)));
        }

        [Test]
        public void Evaluate_MultiCellOwner_Chebyshev_UsesNearestCell()
        {
            _grid.LoadRoom(NavGraph.Rect(8, 8));
            _grid.TryRegister(_ownerId, new GridCoord(0, 0), new UnityEngine.Vector2Int(2, 2));
            _grid.Register(_opponentId, new GridCoord(3, 3)); // Chebyshev rect = 2, del ancla = 3
            Assert.IsTrue(new PcTargetInRange { Range = 2, Metric = DistanceMetric.Chebyshev }
                .Evaluate(Ctx(_ownerId, _opponentId)));
            Assert.IsFalse(new PcTargetInRange { Range = 1, Metric = DistanceMetric.Chebyshev }
                .Evaluate(Ctx(_ownerId, _opponentId)));
        }

        [Test]
        public void Evaluate_DiagonalOnly_PassesOnExactDiagonal()
        {
            _grid.LoadRoom(NavGraph.Rect(8, 8));
            _grid.Register(_ownerId, new GridCoord(2, 2));
            _grid.Register(_opponentId, new GridCoord(4, 4)); // |dx| == |dy| == 2

            var pc = new PcTargetInRange
            {
                Range = 4, Metric = DistanceMetric.Chebyshev, Alignment = TargetAlignment.DiagonalOnly,
            };
            Assert.IsTrue(pc.Evaluate(Ctx(_ownerId, _opponentId)));
        }

        [Test]
        public void Evaluate_DiagonalOnly_FailsOffDiagonal()
        {
            _grid.LoadRoom(NavGraph.Rect(8, 8));
            _grid.Register(_ownerId, new GridCoord(2, 2));
            _grid.Register(_opponentId, new GridCoord(4, 3)); // (2,1): no es diagonal exacta

            var pc = new PcTargetInRange
            {
                Range = 4, Metric = DistanceMetric.Chebyshev, Alignment = TargetAlignment.DiagonalOnly,
            };
            Assert.IsFalse(pc.Evaluate(Ctx(_ownerId, _opponentId)));
        }

        [Test]
        public void Evaluate_SameRowWithClearLineOfSight_Passes()
        {
            _grid.LoadRoom(NavGraph.Rect(8, 8));
            _grid.Register(_ownerId, new GridCoord(0, 3));
            _grid.Register(_opponentId, new GridCoord(5, 3));

            var pc = new PcTargetInRange
            {
                Range = 8, Alignment = TargetAlignment.SameRowOrColumn, RequireLineOfSight = true,
            };
            Assert.IsTrue(pc.Evaluate(Ctx(_ownerId, _opponentId)));
        }

        [Test]
        public void Evaluate_LineOfSight_BlockedByOccupant()
        {
            _grid.LoadRoom(NavGraph.Rect(8, 8));
            _grid.Register(_ownerId, new GridCoord(0, 3));
            _grid.Register(_opponentId, new GridCoord(5, 3));
            _grid.Register(Guid.NewGuid(), new GridCoord(3, 3)); // en el medio

            var pc = new PcTargetInRange
            {
                Range = 8, Alignment = TargetAlignment.SameRowOrColumn, RequireLineOfSight = true,
            };
            Assert.IsFalse(pc.Evaluate(Ctx(_ownerId, _opponentId)));
        }

        [Test]
        public void Evaluate_LineOfSight_BlockedByWall()
        {
            // Fila 3 con (3,3) no caminable.
            var walkable = new bool[64];
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    walkable[y * 8 + x] = !(x == 3 && y == 3);
            _grid.LoadRoom(NavGraph.FromSnapshot(new GridSnapshot(8, 8, walkable)));
            _grid.Register(_ownerId, new GridCoord(0, 3));
            _grid.Register(_opponentId, new GridCoord(5, 3));

            var pc = new PcTargetInRange
            {
                Range = 8, Alignment = TargetAlignment.SameRowOrColumn, RequireLineOfSight = true,
            };
            Assert.IsFalse(pc.Evaluate(Ctx(_ownerId, _opponentId)));
        }

        [Test]
        public void Evaluate_Alignment_UsesNearestCellPairOfFootprints()
        {
            // Owner 2×2 en (0,2) cubre (0,2)-(1,3); opponent en (4,3): alineado en fila con la
            // celda (1,3) — desde el ancla (0,2) no habría fila común.
            _grid.LoadRoom(NavGraph.Rect(8, 8));
            _grid.TryRegister(_ownerId, new GridCoord(0, 2), new UnityEngine.Vector2Int(2, 2));
            _grid.Register(_opponentId, new GridCoord(4, 3));

            var pc = new PcTargetInRange
            {
                Range = 8, Alignment = TargetAlignment.SameRowOrColumn, RequireLineOfSight = true,
            };
            Assert.IsTrue(pc.Evaluate(Ctx(_ownerId, _opponentId)));
        }

        [Test]
        public void Evaluate_NoGrid_ReturnsFalse()
        {
            ServiceLocator.Clear();
            Assert.IsFalse(new PcTargetInRange { Range = 5 }.Evaluate(Ctx(_ownerId, _opponentId)));
        }

        [Test]
        public void Evaluate_OpponentNotRegistered_ReturnsFalse()
        {
            _grid.Register(_ownerId, new GridCoord(0, 0));
            Assert.IsFalse(new PcTargetInRange { Range = 5 }.Evaluate(Ctx(_ownerId, _opponentId)));
        }

        [Test]
        public void Evaluate_EmptyGuids_ReturnsFalse()
        {
            Assert.IsFalse(new PcTargetInRange { Range = 5 }.Evaluate(new PreConditionContext()));
        }
    }
}
