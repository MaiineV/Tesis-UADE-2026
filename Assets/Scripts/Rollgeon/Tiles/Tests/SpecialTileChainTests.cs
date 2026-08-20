using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Status;
using Rollgeon.Entities.Traits;
using Rollgeon.Grid;
using Rollgeon.Movement;
using Rollgeon.Tiles.Forced;
using UnityEngine;

namespace Rollgeon.Tiles.Tests
{
    /// <summary>
    /// Tests del motor de cadenas: empuje (OnForcedMovementInto), deslizamiento de Hielo,
    /// Portal (teleport + reubicación + remanente) y el orden de resolución del GDD §12
    /// (Movimiento → Trigger → Daño → Estado → Mov. forzado adicional → Muerte).
    /// </summary>
    [TestFixture]
    public class SpecialTileChainTests
    {
        private GridManager _grid;
        private MovementService _movement;
        private UnitTraitService _traits;
        private SpecialTileService _svc;
        private ForcedMovementService _forced;
        private StunService _stun;
        private SpyDamagePipeline _damage;
        private Guid _player;
        private readonly List<ScriptableObject> _createdAssets = new List<ScriptableObject>();
        private List<Guid> _triggeredInstances;
        private EventManager.EventReceiver _onTriggered;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(10, 10));
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

            _stun = new StunService();
            _stun.ConfigureForTests(() => _player);
            ServiceLocator.AddService<IStunService>(_stun, ServiceScope.Global);

            _svc = new SpecialTileService();
            _svc.ConfigureForTests(() => _player);
            ServiceLocator.AddService<SpecialTileService>(_svc, ServiceScope.Global);

            _forced = new ForcedMovementService();
            ServiceLocator.AddService<IForcedMovementService>(_forced, ServiceScope.Global);

            _triggeredInstances = new List<Guid>();
            _onTriggered = args => _triggeredInstances.Add((Guid)args[0]);
            EventManager.Subscribe(EventName.OnSpecialTileTriggered, _onTriggered);
        }

        [TearDown]
        public void TearDown()
        {
            _svc?.Dispose();
            _stun?.Dispose();
            _svc = null;

            foreach (var asset in _createdAssets)
                if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
            _createdAssets.Clear();

            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        // ======================================================================
        // Helpers de definición
        // ======================================================================

        private SpecialTileDefinitionSO MakeDef(Action<SpecialTileDefinitionSO> configure)
        {
            var def = ScriptableObject.CreateInstance<SpecialTileDefinitionSO>();
            configure(def);
            _createdAssets.Add(def);
            return def;
        }

        private SpecialTileDefinitionSO Spikes() => MakeDef(d =>
        {
            d.TileType = SpecialTileType.Spikes;
            d.Triggers = TileTrigger.OnEnter | TileTrigger.OnForcedMovementInto;
            d.Category = TileEffectCategory.Damage;
            d.Affinity = TileAffinity.GroundOnly;
            d.EnterDamage = 12;
            d.DisarmOnTrigger = true;
            d.RearmOnRoundWrap = true;
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

        private SpecialTileDefinitionSO Ice() => MakeDef(d =>
        {
            d.TileType = SpecialTileType.Ice;
            d.Triggers = TileTrigger.OnEnter | TileTrigger.OnPassThrough;
            d.Category = TileEffectCategory.ForcedSlide;
            d.Affinity = TileAffinity.GroundOnly;
        });

        private SpecialTileDefinitionSO Puddle() => MakeDef(d =>
        {
            d.TileType = SpecialTileType.ElectricPuddle;
            d.Triggers = TileTrigger.OnEnter | TileTrigger.OnForcedMovementInto;
            d.Category = TileEffectCategory.ApplyStatus;
            d.Affinity = TileAffinity.All;
            d.StatusKind = TileStatusKind.Stun;
            d.StatusTurns = 1;
        });

        private SpecialTileDefinitionSO Portal() => MakeDef(d =>
        {
            d.TileType = SpecialTileType.Portal;
            d.Triggers = TileTrigger.OnEnter | TileTrigger.OnForcedMovementInto;
            d.Category = TileEffectCategory.Teleport;
            d.Affinity = TileAffinity.All;
        });

        private (Guid a, Guid b) PlacePortalPair(GridCoord a, GridCoord b)
        {
            var idA = _svc.Place(Portal(), new[] { a });
            var idB = _svc.Place(Portal(), new[] { b }, new TilePlacementOptions { LinkTo = idA });
            return (idA, idB);
        }

        private GridCoord PositionOf(Guid entity)
        {
            Assert.IsTrue(_grid.TryGetPosition(entity, out var pos), "La entidad tiene que seguir en el grid.");
            return pos;
        }

        // ======================================================================
        // Empuje — OnForcedMovementInto
        // ======================================================================

        [Test]
        public void Push_OntoSpikes_Deals12Damage()
        {
            var id = _svc.Place(Spikes(), new[] { new GridCoord(2, 0) });

            var result = _forced.Push(_player, Cardinal.East, 2, Guid.NewGuid());

            Assert.AreEqual(new GridCoord(2, 0), result.FinalCoord);
            Assert.AreEqual(1, _damage.Resolved.Count);
            Assert.AreEqual(12, _damage.Resolved[0].BaseDamage);
            Assert.AreEqual(id, _damage.Resolved[0].SourceId);
        }

        [Test]
        public void Push_StopsBeforeObstacle()
        {
            _grid.Register(Guid.NewGuid(), new GridCoord(3, 0));

            var result = _forced.Push(_player, Cardinal.East, 5, Guid.NewGuid());

            Assert.AreEqual(new GridCoord(2, 0), result.FinalCoord);
            Assert.AreEqual(2, result.TilesTraveled);
            Assert.AreEqual(ForcedMoveStop.Obstacle, result.StoppedBy);
        }

        // ======================================================================
        // Cadena canónica del GDD §12
        // ======================================================================

        [Test]
        public void Push_CrossingIceFirePuddle_ResolvesInDocumentedOrder()
        {
            var iceId = _svc.Place(Ice(), new[] { new GridCoord(2, 0) });
            var fireId = _svc.Place(Fire(), new[] { new GridCoord(3, 0) });
            var puddleId = _svc.Place(Puddle(), new[] { new GridCoord(4, 0) });

            var result = _forced.Push(_player, Cardinal.East, 5, Guid.NewGuid());

            Assert.AreEqual(new GridCoord(5, 0), result.FinalCoord);
            CollectionAssert.AreEqual(new[] { iceId, fireId, puddleId }, _triggeredInstances,
                "Hielo → Fuego → Charco, exactamente en el orden en que se cruzan.");
            Assert.AreEqual(1, _damage.Resolved.Count, "El daño de Fuego dispara dentro del empuje.");
            Assert.AreEqual(8, _damage.Resolved[0].BaseDamage);
            Assert.IsTrue(_stun.IsStunned(_player), "Termina aturdido por el Charco.");
        }

        [Test]
        public void Push_EndingOnIce_SlidesUntilLeavingIce()
        {
            _svc.Place(Ice(), new[] { new GridCoord(2, 0), new GridCoord(3, 0) });
            _svc.Place(Fire(), new[] { new GridCoord(4, 0) });

            var result = _forced.Push(_player, Cardinal.East, 2, Guid.NewGuid());

            Assert.AreEqual(new GridCoord(4, 0), result.FinalCoord,
                "El empuje termina sobre hielo: desliza hasta la primera celda no-hielo.");
            Assert.AreEqual(1, _damage.Resolved.Count, "La celda donde frena (Fuego) dispara su OnEnter.");
            Assert.AreEqual(8, _damage.Resolved[0].BaseDamage);
        }

        [Test]
        public void Slide_StopsAgainstObstacle()
        {
            _svc.Place(Ice(), new[] { new GridCoord(2, 0), new GridCoord(3, 0) });
            _grid.Register(Guid.NewGuid(), new GridCoord(4, 0));

            var result = _forced.Push(_player, Cardinal.East, 2, Guid.NewGuid());

            Assert.AreEqual(new GridCoord(3, 0), result.FinalCoord,
                "Desliza de (2,0) a (3,0) y el obstáculo en (4,0) lo clava en el hielo.");
            Assert.AreEqual(ForcedMoveStop.Obstacle, result.StoppedBy);
        }

        [Test]
        public void Move_OntoIce_SlidesInEntryDirection()
        {
            _svc.Place(Ice(), new[] { new GridCoord(2, 0) });

            Assert.IsTrue(_movement.Move(_player, new GridCoord(2, 0)));

            Assert.AreEqual(new GridCoord(3, 0), PositionOf(_player),
                "Entrar voluntariamente al hielo desliza una celda más en la dirección de entrada.");
        }

        [Test]
        public void Move_PathThroughIce_TruncatesAndSlides()
        {
            _svc.Place(Ice(), new[] { new GridCoord(2, 0) });

            // El player clickea (2,2): el A* pasa por el hielo — el filtro corta ahí y desliza.
            Assert.IsTrue(_movement.Move(_player, new GridCoord(4, 0)));

            Assert.AreEqual(new GridCoord(3, 0), PositionOf(_player),
                "El path se trunca en el hielo y la física del deslizamiento decide el final.");
        }

        [Test]
        public void FlyingUnit_PushedOverIce_DoesNotSlide()
        {
            var flyer = Guid.NewGuid();
            _grid.Register(flyer, new GridCoord(0, 2));
            _traits.Register(flyer, new UnitTraits(isFlying: true, isBoss: false));
            _svc.Place(Ice(), new[] { new GridCoord(2, 2) });

            var result = _forced.Push(flyer, Cardinal.East, 2, Guid.NewGuid());

            Assert.AreEqual(new GridCoord(2, 2), result.FinalCoord,
                "El hielo es 'solo terrestres': una voladora queda donde el empuje la dejó.");
        }

        // ======================================================================
        // Portal
        // ======================================================================

        [Test]
        public void Move_IntoPortal_TeleportsAndRelocatesInEntryDirection()
        {
            var (idA, idB) = PlacePortalPair(new GridCoord(2, 0), new GridCoord(5, 5));

            Assert.IsTrue(_movement.Move(_player, new GridCoord(2, 0)));

            Assert.AreEqual(new GridCoord(6, 5), PositionOf(_player),
                "Entró hacia el Este: aparece en el otro portal y se corre una celda al Este.");
            CollectionAssert.Contains(_triggeredInstances, idA, "El portal de entrada disparó.");
            CollectionAssert.DoesNotContain(_triggeredInstances, idB,
                "El teleport NO dispara OnEnter en el portal de destino.");
        }

        [Test]
        public void Move_IntoPortal_RelocationBlocked_ScansClockwise()
        {
            PlacePortalPair(new GridCoord(2, 0), new GridCoord(5, 5));
            _grid.Register(Guid.NewGuid(), new GridCoord(6, 5)); // Este bloqueado

            Assert.IsTrue(_movement.Move(_player, new GridCoord(2, 0)));

            Assert.AreEqual(new GridCoord(5, 4), PositionOf(_player),
                "Este bloqueado → horario desde la dirección de entrada: el Sur es la primera libre.");
        }

        [Test]
        public void Portal_AdjacentSpecialTile_TriggersOnRelocation()
        {
            PlacePortalPair(new GridCoord(2, 0), new GridCoord(5, 5));
            _svc.Place(Fire(), new[] { new GridCoord(6, 5) });

            Assert.IsTrue(_movement.Move(_player, new GridCoord(2, 0)));

            Assert.AreEqual(1, _damage.Resolved.Count,
                "La celda de reubicación sí dispara OnEnter (especiales rodeando al portal).");
            Assert.AreEqual(8, _damage.Resolved[0].BaseDamage);
        }

        [Test]
        public void Push5_PortalAtOneTile_TravelsOnePlusFourFromExit()
        {
            PlacePortalPair(new GridCoord(1, 0), new GridCoord(4, 4));

            var result = _forced.Push(_player, Cardinal.East, 5, Guid.NewGuid());

            Assert.AreEqual(new GridCoord(8, 4), result.FinalCoord,
                "Empuje de 5 con portal a 1: recorre 1 hasta el portal + 4 desde el otro lado.");
            Assert.AreEqual(5, result.TilesTraveled);
            Assert.AreEqual(ForcedMoveStop.CompletedDistance, result.StoppedBy);
        }

        [Test]
        public void Push_ThroughPortal_ChainsOntoTilesBeyondExit()
        {
            PlacePortalPair(new GridCoord(1, 0), new GridCoord(4, 4));
            _svc.Place(Puddle(), new[] { new GridCoord(6, 4) });

            _forced.Push(_player, Cardinal.East, 4, Guid.NewGuid());

            Assert.IsTrue(_stun.IsStunned(_player),
                "El remanente del empuje cruza el charco del otro lado del portal.");
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
