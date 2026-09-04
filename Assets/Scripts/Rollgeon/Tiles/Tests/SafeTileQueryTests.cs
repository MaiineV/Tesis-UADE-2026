using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Tiles.Tests
{
    /// <summary>
    /// <see cref="SafeTileQuery"/>: unifica walkable + libre + no-dañina + no-Portal +
    /// no-telegrafiada. Usada por Probability Drive (rediseño de ítems activos) para elegir
    /// dónde reubicar sin matar a nadie sin querer.
    /// </summary>
    [TestFixture]
    public sealed class SafeTileQueryTests
    {
        private readonly List<ScriptableObject> _createdAssets = new List<ScriptableObject>();
        private GridManager _grid;
        private SpecialTileService _tiles;
        private ThreatenedAreaService _threats;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(9, 9));
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);

            _tiles = new SpecialTileService();
            ServiceLocator.AddService<ISpecialTileService>(_tiles, ServiceScope.Global);

            _threats = new ThreatenedAreaService();
            ServiceLocator.AddService<IThreatenedAreaService>(_threats, ServiceScope.Global);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _createdAssets)
                if (asset != null) Object.DestroyImmediate(asset);
            _createdAssets.Clear();

            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private SpecialTileDefinitionSO MakeDefinition(System.Action<SpecialTileDefinitionSO> configure)
        {
            var def = ScriptableObject.CreateInstance<SpecialTileDefinitionSO>();
            configure(def);
            _createdAssets.Add(def);
            return def;
        }

        [Test]
        public void IsSafe_FreeWalkableCell_ReturnsTrue()
        {
            Assert.IsTrue(SafeTileQuery.IsSafe(new GridCoord(4, 4), _grid, _tiles, _threats));
        }

        [Test]
        public void IsSafe_OutOfBounds_ReturnsFalse()
        {
            Assert.IsFalse(SafeTileQuery.IsSafe(new GridCoord(99, 99), _grid, _tiles, _threats));
        }

        [Test]
        public void IsSafe_OccupiedCell_ReturnsFalse()
        {
            _grid.Register(System.Guid.NewGuid(), new GridCoord(4, 4));

            Assert.IsFalse(SafeTileQuery.IsSafe(new GridCoord(4, 4), _grid, _tiles, _threats));
        }

        [Test]
        public void IsSafe_HarmfulTile_ReturnsFalse()
        {
            var spikes = MakeDefinition(d =>
            {
                d.TileId = "TILE_SPIKES";
                d.TileType = SpecialTileType.Spikes;
                d.Category = TileEffectCategory.Damage;
                d.EnterDamage = 12;
            });
            _tiles.Place(spikes, new[] { new GridCoord(4, 4) });

            Assert.IsFalse(SafeTileQuery.IsSafe(new GridCoord(4, 4), _grid, _tiles, _threats));
        }

        [Test]
        public void IsSafe_PortalTile_ReturnsFalse()
        {
            var portal = MakeDefinition(d =>
            {
                d.TileId = "TILE_PORTAL";
                d.TileType = SpecialTileType.Portal;
                d.Category = TileEffectCategory.Teleport;
            });
            _tiles.Place(portal, new[] { new GridCoord(4, 4) });

            Assert.IsFalse(SafeTileQuery.IsSafe(new GridCoord(4, 4), _grid, _tiles, _threats));
        }

        [Test]
        public void IsSafe_ThreatenedCell_ReturnsFalse()
        {
            _threats.Mark(System.Guid.NewGuid(), new[] { new GridCoord(4, 4) }, 10, AttackKind.BasicAttack);

            Assert.IsFalse(SafeTileQuery.IsSafe(new GridCoord(4, 4), _grid, _tiles, _threats));
        }

        [Test]
        public void IsSafe_NullServices_DegradeToNoFilter()
        {
            Assert.IsTrue(SafeTileQuery.IsSafe(new GridCoord(4, 4), _grid, null, null));
        }

        [Test]
        public void CollectRing_ManhattanOne_ReturnsFourNeighbors()
        {
            var center = new GridCoord(4, 4);

            var ring = SafeTileQuery.CollectRing(center, 1, 1, _grid, _tiles, _threats);

            Assert.AreEqual(4, ring.Count);
            foreach (var c in ring)
                Assert.AreEqual(1, center.Manhattan(c));
        }

        [Test]
        public void CollectRing_ExcludesOccupiedCellWithinRange()
        {
            var center = new GridCoord(4, 4);
            _grid.Register(System.Guid.NewGuid(), new GridCoord(5, 4)); // Manhattan 1

            var ring = SafeTileQuery.CollectRing(center, 1, 1, _grid, _tiles, _threats);

            Assert.AreEqual(3, ring.Count);
            CollectionAssert.DoesNotContain(ring, new GridCoord(5, 4));
        }

        [Test]
        public void CollectRing_RowMajorOrder_IsDeterministic()
        {
            var center = new GridCoord(4, 4);

            var ring = SafeTileQuery.CollectRing(center, 0, 1, _grid, _tiles, _threats);

            // Row-major: Y ascendente, X ascendente dentro de cada fila.
            var expected = new List<GridCoord>
            {
                new GridCoord(4, 3),
                new GridCoord(3, 4), new GridCoord(4, 4), new GridCoord(5, 4),
                new GridCoord(4, 5),
            };
            CollectionAssert.AreEqual(expected, ring);
        }
    }
}
