using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI.Bosses.Croupier;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Dice;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// <see cref="AINode_IgniteDetonatedSectors"/> contra el <see cref="HazardService"/> real.
    /// </summary>
    [TestFixture]
    public class CroupierIgnitionTests
    {
        private const int FireDamage = 6;

        private GridManager _grid;
        private ThreatenedAreaService _threat;
        private HazardService _hazard;
        private SpyDamagePipeline _pipeline;
        private CroupierWheelService _wheel;
        private HazardDefinitionSO _fire;
        private HazardDefinitionSO _firePhase2;

        private Guid _bossGuid;
        private Guid _playerGuid;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            TypedEvent<DamageResolvedPayload>.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(11, 7));
            ServiceLocator.AddService<IGridManager>(_grid);

            _threat = new ThreatenedAreaService();
            _threat.Register();

            _pipeline = new SpyDamagePipeline();
            ServiceLocator.AddService<IDamagePipeline>(_pipeline);

            _hazard = new HazardService();
            _hazard.Register();

            _bossGuid = Guid.NewGuid();
            _playerGuid = Guid.NewGuid();
            _grid.Register(_bossGuid, new GridCoord(5, 3));
            _grid.Register(_playerGuid, new GridCoord(0, 0)); // Sector 4.

            // El fuego es PlayerOnly y el filtro es fail-closed: sin IPlayerService no cobra a nadie.
            ServiceLocator.AddService<IPlayerService>(new StubPlayerService { PlayerGuid = _playerGuid });

            _wheel = (CroupierWheelService)CroupierWheelService.ResolveOrCreate();
            _wheel.Bind(_bossGuid);

            // 3 y 4 rondas de hazard = "arde 2" y "arde 3" para el jugador: el fuego nace en el turno
            // del jefe, cuando el jugador ya jugó esa ronda.
            _fire = CreateFire("Fire P1", durationRounds: 3);
            _firePhase2 = CreateFire("Fire P2", durationRounds: 4);
        }

        [TearDown]
        public void TearDown()
        {
            _wheel.Dispose();
            _hazard.Dispose();

            if (ServiceLocator.TryGetService<IThreatOverlayService>(out var overlay) && overlay is IDisposable d)
                d.Dispose();
            var leftover = GameObject.Find("ThreatTelegraphOverlay");
            if (leftover != null) UnityEngine.Object.DestroyImmediate(leftover);

            if (_fire != null) UnityEngine.Object.DestroyImmediate(_fire);
            if (_firePhase2 != null) UnityEngine.Object.DestroyImmediate(_firePhase2);

            TypedEvent<DamageResolvedPayload>.Clear();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        // =====================================================================
        // Ignición
        // =====================================================================

        [Test]
        public void Ignite_OneFirePerDetonatedSector()
        {
            // Arrange
            Detonate(2, 3);

            // Act
            var result = Ignite();

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            var instances = new List<HazardInstanceInfo>(_hazard.ActiveInstances());
            Assert.AreEqual(2, instances.Count, "Un fuego por sector detonado, no uno solo para los dos.");

            var sector2 = ThreatAreaShape.ComputeRoomSector(_grid, 2);
            foreach (var tile in sector2)
                Assert.IsTrue(_hazard.TryGetHazardAt(tile, out _), $"{tile} del sector 2 debería estar en llamas.");
        }

        [Test]
        public void Ignite_UsesThePhase2Fire_WhenTheTableIsRigged()
        {
            // Arrange
            _wheel.SetMode(2, rigged: true, phaseIndex: 2);
            Detonate(1);

            // Act
            Ignite();

            // Assert
            var instances = new List<HazardInstanceInfo>(_hazard.ActiveInstances());
            Assert.AreEqual(1, instances.Count);
            Assert.AreSame(_firePhase2, instances[0].Definition);
            Assert.AreEqual(4, instances[0].RemainingRounds, "Fase 2 arde 3 rondas para el jugador.");
        }

        [Test]
        public void Ignite_WithoutAPhase2Definition_FallsBackToTheSameFire()
        {
            // Arrange
            _wheel.SetMode(2, rigged: true, phaseIndex: 2);
            Detonate(1);

            // Act
            Ignite(withPhase2Def: false);

            // Assert
            var instances = new List<HazardInstanceInfo>(_hazard.ActiveInstances());
            Assert.AreEqual(1, instances.Count);
            Assert.AreSame(_fire, instances[0].Definition);
        }

        [Test]
        public void Ignite_NothingDetonated_SucceedsWithoutIgniting()
        {
            // Arrange — turno 1: cantó, pero todavía no detonó nada.
            // Act
            var result = Ignite();

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.IsEmpty(new List<HazardInstanceInfo>(_hazard.ActiveInstances()));
        }

        [Test]
        public void Ignite_ConsumesTheDetonatedSectors_SoItNeverIgnitesTwice()
        {
            // Arrange
            Detonate(1);

            // Act
            Ignite();
            Ignite();

            // Assert
            Assert.AreEqual(1, new List<HazardInstanceInfo>(_hazard.ActiveInstances()).Count,
                "El segundo tick no debería re-encender el mismo sector.");
        }

        // =====================================================================
        // "La explosión consume la llama"
        // =====================================================================

        [Test]
        public void PlayerCaughtByTheBlast_FirstFireTickIsSwallowed()
        {
            // Arrange — el jugador está en el sector que detona: ya pagó los 20 este turno.
            MovePlayer(new GridCoord(0, 5)); // Sector 1.
            Detonate(1);

            // Act
            Ignite();
            EndTurn(_playerGuid);

            // Assert
            Assert.IsEmpty(_pipeline.Resolved,
                "La detonación consume la llama: el fuego no puede cobrar 6 encima en el mismo turno.");

            // ...y el fuego sigue vivo: el candado es de un tick, no apaga el bloque.
            EndTurn(_playerGuid);
            Assert.AreEqual(1, _pipeline.Resolved.Count);
            Assert.AreEqual(FireDamage, _pipeline.Resolved[0].BaseDamage);
        }

        [Test]
        public void PlayerDodgedTheBlast_FireBillsTheFirstTurnEndInside()
        {
            // Arrange — esquivó el sector, así que no se armó el candado de la detonación.
            MovePlayer(new GridCoord(10, 0)); // Sector 6, lejos del que detona.
            Detonate(1);

            // Act
            Ignite();
            MovePlayer(new GridCoord(0, 5)); // Vuelve al bloque quemado.
            EndTurn(_playerGuid);

            // Assert
            Assert.AreEqual(1, _pipeline.Resolved.Count, "El fuego tiene que cobrar 6 al entrar y quedarse.");
            Assert.AreEqual(FireDamage, _pipeline.Resolved[0].BaseDamage);
            Assert.AreEqual(_playerGuid, _pipeline.Resolved[0].TargetId);
            Assert.AreEqual(AttackKind.Environmental, _pipeline.Resolved[0].Kind);
        }

        [Test]
        public void BlastConsumesFlameDisabled_FireBillsRightAway()
        {
            // Arrange — el flag es autorable: apagarlo suma detonación + fuego.
            MovePlayer(new GridCoord(0, 5));
            Detonate(1);

            // Act
            Ignite(blastConsumesFlame: false);
            EndTurn(_playerGuid);

            // Assert
            Assert.AreEqual(1, _pipeline.Resolved.Count);
        }

        // =====================================================================
        // "El fuego dura y se acumula"
        // =====================================================================

        [Test]
        public void Phase1Fire_BillsTwoPlayerTurnEnds_ThenGoesOut()
        {
            // Arrange
            MovePlayer(new GridCoord(10, 0)); // Sector 6: esquiva la detonación, no se arma el skip.
            Detonate(1);
            Ignite();
            MovePlayer(new GridCoord(0, 5)); // Sector 1: se mete en el bloque quemado.

            // Act + Assert
            NewRound(2);
            EndTurn(_playerGuid);
            Assert.AreEqual(1, _pipeline.Resolved.Count, "Primera ronda de fuego.");

            NewRound(3);
            EndTurn(_playerGuid);
            Assert.AreEqual(2, _pipeline.Resolved.Count, "Segunda ronda de fuego.");

            NewRound(4);
            EndTurn(_playerGuid);
            Assert.AreEqual(2, _pipeline.Resolved.Count, "A la tercera el bloque ya se apagó.");
        }

        [Test]
        public void Ignite_DoesNotPutOutThePreviousBlock()
        {
            // Arrange
            MovePlayer(new GridCoord(10, 0)); // Sector 6: lejos de los dos que caen.
            Detonate(1);
            Ignite();

            // Act — al turno siguiente cae otro bloque.
            NewRound(2);
            Detonate(4);
            Ignite();

            // Assert
            Assert.AreEqual(2, new List<HazardInstanceInfo>(_hazard.ActiveInstances()).Count,
                "El fuego del sector 1 tiene que seguir vivo cuando prende el del 4.");

            MovePlayer(new GridCoord(0, 5)); // Sector 1: el bloque de la ronda pasada.
            EndTurn(_playerGuid);
            Assert.AreEqual(1, _pipeline.Resolved.Count, "El bloque viejo sigue cobrando sus 6.");
            Assert.AreEqual(FireDamage, _pipeline.Resolved[0].BaseDamage);
        }

        [Test]
        public void SeamColumnInPhase2_BothFiresCoverIt()
        {
            // Arrange — dos sectores contiguos comparten la columna de costura.
            _wheel.SetMode(2, rigged: true, phaseIndex: 2);
            Detonate(2, 3);

            // Act
            Ignite();

            // Assert
            var seam = new GridCoord(7, 5);
            int covering = 0;
            foreach (var instance in _hazard.ActiveInstances())
                if (instance.Tiles.Contains(seam)) covering++;

            Assert.AreEqual(2, covering, "La columna de costura queda dentro de los dos fuegos.");
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private void Detonate(params int[] sectors)
        {
            _wheel.Sing(new List<int>(sectors));
            _wheel.ConsumeWindup();
        }

        private AIResult Ignite(bool withPhase2Def = true, bool blastConsumesFlame = true)
        {
            var node = new AINode_IgniteDetonatedSectors
            {
                Fire = _fire,
                FirePhase2 = withPhase2Def ? _firePhase2 : null,
                BlastConsumesFlame = blastConsumesFlame,
            };

            return node.Tick(new AIContext
            {
                SelfGuid = _bossGuid,
                PlayerGuid = _playerGuid,
                Grid = _grid,
                DamagePipeline = _pipeline,
            });
        }

        private void MovePlayer(GridCoord coord) => _grid.Move(_playerGuid, coord);

        private static void EndTurn(Guid entity) => EventManager.Trigger(EventName.OnTurnFinished, entity);

        /// <summary>
        /// Abre la ronda <paramref name="roundIndex"/>. Es el tick de duración de los hazards: el
        /// <c>HazardService</c> descuenta rondas en <c>OnTurnQueueBuilt</c>, no en el cierre de turno.
        /// </summary>
        private static void NewRound(int roundIndex)
            => EventManager.Trigger(EventName.OnTurnQueueBuilt, new List<Guid>(), roundIndex);

        private static HazardDefinitionSO CreateFire(string name, int durationRounds)
        {
            var def = ScriptableObject.CreateInstance<HazardDefinitionSO>();
            def.name = name;
            def.hideFlags = HideFlags.HideAndDontSave;
            def.Trigger = HazardTriggerMode.OnTurnEndInTile;
            def.Damage = FireDamage;
            def.Kind = AttackKind.Environmental;
            def.DurationRounds = durationRounds;
            def.ConsumeOnTrigger = false;
            def.SourceId = Guid.NewGuid().ToString();
            return def;
        }

        private sealed class SpyDamagePipeline : IDamagePipeline
        {
            public readonly List<DamageContext> Resolved = new List<DamageContext>();

            public DamageContext Resolve(DamageContext ctx)
            {
                Resolved.Add(ctx);
                return ctx;
            }

            public DamageContext Preview(DamageContext ctx) => ctx;
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
    }
}
