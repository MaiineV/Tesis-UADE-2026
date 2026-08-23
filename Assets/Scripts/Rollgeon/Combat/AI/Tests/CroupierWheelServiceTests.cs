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
    // Sala canónica 11×7 ⇒ sectores de 4×3: 1 = x0-3/y4-6, 2 = x4-7/y4-6, 3 = x7-10/y4-6,
    // 4 = x0-3/y0-2, 5 = x4-7/y0-2, 6 = x7-10/y0-2. La columna x=7 es costura: cae en dos sectores.
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
            _grid.Register(_bossGuid, new GridCoord(5, 3));  // La costura: cae con el 2 y con el 5.
            _grid.Register(_playerGuid, new GridCoord(0, 0)); // Sector 4.

            // El corrimiento sólo lo dispara el jugador, y quién es el jugador lo dice este servicio.
            ServiceLocator.AddService<IPlayerService>(new StubPlayerService { PlayerGuid = _playerGuid });

            // Por el camino lazy real: registra Global y se suscribe al fin de combate.
            _wheel = (CroupierWheelService)CroupierWheelService.ResolveOrCreate();
            _wheel.RetaliationDamage = Retaliation;
            _wheel.Bind(_bossGuid);
        }

        [TearDown]
        public void TearDown()
        {
            _wheel.Dispose();

            // Marcar crea GameObject + materiales por tint: sin esto quedan huérfanos y contaminan
            // a cualquier test que los busque por nombre.
            if (ServiceLocator.TryGetService<IThreatOverlayService>(out var overlay) && overlay is IDisposable d)
                d.Dispose();
            var leftover = GameObject.Find("ThreatTelegraphOverlay");
            if (leftover != null) UnityEngine.Object.DestroyImmediate(leftover);

            TypedEvent<DamageResolvedPayload>.Clear();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        [Test]
        public void Hit_WithOddNumberInTheAir_ChargesRetaliation()
        {
            _wheel.Sing(new List<int> { 3 });

            HitBoss();

            Assert.AreEqual(1, _pipeline.Resolved.Count, "La Represalia se cobra una vez por golpe.");
            Assert.AreEqual(_playerGuid, _pipeline.Resolved[0].TargetId, "La cobra el atacante.");
            Assert.AreEqual(_bossGuid, _pipeline.Resolved[0].SourceId);
            Assert.AreEqual(Retaliation, _pipeline.Resolved[0].BaseDamage);
            Assert.AreEqual(AttackKind.Reaction, _pipeline.Resolved[0].Kind);
        }

        [Test]
        public void Hit_WithEvenNumberInTheAir_ChargesRetaliationToo()
        {
            _wheel.Sing(new List<int> { 4 });

            HitBoss();

            Assert.AreEqual(1, _pipeline.Resolved.Count, "En los pares también se cobra.");
            Assert.AreEqual(Retaliation, _pipeline.Resolved[0].BaseDamage);
        }

        [Test]
        public void TwoHitsInTheSameTurn_ChargeTwice()
        {
            // El candado por windup es del corrimiento, no del cobro.
            _wheel.Sing(new List<int> { 3 });

            HitBoss();
            HitBoss();

            Assert.AreEqual(2, _pipeline.Resolved.Count);
            Assert.AreEqual(Retaliation * 2, Total(_pipeline.Resolved));
        }

        [Test]
        public void RiggedWheel_StillChargesRetaliation()
        {
            // La rueda trucada apaga la palanca, no el precio de la casilla de melee.
            _wheel.SetMode(numbersPerTurn: 2, rigged: true, phaseIndex: 2);
            _wheel.Sing(new List<int> { 3, 4 });

            HitBoss();

            Assert.AreEqual(1, _pipeline.Resolved.Count);
            Assert.AreEqual(Retaliation, _pipeline.Resolved[0].BaseDamage);
        }

        [Test]
        public void Hit_OutsideTheWindup_StillChargesRetaliation()
        {
            _wheel.Sing(new List<int> { 3 });
            _wheel.ConsumeWindup();

            HitBoss();

            Assert.AreEqual(1, _pipeline.Resolved.Count);
        }

        [Test]
        public void LethalHit_DoesNotCharge()
        {
            // Sin esto la pelea se puede ganar y perder en el mismo intercambio.
            _wheel.Sing(new List<int> { 3 });

            TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                SourceGuid = _playerGuid,
                TargetGuid = _bossGuid,
                FinalDamage = 27,
                WasLethal = true,
            });

            Assert.IsEmpty(_pipeline.Resolved);
        }

        [Test]
        public void Hit_ThatDealtNoDamageAtAll_DoesNotCharge()
        {
            _wheel.Sing(new List<int> { 3 });

            // Un evento de 0 (esquivado / inmune) lo publica el pipeline igual.
            TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                SourceGuid = _playerGuid,
                TargetGuid = _bossGuid,
                FinalDamage = 0,
                ShieldAbsorbed = 0,
            });

            Assert.IsEmpty(_pipeline.Resolved);
        }

        [Test]
        public void DamageToSomeoneElse_DoesNotCharge()
        {
            _wheel.Sing(new List<int> { 3 });

            TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                SourceGuid = _bossGuid,
                TargetGuid = _playerGuid,
                FinalDamage = 20,
            });

            Assert.IsEmpty(_pipeline.Resolved);
        }

        [Test]
        public void Hit_DoesNotMoveTheWheel()
        {
            _wheel.Sing(new List<int> { 3 });

            HitBoss();

            Assert.AreEqual(new[] { 3 }, _wheel.SungNumbers, "Pegarle no corre la rueda.");
        }

        [Test]
        public void EndTurnInsideTheCalledSector_MovesTheWheel()
        {
            _wheel.Sing(new List<int> { 4 });

            EndPlayerTurn();

            Assert.AreEqual(new[] { 5 }, _wheel.SungNumbers, "El 4 tiene que pasar a 5.");
            Assert.IsEmpty(_pipeline.Resolved, "Correr la rueda con el cuerpo no cobra Represalia.");
        }

        [Test]
        public void EndTurnOutsideTheCalledSector_DoesNothing()
        {
            _wheel.Sing(new List<int> { 3 });

            EndPlayerTurn();

            Assert.AreEqual(new[] { 3 }, _wheel.SungNumbers, "Desde afuera la rueda no se toca.");
        }

        [Test]
        public void SecondTurnEndInsideTheSameWindup_DoesNotMoveItAgain()
        {
            // La costura x=7 cae en el sector 5 y en el 6, así que sin candado correría dos veces.
            MovePlayer(new GridCoord(7, 1));
            _wheel.Sing(new List<int> { 5 });

            EndPlayerTurn();
            EndPlayerTurn();

            Assert.AreEqual(new[] { 6 }, _wheel.SungNumbers, "Un solo corrimiento por número.");
        }

        [Test]
        public void RiggedWheel_DoesNotMove()
        {
            _wheel.SetMode(numbersPerTurn: 2, rigged: true, phaseIndex: 2);
            _wheel.Sing(new List<int> { 4, 5 });

            EndPlayerTurn();

            Assert.AreEqual(new[] { 4, 5 }, _wheel.SungNumbers, "Con la rueda trucada el número no se mueve.");
        }

        [Test]
        public void EndTurnInsideOneOfTwoSectors_MovesOnlyThatNumber()
        {
            // Hoy fase 2 va trucada; esto fija el criterio por-número si se destruca.
            _wheel.SetMode(numbersPerTurn: 2, rigged: false, phaseIndex: 2);
            _wheel.Sing(new List<int> { 4, 3 });

            EndPlayerTurn();

            Assert.AreEqual(new[] { 5, 3 }, _wheel.SungNumbers);
        }

        [Test]
        public void TurnEndOfSomeoneElse_DoesNotMoveTheWheel()
        {
            var otherGuid = Guid.NewGuid();
            _grid.Register(otherGuid, new GridCoord(1, 1)); // Sector 4, igual que el jugador.
            _wheel.Sing(new List<int> { 4 });

            EventManager.Trigger(EventName.OnTurnFinished, otherGuid);

            Assert.AreEqual(new[] { 4 }, _wheel.SungNumbers);
        }

        [Test]
        public void EndTurn_OutsideTheWindup_DoesNothing()
        {
            _wheel.Sing(new List<int> { 4 });
            _wheel.ConsumeWindup();

            EndPlayerTurn();

            Assert.IsEmpty(_wheel.SungNumbers);
        }

        [Test]
        public void Nudge_FromSix_WrapsToOne()
        {
            MovePlayer(new GridCoord(9, 1)); // Sector 6.
            _wheel.Sing(new List<int> { 6 });

            EndPlayerTurn();

            Assert.AreEqual(new[] { 1 }, _wheel.SungNumbers);
        }

        [Test]
        public void Nudge_MovesThePendingAreaToTheNewSector()
        {
            _wheel.Sing(new List<int> { 4 });
            Assert.IsTrue(CroupierSectorTelegraph.Mark(_bossGuid, slot: 0, sector: 4, damage: 20, kind: AttackKind.BasicAttack));
            _wheel.RecordMark(0, 20, AttackKind.BasicAttack);

            EndPlayerTurn();

            var slotGuid = CroupierSectorTelegraph.SlotGuid(_bossGuid, 0);
            var pending = _threat.GetPendingTiles(slotGuid);
            var expected = ThreatAreaShape.ComputeRoomSector(_grid, 5);

            Assert.AreEqual(expected.Count, pending.Count, "El área tiene que ser la del sector 5.");
            foreach (var tile in expected)
                Assert.IsTrue(pending.Contains(tile), $"Falta {tile} del sector 5 en el área pendiente.");
        }

        [Test]
        public void Nudge_KeepsTheMarkedDamage()
        {
            _wheel.Sing(new List<int> { 4 });
            CroupierSectorTelegraph.Mark(_bossGuid, 0, 4, 20, AttackKind.BasicAttack);
            _wheel.RecordMark(0, 20, AttackKind.BasicAttack);

            EndPlayerTurn();

            var slotGuid = CroupierSectorTelegraph.SlotGuid(_bossGuid, 0);
            Assert.IsTrue(_threat.TryConsume(slotGuid, out var area));
            Assert.AreEqual(20, area.Damage);
        }

        [Test]
        public void ConsumeWindup_PublishesTheDetonatedSectors()
        {
            _wheel.SetMode(2, rigged: true, phaseIndex: 2);
            _wheel.Sing(new List<int> { 2, 3 });

            var slots = _wheel.ConsumeWindup();

            Assert.AreEqual(2, slots.Count);
            Assert.AreEqual(new[] { 2, 3 }, _wheel.DetonatedSectors);
            Assert.IsFalse(_wheel.WindupActive, "Detonar cierra el windup.");

            _wheel.ClearDetonated();
            Assert.IsEmpty(_wheel.DetonatedSectors);
        }

        [Test]
        public void SlotGuids_AreStableDistinctAndNeverTheBossGuid()
        {
            // Dos áreas simultáneas necesitan dos fuentes distintas de la del propio jefe.
            var slot0 = CroupierSectorTelegraph.SlotGuid(_bossGuid, 0);
            var slot1 = CroupierSectorTelegraph.SlotGuid(_bossGuid, 1);

            Assert.AreNotEqual(Guid.Empty, slot0);
            Assert.AreNotEqual(slot0, slot1);
            Assert.AreNotEqual(_bossGuid, slot0);
            Assert.AreNotEqual(_bossGuid, slot1);
            Assert.AreEqual(slot0, CroupierSectorTelegraph.SlotGuid(_bossGuid, 0), "Tiene que ser determinístico.");
        }

        [Test]
        public void CombatEnd_ResetsTheTableToPhaseOne()
        {
            // El servicio es Global pero su estado es por combate.
            _wheel.SetMode(2, rigged: true, phaseIndex: 2);
            _wheel.Sing(new List<int> { 5 });

            EventManager.Trigger(EventName.OnCombatEnd);

            Assert.AreEqual(1, _wheel.PhaseIndex);
            Assert.AreEqual(1, _wheel.NumbersPerTurn);
            Assert.IsFalse(_wheel.Rigged);
            Assert.IsEmpty(_wheel.SungNumbers);
        }

        [Test]
        public void CombatEnd_UnhooksBothChannels()
        {
            // Los dos hooks viven fuera del turno del jefe.
            _wheel.Sing(new List<int> { 4 });
            EventManager.Trigger(EventName.OnCombatEnd);

            HitBoss();
            EndPlayerTurn();

            Assert.IsEmpty(_pipeline.Resolved);
            Assert.IsEmpty(_wheel.SungNumbers);
        }

        [Test]
        public void SetMode_ClampsToTheAvailableSlots()
        {
            _wheel.SetMode(numbersPerTurn: 99, rigged: false, phaseIndex: 2);

            Assert.AreEqual(CroupierSectorTelegraph.MaxSlots, _wheel.NumbersPerTurn);
        }

        private void HitBoss()
        {
            TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                SourceGuid = _playerGuid,
                TargetGuid = _bossGuid,
                FinalDamage = 13,
            });
        }

        private void EndPlayerTurn() => EventManager.Trigger(EventName.OnTurnFinished, _playerGuid);

        private void MovePlayer(GridCoord coord) => _grid.Move(_playerGuid, coord);

        private static int Total(List<DamageContext> resolved)
        {
            int sum = 0;
            foreach (var ctx in resolved) sum += ctx.BaseDamage;
            return sum;
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
