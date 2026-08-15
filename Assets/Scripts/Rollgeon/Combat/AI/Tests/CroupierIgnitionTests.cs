using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI.Bosses.Croupier;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// Tests de <see cref="AINode_IgniteDetonatedSectors"/> contra el <see cref="HazardService"/> real:
    /// el sector que acaba de caer queda en llamas, y la explosión consume la llama del turno en que
    /// detonó (el peor caso de la columna de costura sigue siendo 24, no 30).
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

            _wheel = (CroupierWheelService)CroupierWheelService.ResolveOrCreate();
            _wheel.Bind(_bossGuid);

            // 3 y 4 rondas de hazard = "arde 2 rondas" y "arde 3": el fuego nace en el turno del jefe y
            // el jugador ya jugó esa ronda, así que la primera no le llega nunca. Ver los remarks de
            // AINode_IgniteDetonatedSectors.
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
            // Arrange — fase 2: dos sectores caen, hasta dos bloques quemándose.
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
            // Arrange — en fase 2 el fuego dura un turno más, y la duración vive en la definición.
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
            // Arrange — turno 1: cantó, pero todavía no detonó nada. No es un fallo.
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
            // Arrange — el jugador está en el sector que detona (ya pagó los 20 este turno).
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
            // Arrange — el jugador esquivó el sector: el fuego no le debe nada todavía, así que su
            // primer cierre de turno adentro sí cobra. Es la lectura que reeduca al veterano — el
            // bloque que acaba de caer ya no es el lugar seguro del paño.
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
            // Arrange — "arde 2 rondas": quedarse cuesta 6 dos veces, y recién a la tercera el bloque
            // vuelve a ser pisable. Con la duración vieja el fuego llegaba a cobrar una sola vez, así
            // que salir del bloque nunca era una decisión: bastaba con no volver.
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
            // Arrange — encender el bloque nuevo no limpia el que todavía arde: el paño se gasta ronda
            // a ronda en vez de volver a foja cero cada vez que cae un número.
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
            // Arrange — dos sectores contiguos: la costura queda cubierta por los dos fuegos, igual
            // que la cobran las dos detonaciones. El paño se pudre entero, como pide la fase.
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
    }
}
