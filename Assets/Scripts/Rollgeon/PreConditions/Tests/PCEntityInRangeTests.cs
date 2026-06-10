using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Grid;
using Rollgeon.PreConditions.Concretes;
using UnityEngine;

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
            // Arrange — el GridManager real impone 1 entity por tile (Register
            // desaloja al ocupante previo), así que para probar la semántica de
            // distancia 0 usamos un stub que solo trackea posiciones.
            var grid = new PositionOnlyGridStub();
            ServiceLocator.AddService<IGridManager>(grid);
            grid.Register(_ownerId,    new GridCoord(3, 3));
            grid.Register(_opponentId, new GridCoord(3, 3));
            var pc = new PCEntityInRange { MaxRange = 0 };

            // Act
            bool result = pc.Evaluate(Ctx(_ownerId, _opponentId));

            // Assert
            Assert.IsTrue(result);
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

        /// <summary>
        /// <see cref="IGridManager"/> mínimo sin invariante de ocupancia: permite dos
        /// entities en el mismo tile (el GridManager real desaloja al ocupante previo),
        /// necesario para testear la rama de distancia 0 de PCEntityInRange.
        /// </summary>
        private sealed class PositionOnlyGridStub : IGridManager
        {
            private readonly Dictionary<Guid, GridCoord> _positions =
                new Dictionary<Guid, GridCoord>();

            public NavGraph Graph { get; } = new NavGraph();
            public Vector3 GridOrigin => Vector3.zero;
            public float TileSize => 1f;

            public void LoadRoom(NavGraph graph, Vector3 origin = default, float tileSize = 1f) { }
            public bool InBounds(GridCoord c) => true;
            public bool IsWalkable(GridCoord c) => true;
            public bool IsOccupied(GridCoord c) => false;
            public bool IsFree(GridCoord c) => true;

            public bool TryGetOccupant(GridCoord c, out Guid entityGuid)
            {
                entityGuid = Guid.Empty;
                return false;
            }

            public bool TryGetPosition(Guid entityGuid, out GridCoord coord) =>
                _positions.TryGetValue(entityGuid, out coord);

            public void Register(Guid entityGuid, GridCoord coord) =>
                _positions[entityGuid] = coord;

            public void Unregister(Guid entityGuid) => _positions.Remove(entityGuid);

            public bool Move(Guid entityGuid, GridCoord to)
            {
                _positions[entityGuid] = to;
                return true;
            }

            public Vector3 GridToWorld(GridCoord c) => new Vector3(c.X, 0f, c.Y);
            public GridCoord WorldToGrid(Vector3 world) =>
                new GridCoord((int)world.x, (int)world.z);

            public IEnumerable<KeyValuePair<Guid, GridCoord>> Occupants() => _positions;
        }
    }
}
