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
    /// Tests del ciclo de dos turnos del ataque del Croupier: <see cref="AINode_MarkSungSectors"/>
    /// marca el sector cantado y <see cref="AINode_DetonateSungSectors"/> lo cobra al turno siguiente.
    /// El caso que justifica que los dos nodos existan aparte de los genéricos es la columna de
    /// costura: en fase 2 dos áreas se resuelven por separado y el jugador que está ahí cobra las dos.
    /// </summary>
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

        // =====================================================================
        // Marcar
        // =====================================================================

        [Test]
        public void Mark_UsesThePhase1Damage_ByDefault()
        {
            // Arrange
            _wheel.Sing(new List<int> { 1 });

            // Act
            Assert.AreEqual(AIResult.Succeeded, Mark());

            // Assert
            var area = Pending(slot: 0);
            Assert.AreEqual(SectorDamage, area.Damage);
            Assert.AreEqual(12, area.Tiles.Count, "El sector es de 4×3.");
        }

        [Test]
        public void Mark_UsesThePhase2Damage_WhenTheTableIsRigged()
        {
            // Arrange — la fase no sube el daño por bloque: lo baja a 12 y canta dos.
            _wheel.SetMode(2, rigged: true, phaseIndex: 2);
            _wheel.Sing(new List<int> { 2, 3 });

            // Act
            Mark();

            // Assert
            Assert.AreEqual(SectorDamagePhase2, Pending(0).Damage);
            Assert.AreEqual(SectorDamagePhase2, Pending(1).Damage);
        }

        [Test]
        public void Mark_WithNothingInTheAir_Fails()
        {
            // Arrange — sin número cantado no hay nada que marcar. Falla, y por eso en el árbol va
            // envuelto en Selector[.., Wait]: el turno tiene que seguir igual.
            // Act + Assert
            Assert.AreEqual(AIResult.Failed, Mark());
        }

        [Test]
        public void Mark_TwoNumbers_KeepsBothAreasAlive()
        {
            // Arrange — el bug que motivó los guids por slot: IThreatenedAreaService guarda un área por
            // fuente, así que marcar las dos bajo el guid del jefe dejaba sólo la segunda.
            _wheel.SetMode(2, rigged: true, phaseIndex: 2);
            _wheel.Sing(new List<int> { 2, 3 });

            // Act
            Mark();

            // Assert
            Assert.IsTrue(_threat.HasPending(CroupierSectorTelegraph.SlotGuid(_bossGuid, 0)));
            Assert.IsTrue(_threat.HasPending(CroupierSectorTelegraph.SlotGuid(_bossGuid, 1)));
        }

        // =====================================================================
        // Detonar
        // =====================================================================

        [Test]
        public void Detonate_PlayerInsideTheSector_TakesTheSectorDamage()
        {
            // Arrange
            MovePlayer(new GridCoord(0, 5)); // Sector 1.
            _wheel.Sing(new List<int> { 1 });
            Mark();

            // Act
            Assert.AreEqual(AIResult.Succeeded, Detonate());

            // Assert
            Assert.AreEqual(1, _pipeline.Resolved.Count);
            Assert.AreEqual(SectorDamage, _pipeline.Resolved[0].BaseDamage);
            Assert.AreEqual(_bossGuid, _pipeline.Resolved[0].SourceId,
                "El daño lo firma el jefe, no el guid derivado del slot.");
            Assert.AreEqual(_playerGuid, _pipeline.Resolved[0].TargetId);
        }

        [Test]
        public void Detonate_PlayerInTheCorridor_TakesNothing()
        {
            // Arrange — el pasillo no cae nunca: es la promesa estructural del jefe.
            MovePlayer(new GridCoord(4, 3));
            _wheel.Sing(new List<int> { 2 });
            Mark();

            // Act
            Assert.AreEqual(AIResult.Succeeded, Detonate());

            // Assert
            Assert.IsEmpty(_pipeline.Resolved);
        }

        [Test]
        public void Detonate_SeamColumnInPhase2_TakesBothHits()
        {
            // Arrange — 12 + 12 = los 24 de la ficha, en dos golpes: cada uno queda debajo del techo
            // de daño por golpe del piso y el escudo se aplica como en cualquier otro par.
            _wheel.SetMode(2, rigged: true, phaseIndex: 2);
            MovePlayer(new GridCoord(7, 5)); // La costura, arriba.
            _wheel.Sing(new List<int> { 2, 3 });
            Mark();

            // Act
            Detonate();

            // Assert
            Assert.AreEqual(2, _pipeline.Resolved.Count, "En la costura pegan los dos sectores.");
            Assert.AreEqual(SectorDamagePhase2 * 2, _pipeline.Resolved.Sum(c => c.BaseDamage));
            foreach (var ctx in _pipeline.Resolved)
                Assert.AreEqual(SectorDamagePhase2, ctx.BaseDamage, "Ningún golpe individual pasa de 12.");
        }

        [Test]
        public void Detonate_OutsideTheSeamInPhase2_TakesOnlyOneHit()
        {
            // Arrange
            _wheel.SetMode(2, rigged: true, phaseIndex: 2);
            MovePlayer(new GridCoord(4, 5)); // Sector 2, fuera de la costura.
            _wheel.Sing(new List<int> { 2, 3 });
            Mark();

            // Act
            Detonate();

            // Assert
            Assert.AreEqual(1, _pipeline.Resolved.Count);
            Assert.AreEqual(SectorDamagePhase2, _pipeline.Resolved[0].BaseDamage);
        }

        [Test]
        public void Detonate_WithNothingMarked_SucceedsAnyway()
        {
            // Arrange — turno 1. Devolver Failed acá le cancelaría al jefe el resto del turno.
            // Act + Assert
            Assert.AreEqual(AIResult.Succeeded, Detonate());
            Assert.IsEmpty(_pipeline.Resolved);
        }

        [Test]
        public void Detonate_ClosesTheWindup_AndPublishesTheDetonatedSector()
        {
            // Arrange
            _wheel.Sing(new List<int> { 5 });
            Mark();

            // Act
            Detonate();

            // Assert
            Assert.IsFalse(_wheel.WindupActive, "Después de detonar, pegarle ya no mueve la rueda.");
            Assert.AreEqual(new[] { 5 }, _wheel.DetonatedSectors, "El sector que cayó tiene que quedar para el fuego.");
            Assert.IsFalse(_threat.HasPending(CroupierSectorTelegraph.SlotGuid(_bossGuid, 0)));
        }

        [Test]
        public void NudgedNumber_DetonatesTheNewSector_NotTheOneItSang()
        {
            // Arrange — el error típico del jugador: correr la rueda sin mirar a dónde la manda. Si el
            // corrimiento no moviera el hacha, la palanca sería decorativa.
            MovePlayer(new GridCoord(0, 0)); // Sector 4.
            _wheel.Sing(new List<int> { 3 });
            Mark();

            // Act — el jugador pega con el 3 en el aire: pasa a 4, que es justo donde está parado.
            TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                SourceGuid = _playerGuid,
                TargetGuid = _bossGuid,
                FinalDamage = 13,
            });
            _pipeline.Resolved.Clear(); // Descartamos la Represalia: acá se mide la detonación.
            Detonate();

            // Assert
            Assert.AreEqual(1, _pipeline.Resolved.Count, "El sector 4 (el corrido) es el que detona.");
            Assert.AreEqual(SectorDamage, _pipeline.Resolved[0].BaseDamage);
        }

        // =====================================================================
        // Helpers
        // =====================================================================

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
