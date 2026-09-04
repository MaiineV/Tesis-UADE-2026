using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Entities.Traits;
using Rollgeon.Entities.Visuals;
using Rollgeon.Grid;
using Rollgeon.Movement;
using UnityEngine;

namespace Rollgeon.Tiles.Tests
{
    /// <summary>
    /// Spawn diferido del arte de los rastros (Incendiario / Rastro tóxico / Sendero de espinas):
    /// la lógica coloca la casilla al instante, pero su visual espera a que el pawn del dueño
    /// abandone la celda (<see cref="IPawnWalkTracker"/>). Sin tracker, o con el pawn quieto,
    /// todo aparece al instante como siempre.
    /// </summary>
    [TestFixture]
    public sealed class DeferredTrailVisualTests
    {
        private GridManager _grid;
        private SpecialTileService _svc;
        private FakeWalkTracker _tracker;
        private GameObject _marker;
        private Guid _player;
        private readonly List<ScriptableObject> _created = new List<ScriptableObject>();

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(7, 3));
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);
            ServiceLocator.AddService<IMovementService>(new MovementService(_grid), ServiceScope.Global);
            ServiceLocator.AddService<IUnitTraitService>(new UnitTraitService(), ServiceScope.Global);

            _player = Guid.NewGuid();
            _grid.Register(_player, new GridCoord(3, 0));

            _tracker = new FakeWalkTracker();
            _marker = new GameObject("TrailMarker");

            _svc = new SpecialTileService();
            _svc.ConfigureForTests(() => _player);
            ServiceLocator.AddService<ISpecialTileService>(_svc, ServiceScope.Global);
        }

        [TearDown]
        public void TearDown()
        {
            _svc?.Dispose();
            _svc = null;
            if (_marker != null) UnityEngine.Object.DestroyImmediate(_marker);
            foreach (var so in _created) if (so != null) UnityEngine.Object.DestroyImmediate(so);
            _created.Clear();
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        private void RegisterTracker()
            => ServiceLocator.AddService<IPawnWalkTracker>(_tracker, ServiceScope.Global);

        private SpecialTileDefinitionSO MakeTrail()
        {
            var d = ScriptableObject.CreateInstance<SpecialTileDefinitionSO>();
            d.TileId = "TILE_TRAIL_TEST";
            d.TileType = SpecialTileType.Spikes;
            d.Triggers = TileTrigger.OnEnter;
            d.Category = TileEffectCategory.Damage;
            d.Affinity = TileAffinity.All;
            d.EnterDamage = 1;
            d.OwnerAndAlliesImmune = true;
            d.DefaultDurationRounds = 2;
            d.VisualPrefab = _marker;
            _created.Add(d);
            return d;
        }

        private Guid Trail(SpecialTileDefinitionSO def, GridCoord coord)
        {
            var id = _svc.CreateRuntime(def, coord, new RuntimeTileRequest { Owner = _player, DurationRounds = 2 }, out var error);
            Assert.AreEqual(TilePlacementError.None, error, $"pre-condition: casilla en {coord}");
            return id;
        }

        [Test]
        public void WithoutTracker_TheVisualSpawnsImmediately()
        {
            var id = Trail(MakeTrail(), new GridCoord(2, 0));

            Assert.AreEqual(1, _svc.VisualCountForTests(id));
            Assert.AreEqual(0, _svc.DeferredVisualCountForTests);
        }

        [Test]
        public void OwnerStillWalkingThroughTheCell_DefersOnlyThatVisual()
        {
            RegisterTracker();
            _tracker.Ahead.Add((_player, new GridCoord(2, 0)));
            var def = MakeTrail();

            var behind = Trail(def, new GridCoord(1, 0)); // ya cruzada: aparece
            var ahead = Trail(def, new GridCoord(2, 0));  // el cuerpo todavía no la abandonó

            Assert.AreEqual(1, _svc.VisualCountForTests(behind));
            Assert.AreEqual(0, _svc.VisualCountForTests(ahead));
            Assert.AreEqual(1, _svc.DeferredVisualCountForTests);
        }

        [Test]
        public void OnCellLeft_RevealsTheVisualOfThatCell()
        {
            RegisterTracker();
            _tracker.Ahead.Add((_player, new GridCoord(1, 0)));
            _tracker.Ahead.Add((_player, new GridCoord(2, 0)));
            var def = MakeTrail();
            var first = Trail(def, new GridCoord(1, 0));
            var second = Trail(def, new GridCoord(2, 0));

            _tracker.LeaveCell(_player, new GridCoord(1, 0));

            Assert.AreEqual(1, _svc.VisualCountForTests(first));
            Assert.AreEqual(0, _svc.VisualCountForTests(second), "la siguiente sigue esperando");
            Assert.AreEqual(1, _svc.DeferredVisualCountForTests);

            _tracker.LeaveCell(_player, new GridCoord(2, 0));

            Assert.AreEqual(1, _svc.VisualCountForTests(second));
            Assert.AreEqual(0, _svc.DeferredVisualCountForTests);
        }

        [Test]
        public void OnWalkEnded_FlushesEverythingPendingForThatOwner()
        {
            RegisterTracker();
            _tracker.Ahead.Add((_player, new GridCoord(1, 0)));
            _tracker.Ahead.Add((_player, new GridCoord(2, 0)));
            var def = MakeTrail();
            var first = Trail(def, new GridCoord(1, 0));
            var second = Trail(def, new GridCoord(2, 0));

            _tracker.EndWalk(_player);

            Assert.AreEqual(1, _svc.VisualCountForTests(first));
            Assert.AreEqual(1, _svc.VisualCountForTests(second));
            Assert.AreEqual(0, _svc.DeferredVisualCountForTests);
        }

        [Test]
        public void AnotherEntityWalking_DoesNotDeferTheOwnersTrail()
        {
            RegisterTracker();
            _tracker.Ahead.Add((Guid.NewGuid(), new GridCoord(2, 0)));

            var id = Trail(MakeTrail(), new GridCoord(2, 0));

            Assert.AreEqual(1, _svc.VisualCountForTests(id));
            Assert.AreEqual(0, _svc.DeferredVisualCountForTests);
        }

        [Test]
        public void ExpiredBeforeTheReveal_NeverSpawnsAndDropsThePending()
        {
            RegisterTracker();
            _tracker.Ahead.Add((_player, new GridCoord(2, 0)));
            var id = Trail(MakeTrail(), new GridCoord(2, 0));
            Assert.AreEqual(1, _svc.DeferredVisualCountForTests, "pre-condition");

            _svc.Remove(id);
            Assert.AreEqual(0, _svc.DeferredVisualCountForTests, "expirar suelta lo pendiente");

            Assert.DoesNotThrow(() => _tracker.LeaveCell(_player, new GridCoord(2, 0)));
            Assert.AreEqual(-1, _svc.VisualCountForTests(id));
        }

        [Test]
        public void Dispose_WithPendingVisuals_UnsubscribesFromTheTracker()
        {
            RegisterTracker();
            _tracker.Ahead.Add((_player, new GridCoord(2, 0)));
            Trail(MakeTrail(), new GridCoord(2, 0));

            _svc.Dispose();
            _svc = null;

            Assert.AreEqual(0, _tracker.SubscriberCount, "sin listeners colgados tras el teardown");
        }

        // ---- Fake -------------------------------------------------------------

        private sealed class FakeWalkTracker : IPawnWalkTracker
        {
            public readonly HashSet<(Guid, GridCoord)> Ahead = new HashSet<(Guid, GridCoord)>();

            public event Action<Guid, GridCoord> OnCellLeft;
            public event Action<Guid> OnWalkEnded;

            public int SubscriberCount
                => (OnCellLeft?.GetInvocationList().Length ?? 0) + (OnWalkEnded?.GetInvocationList().Length ?? 0);

            public bool IsWalkingThrough(Guid entity, GridCoord coord) => Ahead.Contains((entity, coord));

            public void LeaveCell(Guid entity, GridCoord coord)
            {
                Ahead.Remove((entity, coord));
                OnCellLeft?.Invoke(entity, coord);
            }

            public void EndWalk(Guid entity)
            {
                Ahead.RemoveWhere(x => x.Item1 == entity);
                OnWalkEnded?.Invoke(entity);
            }
        }
    }
}
