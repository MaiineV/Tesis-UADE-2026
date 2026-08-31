using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Selection;
using Rollgeon.Entities;
using Rollgeon.Entities.Behaviors;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// Área instantánea de <see cref="EnemyActionBehavior"/> (Sweeper): los efectos golpean a
    /// los ocupantes del área que pasan el filtro, sin friendly fire y sin fallback al target
    /// cuando el área no atrapa a nadie.
    /// </summary>
    [TestFixture]
    public class EnemyAreaBehaviorTests
    {
        private sealed class SpyPipeline : IDamagePipeline
        {
            public readonly List<DamageContext> Resolved = new List<DamageContext>();
            public DamageContext Resolve(DamageContext ctx) { Resolved.Add(ctx); return ctx; }
            public DamageContext Preview(DamageContext ctx) => ctx;
        }

        private GridManager _grid;
        private SpyPipeline _pipeline;
        private Guid _self;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(9, 9));
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);

            _pipeline = new SpyPipeline();
            ServiceLocator.AddService<IDamagePipeline>(_pipeline, ServiceScope.Global);

            _self = Guid.NewGuid();
            _player = Guid.NewGuid();
            _grid.Register(_self, new GridCoord(2, 4));
        }

        [TearDown]
        public void TearDown()
        {
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

        private static EffDealDamage ConstantDamage(int amount)
        {
            var e = new EffDealDamage();
            SetPrivate(e, "_damageSource", DamageSource.Constant);
            SetPrivate(e, "_baseAmount", amount);
            return e;
        }

        private AINode_Behavior SweepNode()
        {
            var group = new EffectData();
            group.Effects.Add(ConstantDamage(9));
            var behavior = new EnemyActionBehavior
            {
                ActionName = "Barrido",
                UseArea = true,
                AreaShape = ThreatShape.DirectionalCone,
                AreaSize = 0,
                AreaDepth = 2,
                AreaFilter = EntityFilterMask.Player,
            };
            behavior.Effects.Add(group);
            return new AINode_Behavior { Behavior = behavior };
        }

        private AIContext Ctx() => new AIContext
        {
            SelfGuid = _self,
            PlayerGuid = _player,
            Grid = _grid,
        };

        [Test]
        public void Sweep_PlayerInsideCone_HitOnce()
        {
            // Cono al Este desde (2,4), apex 0, depth 2: paso 1 = (3,4); paso 2 = (4,3..5).
            _grid.Register(_player, new GridCoord(4, 4));

            Assert.AreEqual(AIResult.Succeeded, SweepNode().Tick(Ctx()));

            Assert.AreEqual(1, _pipeline.Resolved.Count);
            Assert.AreEqual(_player, _pipeline.Resolved[0].TargetId);
            Assert.AreEqual(9, _pipeline.Resolved[0].BaseDamage);
        }

        [Test]
        public void Sweep_PlayerOutsideCone_NoFallbackHit()
        {
            // El player está DETRÁS del enemigo: el cono sale hacia él… no — el cono sale
            // hacia el target (player), así que ponerlo lejos en diagonal profunda lo deja
            // fuera del depth 2.
            _grid.Register(_player, new GridCoord(8, 8));

            Assert.AreEqual(AIResult.Succeeded, SweepNode().Tick(Ctx()));

            CollectionAssert.IsEmpty(_pipeline.Resolved,
                "un barrido que erra no golpea a nadie (sin fallback al TargetGuid)");
        }

        [Test]
        public void Sweep_AllyInsideCone_NotHit()
        {
            // Sin IEntityQueryService, el filtro default (Player) solo deja pasar al player:
            // un enemigo aliado dentro del cono queda intacto.
            _grid.Register(_player, new GridCoord(4, 4));
            var ally = Guid.NewGuid();
            _grid.Register(ally, new GridCoord(3, 4)); // primer paso del cono

            SweepNode().Tick(Ctx());

            Assert.AreEqual(1, _pipeline.Resolved.Count);
            Assert.AreEqual(_player, _pipeline.Resolved[0].TargetId, "el aliado no se toca");
        }

        [Test]
        public void Sweep_MultiCellVictimCoveredByTwoConeCells_HitOnce()
        {
            // "Player" 2×2 (footprint) cubriendo dos celdas del cono → el dedupe por guid de
            // los efectos lo golpea una vez.
            Assert.IsTrue(_grid.TryRegister(_player, new GridCoord(3, 3), new Vector2Int(2, 2)));

            SweepNode().Tick(Ctx());

            Assert.AreEqual(1, _pipeline.Resolved.Count);
        }
    }
}
