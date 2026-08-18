using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Status;
using Rollgeon.Combat.Threat;
using Rollgeon.Dice;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.Movement;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Combat.Tests
{
    /// <summary>
    /// Estela helada de El Anotador: <see cref="AINode_IceTrail"/> + <see cref="IceStunBinder"/>
    /// sobre el <see cref="HazardService"/> real, con el harness de <see cref="HazardServiceTests"/>.
    /// </summary>
    [TestFixture]
    public class AnotadorIceTrailTests
    {
        private GridManager _grid;
        private TurnOrderService _turnOrder;
        private StubMovementService _movement;
        private SpyDamagePipeline _pipeline;
        private HazardService _hazard;
        private StunService _stun;
        private IceStunBinder _binder;
        private HazardDefinitionSO _ice;

        private Guid _playerGuid;
        private Guid _bossGuid;

        // Fila y=4. El boss se repliega de (8,4) a (5,4): la estela son las 3 casillas que pisó.
        private static readonly GridCoord BossStart = new GridCoord(8, 4);
        private static readonly GridCoord BossEnd = new GridCoord(5, 4);
        private static readonly GridCoord Trail1 = new GridCoord(7, 4);
        private static readonly GridCoord Trail2 = new GridCoord(6, 4);
        private static readonly GridCoord Trail3 = new GridCoord(5, 4);
        private static readonly GridCoord PlayerStart = new GridCoord(4, 4);

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(9, 9));
            ServiceLocator.AddService<IGridManager>(_grid);

            _playerGuid = Guid.NewGuid();
            _bossGuid = Guid.NewGuid();
            _grid.Register(_playerGuid, PlayerStart);
            _grid.Register(_bossGuid, BossStart);

            ServiceLocator.AddService<IPlayerService>(new StubPlayerService { PlayerGuid = _playerGuid });

            _pipeline = new SpyDamagePipeline();
            ServiceLocator.AddService<IDamagePipeline>(_pipeline);

            // Antes de los servicios: la suscripción a OnEntityMoved se resuelve al registrarlos.
            _movement = new StubMovementService();
            ServiceLocator.AddService<IMovementService>(_movement);

            _turnOrder = new TurnOrderService();

            _hazard = new HazardService();
            _hazard.Register();

            _stun = new StunService();
            _stun.Register();

            _binder = new IceStunBinder();
            _binder.Register();

            _ice = CreateIceDefinition();
        }

        [TearDown]
        public void TearDown()
        {
            _binder?.Dispose();
            _stun?.Dispose();
            _hazard?.Dispose();

            if (_ice != null) UnityEngine.Object.DestroyImmediate(_ice);
            _ice = null;

            // Activar pinta overlay: GameObject + un material por tint. Ver HazardServiceTests.
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

        // ======================================================================
        // La estela sale del repliegue
        // ======================================================================

        [Test]
        public void Tick_AfterARetreat_FreezesExactlyTheTilesHeWalked()
        {
            // Arrange
            RetreatAlongRow();

            // Act
            var result = NewTrailNode().Tick(BossContext());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.IsTrue(_hazard.TryGetHazardAt(Trail1, out var info), "La primera casilla pisada debería estar helada.");
            Assert.IsTrue(_hazard.TryGetHazardAt(Trail2, out _));
            Assert.IsTrue(_hazard.TryGetHazardAt(Trail3, out _));
            Assert.AreEqual(3, info.Tiles.Count, "La estela son 3 casillas: 1 por paso del repliegue.");
            Assert.IsFalse(_hazard.TryGetHazardAt(BossStart, out _),
                "El origen del movimiento no se pisa — no debería congelarse.");
        }

        [Test]
        public void Tick_PathLongerThanMaxTiles_KeepsTheTilesClosestToHim()
        {
            // Arrange — 5 pasos con tope 3.
            _movement.RaiseMoved(_bossGuid, new GridCoord(8, 0), new GridCoord(8, 5), Path(
                new GridCoord(8, 0), new GridCoord(8, 1), new GridCoord(8, 2),
                new GridCoord(8, 3), new GridCoord(8, 4), new GridCoord(8, 5)));

            // Act
            NewTrailNode(maxTiles: 3).Tick(BossContext());

            // Assert
            Assert.IsTrue(_hazard.TryGetHazardAt(new GridCoord(8, 5), out var info));
            Assert.AreEqual(3, info.Tiles.Count, "MaxTiles debería recortar la estela a 3 casillas.");
            Assert.IsTrue(_hazard.TryGetHazardAt(new GridCoord(8, 4), out _));
            Assert.IsTrue(_hazard.TryGetHazardAt(new GridCoord(8, 3), out _));
            Assert.IsFalse(_hazard.TryGetHazardAt(new GridCoord(8, 1), out _),
                "Las casillas más viejas del recorrido quedan afuera del recorte.");
        }

        /// <summary>Un <see cref="AIResult.Failed"/> acá cortaría el Sequence y la marca de fila.</summary>
        [Test]
        public void Tick_WithoutRetreat_SucceedsAndFreezesNothing()
        {
            // Act
            var result = NewTrailNode().Tick(BossContext());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result,
                "Sin repliegue el nodo tiene que ser un no-op transparente, no un Failed.");
            CollectionAssert.IsEmpty(_hazard.ActiveInstances());
        }

        [Test]
        public void Tick_TwiceWithoutMovingAgain_DoesNotRefreezeTheOldPath()
        {
            // Arrange — el path se consume al leerlo.
            RetreatAlongRow();
            var node = NewTrailNode();
            node.Tick(BossContext());
            int afterFirst = CountInstances();

            // Act
            node.Tick(BossContext());

            // Assert
            Assert.AreEqual(afterFirst, CountInstances(), "No debería nacer una segunda estela sin un segundo repliegue.");
        }

        [Test]
        public void SecondRetreat_ReplacesThePreviousTrail()
        {
            // Arrange
            RetreatAlongRow();
            var node = NewTrailNode();
            node.Tick(BossContext());

            // Act — se repliega por otra fila.
            _movement.RaiseMoved(_bossGuid, BossEnd, new GridCoord(5, 1), Path(
                new GridCoord(5, 4), new GridCoord(5, 3), new GridCoord(5, 2), new GridCoord(5, 1)));
            node.Tick(BossContext());

            // Assert
            Assert.AreEqual(1, CountInstances(), "Una sola estela viva por vez.");
            Assert.IsTrue(_hazard.TryGetHazardAt(new GridCoord(5, 2), out _), "La estela nueva debería estar helada.");
            Assert.IsFalse(_hazard.TryGetHazardAt(Trail1, out _), "La estela del turno anterior debería haberse ido.");
        }

        // ======================================================================
        // Pisarla: stun 1 y se derrite
        // ======================================================================

        [Test]
        public void PlayerStepsOnTheTrail_IsStunnedOneTurn_AndTheTileMelts()
        {
            // Arrange
            ArmTrail();

            // Act
            _movement.RaiseMoved(_playerGuid, PlayerStart, Trail3, Path(PlayerStart, Trail3));

            // Assert
            Assert.IsTrue(_stun.IsStunned(_playerGuid), "Pisar la estela debería stunear al jugador.");
            Assert.AreEqual(1, _stun.GetStunTurns(_playerGuid), "El stun de la estela es de 1 turno.");
            CollectionAssert.IsEmpty(_pipeline.Resolved, "La estela cobra en turnos, no en HP (Damage = 0).");
            Assert.IsFalse(_hazard.TryGetHazardAt(Trail3, out _),
                "La casilla pisada se derrite — es lo que impide encadenar stuns.");
            Assert.IsTrue(_hazard.TryGetHazardAt(Trail2, out _), "El resto de la estela sigue helado.");
        }

        [Test]
        public void CrossingTwoTrailTilesInOneMove_DoesNotChainStuns()
        {
            // Arrange
            ArmTrail();

            // Act — un movimiento que cruza dos casillas heladas.
            _movement.RaiseMoved(_playerGuid, PlayerStart, Trail2, Path(PlayerStart, Trail3, Trail2));

            // Assert
            Assert.AreEqual(1, _stun.GetStunTurns(_playerGuid),
                "ApplyStun toma max(actual, nuevo): dos pisadas siguen siendo 1 turno perdido.");
            Assert.IsFalse(_hazard.TryGetHazardAt(Trail3, out _));
            Assert.IsFalse(_hazard.TryGetHazardAt(Trail2, out _), "Las dos casillas cruzadas se derriten.");
        }

        [Test]
        public void SteppingOnAMeltedTile_DoesNotStunAgain()
        {
            // Arrange
            ArmTrail();
            _movement.RaiseMoved(_playerGuid, PlayerStart, Trail3, Path(PlayerStart, Trail3));
            _stun.ConsumeTurn(_playerGuid); // el jugador pierde el turno y sale del stun.
            Assert.IsFalse(_stun.IsStunned(_playerGuid));

            // Act — vuelve a pisar la misma casilla, ya derretida.
            _movement.RaiseMoved(_playerGuid, PlayerStart, Trail3, Path(PlayerStart, Trail3));

            // Assert
            Assert.IsFalse(_stun.IsStunned(_playerGuid),
                "Una casilla derretida no vuelve a cobrar: sin eso el jugador pierde dos turnos seguidos.");
        }

        [Test]
        public void TheBoss_WalkingOverItsOwnTrail_IsNotStunned()
        {
            // Arrange
            ArmTrail();

            // Act — el repliegue del turno siguiente cruza su propia estela.
            _movement.RaiseMoved(_bossGuid, BossEnd, Trail1, Path(Trail3, Trail2, Trail1));

            // Assert
            Assert.IsFalse(_stun.IsStunned(_bossGuid),
                "El dueño de la estela no se congela a sí mismo: auto-stunearse le regalaría al " +
                "jugador un turno gratis y se leería como bug.");
        }

        [Test]
        public void ForeignOnEnterHazard_DoesNotStun()
        {
            // Arrange — otra instancia OnEnter que NO es del Anotador.
            ArmTrail();
            var foreign = CreateIceDefinition();
            foreign.Damage = 4;
            try
            {
                _hazard.Activate(foreign, new[] { new GridCoord(1, 1) });

                // Act
                _movement.RaiseMoved(_playerGuid, new GridCoord(1, 0), new GridCoord(1, 1), Path(
                    new GridCoord(1, 0), new GridCoord(1, 1)));

                // Assert
                Assert.IsFalse(_stun.IsStunned(_playerGuid),
                    "El binder solo stunea por SUS instancias — un hazard ajeno con trigger OnEnter " +
                    "no debería robar el stun de la estela.");
                Assert.AreEqual(1, _pipeline.Resolved.Count, "El hazard ajeno sí debería cobrar su daño.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(foreign);
            }
        }

        [Test]
        public void StunApplied_UsesTheNodeStunTurns()
        {
            // Arrange — el número sale del nodo, no está hardcodeado en el binder.
            RetreatAlongRow();
            NewTrailNode(stunTurns: 2).Tick(BossContext());

            // Act
            _movement.RaiseMoved(_playerGuid, PlayerStart, Trail3, Path(PlayerStart, Trail3));

            // Assert
            Assert.AreEqual(2, _stun.GetStunTurns(_playerGuid));
        }

        // ======================================================================
        // Duración
        // ======================================================================

        /// <summary>
        /// El jugador abre cada ronda (CNF-006) y la duración se descuenta en el wrap, así que
        /// <c>DurationRounds = 1</c> mataría la estela antes de que pudiera pisarla.
        /// </summary>
        [Test]
        public void Trail_SurvivesTheRoundWrapAfterItWasLaid_AndDiesOnTheNext()
        {
            // Arrange
            ArmTrail();

            // Act — arranca la ronda siguiente: el jugador todavía no se movió.
            FireRound(1);

            // Assert
            Assert.IsTrue(_hazard.TryGetHazardAt(Trail3, out _),
                "La estela tiene que llegar viva al turno del jugador de la ronda siguiente.");

            // Act — una ronda más y se derrite sola.
            FireRound(2);

            // Assert
            Assert.IsFalse(_hazard.TryGetHazardAt(Trail3, out _), "Dura un turno del jugador, no más.");
        }

        [Test]
        public void AfterCombatEnd_TheTrailNoLongerStuns()
        {
            // Arrange
            ArmTrail();

            // Act
            EventManager.Trigger(EventName.OnCombatEnd, Guid.NewGuid());
            _movement.RaiseMoved(_playerGuid, PlayerStart, Trail3, Path(PlayerStart, Trail3));

            // Assert
            Assert.IsFalse(_stun.IsStunned(_playerGuid), "El estado de la estela es combat-scoped.");
            Assert.AreEqual(0, CountInstances());
        }

        // ======================================================================
        // Helpers
        // ======================================================================

        private static IReadOnlyList<GridCoord> Path(params GridCoord[] coords) => coords;

        private AIContext BossContext() => new AIContext
        {
            SelfGuid = _bossGuid,
            PlayerGuid = _playerGuid,
            Grid = _grid,
            Movement = _movement,
            DamagePipeline = _pipeline,
        };

        private AINode_IceTrail NewTrailNode(int maxTiles = 3, int stunTurns = 1)
            => new AINode_IceTrail
            {
                Hazard = _ice,
                MaxTiles = maxTiles,
                StunTurns = stunTurns,
                ReplacePreviousTrail = true,
            };

        /// <summary>Publica el movimiento del repliegue tal como lo haría <c>MovementService</c>.</summary>
        private void RetreatAlongRow()
            => _movement.RaiseMoved(_bossGuid, BossStart, BossEnd, Path(BossStart, Trail1, Trail2, Trail3));

        /// <summary>Repliegue + tick del nodo: deja la estela viva y trackeada.</summary>
        private void ArmTrail()
        {
            RetreatAlongRow();
            Assert.AreEqual(AIResult.Succeeded, NewTrailNode().Tick(BossContext()));
        }

        private void FireRound(int roundIndex)
            => _turnOrder.RestoreState(new[] { _playerGuid, _bossGuid }, cursor: 0, roundIndex: roundIndex);

        private int CountInstances()
        {
            int count = 0;
            foreach (var _ in _hazard.ActiveInstances()) count++;
            return count;
        }

        private static HazardDefinitionSO CreateIceDefinition()
        {
            var def = ScriptableObject.CreateInstance<HazardDefinitionSO>();
            def.hideFlags = HideFlags.HideAndDontSave;
            def.Trigger = HazardTriggerMode.OnEnter;
            def.Damage = 0;
            def.Kind = AttackKind.Environmental;
            def.ConsumeOnTrigger = true;
            def.DurationRounds = 2;
            def.SourceId = Guid.NewGuid().ToString();
            return def;
        }

        private sealed class StubMovementService : IMovementService
        {
            public List<GridCoord> GetReachableTiles(GridCoord origin, int range, bool includeOrigin = false)
                => new List<GridCoord>();

            public List<GridCoord> FindPath(GridCoord from, GridCoord to) => new List<GridCoord>();

            public bool Move(Guid entity, GridCoord destination) => false;

            public event Action<Guid, GridCoord, GridCoord, IReadOnlyList<GridCoord>> OnEntityMoved;

            /// <summary>Dispara OnEntityMoved como lo haría el service real.</summary>
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
