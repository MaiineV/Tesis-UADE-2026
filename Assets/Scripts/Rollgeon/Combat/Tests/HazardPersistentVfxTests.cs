using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Dice;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.Movement;
using Rollgeon.Player;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Combat.Tests
{
    /// <summary>
    /// <see cref="HazardDefinitionSO.PersistentVfxPrefab"/>: la llama que queda encendida mientras
    /// el hazard dura, a diferencia del fogonazo de <c>TriggerVfxPrefab</c>.
    /// </summary>
    /// <remarks>
    /// Mismo seam que <see cref="HazardTriggerVfxTests"/>: el efecto observable es el clon en la
    /// escena, y se pasa un marker en vez de <c>VFX_Fire.prefab</c> para no depender de cómo esté
    /// autorado.
    /// </remarks>
    [TestFixture]
    public class HazardPersistentVfxTests
    {
        private const string MarkerName = "VFX_PersistentMarker";

        private GridManager _grid;
        private HazardService _hazard;
        private StubMovement _movement;
        private GameObject _markerPrefab;
        private Guid _walkerGuid;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(9, 9));
            ServiceLocator.AddService<IGridManager>(_grid);

            _walkerGuid = Guid.NewGuid();
            _grid.Register(_walkerGuid, new GridCoord(4, 4));

            // Los hazards son PlayerOnly por default y el filtro es fail-closed: sin IPlayerService
            // no hay disparo, y la llama de la casilla consumida nunca se apagaría.
            ServiceLocator.AddService<IPlayerService>(new StubPlayerService { PlayerGuid = _walkerGuid });

            _movement = new StubMovement();
            ServiceLocator.AddService<IMovementService>(_movement);

            _hazard = new HazardService();
            _hazard.Register();

            _markerPrefab = new GameObject(MarkerName);
        }

        [TearDown]
        public void TearDown()
        {
            DestroySpawned();

            if (_markerPrefab != null) Object.DestroyImmediate(_markerPrefab);
            _markerPrefab = null;

            if (ServiceLocator.TryGetService<IThreatOverlayService>(out var overlay)
                && overlay is IDisposable disposable)
            {
                disposable.Dispose();
            }
            var leftover = GameObject.Find("ThreatTelegraphOverlay");
            if (leftover != null) Object.DestroyImmediate(leftover);

            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        // ======================================================================
        // El comportamiento histórico no cambia
        // ======================================================================

        [Test]
        public void WithoutPersistentPrefab_ActivatingSpawnsNothing()
        {
            // Arrange
            var def = CreateDefinition();
            Assert.IsNull(def.PersistentVfxPrefab, "El default del campo tiene que ser 'sin llama'.");

            // Act
            _hazard.Activate(def, new[] { new GridCoord(2, 2) });

            // Assert
            Assert.AreEqual(0, CountSpawned(), "Los hazards autorados antes del campo no cambian.");
        }

        // ======================================================================
        // Encendido
        // ======================================================================

        [Test]
        public void PersistentPrefab_SpawnsOnePerTile()
        {
            // Arrange
            var def = CreateDefinition(persistent: _markerPrefab);
            var tiles = new[] { new GridCoord(2, 2), new GridCoord(2, 3), new GridCoord(3, 2) };

            // Act
            _hazard.Activate(def, tiles);

            // Assert
            Assert.AreEqual(tiles.Length, CountSpawned());
        }

        // ======================================================================
        // Apagado
        // ======================================================================

        [Test]
        public void Deactivating_DestroysEveryFlame()
        {
            // Arrange
            var def = CreateDefinition(persistent: _markerPrefab);
            var instanceId = _hazard.Activate(def, new[] { new GridCoord(2, 2), new GridCoord(2, 3) });
            Assume.That(CountSpawned(), Is.EqualTo(2));

            // Act
            _hazard.Deactivate(instanceId);

            // Assert
            Assert.AreEqual(0, CountSpawned(),
                "Una instancia expirada que deja la llama prendida miente: esa casilla ya no cobra.");
        }

        [Test]
        public void ConsumingATile_DestroysOnlyThatTilesFlame()
        {
            // Arrange
            var def = CreateDefinition(persistent: _markerPrefab, consumeOnTrigger: true);
            def.Trigger = HazardTriggerMode.OnEnter;
            _hazard.Activate(def, new[] { new GridCoord(2, 2), new GridCoord(2, 3) });
            Assume.That(CountSpawned(), Is.EqualTo(2));

            // Act
            _grid.Move(_walkerGuid, new GridCoord(2, 2));
            _movement.RaiseMoved(_walkerGuid, new GridCoord(4, 4), new GridCoord(2, 2));

            // Assert
            Assert.AreEqual(1, CountSpawned(),
                "Se apaga la casilla gastada y sólo esa — la otra sigue ardiendo.");
        }

        [Test]
        public void CombatEnd_DestroysEveryFlame()
        {
            // Arrange — el teardown no dispara OnHazardExpired a propósito.
            var def = CreateDefinition(persistent: _markerPrefab);
            _hazard.Activate(def, new[] { new GridCoord(2, 2), new GridCoord(2, 3) });
            Assume.That(CountSpawned(), Is.EqualTo(2));

            // Act
            EventManager.Trigger(EventName.OnCombatEnd);

            // Assert
            Assert.AreEqual(0, CountSpawned(), "La llama no puede sobrevivir al fin del combate.");
        }

        // ======================================================================
        // Helpers
        // ======================================================================

        private static HazardDefinitionSO CreateDefinition(
            GameObject persistent = null, bool consumeOnTrigger = false)
        {
            var def = ScriptableObject.CreateInstance<HazardDefinitionSO>();
            def.hideFlags = HideFlags.HideAndDontSave;
            def.Trigger = HazardTriggerMode.OnTurnEndInTile;
            def.Damage = 0; // Sin daño el fixture no necesita IDamagePipeline.
            def.Kind = AttackKind.Environmental;
            def.ConsumeOnTrigger = consumeOnTrigger;
            def.PersistentVfxPrefab = persistent;
            def.SourceId = Guid.NewGuid().ToString();
            return def;
        }

        /// <summary><c>HazardService</c> le agrega la casilla al nombre: se busca por prefijo.</summary>
        private static IEnumerable<GameObject> Spawned()
            => Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
                .Where(go => go != null
                             && go.name.StartsWith(MarkerName)
                             && go.name != MarkerName);

        private static int CountSpawned() => Spawned().Count();

        private static void DestroySpawned()
        {
            foreach (var go in Spawned().ToList()) Object.DestroyImmediate(go);
        }

        private sealed class StubMovement : IMovementService
        {
            public List<GridCoord> GetReachableTiles(GridCoord origin, int range, bool includeOrigin = false)
                => new List<GridCoord>();

            public List<GridCoord> FindPath(GridCoord from, GridCoord to) => new List<GridCoord>();

            public bool Move(Guid entity, GridCoord destination) => false;

            public event Action<Guid, GridCoord, GridCoord, IReadOnlyList<GridCoord>> OnEntityMoved;

            public void RaiseMoved(Guid entity, GridCoord from, GridCoord to)
                => OnEntityMoved?.Invoke(entity, from, to, new List<GridCoord> { to });
        }

        private sealed class StubPlayerService : IPlayerService
        {
            public Guid PlayerGuid { get; set; } = Guid.NewGuid();
            public Guid RunId { get; set; } = Guid.NewGuid();
            public ClassHeroSO CurrentHero { get; set; }
            public DiceBagSO DiceBag { get; set; }
            public void SetPlayer(ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(DiceBagSO bag) { DiceBag = bag; }
            public void ClearPlayer() { }
            public event Action<ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;
        }
    }
}
