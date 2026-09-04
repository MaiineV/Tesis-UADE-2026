using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Dice;
using Rollgeon.Effects;
using Rollgeon.Entities.Traits;
using Rollgeon.Grid;
using Rollgeon.Movement;
using Rollgeon.Upgrades;
using Rollgeon.Upgrades.Dice;
using Rollgeon.Upgrades.Dice.Effects;
using UnityEngine;

namespace Rollgeon.Tiles.Tests
{
    /// <summary>
    /// Rastros del dado de Movimiento (Incendiario / Rastro tóxico / Sendero de espinas):
    /// <see cref="EffPlaceTrailTiles"/> deja casillas en las celdas abandonadas, el dueño no
    /// las activa (<c>OwnerAndAlliesImmune</c>) y las espinas terminan el movimiento del
    /// enemigo (<c>EndsMovementOnEnter</c>).
    /// </summary>
    [TestFixture]
    public sealed class MovementTrailTilesTests
    {
        private GridManager _grid;
        private MovementService _movement;
        private UnitTraitService _traits;
        private SpecialTileService _svc;
        private SpyDamagePipeline _damage;
        private Guid _player;
        private Guid _enemy;
        private readonly List<ScriptableObject> _created = new List<ScriptableObject>();

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(7, 3));
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);
            _movement = new MovementService(_grid);
            ServiceLocator.AddService<IMovementService>(_movement, ServiceScope.Global);
            _traits = new UnitTraitService();
            ServiceLocator.AddService<IUnitTraitService>(_traits, ServiceScope.Global);
            _damage = new SpyDamagePipeline();
            ServiceLocator.AddService<IDamagePipeline>(_damage, ServiceScope.Global);

            _player = Guid.NewGuid();
            _enemy = Guid.NewGuid();
            _grid.Register(_player, new GridCoord(0, 0));
            _grid.Register(_enemy, new GridCoord(6, 0));
            _traits.Register(_player, UnitTraits.DefaultGround);
            _traits.Register(_enemy, UnitTraits.DefaultGround);

            _svc = new SpecialTileService();
            _svc.ConfigureForTests(() => _player);
            ServiceLocator.AddService<ISpecialTileService>(_svc, ServiceScope.Global);
        }

        [TearDown]
        public void TearDown()
        {
            _svc?.Dispose();
            _svc = null;
            foreach (var so in _created) if (so != null) UnityEngine.Object.DestroyImmediate(so);
            _created.Clear();
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        private SpecialTileDefinitionSO MakeTrailSpikes()
        {
            var d = ScriptableObject.CreateInstance<SpecialTileDefinitionSO>();
            d.TileId = "TILE_SPIKES_SENDERO";
            d.TileType = SpecialTileType.Spikes;
            d.Triggers = TileTrigger.OnEnter | TileTrigger.OnForcedMovementInto;
            d.Category = TileEffectCategory.Damage;
            d.Affinity = TileAffinity.All;
            d.EnterDamage = 2;
            d.DisarmOnTrigger = true;
            d.OwnerBossImmune = false;
            d.OwnerAndAlliesImmune = true;
            d.EndsMovementOnEnter = true;
            d.DefaultDurationRounds = 1;
            _created.Add(d);
            return d;
        }

        private EffectContext WalkCtx(IReadOnlyList<GridCoord> path, int enchSlot = 0)
        {
            return new EffectContext
            {
                SourceGuid = _player,
                TriggerContext = new ScratchTriggerContext
                {
                    Scratch = new EnchantmentScratch(),
                    Slot = new EnchantmentSlotRef(DiceType.D6, EnchantmentSlotRef.MovementDieSlot, enchSlot),
                    Channel = ScratchChannel.DiceEnchantment,
                    Path = path,
                    TilesTraversed = path.Count - 1,
                    TilesTraversedThisTurn = path.Count - 1,
                },
            };
        }

        private static IReadOnlyList<GridCoord> Row(int fromX, int toX)
        {
            var path = new List<GridCoord>();
            for (int x = fromX; x <= toX; x++) path.Add(new GridCoord(x, 0));
            return path;
        }

        [Test]
        public void PlaceTrailTiles_LeavesTilesOnAbandonedCellsOnly_OwnedByThePlayer()
        {
            var eff = new EffPlaceTrailTiles { Definition = MakeTrailSpikes(), DurationRounds = 1 };
            // Como en el juego: el hook llega con el jugador YA en el destino.
            Assert.IsTrue(_movement.Move(_player, new GridCoord(3, 0)));

            Assert.IsTrue(eff.ApplyEffect(WalkCtx(Row(0, 3))));

            var placed = _svc.ActiveInstances().Select(i => i.Coords.Single()).ToList();
            CollectionAssert.AreEquivalent(new[] { new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(2, 0) }, placed);
            Assert.IsFalse(placed.Contains(new GridCoord(3, 0)), "El destino no recibe rastro.");
        }

        [Test]
        public void TrailTile_DoesNotAffectItsOwner_ButHurtsAndStopsAnEnemy()
        {
            var eff = new EffPlaceTrailTiles { Definition = MakeTrailSpikes(), DurationRounds = 1 };
            // El player camina 0→3 y deja espinas en 0,1,2; después vuelve a 0.
            Assert.IsTrue(_movement.Move(_player, new GridCoord(3, 0)));
            Assert.IsTrue(eff.ApplyEffect(WalkCtx(Row(0, 3))));
            Assert.IsTrue(_movement.Move(_player, new GridCoord(0, 1)));
            Assert.AreEqual(0, _damage.Resolved.Count, "El dueño no activa su propio rastro.");

            // El enemigo camina 6→1: entra en (2,0), cobra 2 y frena ahí.
            _movement.SetPathFilter(_svc);
            Assert.IsTrue(_movement.Move(_enemy, new GridCoord(1, 0)));

            Assert.AreEqual(1, _damage.Resolved.Count);
            Assert.AreEqual(2, _damage.Resolved[0].BaseDamage);
            Assert.AreEqual(_enemy, _damage.Resolved[0].TargetId);
            Assert.IsTrue(_grid.TryGetPosition(_enemy, out var pos));
            Assert.AreEqual(new GridCoord(2, 0), pos, "Las espinas terminan el movimiento.");
        }

        [Test]
        public void PlaceTrailTiles_TilesExpireAfterTheirDuration()
        {
            var eff = new EffPlaceTrailTiles { Definition = MakeTrailSpikes(), DurationRounds = 1 };
            Assert.IsTrue(_movement.Move(_player, new GridCoord(2, 0)));
            Assert.IsTrue(eff.ApplyEffect(WalkCtx(Row(0, 2))));
            Assert.AreEqual(2, _svc.ActiveInstances().Count());
            Assert.IsTrue(_svc.ActiveInstances().All(i => i.RemainingRounds == 1 && i.OwnerGuid == _player));

            EventManager.Trigger(EventName.OnTurnQueueBuilt, new List<Guid>().AsReadOnly(), 1);
            EventManager.Trigger(EventName.OnTurnQueueBuilt, new List<Guid>().AsReadOnly(), 2);

            Assert.AreEqual(0, _svc.ActiveInstances().Count(), "Espinas de 1 ronda ya no están.");
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
