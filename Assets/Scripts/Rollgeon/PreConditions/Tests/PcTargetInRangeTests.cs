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
