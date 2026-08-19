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
    /// Tests del núcleo de <see cref="SpecialTileService"/>: triggers de entrada y de turno,
    /// armado/desarmado (Pinchos), duración por rondas (Fuego Temporal + regresión Append),
    /// curación solo al terminar turno, affinity Ground/Flying, ownership de jefes, kill
    /// credit, invariante de spawn y validación de creación runtime.
    /// </summary>
    [TestFixture]
    public class SpecialTileServiceTests
    {
        private GridManager _grid;
        private MovementService _movement;
        private UnitTraitService _traits;
        private SpecialTileService _svc;
        private SpyDamagePipeline _damage;
        private SpyHealPipeline _heal;

        private Guid _player;
        private readonly List<ScriptableObject> _createdAssets = new List<ScriptableObject>();
        private List<object[]> _expiredLog;
        private EventManager.EventReceiver _onExpired;

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

            _damage = new SpyDamagePipeline();
            ServiceLocator.AddService<IDamagePipeline>(_damage, ServiceScope.Global);

            _heal = new SpyHealPipeline();
            ServiceLocator.AddService<IHealPipeline>(_heal, ServiceScope.Global);

            _player = Guid.NewGuid();
            _grid.Register(_player, new GridCoord(0, 0));
            _traits.Register(_player, UnitTraits.DefaultGround);

            _svc = new SpecialTileService();
            _svc.ConfigureForTests(() => _player);

            _expiredLog = new List<object[]>();
            _onExpired = args => _expiredLog.Add(args);
            EventManager.Subscribe(EventName.OnSpecialTileExpired, _onExpired);
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
        // Helpers
        // ======================================================================

        private SpecialTileDefinitionSO MakeDefinition(Action<SpecialTileDefinitionSO> configure)
        {
            var def = ScriptableObject.CreateInstance<SpecialTileDefinitionSO>();
            configure(def);
            _createdAssets.Add(def);
            return def;
        }

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

        private SpecialTileDefinitionSO MakeFireTemp() => MakeDefinition(d =>
        {
            d.TileId = "TILE_FIRE_TEMP";
            d.TileType = SpecialTileType.FireTemp;
            d.Triggers = TileTrigger.OnEnter | TileTrigger.OnTurnStart;
            d.Category = TileEffectCategory.Damage;
            d.Affinity = TileAffinity.All;
            d.EnterDamage = 6;
            d.TurnStartDamage = 10;
            d.DefaultDurationRounds = 2;
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

        private static void WrapRound(int roundIndex)
            => EventManager.Trigger(EventName.OnTurnQueueBuilt,
                new List<Guid>().AsReadOnly(), roundIndex);

        private void MovePlayerTo(int x, int y)
            => Assert.IsTrue(_movement.Move(_player, new GridCoord(x, y)),
                $"Setup: el player tiene que poder moverse a ({x},{y}).");

        // ======================================================================
        // Pinchos — OnEnter, armado/desarmado
        // ======================================================================

        [Test]
        public void Spikes_EnterVoluntarily_Deals12EnvironmentalDamage()
        {
            var id = _svc.Place(MakeSpikes(), new[] { new GridCoord(2, 0) });

            MovePlayerTo(2, 0);

            Assert.AreEqual(1, _damage.Resolved.Count);
            Assert.AreEqual(12, _damage.Resolved[0].BaseDamage);
            Assert.AreEqual(id, _damage.Resolved[0].SourceId);
            Assert.AreEqual(_player, _damage.Resolved[0].TargetId);
            Assert.AreEqual(AttackKind.Environmental, _damage.Resolved[0].Kind);
        }

        [Test]
        public void Spikes_ReEnterWhileDisarmed_DealsNoDamage()
        {
            _svc.Place(MakeSpikes(), new[] { new GridCoord(2, 0) });
            MovePlayerTo(2, 0);

            MovePlayerTo(1, 0);
            MovePlayerTo(2, 0);

            Assert.AreEqual(1, _damage.Resolved.Count,
                "Los pinchos quedan desarmados tras disparar: re-entrar antes del rearme no cobra.");
        }

        [Test]
        public void Spikes_RearmedOnRoundWrap_DamagesOnNextEntry()
        {
            _svc.Place(MakeSpikes(), new[] { new GridCoord(2, 0) });
            MovePlayerTo(2, 0);
            MovePlayerTo(1, 0);

            WrapRound(1);
            MovePlayerTo(2, 0);

            Assert.AreEqual(2, _damage.Resolved.Count,
                "El wrap de ronda rearma la celda — 'listo para el próximo ciclo'.");
        }

        [Test]
        public void Spikes_TurnStartWhileStanding_NoRepeatDamage()
        {
            _svc.Place(MakeSpikes(), new[] { new GridCoord(2, 0) });
            MovePlayerTo(2, 0);

            EventManager.Trigger(EventName.OnTurnStarted, _player);
            EventManager.Trigger(EventName.OnTurnFinished, _player);

            Assert.AreEqual(1, _damage.Resolved.Count,
                "Permanecer sobre pinchos no re-dispara: solo entrar/atravesar.");
        }

        // ======================================================================
        // Fuego — OnEnter + OnTurnStart, afecta voladoras
        // ======================================================================

        [Test]
        public void Fire_Enter_Deals8_AndTurnStartStanding_Deals12More()
        {
            _svc.Place(MakeFire(), new[] { new GridCoord(2, 0) });

            MovePlayerTo(2, 0);
            EventManager.Trigger(EventName.OnTurnStarted, _player);

            Assert.AreEqual(2, _damage.Resolved.Count);
            Assert.AreEqual(8, _damage.Resolved[0].BaseDamage);
            Assert.AreEqual(12, _damage.Resolved[1].BaseDamage);
        }

        [Test]
        public void Fire_TurnStartRecurring_DamagesEveryTurnWhileStanding()
        {
            _svc.Place(MakeFire(), new[] { new GridCoord(2, 0) });
            MovePlayerTo(2, 0);

            EventManager.Trigger(EventName.OnTurnStarted, _player);
            EventManager.Trigger(EventName.OnTurnStarted, _player);

            Assert.AreEqual(3, _damage.Resolved.Count, "8 de entrada + 12 + 12 recurrentes.");
        }

        [Test]
        public void Fire_FlyingUnit_IsAffected()
        {
            var flyer = RegisterUnit(new GridCoord(0, 2), new UnitTraits(isFlying: true, isBoss: false));
            _svc.Place(MakeFire(), new[] { new GridCoord(2, 2) });

            Assert.IsTrue(_movement.Move(flyer, new GridCoord(2, 2)));

            Assert.AreEqual(1, _damage.Resolved.Count,
                "Fuego es Affinity.All: pega a voladoras (única junto a Fuego Temporal).");
        }

        [Test]
        public void Spikes_FlyingUnit_IsIgnored()
        {
            var flyer = RegisterUnit(new GridCoord(0, 2), new UnitTraits(isFlying: true, isBoss: false));
            _svc.Place(MakeSpikes(), new[] { new GridCoord(2, 2) });

            Assert.IsTrue(_movement.Move(flyer, new GridCoord(2, 2)));

            Assert.AreEqual(0, _damage.Resolved.Count,
                "Pinchos es 'solo terrestres': una voladora la ignora.");
        }

        // ======================================================================
        // Fuego Temporal — duración por rondas
        // ======================================================================

        [Test]
        public void FireTemp_ExpiresAfterTwoRoundWraps()
        {
            var id = _svc.Place(MakeFireTemp(), new[] { new GridCoord(3, 3) });

            WrapRound(1);
            Assert.IsTrue(_svc.TryGetTileAt(new GridCoord(3, 3), out _), "A la ronda 1 sigue viva.");

            WrapRound(2);

            Assert.IsFalse(_svc.TryGetTileAt(new GridCoord(3, 3), out _));
            Assert.AreEqual(1, _expiredLog.Count);
            Assert.AreEqual(id, _expiredLog[0][0]);
        }

        [Test]
        public void FireTemp_QueueRebuildWithoutRoundChange_DoesNotTickDuration()
        {
            _svc.Place(MakeFireTemp(), new[] { new GridCoord(3, 3) });
            WrapRound(1);

            // Regresión: Append de refuerzos re-dispara OnTurnQueueBuilt con el MISMO round.
            WrapRound(1);
            WrapRound(1);

            Assert.IsTrue(_svc.TryGetTileAt(new GridCoord(3, 3), out var info),
                "Rebuilds sin cambio de ronda no descuentan vida.");
            Assert.AreEqual(1, info.RemainingRounds);
        }

        [Test]
        public void FireTemp_ResumeJumpInRounds_SyncsWithoutTicking()
        {
            _svc.Place(MakeFireTemp(), new[] { new GridCoord(3, 3) });

            // Un resume restaura la cola con un roundIndex alto de una: no es un wrap.
            WrapRound(5);

            Assert.IsTrue(_svc.TryGetTileAt(new GridCoord(3, 3), out var info));
            Assert.AreEqual(2, info.RemainingRounds);
        }

        // ======================================================================
        // Curación — solo al terminar el turno encima
        // ======================================================================

        [Test]
        public void Heal_EndTurnOnTile_Heals12()
        {
            var id = _svc.Place(MakeHeal(), new[] { new GridCoord(2, 0) });
            MovePlayerTo(2, 0);

            EventManager.Trigger(EventName.OnTurnFinished, _player);

            Assert.AreEqual(1, _heal.Resolved.Count);
            Assert.AreEqual(12, _heal.Resolved[0].BaseHeal);
            Assert.AreEqual(id, _heal.Resolved[0].SourceId);
            Assert.AreEqual(_player, _heal.Resolved[0].TargetId);
        }

        [Test]
        public void Heal_PassThroughWithoutEndingTurn_DoesNotHeal()
        {
            _svc.Place(MakeHeal(), new[] { new GridCoord(2, 0) });

            MovePlayerTo(4, 0);
            EventManager.Trigger(EventName.OnTurnFinished, _player);

            Assert.AreEqual(0, _heal.Resolved.Count,
                "Pasar de largo no cura: el trigger es OnEndTurn y el turno terminó en (4,0).");
        }

        // ======================================================================
        // Ownership — jefes
        // ======================================================================

        [Test]
        public void BossOwner_EnteringOwnTile_IsImmune()
        {
            var boss = RegisterUnit(new GridCoord(0, 4), new UnitTraits(isFlying: false, isBoss: true));
            _svc.Place(MakeFire(), new[] { new GridCoord(2, 4) },
                new TilePlacementOptions { Owner = boss });

            Assert.IsTrue(_movement.Move(boss, new GridCoord(2, 4)));

            Assert.AreEqual(0, _damage.Resolved.Count,
                "GDD §15: el jefe es inmune a sus propias casillas.");
        }

        [Test]
        public void NonBossOwner_EnteringOwnTile_IsAffected()
        {
            _svc.Place(MakeFire(), new[] { new GridCoord(2, 0) },
                new TilePlacementOptions { Owner = _player });

            MovePlayerTo(2, 0);

            Assert.AreEqual(1, _damage.Resolved.Count,
                "Si el owner no es jefe (player, enemigo raso) su casilla lo afecta igual.");
        }

        [Test]
        public void BossAlly_EnteringBossOwnedTile_IsImmune()
        {
            var ownerBoss = Guid.NewGuid();
            _traits.Register(ownerBoss, new UnitTraits(isFlying: false, isBoss: true));
            var allyBoss = RegisterUnit(new GridCoord(0, 4), new UnitTraits(isFlying: false, isBoss: true));
            _svc.Place(MakeFire(), new[] { new GridCoord(2, 4) },
                new TilePlacementOptions { Owner = ownerBoss });

            Assert.IsTrue(_movement.Move(allyBoss, new GridCoord(2, 4)));

            Assert.AreEqual(0, _damage.Resolved.Count,
                "Afecta a aliados del creador siempre, EXCEPTO que el aliado sea otro jefe.");
        }

        [Test]
        public void Boss_EnteringScenarioTile_IsAffected()
        {
            var boss = RegisterUnit(new GridCoord(0, 4), new UnitTraits(isFlying: false, isBoss: true));
            _svc.Place(MakeFire(), new[] { new GridCoord(2, 4) }); // sin owner = escenario

            Assert.IsTrue(_movement.Move(boss, new GridCoord(2, 4)));

            Assert.AreEqual(1, _damage.Resolved.Count,
                "La inmunidad de jefe aplica solo a casillas de owner jefe, no al escenario.");
        }

        // ======================================================================
        // Kill credit
        // ======================================================================

        [Test]
        public void KillCredit_TileInstanceId_ResolvesToPlayer()
        {
            var id = _svc.Place(MakeSpikes(), new[] { new GridCoord(2, 0) });

            bool resolved = _svc.TryResolveCredit(id, out var credit);

            Assert.IsTrue(resolved);
            Assert.AreEqual(_player, credit);
        }

        [Test]
        public void KillCredit_SurvivesInstanceExpiry()
        {
            var id = _svc.Place(MakeFireTemp(), new[] { new GridCoord(3, 3) });
            _svc.Remove(id);

            bool resolved = _svc.TryResolveCredit(id, out var credit);

            Assert.IsTrue(resolved,
                "El veneno de una casilla expirada sigue matando con su instanceId: el crédito persiste.");
            Assert.AreEqual(_player, credit);
        }

        [Test]
        public void KillCredit_UnknownSource_IsNotResolved()
        {
            _svc.Place(MakeSpikes(), new[] { new GridCoord(2, 0) });

            Assert.IsFalse(_svc.TryResolveCredit(Guid.NewGuid(), out _));
        }

        // ======================================================================
        // Spawn / teleport — nunca disparan OnEnter
        // ======================================================================

        [Test]
        public void Spawn_RegisteredDirectlyOnTile_NeverTriggers()
        {
            _svc.Place(MakeFire(), new[] { new GridCoord(2, 0) });

            var spawned = Guid.NewGuid();
            _grid.Register(spawned, new GridCoord(2, 0));

            Assert.AreEqual(0, _damage.Resolved.Count,
                "Aparecer (spawn) sobre una casilla especial NUNCA dispara OnEnter (GDD §10).");
        }

        [Test]
        public void Teleport_OntoTile_DoesNotTriggerEnter()
        {
            _svc.Place(MakeFire(), new[] { new GridCoord(4, 4) });

            Assert.IsTrue(_movement.Teleport(_player, new GridCoord(4, 4)));

            Assert.AreEqual(0, _damage.Resolved.Count,
                "El teleport no dispara OnEnter en el destino (spec del Portal).");
        }

        // ======================================================================
        // Creación runtime — validación de casilla libre válida
        // ======================================================================

        [Test]
        public void CreateRuntime_ValidRequest_PlacesWithDuration()
        {
            var owner = Guid.NewGuid();
            var id = _svc.CreateRuntime(MakeFireTemp(), new GridCoord(5, 5),
                new RuntimeTileRequest { Owner = owner, DurationRounds = 3 }, out var error);

            Assert.AreEqual(TilePlacementError.None, error);
            Assert.AreNotEqual(Guid.Empty, id);
            Assert.IsTrue(_svc.TryGetTileAt(new GridCoord(5, 5), out var info));
            Assert.AreEqual(3, info.RemainingRounds);
            Assert.AreEqual(owner, info.OwnerGuid);
        }

        [Test]
        public void CreateRuntime_MissingOwner_IsRejected()
        {
            var id = _svc.CreateRuntime(MakeFireTemp(), new GridCoord(5, 5),
                new RuntimeTileRequest { DurationRounds = 2 }, out var error);

            Assert.AreEqual(TilePlacementError.MissingOwner, error);
            Assert.AreEqual(Guid.Empty, id);
        }

        [Test]
        public void CreateRuntime_MissingDuration_IsRejected()
        {
            var id = _svc.CreateRuntime(MakeFireTemp(), new GridCoord(5, 5),
                new RuntimeTileRequest { Owner = Guid.NewGuid() }, out var error);

            Assert.AreEqual(TilePlacementError.MissingDuration, error);
            Assert.AreEqual(Guid.Empty, id);
        }

        [Test]
        public void CreateRuntime_ExplicitPermanent_IsAccepted()
        {
            var id = _svc.CreateRuntime(MakeFire(), new GridCoord(5, 5),
                new RuntimeTileRequest { Owner = Guid.NewGuid(), Permanent = true }, out var error);

            Assert.AreEqual(TilePlacementError.None, error);
            Assert.IsTrue(_svc.TryGetTileAt(new GridCoord(5, 5), out var info));
            Assert.AreEqual(0, info.RemainingRounds);
        }

        [Test]
        public void CreateRuntime_CoordWithUnit_IsRejected()
        {
            var id = _svc.CreateRuntime(MakeFireTemp(), new GridCoord(0, 0),
                new RuntimeTileRequest { Owner = Guid.NewGuid(), DurationRounds = 2 }, out var error);

            Assert.AreEqual(TilePlacementError.CoordOccupiedByUnit, error);
            Assert.AreEqual(Guid.Empty, id);
        }

        [Test]
        public void CreateRuntime_CoordWithAnotherSpecialTile_IsRejected()
        {
            _svc.Place(MakeSpikes(), new[] { new GridCoord(5, 5) });

            var id = _svc.CreateRuntime(MakeFireTemp(), new GridCoord(5, 5),
                new RuntimeTileRequest { Owner = Guid.NewGuid(), DurationRounds = 2 }, out var error);

            Assert.AreEqual(TilePlacementError.CoordHasSpecialTile, error);
            Assert.AreEqual(Guid.Empty, id);
        }

        [Test]
        public void CreateRuntime_UnwalkableCoord_IsRejected()
        {
            var id = _svc.CreateRuntime(MakeFireTemp(), new GridCoord(99, 99),
                new RuntimeTileRequest { Owner = Guid.NewGuid(), DurationRounds = 2 }, out var error);

            Assert.AreEqual(TilePlacementError.CoordNotWalkable, error);
            Assert.AreEqual(Guid.Empty, id);
        }

        // ======================================================================
        // Reset de scope
        // ======================================================================

        [Test]
        public void CombatEnd_ExpiresTemporaries_KeepsPermanents()
        {
            _svc.Place(MakeFire(), new[] { new GridCoord(2, 0) });
            _svc.Place(MakeFireTemp(), new[] { new GridCoord(3, 3) });

            EventManager.Trigger(EventName.OnCombatEnd);

            Assert.IsTrue(_svc.TryGetTileAt(new GridCoord(2, 0), out _),
                "Las permanentes sobreviven al combate: la sala sigue transitable en exploración.");
            Assert.IsFalse(_svc.TryGetTileAt(new GridCoord(3, 3), out _),
                "Las temporales mueren con el combate (las rondas dejaron de existir).");
            Assert.AreEqual(0, _expiredLog.Count, "Teardown silencioso: sin eventos de expiry.");
        }

        [Test]
        public void RoomEntered_ClearsEverything()
        {
            _svc.Place(MakeFire(), new[] { new GridCoord(2, 0) });

            EventManager.Trigger(EventName.OnRoomEntered, Guid.NewGuid());

            Assert.IsFalse(_svc.HasAnySpecialTiles);
        }

        // ======================================================================
        // Fakes
        // ======================================================================

        private Guid RegisterUnit(GridCoord at, UnitTraits traits)
        {
            var guid = Guid.NewGuid();
            _grid.Register(guid, at);
            _traits.Register(guid, traits);
            return guid;
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
