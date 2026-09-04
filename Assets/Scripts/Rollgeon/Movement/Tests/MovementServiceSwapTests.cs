using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Grid;

namespace Rollgeon.Movement.Tests
{
    /// <summary>
    /// <see cref="IPathedMovementService.Swap"/> (Probability Drive del rediseño de ítems
    /// activos): intercambia dos entidades ya registradas y avisa por <c>OnEntityTeleported</c>,
    /// igual que <see cref="MovementService.Teleport"/>.
    /// </summary>
    [TestFixture]
    public sealed class MovementServiceSwapTests
    {
        private GridManager _grid;
        private MovementService _movement;
        private Guid _a;
        private Guid _b;

        private List<(Guid entity, GridCoord from, GridCoord to)> _teleportedLog;

        [SetUp]
        public void SetUp()
        {
            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(5, 5));
            _movement = new MovementService(_grid);

            _a = Guid.NewGuid();
            _b = Guid.NewGuid();
            _grid.Register(_a, new GridCoord(0, 0));
            _grid.Register(_b, new GridCoord(4, 4));

            _teleportedLog = new List<(Guid, GridCoord, GridCoord)>();
            _movement.OnEntityTeleported += (e, f, t) => _teleportedLog.Add((e, f, t));
        }

        [Test]
        public void Swap_BothRegistered_ExchangesPositions()
        {
            Assert.IsTrue(_movement.Swap(_a, _b));

            Assert.IsTrue(_grid.TryGetPosition(_a, out var coordA));
            Assert.IsTrue(_grid.TryGetPosition(_b, out var coordB));
            Assert.AreEqual(new GridCoord(4, 4), coordA);
            Assert.AreEqual(new GridCoord(0, 0), coordB);
        }

        [Test]
        public void Swap_BothRegistered_RaisesTeleportedForBoth()
        {
            Assert.IsTrue(_movement.Swap(_a, _b));

            Assert.AreEqual(2, _teleportedLog.Count);
            CollectionAssert.Contains(_teleportedLog, (_a, new GridCoord(0, 0), new GridCoord(4, 4)));
            CollectionAssert.Contains(_teleportedLog, (_b, new GridCoord(4, 4), new GridCoord(0, 0)));
        }

        [Test]
        public void Swap_FirstEntityNotRegistered_ReturnsFalseAndNoOp()
        {
            var unregistered = Guid.NewGuid();

            Assert.IsFalse(_movement.Swap(unregistered, _b));

            Assert.IsTrue(_grid.TryGetPosition(_b, out var coordB));
            Assert.AreEqual(new GridCoord(4, 4), coordB, "sin cambios: la otra entidad no se movió");
            CollectionAssert.IsEmpty(_teleportedLog);
        }

        [Test]
        public void Swap_SecondEntityNotRegistered_ReturnsFalseAndNoOp()
        {
            var unregistered = Guid.NewGuid();

            Assert.IsFalse(_movement.Swap(_a, unregistered));

            Assert.IsTrue(_grid.TryGetPosition(_a, out var coordA));
            Assert.AreEqual(new GridCoord(0, 0), coordA);
            CollectionAssert.IsEmpty(_teleportedLog);
        }
    }
}
