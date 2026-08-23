using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.AI.Bosses.Generala;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Status;
using Rollgeon.Combat.Threat;
using Rollgeon.Dice;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.Movement;
using Rollgeon.Player;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rollgeon.Combat.Tests
{
    [TestFixture]
    public class GeneralaFrostRingTests
    {
        /// <summary>La mesa, con espacio de sobra para que el anillo entre entero.</summary>
        private static readonly GridCoord TableTile = new GridCoord(5, 5);

        /// <summary>Chebyshev 1: pegado a ella, donde vive su mesa de dados.</summary>
        private static readonly GridCoord GluedTile = new GridCoord(6, 5);

        /// <summary>Chebyshev 2: el borde del anillo, sobre el eje.</summary>
        private static readonly GridCoord RingTile = new GridCoord(7, 5);

        /// <summary>Chebyshev 3: afuera del anillo.</summary>
        private static readonly GridCoord OutsideTile = new GridCoord(8, 5);

        private GridManager _grid;
        private StubMovementService _movement;
        private SpyDamagePipeline _pipeline;
        private HazardService _hazard;
        private StunService _stun;
        private IceStunBinder _binder;
        private HazardDefinitionSO _frost;

        private Guid _playerGuid;
        private Guid _bossGuid;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(11, 11));
            ServiceLocator.AddService<IGridManager>(_grid);

            _playerGuid = Guid.NewGuid();
            _bossGuid = Guid.NewGuid();
            _grid.Register(_bossGuid, TableTile);
            _grid.Register(_playerGuid, OutsideTile);

            ServiceLocator.AddService<IPlayerService>(new StubPlayerService { PlayerGuid = _playerGuid });

            _pipeline = new SpyDamagePipeline();
            ServiceLocator.AddService<IDamagePipeline>(_pipeline);

            // Antes de los servicios: la suscripción a OnEntityMoved se resuelve al registrarlos.
            _movement = new StubMovementService();
            ServiceLocator.AddService<IMovementService>(_movement);

            _hazard = new HazardService();
            _hazard.Register();

            _stun = new StunService();
            _stun.Register();

            _binder = new IceStunBinder();
            _binder.Register();

            _frost = ScriptableObject.CreateInstance<HazardDefinitionSO>();
            _frost.hideFlags = HideFlags.HideAndDontSave;
            _frost.Trigger = HazardTriggerMode.OnEnter;
            _frost.Damage = 0;
            _frost.Kind = AttackKind.Environmental;
            _frost.ConsumeOnTrigger = true;
            _frost.DurationRounds = 2;
            _frost.SourceId = Guid.NewGuid().ToString();
        }

        [TearDown]
        public void TearDown()
        {
            _binder?.Dispose();
            _stun?.Dispose();
            _hazard?.Dispose();

            if (_frost != null) UnityEngine.Object.DestroyImmediate(_frost);
            _frost = null;

            // Publicar pinta overlay: GameObject + un material por tint.
            if (ServiceLocator.TryGetService<IThreatOverlayService>(out var overlay)
                && overlay is IDisposable disposable)
            {
                disposable.Dispose();
            }
            var leftover = GameObject.Find("ThreatTelegraphOverlay");
            if (leftover != null) UnityEngine.Object.DestroyImmediate(leftover);

            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        [Test]
        public void ComputeArea_ReturnsTheWholeSquare_UpToTheRadius()
        {
            var area = AINode_GeneralaFrostRing.ComputeArea(_grid, TableTile, 2);

            Assert.AreEqual(25, area.Count);
            foreach (var coord in area)
                Assert.LessOrEqual(coord.Chebyshev(TableTile), 2, $"{coord} se fue del cuadrado.");

            CollectionAssert.Contains(area, TableTile, "El centro entra: ella no se congela por dueña.");
            CollectionAssert.Contains(area, GluedTile, "Y la mesa pegada a ella también.");
            CollectionAssert.Contains(area, RingTile);
            CollectionAssert.DoesNotContain(area, OutsideTile, "Chebyshev 3 queda afuera.");
        }

        [Test]
        public void ComputeRing_ReturnsOnlyTilesAtExactlyTheChebyshevRadius()
        {
            var ring = AINode_GeneralaFrostRing.ComputeRing(_grid, TableTile, 2);

            // El perímetro del 5×5, sin su interior.
            Assert.AreEqual(16, ring.Count);
            foreach (var coord in ring)
                Assert.AreEqual(2, coord.Chebyshev(TableTile),
                    $"{coord} no está en el borde del anillo.");

            CollectionAssert.DoesNotContain(ring, TableTile, "Ella no se para sobre su propio hielo.");
            CollectionAssert.DoesNotContain(ring, GluedTile,
                "Las casillas pegadas a ella quedan libres: son desde donde se le rompen los dados.");
            CollectionAssert.Contains(ring, RingTile);
            CollectionAssert.Contains(ring, new GridCoord(3, 3), "La esquina también es Chebyshev 2.");
        }

        [Test]
        public void ComputeRing_AgainstAWall_KeepsOnlyTheHalfThatFitsInTheRoom()
        {
            var corner = new GridCoord(0, 0);

            var ring = AINode_GeneralaFrostRing.ComputeRing(_grid, corner, 2);

            Assert.AreEqual(5, ring.Count,
                "Desde la esquina sólo entran las 5 casillas del cuadrante válido.");
            foreach (var coord in ring)
            {
                Assert.IsTrue(_grid.InBounds(coord), $"{coord} está fuera de la sala.");
                Assert.AreEqual(2, coord.Chebyshev(corner));
            }
        }

        [Test]
        public void Tick_FreezesTheWholeTable_CenterIncluded()
        {
            var result = NewFrostNode().Tick(BossContext());

            // El 5×5 entero, no las 16 del borde.
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.IsTrue(_hazard.TryGetHazardAt(RingTile, out var info));
            Assert.AreEqual(25, info.Tiles.Count);
            Assert.IsTrue(_hazard.TryGetHazardAt(GluedTile, out _),
                "La casilla pegada a ella es donde vive su mesa, y ahora también se congela.");
            Assert.IsTrue(_hazard.TryGetHazardAt(TableTile, out _),
                "Su propia casilla incluida — no se congela por ser la dueña, no por estar afuera.");
        }

        [Test]
        public void Tick_WithSolidOff_StillFreezesOnlyTheBorder()
        {
            // Odin no corre los inicializadores, así que un asset viejo trae Solid = false.
            var node = NewFrostNode();
            node.Solid = false;

            node.Tick(BossContext());

            Assert.IsTrue(_hazard.TryGetHazardAt(RingTile, out var info));
            Assert.AreEqual(16, info.Tiles.Count);
            Assert.IsFalse(_hazard.TryGetHazardAt(TableTile, out _));
        }

        [Test]
        public void PlayerAlreadyInsideWhenItFalls_IsNotFrozen_SoTheFreeRoundBuysHimTheTable()
        {
            _grid.Register(_playerGuid, GluedTile);

            NewFrostNode().Tick(BossContext());

            // OnEnter se dispara al pisar, no por estar.
            Assert.AreEqual(0, _stun.GetStunTurns(_playerGuid),
                "Publicar el área no stunea a quien ya estaba adentro.");
            Assert.IsTrue(_hazard.TryGetHazardAt(GluedTile, out _),
                "La casilla queda helada igual: la paga cuando salga y vuelva a entrar.");
        }

        [Test]
        public void Tick_WithoutAHazardDefinition_Fails_InsteadOfFreezingNothingSilently()
        {
            // El Failed lo absorbe el Selector[nodo, Wait]; lo que importa es el LogError.
            var node = NewFrostNode();
            node.Hazard = null;

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("FrostRing"));
            Assert.AreEqual(AIResult.Failed, node.Tick(BossContext()));
        }

        [Test]
        public void SecondCast_ReplacesThePreviousRing_InsteadOfStacking()
        {
            var node = NewFrostNode();
            node.Tick(BossContext());

            _grid.Register(_bossGuid, new GridCoord(5, 8));
            node.Tick(BossContext());

            Assert.AreEqual(1, _hazard.ActiveInstances().Count(), "Un solo anillo vivo por vez.");
            Assert.IsTrue(_hazard.TryGetHazardAt(new GridCoord(7, 8), out _), "El anillo nuevo está helado.");
            Assert.IsFalse(_hazard.TryGetHazardAt(RingTile, out _), "El anterior se apagó.");
        }

        [Test]
        public void PlayerCrossingTheRing_LosesOneTurn_AndPaysNoHp()
        {
            NewFrostNode().Tick(BossContext());

            _movement.RaiseMoved(_playerGuid, OutsideTile, GluedTile,
                Path(OutsideTile, RingTile, GluedTile));

            Assert.IsTrue(_stun.IsStunned(_playerGuid), "Cruzar el anillo tiene que costar el turno.");
            Assert.AreEqual(1, _stun.GetStunTurns(_playerGuid));
            CollectionAssert.IsEmpty(_pipeline.Resolved, "La escarcha no cobra HP.");
            Assert.IsFalse(_hazard.TryGetHazardAt(RingTile, out _),
                "La casilla pisada se derrite — es lo que impide encadenar stuns.");
        }

        [Test]
        public void PlayerStandingOnTheRingWhenItForms_IsNotStunned()
        {
            _grid.Register(_playerGuid, RingTile);

            NewFrostNode().Tick(BossContext());

            Assert.IsFalse(_stun.IsStunned(_playerGuid));
        }

        [Test]
        public void SheDoesNotFreezeHerself_WhenRepositioningThroughHerOwnRing()
        {
            // El reposicionamiento corre DESPUÉS de la escarcha en su turno.
            NewFrostNode().Tick(BossContext());

            _movement.RaiseMoved(_bossGuid, TableTile, new GridCoord(8, 5),
                Path(TableTile, GluedTile, RingTile, OutsideTile));

            Assert.IsFalse(_stun.IsStunned(_bossGuid),
                "Auto-stunearse le regalaría al jugador un turno gratis y se leería como bug.");
        }

        private AINode_GeneralaFrostRing NewFrostNode() => new AINode_GeneralaFrostRing
        {
            Hazard = _frost,
            Radius = 2,
            StunTurns = 1,
            ReplacePreviousRing = true,
        };

        private AIContext BossContext() => new AIContext
        {
            SelfGuid = _bossGuid,
            PlayerGuid = _playerGuid,
            Grid = _grid,
            DamagePipeline = _pipeline,
        };

        private static IReadOnlyList<GridCoord> Path(params GridCoord[] coords) => coords;

        private sealed class StubMovementService : IMovementService
        {
            public List<GridCoord> GetReachableTiles(GridCoord origin, int range, bool includeOrigin = false)
                => new List<GridCoord>();

            public List<GridCoord> FindPath(GridCoord from, GridCoord to) => new List<GridCoord>();

            public bool Move(Guid entity, GridCoord destination) => false;

            public event Action<Guid, GridCoord, GridCoord, IReadOnlyList<GridCoord>> OnEntityMoved;

            public void RaiseMoved(Guid entity, GridCoord from, GridCoord to, IReadOnlyList<GridCoord> path)
                => OnEntityMoved?.Invoke(entity, from, to, path);
        }

        private sealed class StubPlayerService : IPlayerService
        {
            public Guid PlayerGuid { get; set; } = Guid.NewGuid();
            public Guid RunId { get; set; } = Guid.NewGuid();
            public ClassHeroSO CurrentHero { get; set; }
            public DiceBagSO DiceBag { get; set; }
            public void SetPlayer(ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(DiceBagSO bag) { DiceBag = bag; }
            public void ClearPlayer() { }
            public event Action<ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;
        }

        private sealed class SpyDamagePipeline : IDamagePipeline
        {
            public readonly List<DamageContext> Resolved = new List<DamageContext>();

            public DamageContext Resolve(DamageContext ctx)
            {
                Resolved.Add(ctx);
                ctx.FinalDamage = ctx.BaseDamage;
                return ctx;
            }

            public DamageContext Preview(DamageContext ctx) { ctx.FinalDamage = ctx.BaseDamage; return ctx; }
        }
    }
}
