using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Status;
using Rollgeon.Entities.Traits;
using Rollgeon.Grid;
using Rollgeon.Movement;
using UnityEngine;

namespace Rollgeon.Tiles.Tests
{
    /// <summary>
    /// Tests de las casillas de estado (Veneno, Charco Eléctrico) integrando el
    /// <see cref="ApplyStatusTileEffect"/> con los servicios de estado reales.
    /// </summary>
    [TestFixture]
    public class SpecialTileStatusTests
    {
        private GridManager _grid;
        private MovementService _movement;
        private UnitTraitService _traits;
        private SpecialTileService _svc;
        private PoisonService _poison;
        private StunService _stun;
        private Guid _player;
        private readonly List<ScriptableObject> _createdAssets = new List<ScriptableObject>();

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(7, 7));
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);

            _movement = new MovementService(_grid);
            ServiceLocator.AddService<IMovementService>(_movement, ServiceScope.Global);

            _traits = new UnitTraitService();
            ServiceLocator.AddService<IUnitTraitService>(_traits, ServiceScope.Global);

            ServiceLocator.AddService<IDamagePipeline>(new NullDamagePipeline(), ServiceScope.Global);

            _poison = new PoisonService();
            _poison.ConfigureForTests();
            ServiceLocator.AddService<IPoisonService>(_poison, ServiceScope.Global);

            _stun = new StunService();
            _stun.ConfigureForTests(() => _player);
            ServiceLocator.AddService<IStunService>(_stun, ServiceScope.Global);

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
            _poison?.Dispose();
            _stun?.Dispose();
            _svc = null;

            foreach (var asset in _createdAssets)
                if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
            _createdAssets.Clear();

            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        private SpecialTileDefinitionSO MakePoisonTile()
        {
            var def = ScriptableObject.CreateInstance<SpecialTileDefinitionSO>();
            def.TileId = "TILE_POISON";
            def.TileType = SpecialTileType.Poison;
            def.Triggers = TileTrigger.OnEnter;
            def.Category = TileEffectCategory.ApplyStatus;
            def.Affinity = TileAffinity.GroundOnly;
            def.StatusKind = TileStatusKind.Poison;
            def.StatusTurns = 3;
            def.StatusTickDamage = 5;
            _createdAssets.Add(def);
            return def;
        }

        private SpecialTileDefinitionSO MakeElectricPuddle()
        {
            var def = ScriptableObject.CreateInstance<SpecialTileDefinitionSO>();
            def.TileId = "TILE_ELECTRIC_PUDDLE";
            def.TileType = SpecialTileType.ElectricPuddle;
            def.Triggers = TileTrigger.OnEnter | TileTrigger.OnForcedMovementInto;
            def.Category = TileEffectCategory.ApplyStatus;
            def.Affinity = TileAffinity.All;
            def.StatusKind = TileStatusKind.Stun;
            def.StatusTurns = 1;
            _createdAssets.Add(def);
            return def;
        }

        [Test]
        public void PoisonTile_Enter_AppliesPoisonWithTileAsSource()
        {
            var id = _svc.Place(MakePoisonTile(), new[] { new GridCoord(2, 0) });

            Assert.IsTrue(_movement.Move(_player, new GridCoord(2, 0)));

            Assert.IsTrue(_poison.IsPoisoned(_player));
            Assert.AreEqual(3, _poison.GetPoisonTurns(_player));
            Assert.AreEqual(5, _poison.GetDamagePerTurn(_player));
            Assert.IsTrue(_svc.TryResolveCredit(id, out var credit) && credit == _player,
                "El veneno mata con el instanceId de la casilla: el crédito debe resolver al player.");
        }

        [Test]
        public void PoisonTile_ReEnterWhilePoisoned_RefreshesNotStacks()
        {
            _svc.Place(MakePoisonTile(), new[] { new GridCoord(2, 0) });
            Assert.IsTrue(_movement.Move(_player, new GridCoord(2, 0)));
            EventManager.Trigger(EventName.OnTurnStarted, _player); // 2 restantes
            Assert.AreEqual(2, _poison.GetPoisonTurns(_player), "Setup: un tick consumido.");

            Assert.IsTrue(_movement.Move(_player, new GridCoord(1, 0)));
            Assert.IsTrue(_movement.Move(_player, new GridCoord(2, 0)));

            Assert.AreEqual(3, _poison.GetPoisonTurns(_player),
                "Re-pisar Veneno refresca a 3, no acumula (nunca 2+3).");
        }

        [Test]
        public void PoisonTile_FlyingUnit_IsIgnored()
        {
            var flyer = Guid.NewGuid();
            _grid.Register(flyer, new GridCoord(0, 2));
            _traits.Register(flyer, new UnitTraits(isFlying: true, isBoss: false));
            _svc.Place(MakePoisonTile(), new[] { new GridCoord(2, 2) });

            Assert.IsTrue(_movement.Move(flyer, new GridCoord(2, 2)));

            Assert.IsFalse(_poison.IsPoisoned(flyer), "Veneno es 'solo terrestres'.");
        }

        [Test]
        public void ElectricPuddle_Enter_AppliesOneTurnStun()
        {
            _svc.Place(MakeElectricPuddle(), new[] { new GridCoord(2, 0) });

            Assert.IsTrue(_movement.Move(_player, new GridCoord(2, 0)));

            Assert.IsTrue(_stun.IsStunned(_player));
            Assert.AreEqual(1, _stun.GetStunTurns(_player));
        }

        [Test]
        public void ElectricPuddle_CrossingTwoPuddlesInOneMove_DoesNotExtendStun()
        {
            _svc.Place(MakeElectricPuddle(), new[] { new GridCoord(1, 0) });
            _svc.Place(MakeElectricPuddle(), new[] { new GridCoord(2, 0) });

            Assert.IsTrue(_movement.Move(_player, new GridCoord(3, 0)));

            Assert.AreEqual(1, _stun.GetStunTurns(_player),
                "El segundo charco del mismo recorrido no aporta nada: max(1,1) = 1.");
        }

        private sealed class NullDamagePipeline : IDamagePipeline
        {
            public DamageContext Resolve(DamageContext ctx) { ctx.FinalDamage = ctx.BaseDamage; return ctx; }
            public DamageContext Preview(DamageContext ctx) => ctx;
        }
    }
}
