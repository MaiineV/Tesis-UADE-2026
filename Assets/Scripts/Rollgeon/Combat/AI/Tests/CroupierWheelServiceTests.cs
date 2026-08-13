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
    /// Tests de <see cref="CroupierWheelService"/>: la palanca del jefe de piso 1. Pegarle con un
    /// número en el aire corre la rueda +1 y, en los impares, cobra 8 — un solo corrimiento por número,
    /// y ninguno de los dos con la rueda trucada.
    /// </summary>
    [TestFixture]
    public class CroupierWheelServiceTests
    {
        private const int Retaliation = 8;

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
            _grid.Register(_bossGuid, new GridCoord(5, 3));  // El pasillo: nunca cae.
            _grid.Register(_playerGuid, new GridCoord(0, 0));

            // Por el camino lazy real (registra Global y se suscribe al fin de combate), que es como
            // nace en juego: el jefe entra por un asset y nadie agrega un bootstrap a mano.
            _wheel = (CroupierWheelService)CroupierWheelService.ResolveOrCreate();
            _wheel.RetaliationDamage = Retaliation;
            _wheel.Bind(_bossGuid);
        }

        [TearDown]
        public void TearDown()
        {
            _wheel.Dispose();

            // Marcar pinta overlay, y el overlay crea un GameObject + materiales por tint: sin este
            // teardown quedan huérfanos y contaminan cualquier test que los busque por nombre.
            if (ServiceLocator.TryGetService<IThreatOverlayService>(out var overlay) && overlay is IDisposable d)
                d.Dispose();
            var leftover = GameObject.Find("ThreatTelegraphOverlay");
            if (leftover != null) UnityEngine.Object.DestroyImmediate(leftover);

            TypedEvent<DamageResolvedPayload>.Clear();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        // =====================================================================
        // Corrimiento + Represalia (el mismo evento)
        // =====================================================================

        [Test]
        public void Hit_WithOddNumberInTheAir_MovesTheWheelAndChargesRetaliation()
        {
            // Arrange
            _wheel.Sing(new List<int> { 3 });

            // Act
            HitBoss();

            // Assert
            Assert.AreEqual(new[] { 4 }, _wheel.SungNumbers, "El 3 tiene que pasar a 4.");
            Assert.AreEqual(1, _pipeline.Resolved.Count, "La Represalia se cobra una vez.");
            Assert.AreEqual(_playerGuid, _pipeline.Resolved[0].TargetId, "La cobra el atacante.");
            Assert.AreEqual(_bossGuid, _pipeline.Resolved[0].SourceId);
            Assert.AreEqual(Retaliation, _pipeline.Resolved[0].BaseDamage);
            Assert.AreEqual(AttackKind.Reaction, _pipeline.Resolved[0].Kind);
        }

        [Test]
        public void Hit_WithEvenNumberInTheAir_MovesTheWheelForFree()
        {
            // Arrange
            _wheel.Sing(new List<int> { 4 });

            // Act
            HitBoss();

            // Assert — en los pares la palanca es gratis.
            Assert.AreEqual(new[] { 5 }, _wheel.SungNumbers);
            Assert.IsEmpty(_pipeline.Resolved, "En un número par no se cobra Represalia.");
        }

        [Test]
        public void SecondHitSameWindup_NeitherMovesNorCharges()
        {
            // Arrange — el candado por número: sin él el segundo golpe del turno movería dos veces y
            // cobraría dos veces, y la lectura "primero N+1, después decido" sería falsa.
            _wheel.Sing(new List<int> { 3 });

            // Act
            HitBoss();
            HitBoss();

            // Assert
            Assert.AreEqual(new[] { 4 }, _wheel.SungNumbers, "Un solo corrimiento por número.");
            Assert.AreEqual(1, _pipeline.Resolved.Count, "Una sola Represalia por número.");
        }

        [Test]
        public void RiggedWheel_NeitherMovesNorCharges()
        {
            // Arrange — fase 2: la rueda trucada apaga los dos, porque son el mismo evento.
            _wheel.SetMode(numbersPerTurn: 2, rigged: true, phaseIndex: 2);
            _wheel.Sing(new List<int> { 3, 4 });

            // Act
            HitBoss();

            // Assert
            Assert.AreEqual(new[] { 3, 4 }, _wheel.SungNumbers, "Con la rueda trucada el número no se mueve.");
            Assert.IsEmpty(_pipeline.Resolved, "Sin palanca no hay precio.");
        }

        [Test]
        public void Hit_OutsideTheWindup_DoesNothing()
        {
            // Arrange — el windup se cierra al detonar.
            _wheel.Sing(new List<int> { 3 });
            _wheel.ConsumeWindup();

            // Act
            HitBoss();

            // Assert
            Assert.IsEmpty(_wheel.SungNumbers);
            Assert.IsEmpty(_pipeline.Resolved, "Fuera del windup pegarle no cuesta nada.");
        }

        [Test]
        public void Hit_ThatDealtNoDamageAtAll_DoesNotTouchTheLever()
        {
            // Arrange
            _wheel.Sing(new List<int> { 3 });

            // Act — un evento de 0 (esquivado / inmune) lo publica el pipeline igual.
            TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                SourceGuid = _playerGuid,
                TargetGuid = _bossGuid,
                FinalDamage = 0,
                ShieldAbsorbed = 0,
            });

            // Assert
            Assert.AreEqual(new[] { 3 }, _wheel.SungNumbers);
            Assert.IsEmpty(_pipeline.Resolved);
        }

        [Test]
        public void DamageToSomeoneElse_DoesNotMoveTheWheel()
        {
            // Arrange
            _wheel.Sing(new List<int> { 3 });

            // Act
            TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                SourceGuid = _bossGuid,
                TargetGuid = _playerGuid,
                FinalDamage = 20,
            });

            // Assert
            Assert.AreEqual(new[] { 3 }, _wheel.SungNumbers);
            Assert.IsEmpty(_pipeline.Resolved);
        }

        [Test]
        public void Nudge_FromSix_WrapsToOne()
        {
            // Arrange — es una rueda, no una escalera.
            _wheel.Sing(new List<int> { 6 });

            // Act
            HitBoss();

            // Assert
            Assert.AreEqual(new[] { 1 }, _wheel.SungNumbers);
            Assert.IsEmpty(_pipeline.Resolved, "El 6 es par: la palanca es gratis.");
        }

        // =====================================================================
        // El corrimiento mueve la marca
        // =====================================================================

        [Test]
        public void Nudge_MovesThePendingAreaToTheNewSector()
        {
            // Arrange — si el área no se moviera, la palanca no cambiaría nada de lo que va a pasar.
            _wheel.Sing(new List<int> { 3 });
            Assert.IsTrue(CroupierSectorTelegraph.Mark(_bossGuid, slot: 0, sector: 3, damage: 20, kind: AttackKind.BasicAttack));
            _wheel.RecordMark(0, 20, AttackKind.BasicAttack);

            // Act
            HitBoss();

            // Assert
            var slotGuid = CroupierSectorTelegraph.SlotGuid(_bossGuid, 0);
            var pending = _threat.GetPendingTiles(slotGuid);
            var expected = ThreatAreaShape.ComputeRoomSector(_grid, 4);

            Assert.AreEqual(expected.Count, pending.Count, "El área tiene que ser la del sector 4.");
            foreach (var tile in expected)
                Assert.IsTrue(pending.Contains(tile), $"Falta {tile} del sector 4 en el área pendiente.");
        }

        [Test]
        public void Nudge_KeepsTheMarkedDamage()
        {
            // Arrange
            _wheel.Sing(new List<int> { 1 });
            CroupierSectorTelegraph.Mark(_bossGuid, 0, 1, 20, AttackKind.BasicAttack);
            _wheel.RecordMark(0, 20, AttackKind.BasicAttack);

            // Act
            HitBoss();

            // Assert — mover la rueda cambia a dónde cae el hacha, no cuánto pega.
            var slotGuid = CroupierSectorTelegraph.SlotGuid(_bossGuid, 0);
            Assert.IsTrue(_threat.TryConsume(slotGuid, out var area));
            Assert.AreEqual(20, area.Damage);
        }

        // =====================================================================
        // Estado por combate
        // =====================================================================

        [Test]
        public void ConsumeWindup_PublishesTheDetonatedSectors()
        {
            // Arrange
            _wheel.SetMode(2, rigged: true, phaseIndex: 2);
            _wheel.Sing(new List<int> { 2, 3 });

            // Act
            var slots = _wheel.ConsumeWindup();

            // Assert
            Assert.AreEqual(2, slots.Count);
            Assert.AreEqual(new[] { 2, 3 }, _wheel.DetonatedSectors);
            Assert.IsFalse(_wheel.WindupActive, "Detonar cierra el windup.");

            _wheel.ClearDetonated();
            Assert.IsEmpty(_wheel.DetonatedSectors);
        }

        [Test]
        public void SlotGuids_AreStableDistinctAndNeverTheBossGuid()
        {
            // Arrange — dos áreas simultáneas necesitan dos fuentes distintas, y ninguna puede pisar
            // la del propio jefe (ahí vive el área de cualquier otro sistema que marque por él).
            var slot0 = CroupierSectorTelegraph.SlotGuid(_bossGuid, 0);
            var slot1 = CroupierSectorTelegraph.SlotGuid(_bossGuid, 1);

            // Assert
            Assert.AreNotEqual(Guid.Empty, slot0);
            Assert.AreNotEqual(slot0, slot1);
            Assert.AreNotEqual(_bossGuid, slot0);
            Assert.AreNotEqual(_bossGuid, slot1);
            Assert.AreEqual(slot0, CroupierSectorTelegraph.SlotGuid(_bossGuid, 0), "Tiene que ser determinístico.");
        }

        [Test]
        public void CombatEnd_ResetsTheTableToPhaseOne()
        {
            // Arrange — el servicio es Global pero su estado es por combate: una pelea nueva no puede
            // arrancar con la rueda trucada de la anterior.
            _wheel.SetMode(2, rigged: true, phaseIndex: 2);
            _wheel.Sing(new List<int> { 5 });

            // Act
            EventManager.Trigger(EventName.OnCombatEnd);

            // Assert
            Assert.AreEqual(1, _wheel.PhaseIndex);
            Assert.AreEqual(1, _wheel.NumbersPerTurn);
            Assert.IsFalse(_wheel.Rigged);
            Assert.IsEmpty(_wheel.SungNumbers);
        }

        [Test]
        public void SetMode_ClampsToTheAvailableSlots()
        {
            // Act
            _wheel.SetMode(numbersPerTurn: 99, rigged: false, phaseIndex: 2);

            // Assert
            Assert.AreEqual(CroupierSectorTelegraph.MaxSlots, _wheel.NumbersPerTurn);
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private void HitBoss()
        {
            TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                SourceGuid = _playerGuid,
                TargetGuid = _bossGuid,
                FinalDamage = 13,
            });
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
