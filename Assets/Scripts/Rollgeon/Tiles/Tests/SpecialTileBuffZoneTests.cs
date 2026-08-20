using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Entities.Traits;
using Rollgeon.Grid;
using Rollgeon.Movement;
using UnityEngine;

namespace Rollgeon.Tiles.Tests
{
    /// <summary>
    /// Tests de Fortaleza (+10 plano al combo ofensivo vía DamagePipeline), Telegraph
    /// (advertencia → payload al vencer) y Zona de Seguridad (protección declarada,
    /// variante móvil, creación runtime solo-jefes).
    /// </summary>
    [TestFixture]
    public class SpecialTileBuffZoneTests
    {
        private GridManager _grid;
        private MovementService _movement;
        private UnitTraitService _traits;
        private SpecialTileService _svc;
        private AttributesManager _attributes;
        private DamagePipeline _pipeline;
        private Guid _player;
        private Guid _target;
        private readonly List<ScriptableObject> _createdAssets = new List<ScriptableObject>();

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            TypedEvent<DamageResolvedPayload>.Clear();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(8, 8));
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);

            _movement = new MovementService(_grid);
            ServiceLocator.AddService<IMovementService>(_movement, ServiceScope.Global);

            _traits = new UnitTraitService();
            ServiceLocator.AddService<IUnitTraitService>(_traits, ServiceScope.Global);

            _attributes = new AttributesManager();
            _pipeline = new DamagePipeline(_attributes);
            ServiceLocator.AddService<IDamagePipeline>(_pipeline, ServiceScope.Global);

            _player = Guid.NewGuid();
            _grid.Register(_player, new GridCoord(0, 0));
            _traits.Register(_player, UnitTraits.DefaultGround);
            var playerAttrs = new ModifiableAttributes();
            playerAttrs.EnsureInitialized();
            playerAttrs.SetAttribute<Health>(new Health(100));
            _attributes.Register(_player, playerAttrs);

            _target = Guid.NewGuid();
            var targetAttrs = new ModifiableAttributes();
            targetAttrs.EnsureInitialized();
            targetAttrs.SetAttribute<Health>(new Health(100));
            _attributes.Register(_target, targetAttrs);

            _svc = new SpecialTileService();
            _svc.ConfigureForTests(() => _player);
            ServiceLocator.AddService<IOutgoingFlatDamageBonusProvider>(_svc, ServiceScope.Global);
        }

        [TearDown]
        public void TearDown()
        {
            _svc?.Dispose();
            _attributes?.Dispose();
            _svc = null;

            foreach (var asset in _createdAssets)
                if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
            _createdAssets.Clear();

            EventManager.ResetEventDictionary();
            TypedEvent<DamageResolvedPayload>.Clear();
            ServiceLocator.Clear();
        }

        private SpecialTileDefinitionSO MakeDef(Action<SpecialTileDefinitionSO> configure)
        {
            var def = ScriptableObject.CreateInstance<SpecialTileDefinitionSO>();
            configure(def);
            _createdAssets.Add(def);
            return def;
        }

        private SpecialTileDefinitionSO Strength() => MakeDef(d =>
        {
            d.TileType = SpecialTileType.Strength;
            d.Triggers = TileTrigger.OnRemainOn;
            d.Category = TileEffectCategory.StatModifier;
            d.Affinity = TileAffinity.All;
            d.ComboDamageBonus = 10;
        });

        private SpecialTileDefinitionSO Fire() => MakeDef(d =>
        {
            d.TileType = SpecialTileType.Fire;
            d.Triggers = TileTrigger.OnEnter | TileTrigger.OnTurnStart;
            d.Category = TileEffectCategory.Damage;
            d.Affinity = TileAffinity.All;
            d.EnterDamage = 8;
            d.TurnStartDamage = 12;
        });

        private SpecialTileDefinitionSO FireTemp() => MakeDef(d =>
        {
            d.TileType = SpecialTileType.FireTemp;
            d.Triggers = TileTrigger.OnEnter | TileTrigger.OnTurnStart;
            d.Category = TileEffectCategory.Damage;
            d.EnterDamage = 6;
            d.TurnStartDamage = 10;
            d.DefaultDurationRounds = 2;
        });

        private SpecialTileDefinitionSO Telegraph(SpecialTileDefinitionSO payload) => MakeDef(d =>
        {
            d.TileType = SpecialTileType.Telegraph;
            d.Triggers = TileTrigger.OnTelegraphExpire;
            d.Category = TileEffectCategory.Telegraph;
            d.DefaultDurationRounds = 1;
            d.TelegraphPayload = payload;
        });

        private SpecialTileDefinitionSO SafeZone(params SpecialTileType[] protects) => MakeDef(d =>
        {
            d.TileType = SpecialTileType.SafeZone;
            d.Triggers = TileTrigger.OnRemainOn;
            d.Category = TileEffectCategory.ConditionalProtection;
            d.ProtectedTileTypes = protects;
        });

        private static void WrapRound(int roundIndex)
            => EventManager.Trigger(EventName.OnTurnQueueBuilt, new List<Guid>().AsReadOnly(), roundIndex);

        private DamageContext Hit(Guid source, AttackKind kind, int baseDamage = 30)
            => _pipeline.Resolve(new DamageContext
            {
                SourceId = source,
                TargetId = _target,
                BaseDamage = baseDamage,
                Kind = kind,
            });

        // ======================================================================
        // Fortaleza
        // ======================================================================

        [Test]
        public void Strength_StandingOnTile_Adds10ToComboAttack()
        {
            _svc.Place(Strength(), new[] { new GridCoord(0, 0) });

            var ctx = Hit(_player, AttackKind.ComboAttack);

            Assert.AreEqual(40, ctx.FinalDamage, "30 base + 10 de Fortaleza.");
        }

        [Test]
        public void Strength_AfterLeavingTile_BonusDisappears()
        {
            _svc.Place(Strength(), new[] { new GridCoord(0, 0) });
            Assert.AreEqual(40, Hit(_player, AttackKind.ComboAttack).FinalDamage, "Setup: bonus activo.");

            Assert.IsTrue(_movement.Move(_player, new GridCoord(1, 0)));

            Assert.AreEqual(30, Hit(_player, AttackKind.ComboAttack).FinalDamage,
                "El bonus se pierde apenas la unidad abandona la casilla — sin bookkeeping.");
        }

        [Test]
        public void Strength_DoesNotBuffDamageOverTime()
        {
            _svc.Place(Strength(), new[] { new GridCoord(0, 0) });

            var ctx = Hit(_player, AttackKind.DamageOverTime);

            Assert.AreEqual(30, ctx.FinalDamage, "Fortaleza buffea el combo OFENSIVO, no los DoT.");
        }

        [Test]
        public void Strength_BuffsEnemySourceStandingOnTile()
        {
            var enemy = Guid.NewGuid();
            _grid.Register(enemy, new GridCoord(3, 3));
            _traits.Register(enemy, UnitTraits.DefaultGround);
            _svc.Place(Strength(), new[] { new GridCoord(3, 3) });

            var ctx = Hit(enemy, AttackKind.BasicAttack);

            Assert.AreEqual(40, ctx.FinalDamage, "La casilla es del terreno: buffea a quien la pise.");
        }

        // ======================================================================
        // Telegraph
        // ======================================================================

        [Test]
        public void Telegraph_AfterOneRound_SpawnsAnnouncedPayload()
        {
            var payload = FireTemp();
            var telegraphId = _svc.Place(Telegraph(payload), new[] { new GridCoord(4, 4) },
                new TilePlacementOptions { Owner = _player });

            WrapRound(1);

            Assert.IsTrue(_svc.TryGetTileAt(new GridCoord(4, 4), out var info),
                "La marca fue reemplazada por el resultado del efecto.");
            Assert.AreEqual(payload, info.Definition);
            Assert.AreNotEqual(telegraphId, info.InstanceId);
            Assert.AreEqual(2, info.RemainingRounds, "El payload nace con SU duración (FireTemp = 2).");
            Assert.AreEqual(_player, info.OwnerGuid, "El owner del telegraph se propaga al payload.");
        }

        [Test]
        public void Telegraph_EnteringTheMark_HasNoOwnEffect()
        {
            var triggered = new List<object[]>();
            EventManager.EventReceiver onTriggered = args => triggered.Add(args);
            EventManager.Subscribe(EventName.OnSpecialTileTriggered, onTriggered);
            _svc.Place(Telegraph(FireTemp()), new[] { new GridCoord(2, 0) });

            Assert.IsTrue(_movement.Move(_player, new GridCoord(2, 0)));

            Assert.AreEqual(0, triggered.Count,
                "Telegraph no produce efecto propio: solo anuncia (transitable, no bloquea).");
            EventManager.UnSubscribe(EventName.OnSpecialTileTriggered, onTriggered);
        }

        // ======================================================================
        // Zona de Seguridad
        // ======================================================================

        [Test]
        public void SafeZone_ProtectsFromDeclaredTileType()
        {
            _svc.Place(Fire(), new[] { new GridCoord(2, 0) });
            _svc.Place(SafeZone(SpecialTileType.Fire), new[] { new GridCoord(2, 0), new GridCoord(3, 0) });

            Assert.IsTrue(_movement.Move(_player, new GridCoord(2, 0)));
            EventManager.Trigger(EventName.OnTurnStarted, _player);

            Assert.AreEqual(100, _attributes.GetAttribute<Health>(_player).Value,
                "Adentro de la zona ni el OnEnter ni el tick de turno del Fuego cobran.");
        }

        [Test]
        public void SafeZone_ProtectsEnemiesToo()
        {
            var enemy = Guid.NewGuid();
            _grid.Register(enemy, new GridCoord(0, 2));
            _traits.Register(enemy, UnitTraits.DefaultGround);
            var enemyAttrs = new ModifiableAttributes();
            enemyAttrs.EnsureInitialized();
            enemyAttrs.SetAttribute<Health>(new Health(50));
            _attributes.Register(enemy, enemyAttrs);

            _svc.Place(Fire(), new[] { new GridCoord(2, 2) });
            _svc.Place(SafeZone(SpecialTileType.Fire), new[] { new GridCoord(2, 2) });

            Assert.IsTrue(_movement.Move(enemy, new GridCoord(2, 2)));

            Assert.AreEqual(50, _attributes.GetAttribute<Health>(enemy).Value,
                "La zona protege a CUALQUIER unidad adentro, incluidos enemigos.");
        }

        [Test]
        public void SafeZone_DoesNotProtectUndeclaredTypes()
        {
            var enemy = Guid.NewGuid();
            _grid.Register(enemy, new GridCoord(0, 2));
            _traits.Register(enemy, UnitTraits.DefaultGround);
            var enemyAttrs = new ModifiableAttributes();
            enemyAttrs.EnsureInitialized();
            enemyAttrs.SetAttribute<Health>(new Health(50));
            _attributes.Register(enemy, enemyAttrs);

            _svc.Place(Fire(), new[] { new GridCoord(2, 2) });
            _svc.Place(SafeZone(SpecialTileType.Spikes), new[] { new GridCoord(2, 2) });

            Assert.IsTrue(_movement.Move(enemy, new GridCoord(2, 2)));

            Assert.AreEqual(42, _attributes.GetAttribute<Health>(enemy).Value,
                "No es inmunidad general: una zona que declara Pinchos no frena al Fuego.");
        }

        [Test]
        public void SafeZone_Movable_ProtectionFollowsTheZone()
        {
            var enemy = Guid.NewGuid();
            _grid.Register(enemy, new GridCoord(0, 2));
            _traits.Register(enemy, UnitTraits.DefaultGround);
            var enemyAttrs = new ModifiableAttributes();
            enemyAttrs.EnsureInitialized();
            enemyAttrs.SetAttribute<Health>(new Health(50));
            _attributes.Register(enemy, enemyAttrs);

            _svc.Place(Fire(), new[] { new GridCoord(2, 2) });
            var zoneId = _svc.Place(SafeZone(SpecialTileType.Fire), new[] { new GridCoord(2, 2) });

            // La zona se corre a otra parte de la sala: el fuego vuelve a cobrar.
            _svc.MoveInstance(zoneId, new[] { new GridCoord(6, 6) });
            Assert.IsTrue(_movement.Move(enemy, new GridCoord(2, 2)));

            Assert.AreEqual(42, _attributes.GetAttribute<Health>(enemy).Value,
                "La protección vive en las coords vigentes de la zona, no en un cache.");
        }

        [Test]
        public void SafeZone_RuntimeCreation_RequiresBossOwner()
        {
            var boss = Guid.NewGuid();
            _traits.Register(boss, new UnitTraits(isFlying: false, isBoss: true));

            var rejected = _svc.CreateRuntime(SafeZone(SpecialTileType.Fire), new GridCoord(5, 5),
                new RuntimeTileRequest { Owner = _player, DurationRounds = 2 }, out var errorPlayer);
            var accepted = _svc.CreateRuntime(SafeZone(SpecialTileType.Fire), new GridCoord(5, 5),
                new RuntimeTileRequest { Owner = boss, DurationRounds = 2 }, out var errorBoss);

            Assert.AreEqual(TilePlacementError.OwnerNotAuthorized, errorPlayer);
            Assert.AreEqual(Guid.Empty, rejected);
            Assert.AreEqual(TilePlacementError.None, errorBoss);
            Assert.AreNotEqual(Guid.Empty, accepted);
        }
    }
}
