using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Chests;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Rooms;
using Rollgeon.Combat.Status;
using Rollgeon.Entities.Traits;
using Rollgeon.Grid;
using Rollgeon.Movement;
using Rollgeon.Tiles;
using Rollgeon.Tiles.Forced;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rollgeon.Combat.Skills.Push.Tests
{
    /// <summary>
    /// Cobertura de choque de <see cref="ClassSkillPushResolver"/> (Feature#0055 — Empuje del
    /// Guerrero): clasificación del bloqueador (pared / prop / cofre / rompible / enemigo),
    /// daño de choque via el pipeline real, stun contra sólidos, y encadenado (Enemy → recurse).
    /// </summary>
    /// <remarks>
    /// Sin <c>CombatDeathWatcher</c> en EditMode: <see cref="_deathSimHandler"/> simula su único
    /// efecto relevante acá (desregistrar del grid en un golpe letal), suscripto al mismo canal
    /// (<c>TypedEvent&lt;DamageResolvedPayload&gt;</c>) que <c>DamagePipeline.Resolve</c> dispara.
    /// </remarks>
    [TestFixture]
    public class ClassSkillPushResolverTests
    {
        private GridManager _grid;
        private MovementService _movement;
        private UnitTraitService _traits;
        private AttributesManager _attributes;
        private DamagePipeline _damagePipeline;
        private StunService _stun;
        private SpecialTileService _tiles;
        private ForcedMovementService _forced;
        private ClassSkillPushResolver _resolver;
        private IRoomObjectCleanupService _roomObjects;
        private Guid _player;
        private readonly List<ScriptableObject> _createdAssets = new List<ScriptableObject>();
        private Action<DamageResolvedPayload> _deathSimHandler;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(20, 5));
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);

            _movement = new MovementService(_grid);
            ServiceLocator.AddService<IMovementService>(_movement, ServiceScope.Global);

            _traits = new UnitTraitService();
            ServiceLocator.AddService<IUnitTraitService>(_traits, ServiceScope.Global);

            _attributes = new AttributesManager();
            ServiceLocator.AddService<AttributesManager>(_attributes, ServiceScope.Global);

            _damagePipeline = new DamagePipeline(_attributes);
            ServiceLocator.AddService<IDamagePipeline>(_damagePipeline, ServiceScope.Global);

            _player = Guid.NewGuid();
            _grid.Register(_player, new GridCoord(0, 0));
            _traits.Register(_player, UnitTraits.DefaultGround);

            _stun = new StunService();
            _stun.ConfigureForTests(() => _player);
            ServiceLocator.AddService<IStunService>(_stun, ServiceScope.Global);

            _tiles = new SpecialTileService();
            _tiles.ConfigureForTests(() => _player);
            ServiceLocator.AddService<SpecialTileService>(_tiles, ServiceScope.Global);
            ServiceLocator.AddService<ISpecialTileService>(_tiles, ServiceScope.Global);

            _forced = new ForcedMovementService();
            ServiceLocator.AddService<IForcedMovementService>(_forced, ServiceScope.Global);

            _roomObjects = RoomObjectCleanupService.ResolveOrCreate();

            _resolver = new ClassSkillPushResolver();

            _deathSimHandler = payload =>
            {
                if (payload.WasLethal) _grid.Unregister(payload.TargetGuid);
            };
            TypedEvent<DamageResolvedPayload>.Subscribe(_deathSimHandler);
        }

        [TearDown]
        public void TearDown()
        {
            TypedEvent<DamageResolvedPayload>.Unsubscribe(_deathSimHandler);

            (_roomObjects as RoomObjectCleanupService)?.Dispose();
            _tiles?.Dispose();
            _stun?.Dispose();
            _attributes?.Dispose();

            foreach (var asset in _createdAssets)
                if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
            _createdAssets.Clear();

            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        // ======================================================================
        // Helpers
        // ======================================================================

        private void ReloadRoom(int width, int height)
        {
            _grid.LoadRoom(NavGraph.Rect(width, height));
            _grid.Register(_player, new GridCoord(0, 0));
        }

        private Guid SpawnBigEnemy(GridCoord anchor, Vector2Int footprint, int hp)
        {
            var guid = Guid.NewGuid();
            Assert.IsTrue(_grid.TryRegister(guid, anchor, footprint), "setup: el rect tiene que caber");
            _traits.Register(guid, UnitTraits.DefaultGround);
            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(hp));
            attrs.SetAttribute<Shield>(new Shield(0));
            _attributes.Register(guid, attrs);
            return guid;
        }

        // ======================================================================
        // Fase B — empuje de footprints multi-celda
        // ======================================================================

        [Test]
        public void Push_2x2_FreeDestination_MovesWholeRectangle()
        {
            // Player (0,0) pegado al ancla; espacio libre a la derecha.
            var big = SpawnBigEnemy(new GridCoord(1, 0), new Vector2Int(2, 2), 100);

            var outcome = _resolver.Resolve(_player, big, distance: 2, collisionDamage: 10);

            Assert.IsTrue(_grid.TryGetPosition(big, out var anchor));
            Assert.AreEqual(new GridCoord(3, 0), anchor, "el rect entero avanzó 2");
            Assert.IsTrue(_grid.IsOccupied(new GridCoord(4, 1)), "celda no-ancla ocupada en destino");
            Assert.IsFalse(_grid.IsOccupied(new GridCoord(1, 0)), "las celdas viejas quedaron libres");
            Assert.AreEqual(PushHopStop.Completed, outcome.Hops[0].Stop);
        }

        [Test]
        public void Push_2x2_PartiallyBlocked_StopsAsObstacle()
        {
            // El rect (1,0)-(2,1); un enemigo en (5,1) pisa UNA celda del rect destino con
            // ancla (4,0): bloqueo parcial = bloqueado tras avanzar 2.
            var big = SpawnBigEnemy(new GridCoord(1, 0), new Vector2Int(2, 2), 100);
            var blocker = SpawnEnemy(new GridCoord(5, 1), 100);

            var outcome = _resolver.Resolve(_player, big, distance: 4, collisionDamage: 10);

            Assert.IsTrue(_grid.TryGetPosition(big, out var anchor));
            Assert.AreEqual(new GridCoord(3, 0), anchor, "frenó una ancla antes del solape");
            Assert.AreEqual(blocker, outcome.Hops[0].BlockerGuid);
            Assert.AreEqual(PushHopStop.Enemy, outcome.Hops[0].Stop, "choque normal: daño a ambos");
        }

        [Test]
        public void Push_2x2_AgainstWall_StunsLikeAnyBlockedPush()
        {
            // Sala 20×5: el rect (1,3)-(2,4) ya toca el borde superior; empujar al Norte no
            // tiene ni una ancla válida → choque contra pared con stun.
            var big = SpawnBigEnemy(new GridCoord(1, 3), new Vector2Int(2, 2), 100);
            _grid.Unregister(_player);
            _grid.Register(_player, new GridCoord(1, 2)); // pegado por abajo, empuja al Norte

            var outcome = _resolver.Resolve(_player, big, distance: 2, collisionDamage: 10);

            Assert.IsTrue(_grid.TryGetPosition(big, out var anchor));
            Assert.AreEqual(new GridCoord(1, 3), anchor, "no se movió");
            Assert.AreEqual(PushHopStop.Wall, outcome.Hops[0].Stop);
        }

        [Test]
        public void Push_2x2_DirectionFromNearestCell_NotAnchor()
        {
            // Player (0,2) pegado a la celda (1,2) del rect (1,1)-(2,2): el delta al ANCLA
            // es diagonal (1,-1); la dirección tiene que salir de la celda más cercana → East.
            _grid.Unregister(_player);
            _grid.Register(_player, new GridCoord(0, 2));
            var big = SpawnBigEnemy(new GridCoord(1, 1), new Vector2Int(2, 2), 100);

            var outcome = _resolver.Resolve(_player, big, distance: 1, collisionDamage: 0);

            Assert.AreEqual(Cardinal.East, outcome.Direction);
            Assert.IsTrue(_grid.TryGetPosition(big, out var anchor));
            Assert.AreEqual(new GridCoord(2, 1), anchor, "empujado 1 al Este");
        }

        [Test]
        public void ForcedPush_2x2_ReportsTraveledAndBlocker()
        {
            // Push directo del servicio: (1,0)-(2,1) al Este con un 1×1 en (6,0); avanza 2
            // anclas y frena cuando el rect destino pisa al ocupante.
            var big = SpawnBigEnemy(new GridCoord(1, 0), new Vector2Int(2, 2), 100);
            var occupant = SpawnEnemy(new GridCoord(6, 0), 100);

            var move = _forced.Push(big, Cardinal.East, 5, _player);

            Assert.AreEqual(ForcedMoveStop.Obstacle, move.StoppedBy);
            Assert.AreEqual(new GridCoord(4, 0), move.FinalCoord);
            Assert.AreEqual(3, move.TilesTraveled);
            Assert.AreEqual(occupant, move.BlockerGuid);
        }

        private Guid SpawnEnemy(GridCoord coord, int hp)
        {
            var guid = Guid.NewGuid();
            _grid.Register(guid, coord);
            _traits.Register(guid, UnitTraits.DefaultGround);
            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(hp));
            attrs.SetAttribute<Shield>(new Shield(0));
            _attributes.Register(guid, attrs);
            return guid;
        }

        private Guid SpawnRoomObject(GridCoord coord, int hp)
        {
            var guid = SpawnEnemy(coord, hp);
            _roomObjects.Track(guid);
            return guid;
        }

        private Guid SpawnProp(GridCoord coord)
        {
            var guid = Guid.NewGuid();
            _grid.Register(guid, coord);
            return guid;
        }

        private SpecialTileDefinitionSO Spikes(int enterDamage)
        {
            var def = ScriptableObject.CreateInstance<SpecialTileDefinitionSO>();
            def.TileType = SpecialTileType.Spikes;
            def.Triggers = TileTrigger.OnEnter | TileTrigger.OnForcedMovementInto;
            def.Category = TileEffectCategory.Damage;
            def.Affinity = TileAffinity.GroundOnly;
            def.EnterDamage = enterDamage;
            _createdAssets.Add(def);
            return def;
        }

        private sealed class StubChestRegistry : IChestRegistry
        {
            private readonly HashSet<Guid> _chests = new HashSet<Guid>();
            public void MarkChest(Guid guid) => _chests.Add(guid);
            public bool IsChest(Guid guid) => _chests.Contains(guid);
            public bool TryGetActiveChest(out Guid chestGuid)
            {
                foreach (var g in _chests) { chestGuid = g; return true; }
                chestGuid = Guid.Empty;
                return false;
            }
        }

        /// <summary>Fake sin física de grilla: siempre "choca" y alterna el bloqueador entre
        /// dos entidades — regresión del guard de <c>visited</c> contra un rebote infinito.</summary>
        private sealed class AlternatingObstacleForcedMovement : IForcedMovementService
        {
            private readonly Guid _a;
            private readonly Guid _b;

            public AlternatingObstacleForcedMovement(Guid a, Guid b) { _a = a; _b = b; }

            public ForcedMoveResult Push(Guid entity, Cardinal direction, int tiles, Guid sourceId)
            {
                var blocker = entity == _a ? _b : _a;
                return new ForcedMoveResult(default, 0, ForcedMoveStop.Obstacle, false, default, blocker);
            }
        }

        private GridCoord PositionOf(Guid entity)
        {
            Assert.IsTrue(_grid.TryGetPosition(entity, out var pos));
            return pos;
        }

        // ======================================================================
        // Tests
        // ======================================================================

        [Test]
        public void Resolve_DistanceZero_NoOp()
        {
            var enemy = SpawnEnemy(new GridCoord(1, 0), 30);

            var outcome = _resolver.Resolve(_player, enemy, 0, 10);

            Assert.AreEqual(0, outcome.Hops.Count);
        }

        [Test]
        public void Resolve_OpenCorridor_TravelsFullDistance_NoDamageNoStun()
        {
            var enemy = SpawnEnemy(new GridCoord(1, 0), 30);

            var outcome = _resolver.Resolve(_player, enemy, 3, 10);

            Assert.AreEqual(1, outcome.Hops.Count);
            var hop = outcome.Hops[0];
            Assert.AreEqual(PushHopStop.Completed, hop.Stop);
            Assert.AreEqual(3, hop.Traveled);
            Assert.AreEqual(0, hop.DamageToPushed);
            Assert.IsFalse(hop.PushedStunned);
            Assert.AreEqual(new GridCoord(4, 0), PositionOf(enemy));
            Assert.AreEqual(30, _attributes.GetAttribute<Health>(enemy).Value);
        }

        [Test]
        public void Resolve_IntoWall_StunsAndNoHealthChange()
        {
            ReloadRoom(4, 1);
            var enemy = SpawnEnemy(new GridCoord(1, 0), 30);

            var outcome = _resolver.Resolve(_player, enemy, 5, 10);

            Assert.AreEqual(1, outcome.Hops.Count);
            var hop = outcome.Hops[0];
            Assert.AreEqual(PushHopStop.Wall, hop.Stop);
            Assert.IsTrue(hop.PushedStunned);
            Assert.IsTrue(_stun.IsStunned(enemy));
            Assert.AreEqual(1, _stun.GetStunTurns(enemy));
            Assert.AreEqual(30, _attributes.GetAttribute<Health>(enemy).Value);
        }

        [Test]
        public void Resolve_IntoUnregisteredProp_NonBreakableStunned_NoUnexpectedLogs()
        {
            var enemy = SpawnEnemy(new GridCoord(1, 0), 30);
            var prop = SpawnProp(new GridCoord(2, 0));

            var outcome = _resolver.Resolve(_player, enemy, 3, 10);

            Assert.AreEqual(1, outcome.Hops.Count);
            var hop = outcome.Hops[0];
            Assert.AreEqual(PushHopStop.NonBreakableProp, hop.Stop);
            Assert.AreEqual(prop, hop.BlockerGuid);
            Assert.IsTrue(hop.PushedStunned);
            // El resolver loguea el outcome (Log, no Warning): lo único que NO debe aparecer es el
            // ReportMissing del AttributesManager por consultar un guid sintético sin registrar.
            LogAssert.Expect(LogType.Log, new Regex(@"\[ClassSkillPushResolver\]"));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Resolve_IntoChest_NonBreakable_ChestHealthUnchanged()
        {
            var enemy = SpawnEnemy(new GridCoord(1, 0), 30);
            var chest = SpawnEnemy(new GridCoord(2, 0), 50); // cofres tienen Health (mimic)
            var chests = new StubChestRegistry();
            chests.MarkChest(chest);
            ServiceLocator.AddService<IChestRegistry>(chests, ServiceScope.Global);

            var outcome = _resolver.Resolve(_player, enemy, 3, 10);

            Assert.AreEqual(1, outcome.Hops.Count);
            var hop = outcome.Hops[0];
            Assert.AreEqual(PushHopStop.NonBreakableProp, hop.Stop);
            Assert.IsTrue(hop.PushedStunned);
            Assert.AreEqual(0, hop.DamageToBlocker);
            Assert.AreEqual(50, _attributes.GetAttribute<Health>(chest).Value);
        }

        [Test]
        public void Resolve_IntoRoomObject_Breaks_PushedTakesDamage_TileFreed()
        {
            var enemy = SpawnEnemy(new GridCoord(1, 0), 50);
            var roomObj = SpawnRoomObject(new GridCoord(2, 0), 30);

            var outcome = _resolver.Resolve(_player, enemy, 3, 10);

            Assert.AreEqual(1, outcome.Hops.Count);
            var hop = outcome.Hops[0];
            Assert.AreEqual(PushHopStop.BreakableObstacle, hop.Stop);
            Assert.AreEqual(10, hop.DamageToPushed);
            Assert.IsTrue(hop.BlockerBroken);
            Assert.IsTrue(hop.BlockerDied);
            Assert.AreEqual(new GridCoord(1, 0), PositionOf(enemy), "El empujado no avanza a la celda liberada.");
            Assert.AreEqual(0, _attributes.GetAttribute<Health>(roomObj).Value);
            Assert.IsFalse(_grid.IsOccupied(new GridCoord(2, 0)), "El obstáculo roto libera la celda.");
        }

        [Test]
        public void Resolve_IntoEnemy_ChainsBothDamaged_SecondPushed()
        {
            var e1 = SpawnEnemy(new GridCoord(1, 0), 50);
            var e2 = SpawnEnemy(new GridCoord(3, 0), 50);

            var outcome = _resolver.Resolve(_player, e1, 3, 10);

            Assert.AreEqual(2, outcome.Hops.Count);
            var hop0 = outcome.Hops[0];
            Assert.AreEqual(PushHopStop.Enemy, hop0.Stop);
            Assert.AreEqual(10, hop0.DamageToPushed);
            Assert.AreEqual(10, hop0.DamageToBlocker);
            Assert.AreEqual(e2, hop0.BlockerGuid);

            var hop1 = outcome.Hops[1];
            Assert.AreEqual(e2, hop1.Entity);
            Assert.AreEqual(PushHopStop.Completed, hop1.Stop);
            Assert.AreEqual(2, hop1.Traveled);
            Assert.AreEqual(new GridCoord(5, 0), PositionOf(e2));
            Assert.AreEqual(20, outcome.TotalDamage);
        }

        [Test]
        public void Resolve_ChainIntoWall_SecondStunnedFirstNot()
        {
            ReloadRoom(4, 1);
            var e1 = SpawnEnemy(new GridCoord(1, 0), 50);
            var e2 = SpawnEnemy(new GridCoord(2, 0), 50);

            var outcome = _resolver.Resolve(_player, e1, 2, 10);

            Assert.AreEqual(2, outcome.Hops.Count);
            Assert.IsFalse(outcome.Hops[0].PushedStunned, "El primero chocó contra un enemigo, no se aturde.");
            Assert.AreEqual(PushHopStop.Wall, outcome.Hops[1].Stop);
            Assert.IsTrue(outcome.Hops[1].PushedStunned);
            Assert.IsFalse(_stun.IsStunned(e1));
            Assert.IsTrue(_stun.IsStunned(e2));
        }

        [Test]
        public void Resolve_ChainBlockerDiesFromCollision_NoSecondHop()
        {
            var e1 = SpawnEnemy(new GridCoord(1, 0), 50);
            var e2 = SpawnEnemy(new GridCoord(2, 0), 5);

            var outcome = _resolver.Resolve(_player, e1, 3, 10);

            Assert.AreEqual(1, outcome.Hops.Count);
            var hop = outcome.Hops[0];
            Assert.AreEqual(PushHopStop.Enemy, hop.Stop);
            Assert.IsTrue(hop.BlockerDied);
            Assert.IsFalse(_grid.IsOccupied(new GridCoord(2, 0)));
        }

        [Test]
        public void Resolve_PushedDiesOnSpikesMidPush_NoStunNoCollisionDamage()
        {
            var enemy = SpawnEnemy(new GridCoord(1, 0), 5);
            _tiles.Place(Spikes(999), new[] { new GridCoord(2, 0) });

            var outcome = _resolver.Resolve(_player, enemy, 3, 10);

            Assert.AreEqual(1, outcome.Hops.Count);
            var hop = outcome.Hops[0];
            Assert.AreEqual(PushHopStop.Died, hop.Stop);
            Assert.IsTrue(hop.PushedDied);
            Assert.AreEqual(0, hop.DamageToPushed);
            Assert.IsFalse(hop.PushedStunned);
            Assert.IsFalse(_stun.IsStunned(enemy));
            Assert.IsFalse(_grid.TryGetPosition(enemy, out _));
        }

        [Test]
        public void Resolve_PushedDiesFromCollision_BlockerStillDamagedAndPushed()
        {
            var e1 = SpawnEnemy(new GridCoord(1, 0), 5);
            var e2 = SpawnEnemy(new GridCoord(3, 0), 50);

            var outcome = _resolver.Resolve(_player, e1, 3, 10);

            Assert.AreEqual(2, outcome.Hops.Count);
            var hop0 = outcome.Hops[0];
            Assert.AreEqual(PushHopStop.Enemy, hop0.Stop);
            Assert.IsTrue(hop0.PushedDied);
            Assert.IsFalse(hop0.BlockerDied);
            Assert.AreEqual(10, hop0.DamageToBlocker);

            var hop1 = outcome.Hops[1];
            Assert.AreEqual(e2, hop1.Entity);
            Assert.AreEqual(new GridCoord(5, 0), PositionOf(e2));
        }

        [Test]
        public void Resolve_SelfLoopGuard_TerminatesInsteadOfInfiniteLoop()
        {
            var a = SpawnEnemy(new GridCoord(1, 0), 50);
            var b = SpawnEnemy(new GridCoord(2, 0), 50);
            ServiceLocator.AddService<IForcedMovementService>(
                new AlternatingObstacleForcedMovement(a, b), ServiceScope.Global);

            var outcome = _resolver.Resolve(_player, a, 5, 5);

            Assert.LessOrEqual(outcome.Hops.Count, ClassSkillPushResolver.MaxChainDepth);
            Assert.AreEqual(2, outcome.Hops.Count,
                "El visited-set corta el rebote A-B-A antes de un tercer hop.");
            Assert.AreEqual(a, outcome.Hops[0].Entity);
            Assert.AreEqual(b, outcome.Hops[0].BlockerGuid);
            Assert.AreEqual(b, outcome.Hops[1].Entity);
            Assert.AreEqual(a, outcome.Hops[1].BlockerGuid);
        }
    }
}
