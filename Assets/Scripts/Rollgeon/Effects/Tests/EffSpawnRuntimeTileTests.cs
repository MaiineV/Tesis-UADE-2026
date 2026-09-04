using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Selection;
using Rollgeon.Grid;
using Rollgeon.Tiles;
using UnityEngine;

namespace Rollgeon.Effects.Tests
{
    /// <summary>
    /// <see cref="EffSpawnRuntimeTile"/> (Bottle'o Thunder del rediseño de ítems activos):
    /// coloca N casillas runtime cerca del ancla, nearest-ring-first, con shuffle inyectable.
    /// </summary>
    [TestFixture]
    public sealed class EffSpawnRuntimeTileTests
    {
        private readonly List<ScriptableObject> _createdAssets = new List<ScriptableObject>();
        private GridManager _grid;
        private SpecialTileService _tiles;
        private SpecialTileDefinitionSO _definition;
        private Guid _source;

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

            _definition = ScriptableObject.CreateInstance<SpecialTileDefinitionSO>();
            _definition.TileId = "TILE_ELECTRIC_PUDDLE_TEST";
            _definition.TileType = SpecialTileType.ElectricPuddle;
            _definition.Category = TileEffectCategory.ApplyStatus;
            _createdAssets.Add(_definition);

            _source = Guid.NewGuid();
            _grid.Register(_source, new GridCoord(4, 4));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _createdAssets)
                if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
            _createdAssets.Clear();

            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        // Filtra por Definition: distingue lo que puso ESTE efecto de obstáculos pre-plantados
        // por el test (otro SO, misma sala).
        private List<GridCoord> PlacedCoords()
            => _tiles.ActiveInstances()
                .Where(i => i.Definition == _definition)
                .SelectMany(i => i.Coords)
                .ToList();

        [Test]
        public void ApplyEffect_NoSelection_PlacesCountTilesAroundSource()
        {
            var effect = new EffSpawnRuntimeTile
            {
                Definition = _definition,
                Count = 2,
                MaxRadius = 2,
                Rng = new System.Random(1),
            };

            Assert.IsTrue(effect.ApplyEffect(new EffectContext { SourceGuid = _source }));

            var placed = PlacedCoords();
            Assert.AreEqual(2, placed.Count);
            foreach (var c in placed)
            {
                int dist = new GridCoord(4, 4).Manhattan(c);
                Assert.IsTrue(dist >= 1 && dist <= 2, $"celda {c} fuera del anillo 1..2");
            }
        }

        [Test]
        public void ApplyEffect_WithSelection_AnchorsOnSelectedOccupant()
        {
            var occupant = Guid.NewGuid();
            _grid.Register(occupant, new GridCoord(6, 6));
            var effect = new EffSpawnRuntimeTile
            {
                Definition = _definition,
                Count = 1,
                MaxRadius = 1,
                Rng = new System.Random(2),
            };
            var ctx = new EffectContext
            {
                SourceGuid = _source,
                SelectionResult = new TargetSelectionResult
                {
                    WasCompleted = true,
                    SelectedTargets = new List<TargetRef> { TargetRef.At(new GridCoord(6, 6)) },
                },
            };

            Assert.IsTrue(effect.ApplyEffect(ctx));

            var placed = PlacedCoords();
            Assert.AreEqual(1, placed.Count);
            Assert.AreEqual(1, new GridCoord(6, 6).Manhattan(placed[0]),
                "el ancla es el occupant seleccionado, no el source");
        }

        [Test]
        public void ApplyEffect_NearestRingOccupied_SkipsToFartherRing()
        {
            // Los 4 vecinos a distancia 1 quedan ocupados: el único lugar posible es distancia 2.
            foreach (var n in new GridCoord(4, 4).Neighbors4())
                _grid.Register(Guid.NewGuid(), n);

            var effect = new EffSpawnRuntimeTile
            {
                Definition = _definition,
                Count = 1,
                MaxRadius = 2,
                Rng = new System.Random(3),
            };

            Assert.IsTrue(effect.ApplyEffect(new EffectContext { SourceGuid = _source }));

            var placed = PlacedCoords();
            Assert.AreEqual(1, placed.Count);
            Assert.AreEqual(2, new GridCoord(4, 4).Manhattan(placed[0]));
        }

        [Test]
        public void ApplyEffect_CellAlreadyTiled_IsSkippedEvenIfFree()
        {
            // (5,4) es libre de unidades pero ya tiene una casilla especial: CreateRuntime la
            // rechaza (CoordHasSpecialTile) y el efecto sigue con el resto del anillo.
            var otherDef = ScriptableObject.CreateInstance<SpecialTileDefinitionSO>();
            otherDef.TileId = "TILE_OTHER";
            _createdAssets.Add(otherDef);
            _tiles.Place(otherDef, new[] { new GridCoord(5, 4) });

            var effect = new EffSpawnRuntimeTile
            {
                Definition = _definition,
                Count = 4,
                MaxRadius = 1, // solo el anillo 1: como máximo 3 celdas libres y sin tile.
                Rng = new System.Random(4),
            };

            Assert.IsTrue(effect.ApplyEffect(new EffectContext { SourceGuid = _source }));

            var placed = PlacedCoords();
            Assert.AreEqual(3, placed.Count, "pidió 4 pero solo había 3 celdas libres sin tile en el anillo");
            CollectionAssert.DoesNotContain(placed, new GridCoord(5, 4));
        }

        [Test]
        public void ApplyEffect_SeededRng_ProducesSameSetAcrossRuns()
        {
            // Dos salas aisladas (grid + servicio propios, no las del fixture) con el mismo
            // estado inicial y la misma seed: el shuffle debe converger al mismo set de celdas.
            var placedA = RunInFreshWorld(new System.Random(42));
            var placedB = RunInFreshWorld(new System.Random(42));

            CollectionAssert.AreEquivalent(placedA, placedB);
        }

        private List<GridCoord> RunInFreshWorld(System.Random rng)
        {
            var grid = new GridManager();
            grid.LoadRoom(NavGraph.Rect(9, 9));
            var svc = new SpecialTileService();
            var source = Guid.NewGuid();
            grid.Register(source, new GridCoord(4, 4));

            ServiceLocator.Clear();
            ServiceLocator.AddService<IGridManager>(grid, ServiceScope.Global);
            ServiceLocator.AddService<ISpecialTileService>(svc, ServiceScope.Global);

            var effect = new EffSpawnRuntimeTile
            {
                Definition = _definition, Count = 2, MaxRadius = 2, Rng = rng,
            };
            Assert.IsTrue(effect.ApplyEffect(new EffectContext { SourceGuid = source }));

            return svc.ActiveInstances().SelectMany(i => i.Coords).ToList();
        }

        [Test]
        public void ApplyEffect_NoServiceRegistered_WarnsAndReturnsTrue()
        {
            ServiceLocator.Clear();
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);
            var effect = new EffSpawnRuntimeTile { Definition = _definition, Count = 1 };

            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex("ISpecialTileService no registrado"));
            Assert.IsTrue(effect.ApplyEffect(new EffectContext { SourceGuid = _source }));
        }

        [Test]
        public void ApplyEffect_NoDefinition_WarnsAndReturnsTrue()
        {
            var effect = new EffSpawnRuntimeTile { Definition = null, Count = 1 };

            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex("Sin Definition"));
            Assert.IsTrue(effect.ApplyEffect(new EffectContext { SourceGuid = _source }));
        }
    }
}
