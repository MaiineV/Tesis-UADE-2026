using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Grid;
using Rollgeon.Movement;
using Rollgeon.Tiles.Forced;

namespace Rollgeon.Tiles.Tests
{
    /// <summary>
    /// <see cref="PullResolver.PullToward"/>: empuja hacia el ancla frenando adyacente (Garfio del
    /// rediseño de ítems activos) — nunca encima.
    /// </summary>
    [TestFixture]
    public sealed class PullResolverTests
    {
        private GridManager _grid;
        private ForcedMovementService _forced;
        private Guid _entity;
        private Guid _source;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(6, 1)); // x: 0..5
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);

            // El motor de empuje delega el commit del camino en IMovementService.
            ServiceLocator.AddService<IMovementService>(new MovementService(_grid), ServiceScope.Global);

            _forced = new ForcedMovementService();
            ServiceLocator.AddService<IForcedMovementService>(_forced, ServiceScope.Global);

            _entity = Guid.NewGuid();
            _source = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        [Test]
        public void PullToward_FarFromAnchor_StopsAdjacent()
        {
            _grid.Register(_entity, new GridCoord(0, 0));
            var anchor = new GridCoord(5, 0);

            var result = PullResolver.PullToward(_forced, _grid, _entity, anchor, maxTiles: 10, sourceId: _source);

            Assert.IsTrue(_grid.TryGetPosition(_entity, out var coord));
            Assert.AreEqual(new GridCoord(4, 0), coord, "frena adyacente al ancla, nunca encima");
            Assert.AreEqual(4, result.TilesTraveled);
        }

        [Test]
        public void PullToward_AlreadyAdjacent_IsNoOp()
        {
            _grid.Register(_entity, new GridCoord(4, 0));
            var anchor = new GridCoord(5, 0);

            var result = PullResolver.PullToward(_forced, _grid, _entity, anchor, maxTiles: 10, sourceId: _source);

            Assert.AreEqual(0, result.TilesTraveled);
            Assert.AreEqual(ForcedMoveStop.CompletedDistance, result.StoppedBy);
            Assert.IsTrue(_grid.TryGetPosition(_entity, out var coord));
            Assert.AreEqual(new GridCoord(4, 0), coord, "no se movió");
        }

        [Test]
        public void PullToward_MaxTilesBelowDistance_PullsPartial()
        {
            _grid.Register(_entity, new GridCoord(0, 0));
            var anchor = new GridCoord(5, 0);

            var result = PullResolver.PullToward(_forced, _grid, _entity, anchor, maxTiles: 2, sourceId: _source);

            Assert.AreEqual(2, result.TilesTraveled);
            Assert.IsTrue(_grid.TryGetPosition(_entity, out var coord));
            Assert.AreEqual(new GridCoord(2, 0), coord);
        }

        [Test]
        public void PullToward_EntityNotRegistered_ReturnsNoOp()
        {
            var result = PullResolver.PullToward(_forced, _grid, _entity, new GridCoord(5, 0),
                maxTiles: 10, sourceId: _source);

            Assert.AreEqual(0, result.TilesTraveled);
            Assert.AreEqual(ForcedMoveStop.CompletedDistance, result.StoppedBy);
        }
    }
}
