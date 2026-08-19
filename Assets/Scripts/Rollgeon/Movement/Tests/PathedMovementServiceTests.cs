using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rollgeon.Grid;

namespace Rollgeon.Movement.Tests
{
    /// <summary>
    /// Tests de la cara <see cref="IPathedMovementService"/> de <see cref="MovementService"/>:
    /// CommitPath (path explícito, validación, evento con ESE path), Teleport (evento propio,
    /// nunca OnEntityMoved) y el <see cref="IMovementPathFilter"/> sobre Move.
    /// </summary>
    [TestFixture]
    public class PathedMovementServiceTests
    {
        private GridManager _grid;
        private MovementService _movement;
        private Guid _entity;

        private List<(Guid entity, GridCoord from, GridCoord to, IReadOnlyList<GridCoord> path)> _movedLog;
        private List<(Guid entity, GridCoord from, GridCoord to)> _teleportedLog;

        [SetUp]
        public void SetUp()
        {
            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(5, 5));
            _movement = new MovementService(_grid);

            _entity = Guid.NewGuid();
            _grid.Register(_entity, new GridCoord(0, 0));

            _movedLog = new List<(Guid, GridCoord, GridCoord, IReadOnlyList<GridCoord>)>();
            _teleportedLog = new List<(Guid, GridCoord, GridCoord)>();
            _movement.OnEntityMoved += (e, f, t, p) => _movedLog.Add((e, f, t, p));
            _movement.OnEntityTeleported += (e, f, t) => _teleportedLog.Add((e, f, t));
        }

        private static List<GridCoord> Path(params (int x, int y)[] coords)
            => coords.Select(c => new GridCoord(c.x, c.y)).ToList();

        // ======================================================================
        // CommitPath
        // ======================================================================

        [Test]
        public void CommitPath_ValidPath_MovesEntityAndRaisesEventWithGivenPath()
        {
            var path = Path((0, 0), (1, 0), (2, 0));

            bool moved = _movement.CommitPath(_entity, path);

            Assert.IsTrue(moved);
            Assert.IsTrue(_grid.TryGetPosition(_entity, out var pos));
            Assert.AreEqual(new GridCoord(2, 0), pos);
            Assert.AreEqual(1, _movedLog.Count);
            Assert.AreEqual(new GridCoord(0, 0), _movedLog[0].from);
            Assert.AreEqual(new GridCoord(2, 0), _movedLog[0].to);
            CollectionAssert.AreEqual(path, _movedLog[0].path.ToList(),
                "El evento anuncia el path del caller, no un recálculo A*.");
        }

        [Test]
        public void CommitPath_NonContiguousPath_FailsWithoutMoving()
        {
            var path = Path((0, 0), (2, 0));

            bool moved = _movement.CommitPath(_entity, path);

            Assert.IsFalse(moved);
            Assert.IsTrue(_grid.TryGetPosition(_entity, out var pos));
            Assert.AreEqual(new GridCoord(0, 0), pos);
            Assert.AreEqual(0, _movedLog.Count);
        }

        [Test]
        public void CommitPath_PathNotStartingAtEntityPosition_Fails()
        {
            var path = Path((1, 1), (1, 2));

            bool moved = _movement.CommitPath(_entity, path);

            Assert.IsFalse(moved);
            Assert.AreEqual(0, _movedLog.Count);
        }

        [Test]
        public void CommitPath_StepOntoOccupiedTile_Fails()
        {
            _grid.Register(Guid.NewGuid(), new GridCoord(1, 0));
            var path = Path((0, 0), (1, 0), (2, 0));

            bool moved = _movement.CommitPath(_entity, path);

            Assert.IsFalse(moved);
            Assert.IsTrue(_grid.TryGetPosition(_entity, out var pos));
            Assert.AreEqual(new GridCoord(0, 0), pos);
        }

        [Test]
        public void CommitPath_OriginOnlyPath_IsValidNoOpWithoutEvent()
        {
            bool moved = _movement.CommitPath(_entity, Path((0, 0)));

            Assert.IsTrue(moved);
            Assert.AreEqual(0, _movedLog.Count);
        }

        // ======================================================================
        // Teleport
        // ======================================================================

        [Test]
        public void Teleport_MovesEntity_RaisesTeleportedButNotMoved()
        {
            bool teleported = _movement.Teleport(_entity, new GridCoord(4, 4));

            Assert.IsTrue(teleported);
            Assert.IsTrue(_grid.TryGetPosition(_entity, out var pos));
            Assert.AreEqual(new GridCoord(4, 4), pos);
            Assert.AreEqual(1, _teleportedLog.Count);
            Assert.AreEqual(new GridCoord(0, 0), _teleportedLog[0].from);
            Assert.AreEqual(new GridCoord(4, 4), _teleportedLog[0].to);
            Assert.AreEqual(0, _movedLog.Count,
                "Teleport nunca dispara OnEntityMoved — spec: no genera OnEnter.");
        }

        [Test]
        public void Teleport_OccupiedDestination_Fails()
        {
            _grid.Register(Guid.NewGuid(), new GridCoord(4, 4));

            bool teleported = _movement.Teleport(_entity, new GridCoord(4, 4));

            Assert.IsFalse(teleported);
            Assert.AreEqual(0, _teleportedLog.Count);
        }

        // ======================================================================
        // IMovementPathFilter
        // ======================================================================

        private sealed class TruncateAtFilter : IMovementPathFilter
        {
            private readonly GridCoord _terminator;
            public TruncateAtFilter(GridCoord terminator) => _terminator = terminator;

            public IReadOnlyList<GridCoord> Filter(Guid entity, IReadOnlyList<GridCoord> plannedPath)
            {
                var result = new List<GridCoord>();
                foreach (var coord in plannedPath)
                {
                    result.Add(coord);
                    if (coord == _terminator) break;
                }
                return result;
            }
        }

        [Test]
        public void Move_WithTruncatingFilter_CommitsTruncatedPathAndEventMatches()
        {
            // El "hielo" está en (2,0): el path a (4,0) debe cortarse ahí inclusive.
            _movement.SetPathFilter(new TruncateAtFilter(new GridCoord(2, 0)));

            bool moved = _movement.Move(_entity, new GridCoord(4, 0));

            Assert.IsTrue(moved);
            Assert.IsTrue(_grid.TryGetPosition(_entity, out var pos));
            Assert.AreEqual(new GridCoord(2, 0), pos,
                "La ocupación lógica queda en la casilla terminadora, no en el destino pedido.");
            Assert.AreEqual(1, _movedLog.Count);
            Assert.AreEqual(new GridCoord(2, 0), _movedLog[0].to);
            Assert.AreEqual(new GridCoord(2, 0), _movedLog[0].path.Last(),
                "OnEntityMoved anuncia solo las casillas realmente pisadas.");
        }

        [Test]
        public void Move_FilterReturnsOnlyOrigin_FailsWithoutMoving()
        {
            _movement.SetPathFilter(new TruncateAtFilter(new GridCoord(0, 0)));

            bool moved = _movement.Move(_entity, new GridCoord(3, 0));

            Assert.IsFalse(moved);
            Assert.AreEqual(0, _movedLog.Count);
        }

        [Test]
        public void Move_NullFilterCleared_BehavesAsUnfiltered()
        {
            _movement.SetPathFilter(new TruncateAtFilter(new GridCoord(1, 0)));
            _movement.SetPathFilter(null);

            bool moved = _movement.Move(_entity, new GridCoord(3, 0));

            Assert.IsTrue(moved);
            Assert.IsTrue(_grid.TryGetPosition(_entity, out var pos));
            Assert.AreEqual(new GridCoord(3, 0), pos);
        }

        [Test]
        public void CommitPath_WithApplyFilterTrue_TruncatesLikeVoluntaryMove()
        {
            _movement.SetPathFilter(new TruncateAtFilter(new GridCoord(1, 0)));
            var path = Path((0, 0), (1, 0), (2, 0), (3, 0));

            bool moved = _movement.CommitPath(_entity, path, applyPathFilter: true);

            Assert.IsTrue(moved);
            Assert.IsTrue(_grid.TryGetPosition(_entity, out var pos));
            Assert.AreEqual(new GridCoord(1, 0), pos);
            Assert.AreEqual(new GridCoord(1, 0), _movedLog[0].path.Last());
        }

        [Test]
        public void CommitPath_DefaultRaw_IgnoresFilter()
        {
            _movement.SetPathFilter(new TruncateAtFilter(new GridCoord(1, 0)));
            var path = Path((0, 0), (1, 0), (2, 0));

            bool moved = _movement.CommitPath(_entity, path);

            Assert.IsTrue(moved);
            Assert.IsTrue(_grid.TryGetPosition(_entity, out var pos));
            Assert.AreEqual(new GridCoord(2, 0), pos,
                "Los segmentos del motor de tiles son crudos: el caller ya resolvió la semántica.");
        }
    }
}
