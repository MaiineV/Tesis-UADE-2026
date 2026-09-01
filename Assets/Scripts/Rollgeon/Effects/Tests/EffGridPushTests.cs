using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Selection;
using Rollgeon.Grid;
using Rollgeon.Movement;
using Rollgeon.Tiles.Forced;
using UnityEngine;

namespace Rollgeon.Effects.Tests
{
    /// <summary>
    /// EffGridPush (Charger GDD): empuja 1 casilla source → target; bloqueado (no avanzó) →
    /// bono ATK × multiplicador por el pipeline; sin stun ni cadena.
    /// </summary>
    [TestFixture]
    public sealed class EffGridPushTests
    {
        private sealed class SpyPipeline : IDamagePipeline
        {
            public readonly List<DamageContext> Resolved = new List<DamageContext>();
            public DamageContext Resolve(DamageContext ctx) { Resolved.Add(ctx); return ctx; }
            public DamageContext Preview(DamageContext ctx) => ctx;
        }

        private GridManager _grid;
        private MovementService _movement;
        private ForcedMovementService _forced;
        private AttributesManager _attributes;
        private SpyPipeline _pipeline;
        private Guid _source;
        private Guid _target;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(6, 3));
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);

            _movement = new MovementService(_grid);
            ServiceLocator.AddService<IMovementService>(_movement, ServiceScope.Global);

            _forced = new ForcedMovementService();
            ServiceLocator.AddService<IForcedMovementService>(_forced, ServiceScope.Global);

            _attributes = new AttributesManager();
            ServiceLocator.AddService<AttributesManager>(_attributes, ServiceScope.Global);

            _pipeline = new SpyPipeline();
            ServiceLocator.AddService<IDamagePipeline>(_pipeline, ServiceScope.Global);

            _source = Guid.NewGuid();
            _target = Guid.NewGuid();
            _grid.Register(_source, new GridCoord(0, 0));
            _grid.Register(_target, new GridCoord(1, 0));

            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Attack>(new Attack(20));
            _attributes.Register(_source, attrs);
        }

        [TearDown]
        public void TearDown()
        {
            _attributes?.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private static void SetPrivate(object target, string field, object value)
        {
            for (var t = target.GetType(); t != null; t = t.BaseType)
            {
                var f = t.GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null) { f.SetValue(target, value); return; }
            }
            Assert.Fail($"campo privado '{field}' no encontrado");
        }

        private EffectContext Ctx(params GridCoord[] cells) => new EffectContext
        {
            SourceGuid = _source,
            SelectionResult = new TargetSelectionResult
            {
                WasCompleted = true,
                SelectedTargets = cells.Select(TargetRef.At).ToList(),
            },
        };

        [Test]
        public void Apply_FreeDestination_PushesAndNoBonus()
        {
            // Arrange — (2,0) libre: el target avanza 1 al Este.
            var effect = new EffGridPush();

            // Act
            Assert.IsTrue(effect.ApplyEffect(Ctx(new GridCoord(1, 0))));

            // Assert
            Assert.IsTrue(_grid.TryGetPosition(_target, out var coord));
            Assert.AreEqual(new GridCoord(2, 0), coord);
            CollectionAssert.IsEmpty(_pipeline.Resolved, "empuje libre = sin bono");
        }

        [Test]
        public void Apply_BlockedByWall_NoMoveAndBonusFromAttack()
        {
            // Arrange — target contra el borde Este (5,0): el push no avanza.
            _grid.Unregister(_target);
            _grid.Register(_target, new GridCoord(5, 0));
            _grid.Unregister(_source);
            _grid.Register(_source, new GridCoord(4, 0));
            var effect = new EffGridPush();

            // Act
            Assert.IsTrue(effect.ApplyEffect(Ctx(new GridCoord(5, 0))));

            // Assert — bono = round(ATK 20 × 0.5) = 10, por el pipeline.
            Assert.IsTrue(_grid.TryGetPosition(_target, out var coord));
            Assert.AreEqual(new GridCoord(5, 0), coord, "bloqueado: no se mueve");
            Assert.AreEqual(1, _pipeline.Resolved.Count);
            Assert.AreEqual(10, _pipeline.Resolved[0].BaseDamage);
            Assert.AreEqual(_source, _pipeline.Resolved[0].SourceId);
            Assert.AreEqual(_target, _pipeline.Resolved[0].TargetId);
        }

        [Test]
        public void Apply_BlockedByOccupant_NoMoveAndBonus()
        {
            _grid.Register(Guid.NewGuid(), new GridCoord(2, 0)); // bloquea el destino
            var effect = new EffGridPush();

            Assert.IsTrue(effect.ApplyEffect(Ctx(new GridCoord(1, 0))));

            Assert.IsTrue(_grid.TryGetPosition(_target, out var coord));
            Assert.AreEqual(new GridCoord(1, 0), coord);
            Assert.AreEqual(1, _pipeline.Resolved.Count, "bloqueado por ocupante = bono, sin cadena");
        }

        [Test]
        public void Apply_ZeroBonusMultiplier_BlockedDealsNothing()
        {
            _grid.Register(Guid.NewGuid(), new GridCoord(2, 0));
            var effect = new EffGridPush();
            SetPrivate(effect, "_blockedBonusMultiplier", 0f);

            Assert.IsTrue(effect.ApplyEffect(Ctx(new GridCoord(1, 0))));

            CollectionAssert.IsEmpty(_pipeline.Resolved);
        }

        [Test]
        public void Apply_MultiCellTargetSelectedByTwoCells_PushesOnce()
        {
            // Arrange — 2×2 en (1,0) cubre (1,0)-(2,1); dos celdas seleccionadas = UN empuje
            // (si se duplicara, el ancla avanzaría 2).
            _grid.Unregister(_target);
            Assert.IsTrue(_grid.TryRegister(_target, new GridCoord(1, 0), new Vector2Int(2, 2)));
            var effect = new EffGridPush();

            // Act
            Assert.IsTrue(effect.ApplyEffect(Ctx(new GridCoord(1, 0), new GridCoord(2, 0))));

            // Assert
            Assert.IsTrue(_grid.TryGetPosition(_target, out var anchor));
            Assert.AreEqual(new GridCoord(2, 0), anchor, "un solo push de distancia 1");
        }

        [Test]
        public void Apply_WithoutForcedMovementService_WarnsAndReturnsTrue()
        {
            ServiceLocator.Clear();
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);
            var effect = new EffGridPush();

            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex("IForcedMovementService no registrado"));
            Assert.IsTrue(effect.ApplyEffect(Ctx(new GridCoord(1, 0))));
        }
    }
}
