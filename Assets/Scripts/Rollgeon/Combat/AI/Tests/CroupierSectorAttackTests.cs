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
    [TestFixture]
    public class CroupierSectorAttackTests
    {
        private const int SectorDamage = 20;
        private const int SectorDamagePhase2 = 12;

        private GridManager _grid;
        private ThreatenedAreaService _threat;
        private SpyDamagePipeline _pipeline;
        private CroupierWheelService _wheel;

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

            _bossGuid = Guid.NewGuid();
            _playerGuid = Guid.NewGuid();
            _grid.Register(_bossGuid, new GridCoord(5, 3));
            _grid.Register(_playerGuid, new GridCoord(0, 0));

            // El corrimiento sale del cierre de turno del jugador, y quién es el jugador lo dice esto.
            ServiceLocator.AddService<IPlayerService>(new StubPlayerService { PlayerGuid = _playerGuid });

            _wheel = (CroupierWheelService)CroupierWheelService.ResolveOrCreate();
            _wheel.Bind(_bossGuid);
        }

        [TearDown]
        public void TearDown()
        {
            _wheel.Dispose();

            if (ServiceLocator.TryGetService<IThreatOverlayService>(out var overlay) && overlay is IDisposable d)
                d.Dispose();
            var leftover = GameObject.Find("ThreatTelegraphOverlay");
            if (leftover != null) UnityEngine.Object.DestroyImmediate(leftover);

            TypedEvent<DamageResolvedPayload>.Clear();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        [Test]
        public void Mark_UsesThePhase1Damage_ByDefault()
        {
            _wheel.Sing(new List<int> { 1 });

            Assert.AreEqual(AIResult.Succeeded, Mark());

            var area = Pending(slot: 0);
            Assert.AreEqual(SectorDamage, area.Damage);
            Assert.AreEqual(16, area.Tiles.Count, "El sector es de 4×4.");
        }

        [Test]
        public void Mark_UsesThePhase2Damage_WhenTheTableIsRigged()
        {
            _wheel.SetMode(2, rigged: true, phaseIndex: 2);
            _wheel.Sing(new List<int> { 2, 3 });

            Mark();

            Assert.AreEqual(SectorDamagePhase2, Pending(0).Damage);
            Assert.AreEqual(SectorDamagePhase2, Pending(1).Damage);
        }

        [Test]
        public void Mark_WithNothingInTheAir_Fails()
        {
            // El Failed lo absorbe el Selector[.., Wait] del árbol.
            Assert.AreEqual(AIResult.Failed, Mark());
        }

        [Test]
        public void Mark_TwoNumbers_KeepsBothAreasAlive()
        {
            // IThreatenedAreaService guarda un área por fuente: sin guid por slot se pisan.
            _wheel.SetMode(2, rigged: true, phaseIndex: 2);
            _wheel.Sing(new List<int> { 2, 3 });

            Mark();

            Assert.IsTrue(_threat.HasPending(CroupierSectorTelegraph.SlotGuid(_bossGuid, 0)));
            Assert.IsTrue(_threat.HasPending(CroupierSectorTelegraph.SlotGuid(_bossGuid, 1)));
        }

        [Test]
        public void Detonate_PlayerInsideTheSector_TakesTheSectorDamage()
        {
            MovePlayer(new GridCoord(0, 5)); // Sector 1.
            _wheel.Sing(new List<int> { 1 });
            Mark();

            Assert.AreEqual(AIResult.Succeeded, Detonate());

            Assert.AreEqual(1, _pipeline.Resolved.Count);
            Assert.AreEqual(SectorDamage, _pipeline.Resolved[0].BaseDamage);
            Assert.AreEqual(_bossGuid, _pipeline.Resolved[0].SourceId,
                "El daño lo firma el jefe, no el guid derivado del slot.");
            Assert.AreEqual(_playerGuid, _pipeline.Resolved[0].TargetId);
        }

        [Test]
        public void Detonate_PlayerOnTheMiddleRow_TakesTheHit()
        {
            // La fila del medio es costura: la comparten el bloque de arriba y el de abajo.
            MovePlayer(new GridCoord(4, 3));
            _wheel.Sing(new List<int> { 2 });
            Mark();

            Assert.AreEqual(AIResult.Succeeded, Detonate());

            Assert.AreEqual(1, _pipeline.Resolved.Count, "Ninguna casilla caminable es segura para siempre.");
            Assert.AreEqual(SectorDamage, _pipeline.Resolved[0].BaseDamage);
        }

        [Test]
        public void Detonate_MiddleRow_AlsoFallsWithTheBlockBelowIt()
        {
            MovePlayer(new GridCoord(4, 3));
            _wheel.Sing(new List<int> { 5 });
            Mark();

            Detonate();

            Assert.AreEqual(1, _pipeline.Resolved.Count);
            Assert.AreEqual(SectorDamage, _pipeline.Resolved[0].BaseDamage);
        }

        [Test]
        public void Detonate_ApproachingFromAboveOrBelow_IsNormalRisk()
        {
            // Canta el bloque de abajo y el jugador está en el de arriba, pegado al jefe:
            // el contrapeso de la costura es que llegar al melee por arriba/abajo no cuesta doble.
            MovePlayer(new GridCoord(5, 4));
            _wheel.Sing(new List<int> { 5 });
            Mark();

            Detonate();

            Assert.IsEmpty(_pipeline.Resolved);
        }

        [Test]
        public void Detonate_SeamColumnInPhase2_TakesBothHits()
        {
            // Son dos golpes separados, no uno de 24: el escudo se aplica a cada uno.
            _wheel.SetMode(2, rigged: true, phaseIndex: 2);
            MovePlayer(new GridCoord(7, 5)); // La costura, arriba.
            _wheel.Sing(new List<int> { 2, 3 });
            Mark();

            Detonate();

            Assert.AreEqual(2, _pipeline.Resolved.Count, "En la costura pegan los dos sectores.");
            Assert.AreEqual(SectorDamagePhase2 * 2, _pipeline.Resolved.Sum(c => c.BaseDamage));
            foreach (var ctx in _pipeline.Resolved)
                Assert.AreEqual(SectorDamagePhase2, ctx.BaseDamage, "Ningún golpe individual pasa de 12.");
        }

        [Test]
        public void Detonate_OutsideTheSeamInPhase2_TakesOnlyOneHit()
        {
            _wheel.SetMode(2, rigged: true, phaseIndex: 2);
            MovePlayer(new GridCoord(4, 5)); // Sector 2, fuera de la costura.
            _wheel.Sing(new List<int> { 2, 3 });
            Mark();

            Detonate();

            Assert.AreEqual(1, _pipeline.Resolved.Count);
            Assert.AreEqual(SectorDamagePhase2, _pipeline.Resolved[0].BaseDamage);
        }

        [Test]
        public void Detonate_WithNothingMarked_SucceedsAnyway()
        {
            // Turno 1: Failed acá le cancelaría al jefe el resto del turno.
            Assert.AreEqual(AIResult.Succeeded, Detonate());
            Assert.IsEmpty(_pipeline.Resolved);
        }

        [Test]
        public void Detonate_ClosesTheWindup_AndPublishesTheDetonatedSector()
        {
            _wheel.Sing(new List<int> { 5 });
            Mark();

            Detonate();

            Assert.IsFalse(_wheel.WindupActive, "Después de detonar, pegarle ya no mueve la rueda.");
            Assert.AreEqual(new[] { 5 }, _wheel.DetonatedSectors, "El sector que cayó tiene que quedar para el fuego.");
            Assert.IsFalse(_threat.HasPending(CroupierSectorTelegraph.SlotGuid(_bossGuid, 0)));
        }

        [Test]
        public void StandingInTheCalledSector_PushesTheBlastOffYou()
        {
            MovePlayer(new GridCoord(0, 0)); // Sector 4.
            _wheel.Sing(new List<int> { 4 });
            Mark();

            EndPlayerTurn();
            Detonate();

            Assert.IsEmpty(_pipeline.Resolved, "El 4 pasó a 5: donde está parado ya no detona nada.");
        }

        [Test]
        public void NudgedNumber_DetonatesTheNewSector_NotTheOneItSang()
        {
            MovePlayer(new GridCoord(0, 0)); // Sector 4.
            _wheel.Sing(new List<int> { 4 });
            Mark();

            EndPlayerTurn();
            MovePlayer(new GridCoord(4, 0)); // Sector 5, a donde mandó el hacha.
            Detonate();

            Assert.AreEqual(1, _pipeline.Resolved.Count, "El sector 5 (el corrido) es el que detona.");
            Assert.AreEqual(SectorDamage, _pipeline.Resolved[0].BaseDamage);
        }

        [Test]
        public void HittingTheBoss_DoesNotRedirectTheBlast()
        {
            MovePlayer(new GridCoord(0, 0)); // Sector 4.
            _wheel.Sing(new List<int> { 4 });
            Mark();

            TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                SourceGuid = _playerGuid,
                TargetGuid = _bossGuid,
                FinalDamage = 13,
            });
            _pipeline.Resolved.Clear(); // Descartamos la Represalia: acá se mide la detonación.
            Detonate();

            Assert.AreEqual(1, _pipeline.Resolved.Count, "El 4 sigue siendo el 4: le pega igual.");
            Assert.AreEqual(SectorDamage, _pipeline.Resolved[0].BaseDamage);
        }

        private AIResult Mark()
        {
            return new AINode_MarkSungSectors
            {
                SectorDamage = SectorDamage,
                SectorDamagePhase2 = SectorDamagePhase2,
                Kind = AttackKind.BasicAttack,
            }.Tick(Context());
        }

        private AIResult Detonate() => new AINode_DetonateSungSectors().Tick(Context());

        private AIContext Context() => new AIContext
        {
            SelfGuid = _bossGuid,
            PlayerGuid = _playerGuid,
            Grid = _grid,
            DamagePipeline = _pipeline,
        };

        private ThreatenedArea Pending(int slot)
        {
            var slotGuid = CroupierSectorTelegraph.SlotGuid(_bossGuid, slot);
            Assert.IsTrue(_threat.TryConsume(slotGuid, out var area), $"El slot {slot} no marcó nada.");
            return area;
        }

        private void MovePlayer(GridCoord coord) => _grid.Move(_playerGuid, coord);

        private void EndPlayerTurn() => EventManager.Trigger(EventName.OnTurnFinished, _playerGuid);

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
