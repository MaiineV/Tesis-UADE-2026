using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Grid;
using Rollgeon.PreConditions.Concretes;

namespace Rollgeon.PreConditions.Tests
{
    [TestFixture]
    public class PCEntityInRangeTests
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
            _grid.Register(_ownerId,    new GridCoord(0, 0));
            _grid.Register(_opponentId, new GridCoord(2, 1));   // Manhattan = 3
            var pc = new PCEntityInRange { MaxRange = 3, Metric = DistanceMetric.Manhattan };
            Assert.IsTrue(pc.Evaluate(Ctx(_ownerId, _opponentId)));
        }

        [Test]
        public void Evaluate_Manhattan_FailsBeyondRange()
        {
            _grid.Register(_ownerId,    new GridCoord(0, 0));
            _grid.Register(_opponentId, new GridCoord(2, 2));   // Manhattan = 4
            var pc = new PCEntityInRange { MaxRange = 3, Metric = DistanceMetric.Manhattan };
            Assert.IsFalse(pc.Evaluate(Ctx(_ownerId, _opponentId)));
        }

        [Test]
        public void Evaluate_Chebyshev_DiagonalCountsAsOne()
        {
            _grid.Register(_ownerId,    new GridCoord(0, 0));
            _grid.Register(_opponentId, new GridCoord(2, 2));   // Manhattan = 4, Chebyshev = 2
            var manhattan = new PCEntityInRange { MaxRange = 2, Metric = DistanceMetric.Manhattan };
            var chebyshev = new PCEntityInRange { MaxRange = 2, Metric = DistanceMetric.Chebyshev };
            Assert.IsFalse(manhattan.Evaluate(Ctx(_ownerId, _opponentId)));
            Assert.IsTrue(chebyshev.Evaluate(Ctx(_ownerId, _opponentId)));
        }

        [Test]
        public void Evaluate_SameTile_PassesWithRangeZero()
        {
            _grid.Register(_ownerId,    new GridCoord(3, 3));
            _grid.Register(_opponentId, new GridCoord(3, 3));
            var pc = new PCEntityInRange { MaxRange = 0 };
            Assert.IsTrue(pc.Evaluate(Ctx(_ownerId, _opponentId)));
        }

        [Test]
        public void Evaluate_OwnerNotRegistered_ReturnsFalse()
        {
            _grid.Register(_opponentId, new GridCoord(0, 0));
            var pc = new PCEntityInRange { MaxRange = 5 };
            Assert.IsFalse(pc.Evaluate(Ctx(_ownerId, _opponentId)));
        }

        [Test]
        public void Evaluate_OpponentNotRegistered_ReturnsFalse()
        {
            _grid.Register(_ownerId, new GridCoord(0, 0));
            var pc = new PCEntityInRange { MaxRange = 5 };
            Assert.IsFalse(pc.Evaluate(Ctx(_ownerId, _opponentId)));
        }

        [Test]
        public void Evaluate_NoGridManager_ReturnsFalse()
        {
            ServiceLocator.Clear();
            var pc = new PCEntityInRange { MaxRange = 5 };
            Assert.IsFalse(pc.Evaluate(Ctx(_ownerId, _opponentId)));
        }

        [Test]
        public void Evaluate_EmptyGuids_ReturnsFalse()
        {
            var pc = new PCEntityInRange { MaxRange = 5 };
            Assert.IsFalse(pc.Evaluate(new PreConditionContext()));
            Assert.IsFalse(pc.Evaluate(new PreConditionContext { OwnerGuid = _ownerId }));
            Assert.IsFalse(pc.Evaluate(new PreConditionContext { OpponentGuid = _opponentId }));
        }
    }
}
