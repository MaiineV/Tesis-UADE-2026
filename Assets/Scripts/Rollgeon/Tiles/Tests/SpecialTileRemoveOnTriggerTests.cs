using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Entities.Traits;
using Rollgeon.Grid;
using Rollgeon.Movement;
using UnityEngine;

namespace Rollgeon.Tiles.Tests
{
    /// <summary>
    /// <see cref="SpecialTileDefinitionSO.RemoveOnTrigger"/>: la casilla se expira sola en el
    /// primer disparo (Charco Eléctrico de ítem, "un solo uso") — a diferencia de
    /// <c>DisarmOnTrigger</c>, que la deja desarmada pero viva.
    /// </summary>
    [TestFixture]
    public sealed class SpecialTileRemoveOnTriggerTests
    {
        private GridManager _grid;
        private MovementService _movement;
        private UnitTraitService _traits;
        private SpecialTileService _svc;
        private SpyDamagePipeline _damage;
        private Guid _player;
        private readonly List<ScriptableObject> _createdAssets = new List<ScriptableObject>();

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(5, 1));
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);

            _movement = new MovementService(_grid);
            ServiceLocator.AddService<IMovementService>(_movement, ServiceScope.Global);

            _traits = new UnitTraitService();
            ServiceLocator.AddService<IUnitTraitService>(_traits, ServiceScope.Global);

            _damage = new SpyDamagePipeline();
            ServiceLocator.AddService<IDamagePipeline>(_damage, ServiceScope.Global);

            _player = Guid.NewGuid();
            _grid.Register(_player, new GridCoord(0, 0));
            _traits.Register(_player, UnitTraits.DefaultGround);

            _svc = new SpecialTileService();
            _svc.ConfigureForTests(() => _player);
        }

        [TearDown]
        public void TearDown()
        {
            _svc?.Dispose();
            _svc = null;

            foreach (var asset in _createdAssets)
                if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
            _createdAssets.Clear();

            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        // El backing field es privado (sin setter público — RemoveOnTrigger se autora desde el
        // inspector): reflection es el mismo criterio que EffGridPushTests.SetPrivate.
        private static void SetRemoveOnTrigger(SpecialTileDefinitionSO def, bool value)
        {
            var field = typeof(SpecialTileDefinitionSO)
                .GetField("_removeOnTrigger", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "campo privado '_removeOnTrigger' no encontrado");
            field.SetValue(def, value);
        }

        private SpecialTileDefinitionSO MakeSpikes(bool removeOnTrigger)
        {
            var def = ScriptableObject.CreateInstance<SpecialTileDefinitionSO>();
            def.TileId = "TILE_SPIKES_TEST";
            def.TileType = SpecialTileType.Spikes;
            def.Triggers = TileTrigger.OnEnter;
            def.Category = TileEffectCategory.Damage;
            def.EnterDamage = 10;
            SetRemoveOnTrigger(def, removeOnTrigger);

            _createdAssets.Add(def);
            return def;
        }

        [Test]
        public void Trigger_RemoveOnTriggerTrue_ExpiresInstance()
        {
            var def = MakeSpikes(removeOnTrigger: true);
            _svc.Place(def, new[] { new GridCoord(1, 0) });

            Assert.IsTrue(_movement.Move(_player, new GridCoord(1, 0)));

            Assert.IsFalse(_svc.TryGetTileAt(new GridCoord(1, 0), out _), "se fue en el primer disparo");
            Assert.IsFalse(_svc.ActiveInstances().Any(), "sin instancias vivas tras expirar");
        }

        [Test]
        public void Trigger_RemoveOnTriggerFalse_InstanceSurvives()
        {
            var def = MakeSpikes(removeOnTrigger: false);
            _svc.Place(def, new[] { new GridCoord(1, 0) });

            Assert.IsTrue(_movement.Move(_player, new GridCoord(1, 0)));

            Assert.IsTrue(_svc.TryGetTileAt(new GridCoord(1, 0), out var info), "sin RemoveOnTrigger sigue viva");
            Assert.AreEqual(def, info.Definition);
        }

        private sealed class SpyDamagePipeline : IDamagePipeline
        {
            public readonly List<DamageContext> Resolved = new List<DamageContext>();

            public DamageContext Resolve(DamageContext ctx)
            {
                ctx.FinalDamage = ctx.BaseDamage;
                Resolved.Add(ctx);
                return ctx;
            }

            public DamageContext Preview(DamageContext ctx) => ctx;
        }
    }
}
