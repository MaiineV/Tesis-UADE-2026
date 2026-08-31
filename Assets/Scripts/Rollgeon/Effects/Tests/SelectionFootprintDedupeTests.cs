using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Selection;
using Rollgeon.Grid;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Effects.Tests
{
    /// <summary>
    /// Fase C: un footprint multi-celda cubierto por varias celdas de un AoE o de los picks
    /// es UN solo target — el daño no se multiplica y la selección no lo repite.
    /// </summary>
    [TestFixture]
    public sealed class SelectionFootprintDedupeTests
    {
        private static readonly int FlashId = Shader.PropertyToID("_HitFlashAmount");
        private static readonly Vector2Int Two = new Vector2Int(2, 2);

        private sealed class SpyPipeline : IDamagePipeline
        {
            public readonly List<DamageContext> Resolved = new List<DamageContext>();
            public DamageContext Resolve(DamageContext ctx) { Resolved.Add(ctx); return ctx; }
            public DamageContext Preview(DamageContext ctx) => ctx;
        }

        private readonly List<GameObject> _objects = new List<GameObject>();
        private GridManager _grid;
        private Guid _owner;
        private Guid _big;
        private Guid _small;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(6, 6));
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);

            _owner = Guid.NewGuid();
            _big = Guid.NewGuid();
            _small = Guid.NewGuid();
            _grid.Register(_owner, new GridCoord(0, 0));
            Assert.IsTrue(_grid.TryRegister(_big, new GridCoord(1, 0), Two)); // cubre (1,0)-(2,1)
            _grid.Register(_small, new GridCoord(4, 0));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects)
                if (go != null) Object.DestroyImmediate(go);
            _objects.Clear();
            ServiceLocator.Clear();
        }

        // Los campos de autoría de los efectos son privados a propósito (solo la tool los
        // setea); acá se cargan por reflexión igual que EffectAuthoring del editor.
        private static void SetPrivate(object target, string field, object value)
        {
            for (var t = target.GetType(); t != null; t = t.BaseType)
            {
                var f = t.GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null) { f.SetValue(target, value); return; }
            }
            Assert.Fail($"campo privado '{field}' no encontrado en {target.GetType().Name}");
        }

        private static TargetSelectionResult Selection(params GridCoord[] coords) => new TargetSelectionResult
        {
            WasCompleted = true,
            SelectedTargets = coords.Select(TargetRef.At).ToList(),
        };

        [Test]
        public void EffDealDamage_MultiCellCoveredByThreeAoeCells_ResolvesOnce()
        {
            // Arrange — el "AoE" cubre 3 celdas del 2×2 y la del 1×1.
            var pipeline = new SpyPipeline();
            ServiceLocator.AddService<IDamagePipeline>(pipeline, ServiceScope.Global);

            var effect = new EffDealDamage();
            SetPrivate(effect, "_damageSource", DamageSource.Constant);
            SetPrivate(effect, "_baseAmount", 10);

            var ctx = new EffectContext
            {
                SourceGuid = _owner,
                SelectionResult = Selection(
                    new GridCoord(1, 0), new GridCoord(2, 0), new GridCoord(2, 1),
                    new GridCoord(4, 0)),
            };

            // Act
            Assert.IsTrue(effect.ApplyEffect(ctx));

            // Assert — un Resolve por entidad, no por celda.
            Assert.AreEqual(2, pipeline.Resolved.Count);
            Assert.AreEqual(_big, pipeline.Resolved[0].TargetId);
            Assert.AreEqual(_small, pipeline.Resolved[1].TargetId);
        }

        [Test]
        public void AutoResolveTargets_TwoPicks_CannotBothLandOnSameMultiCell()
        {
            var settings = new SelectionSettings
            {
                SlotState = SlotState.Occupied,
                EntityFilter = EntityFilterMask.Enemies,
                Range = 6,
                TargetMode = TargetMode.Single,
                IsConstantSelectionCount = true,
                SelectionCount = 2,
            };

            // Act — sin IEntityQueryService el filtro acepta cualquier ocupante (big + small).
            var result = settings.AutoResolveTargets(new GridCoord(0, 0), _owner);

            // Assert — dos picks, dos OCUPANTES distintos (el 2×2 aporta 4 celdas válidas).
            Assert.IsTrue(result.WasCompleted);
            Assert.AreEqual(2, result.SelectedTargets.Count);
            var occupants = result.SelectedTargets
                .Select(t => { _grid.TryGetOccupant(t.Coord, out var g); return g; })
                .ToList();
            CollectionAssert.AllItemsAreUnique(occupants, "dos celdas del mismo rect son el mismo target");
        }

        [Test]
        public void OnTargetClicked_SecondCellOfSameEnemy_IsSkipped()
        {
            var controller = new SelectionController();
            var settings = new SelectionSettings
            {
                SlotState = SlotState.Occupied,
                EntityFilter = EntityFilterMask.Enemies,
                Range = 6,
                TargetMode = TargetMode.Single,
                IsConstantSelectionCount = true,
                SelectionCount = 2,
                AutoAccept = true,
            };
            controller.BeginSelection(new SelectionRequest
            {
                OwnerGuid = _owner,
                Settings = settings,
                HighlightStyle = "attack",
                ValidTargets = new List<TargetRef>
                {
                    TargetRef.At(new GridCoord(1, 0)),
                    TargetRef.At(new GridCoord(2, 0)),
                    TargetRef.At(new GridCoord(4, 0)),
                },
            });
            TargetSelectionResult result = null;
            controller.OnSelectionCompleted += r => result = r;

            // Act — dos celdas del mismo 2×2: la segunda NO cuenta como segundo pick.
            controller.OnTargetClicked(TargetRef.At(new GridCoord(1, 0)));
            controller.OnTargetClicked(TargetRef.At(new GridCoord(2, 0)));
            Assert.IsNull(result, "el segundo click sobre el mismo enemigo no debe completar la selección");

            controller.OnTargetClicked(TargetRef.At(new GridCoord(4, 0)));

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.WasCompleted);
            CollectionAssert.AreEqual(
                new[] { new GridCoord(1, 0), new GridCoord(4, 0) },
                result.SelectedTargets.Select(t => t.Coord).ToList());
        }

        [Test]
        public void OnTargetClicked_MultiCellOccupant_HighlightsWholeRect()
        {
            var highlight = new TileHighlightService();
            ServiceLocator.AddService<ITileHighlightService>(highlight, ServiceScope.Global);
            var renderers = new Dictionary<GridCoord, Renderer>();
            foreach (var c in _grid.OccupiedCells(_big))
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _objects.Add(cube);
                var r = cube.GetComponent<Renderer>();
                highlight.RegisterTile(c, r);
                renderers[c] = r;
            }

            var controller = new SelectionController();
            controller.BeginSelection(new SelectionRequest
            {
                OwnerGuid = _owner,
                Settings = new SelectionSettings
                {
                    SlotState = SlotState.Occupied,
                    EntityFilter = EntityFilterMask.Enemies,
                    Range = 6,
                    IsConstantSelectionCount = true,
                    SelectionCount = 2, // sin AutoAccept inmediato: queremos ver el "selected"
                },
                HighlightStyle = "attack",
                ValidTargets = new List<TargetRef> { TargetRef.At(new GridCoord(2, 1)) },
            });

            // Act — click en UNA celda del rect.
            controller.OnTargetClicked(TargetRef.At(new GridCoord(2, 1)));

            // Assert — las 4 celdas quedaron pintadas.
            foreach (var pair in renderers)
            {
                var block = new MaterialPropertyBlock();
                pair.Value.GetPropertyBlock(block);
                Assert.Greater(block.GetFloat(FlashId), 0f, $"celda {pair.Key} sin highlight");
            }
        }
    }
}
