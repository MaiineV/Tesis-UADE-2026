using System;
using System.Collections.Generic;
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
    /// Fase C: un footprint multi-celda activa casillas con CUALQUIER celda cubierta, una vez
    /// por instancia; los pinchos desarman las celdas pisadas; SafeZone protege por cualquier
    /// celda; el HUD y la Fortaleza ven todas las celdas cubiertas.
    /// </summary>
    [TestFixture]
    public class SpecialTileFootprintTests
    {
        static readonly Vector2Int Two = new Vector2Int(2, 2);

        private GridManager _grid;
        private MovementService _movement;
        private UnitTraitService _traits;
        private SpecialTileService _svc;
        private SpyDamagePipeline _damage;
        private SpyHealPipeline _heal;

        private Guid _player;
        private Guid _big;
        private readonly List<ScriptableObject> _createdAssets = new List<ScriptableObject>();

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(8, 8));
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);

            _movement = new MovementService(_grid);
            ServiceLocator.AddService<IMovementService>(_movement, ServiceScope.Global);

            _traits = new UnitTraitService();
            ServiceLocator.AddService<IUnitTraitService>(_traits, ServiceScope.Global);

            _damage = new SpyDamagePipeline();
            ServiceLocator.AddService<IDamagePipeline>(_damage, ServiceScope.Global);

            _heal = new SpyHealPipeline();
            ServiceLocator.AddService<IHealPipeline>(_heal, ServiceScope.Global);

            _player = Guid.NewGuid();
            _grid.Register(_player, new GridCoord(7, 7)); // lejos: acá juega el 2×2
            _traits.Register(_player, UnitTraits.DefaultGround);

            // 2×2 anclado en (0,0), cubre (0,0)-(1,1).
            _big = Guid.NewGuid();
            Assert.IsTrue(_grid.TryRegister(_big, new GridCoord(0, 0), Two));
            _traits.Register(_big, UnitTraits.DefaultGround);

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

            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        // ======================================================================
        // Helpers (calcados de SpecialTileServiceTests)
        // ======================================================================

        private SpecialTileDefinitionSO MakeDefinition(Action<SpecialTileDefinitionSO> configure)
        {
            var def = ScriptableObject.CreateInstance<SpecialTileDefinitionSO>();
            configure(def);
            _createdAssets.Add(def);
            return def;
        }

        private SpecialTileDefinitionSO MakeFire() => MakeDefinition(d =>
        {
            d.TileId = "TILE_FIRE";
            d.TileType = SpecialTileType.Fire;
            d.Triggers = TileTrigger.OnEnter | TileTrigger.OnTurnStart;
            d.Category = TileEffectCategory.Damage;
            d.Affinity = TileAffinity.All;
            d.EnterDamage = 8;
            d.TurnStartDamage = 12;
        });

        private SpecialTileDefinitionSO MakeSpikes() => MakeDefinition(d =>
        {
            d.TileId = "TILE_SPIKES";
            d.TileType = SpecialTileType.Spikes;
            d.Triggers = TileTrigger.OnEnter | TileTrigger.OnForcedMovementInto;
            d.Category = TileEffectCategory.Damage;
            d.Affinity = TileAffinity.GroundOnly;
            d.EnterDamage = 12;
            d.DisarmOnTrigger = true;
            d.RearmOnRoundWrap = true;
        });

        private SpecialTileDefinitionSO MakeHeal() => MakeDefinition(d =>
        {
            d.TileId = "TILE_HEAL";
            d.TileType = SpecialTileType.Heal;
            d.Triggers = TileTrigger.OnEndTurn;
            d.Category = TileEffectCategory.Heal;
            d.Affinity = TileAffinity.All;
            d.HealAmount = 12;
        });

        private SpecialTileDefinitionSO MakeSafeZone(params SpecialTileType[] protects) => MakeDefinition(d =>
        {
            d.TileId = "TILE_SAFEZONE";
            d.TileType = SpecialTileType.SafeZone;
            d.Category = TileEffectCategory.ConditionalProtection;
            d.ProtectedTileTypes = protects;
        });

        private SpecialTileDefinitionSO MakeFortress() => MakeDefinition(d =>
        {
            d.TileId = "TILE_FORTRESS";
            d.TileType = SpecialTileType.Strength; // la Fortaleza: GetFlatBonus filtra por categoría
            d.Triggers = TileTrigger.OnRemainOn;
            d.Category = TileEffectCategory.StatModifier;
            d.Affinity = TileAffinity.All;
            d.ComboDamageBonus = 5;
        });

        private void MoveBigTo(int x, int y)
            => Assert.IsTrue(_movement.Move(_big, new GridCoord(x, y)),
                $"Setup: el 2×2 tiene que poder moverse a ({x},{y}).");

        // ======================================================================
        // Entrada
        // ======================================================================

        [Test]
        public void Enter_FireUnderNonAnchorCell_DamagesOnce()
        {
            // Fuego en (2,1): al mover el ancla a (1,0) el rect cubre (1,0)-(2,1) y lo pisa
            // con su celda superior-derecha, no con el ancla.
            _svc.Place(MakeFire(), new[] { new GridCoord(2, 1) });

            MoveBigTo(1, 0);

            Assert.AreEqual(1, _damage.Resolved.Count);
            Assert.AreEqual(8, _damage.Resolved[0].BaseDamage);
            Assert.AreEqual(_big, _damage.Resolved[0].TargetId);
        }

        [Test]
        public void Enter_TwoCellsOfSameInstance_DamagesOnce()
        {
            _svc.Place(MakeFire(), new[] { new GridCoord(2, 0), new GridCoord(2, 1) });

            MoveBigTo(1, 0);

            Assert.AreEqual(1, _damage.Resolved.Count, "dos celdas de la MISMA instancia = un cobro");
        }

        [Test]
        public void Enter_TwoSeparateInstances_DamageTwice()
        {
            // Regla preexistente (dos instancias solapadas cobran dos veces) conservada.
            _svc.Place(MakeFire(), new[] { new GridCoord(2, 0) });
            _svc.Place(MakeFire(), new[] { new GridCoord(2, 1) });

            MoveBigTo(1, 0);

            Assert.AreEqual(2, _damage.Resolved.Count);
        }

        [Test]
        public void Enter_SpikesDisarmEveryCoveredCell()
        {
            _svc.Place(MakeSpikes(), new[] { new GridCoord(2, 0), new GridCoord(2, 1) });

            MoveBigTo(1, 0);
            Assert.AreEqual(1, _damage.Resolved.Count, "primer pisotón: un cobro");

            // Vuelve y re-entra: las DOS celdas pisadas quedaron desarmadas.
            MoveBigTo(4, 4);
            MoveBigTo(1, 0);
            Assert.AreEqual(1, _damage.Resolved.Count, "ambas celdas pisadas quedaron gastadas");
        }

        [Test]
        public void Enter_SafeZoneUnderAnyCoveredCell_Protects()
        {
            // Fuego bajo el ancla destino (1,0); la SafeZone cubre (2,1) — otra celda del rect.
            _svc.Place(MakeFire(), new[] { new GridCoord(1, 0) });
            _svc.Place(MakeSafeZone(SpecialTileType.Fire), new[] { new GridCoord(2, 1) });

            MoveBigTo(1, 0);

            Assert.AreEqual(0, _damage.Resolved.Count,
                "protegido si CUALQUIER celda del rect está dentro de la zona");
        }

        // ======================================================================
        // Parado (turn start / end)
        // ======================================================================

        [Test]
        public void TurnStart_FireUnderNonAnchorCell_DamagesOnce()
        {
            // El 2×2 ya está sobre (0,0)-(1,1); fuego en (1,1) (celda no-ancla), colocado
            // después para no cobrar entrada.
            _svc.Place(MakeFire(), new[] { new GridCoord(1, 1), new GridCoord(0, 1) });

            EventManager.Trigger(EventName.OnTurnStarted, _big);

            Assert.AreEqual(1, _damage.Resolved.Count);
            Assert.AreEqual(12, _damage.Resolved[0].BaseDamage, "TurnStartDamage, una vez por instancia");
        }

        [Test]
        public void EndTurn_HealUnderNonAnchorCell_HealsOnce()
        {
            _svc.Place(MakeHeal(), new[] { new GridCoord(1, 1) });

            EventManager.Trigger(EventName.OnTurnFinished, _big);

            Assert.AreEqual(1, _heal.Resolved.Count);
            Assert.AreEqual(_big, _heal.Resolved[0].TargetId);
        }

        // ======================================================================
        // HUD + Fortaleza
        // ======================================================================

        [Test]
        public void CollectUnder_SeesTileUnderAnyCoveredCell()
        {
            _svc.Place(MakeFire(), new[] { new GridCoord(1, 1) });

            var under = new List<SpecialTileInfo>();
            _svc.CollectUnder(_big, under);

            Assert.IsTrue(under.Exists(i => i.Definition.TileType == SpecialTileType.Fire));
        }

        [Test]
        public void GetFlatBonus_FortressUnderAnyCoveredCell_Applies()
        {
            _svc.Place(MakeFortress(), new[] { new GridCoord(1, 1) });

            int bonus = _svc.GetFlatBonus(new Combat.Pipelines.DamageContext { SourceId = _big });

            Assert.AreEqual(5, bonus);
        }

        // ======================================================================
        // Fakes (calcados: son private en SpecialTileServiceTests)
        // ======================================================================

        private sealed class SpyDamagePipeline : IDamagePipeline
        {
            public readonly List<DamageContext> Resolved = new List<DamageContext>();

            public DamageContext Resolve(DamageContext ctx)
            {
                ctx.FinalDamage = ctx.BaseDamage;
                Resolved.Add(ctx);
                return ctx;
            }

            public DamageContext Preview(DamageContext ctx)
            {
                ctx.FinalDamage = ctx.BaseDamage;
                return ctx;
            }
        }

        private sealed class SpyHealPipeline : IHealPipeline
        {
            public readonly List<HealContext> Resolved = new List<HealContext>();

            public HealContext Resolve(HealContext ctx)
            {
                ctx.FinalHeal = ctx.BaseHeal;
                Resolved.Add(ctx);
                return ctx;
            }
        }
    }
}
