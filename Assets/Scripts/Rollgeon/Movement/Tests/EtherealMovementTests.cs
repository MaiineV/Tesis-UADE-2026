using System;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Grid;

namespace Rollgeon.Movement.Tests
{
    /// <summary>
    /// Paso etéreo: con una <see cref="IMovementTraversalPolicy"/> que lo autorice, la entidad
    /// atraviesa celdas ocupadas como paso intermedio (BFS y A*) pero nunca termina en una.
    /// Sin política, todo sigue como antes.
    /// </summary>
    [TestFixture]
    public sealed class EtherealMovementTests
    {
        private GridManager _grid;
        private MovementService _movement;
        private Guid _mover;
        private Guid _blocker;

        private sealed class PolicyFor : IMovementTraversalPolicy
        {
            private readonly Guid _who;
            public PolicyFor(Guid who) { _who = who; }
            public bool CanPassThroughUnits(Guid entity) => entity == _who;
        }

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            // Pasillo 5×1: el bloqueador en el medio corta el paso salvo con Paso etéreo.
            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(5, 1));
            _movement = new MovementService(_grid);
            _mover = Guid.NewGuid();
            _blocker = Guid.NewGuid();
            _grid.Register(_mover, new GridCoord(0, 0));
            _grid.Register(_blocker, new GridCoord(2, 0));
        }

        [TearDown]
        public void TearDown() => ServiceLocator.Clear();

        [Test]
        public void WithoutPolicy_UnitsBlockBothReachabilityAndPath()
        {
            var reach = _movement.GetReachableTilesFor(_mover, new GridCoord(0, 0), 4);
            CollectionAssert.AreEquivalent(new[] { new GridCoord(1, 0) }, reach);
            Assert.IsFalse(_movement.TryMove(_mover, new GridCoord(4, 0), out _));
        }

        [Test]
        public void WithPolicy_PassesThroughTheUnitButNeverOffersItsCell()
        {
            ServiceLocator.AddService<IMovementTraversalPolicy>(new PolicyFor(_mover), ServiceScope.Global);

            var reach = _movement.GetReachableTilesFor(_mover, new GridCoord(0, 0), 4);

            CollectionAssert.AreEquivalent(
                new[] { new GridCoord(1, 0), new GridCoord(3, 0), new GridCoord(4, 0) }, reach);
            Assert.IsFalse(reach.Contains(new GridCoord(2, 0)), "La celda ocupada no es destino.");
        }

        [Test]
        public void WithPolicy_TryMoveWalksOverTheUnitAndReportsTheFullPath()
        {
            ServiceLocator.AddService<IMovementTraversalPolicy>(new PolicyFor(_mover), ServiceScope.Global);

            Assert.IsTrue(_movement.TryMove(_mover, new GridCoord(4, 0), out var path));

            Assert.AreEqual(5, path.Count);
            Assert.AreEqual(new GridCoord(2, 0), path[2]);
            Assert.IsTrue(_grid.TryGetPosition(_mover, out var pos) && pos == new GridCoord(4, 0));
            Assert.IsTrue(_grid.TryGetPosition(_blocker, out var bpos) && bpos == new GridCoord(2, 0));
        }

        [Test]
        public void WithPolicy_OtherEntitiesStayBlocked()
        {
            ServiceLocator.AddService<IMovementTraversalPolicy>(new PolicyFor(_mover), ServiceScope.Global);

            Assert.IsFalse(_movement.TryMove(_blocker, new GridCoord(0, 0), out _));
            var reach = _movement.GetReachableTiles(new GridCoord(0, 0), 4);
            CollectionAssert.AreEquivalent(new[] { new GridCoord(1, 0) }, reach);
        }

        [Test]
        public void FindPathFor_CrossesTheUnitWithPolicy_WhileFindPathStaysBlind()
        {
            ServiceLocator.AddService<IMovementTraversalPolicy>(new PolicyFor(_mover), ServiceScope.Global);
            var from = new GridCoord(0, 0);
            var to = new GridCoord(4, 0);

            var forMover = _movement.FindPathFor(_mover, from, to);
            var plain = _movement.FindPath(from, to);
            var forBlocker = _movement.FindPathFor(_blocker, from, to);

            // El preview de la UI tiene que mostrar el mismo camino que después camina TryMove.
            Assert.AreEqual(5, forMover.Count);
            Assert.AreEqual(new GridCoord(2, 0), forMover[2]);
            Assert.IsEmpty(plain, "sin entidad no hay política que aplicar");
            Assert.IsEmpty(forBlocker, "otra entidad sigue bloqueada");
        }

        [Test]
        public void CanPassThroughUnits_FollowsTheRegisteredPolicy()
        {
            Assert.IsFalse(_movement.CanPassThroughUnits(_mover), "sin política nadie atraviesa");

            ServiceLocator.AddService<IMovementTraversalPolicy>(new PolicyFor(_mover), ServiceScope.Global);

            Assert.IsTrue(_movement.CanPassThroughUnits(_mover));
            Assert.IsFalse(_movement.CanPassThroughUnits(_blocker));
            Assert.IsFalse(_movement.CanPassThroughUnits(Guid.Empty));
        }
    }
}
