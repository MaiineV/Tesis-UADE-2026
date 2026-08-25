using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Grid;
using Rollgeon.Movement;
using Rollgeon.PreConditions.Concretes;

namespace Rollgeon.PreConditions.Tests
{
    [TestFixture]
    public class PcOwnerAtRoomCenterTests
    {
        private GridManager _grid;
        private Guid _ownerId;

        [SetUp]
        public void Setup()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            ServiceLocator.AddService<IGridManager>(_grid);
            _ownerId = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private static PreConditionContext Ctx(Guid owner) => new PreConditionContext { OwnerGuid = owner };

        [Test]
        public void Evaluate_OwnerOnCenterTile_ReturnsTrue()
        {
            _grid.LoadRoom(NavGraph.Rect(9, 9));
            _grid.Register(_ownerId, new GridCoord(4, 4));

            Assert.IsTrue(new PcOwnerAtRoomCenter().Evaluate(Ctx(_ownerId)));
        }

        [Test]
        public void Evaluate_OwnerOneStepFromCenter_ReturnsFalse()
        {
            _grid.LoadRoom(NavGraph.Rect(9, 9));
            _grid.Register(_ownerId, new GridCoord(5, 4));

            Assert.IsFalse(new PcOwnerAtRoomCenter().Evaluate(Ctx(_ownerId)));
        }

        [Test]
        public void Evaluate_NoRoomLoaded_ReturnsFalse()
        {
            _grid.Register(_ownerId, new GridCoord(0, 0));

            Assert.IsFalse(new PcOwnerAtRoomCenter().Evaluate(Ctx(_ownerId)));
        }

        [Test]
        public void Evaluate_OwnerNotRegistered_ReturnsFalse()
        {
            _grid.LoadRoom(NavGraph.Rect(9, 9));

            Assert.IsFalse(new PcOwnerAtRoomCenter().Evaluate(Ctx(_ownerId)));
        }

        [Test]
        public void Evaluate_NoGrid_ReturnsFalse()
        {
            ServiceLocator.Clear();

            Assert.IsFalse(new PcOwnerAtRoomCenter().Evaluate(Ctx(_ownerId)));
        }

        [Test]
        public void Evaluate_EmptyGuid_ReturnsFalse()
        {
            Assert.IsFalse(new PcOwnerAtRoomCenter().Evaluate(new PreConditionContext()));
        }

        /// <summary>
        /// Ancla el requisito crítico: la precondición y <see cref="AINode_TeleportToRoomCenter"/>
        /// tienen que estar de acuerdo en qué casilla es "el centro", o el gate de fase 2 del
        /// Croupier queda en loop (teleporta, la PC sigue en false, vuelve a teleportar).
        /// </summary>
        [Test]
        public void Evaluate_AfterTeleportNodeRuns_AgreesWithTeleportDestination()
        {
            _grid.LoadRoom(NavGraph.Rect(9, 9));
            _grid.Register(_ownerId, new GridCoord(0, 0));

            var movement = new MovementService(_grid);
            var context = new AIContext { SelfGuid = _ownerId, Grid = _grid, Movement = movement };

            var result = new AINode_TeleportToRoomCenter().Tick(context);

            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.IsTrue(new PcOwnerAtRoomCenter().Evaluate(Ctx(_ownerId)));
        }
    }
}
