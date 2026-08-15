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
    /// Tests de <see cref="CroupierWheelService"/>: los dos hooks del jefe de piso 1, que ahora son
    /// dos cosas distintas. Pegarle cuesta 8 siempre (Represalia), y correr la rueda +1 se paga con el
    /// cuerpo — terminando el turno dentro del sector cantado, una sola vez por número, y nunca con la
    /// rueda trucada.
    /// </summary>
    /// <remarks>
    /// La sala canónica es 11×7, así que los sectores son de 4×3: 1 = x0-3/y4-6, 2 = x4-7/y4-6,
    /// 3 = x7-10/y4-6, 4 = x0-3/y0-2, 5 = x4-7/y0-2, 6 = x7-10/y0-2. La columna x=7 es la costura:
    /// pertenece a la vez al bloque del medio y al de la derecha.
    /// </remarks>
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
            _grid.Register(_playerGuid, new GridCoord(0, 0)); // Sector 4.

            // El corrimiento sólo lo dispara el jugador, y quién es el jugador lo dice este servicio.
            ServiceLocator.AddService<IPlayerService>(new StubPlayerService { PlayerGuid = _playerGuid });

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
        // La Represalia: pegarle cuesta 8, siempre
        // =====================================================================

        [Test]
        public void Hit_WithOddNumberInTheAir_ChargesRetaliation()
        {
            // Arrange
            _wheel.Sing(new List<int> { 3 });

            // Act
            HitBoss();

            // Assert
            Assert.AreEqual(1, _pipeline.Resolved.Count, "La Represalia se cobra una vez por golpe.");
            Assert.AreEqual(_playerGuid, _pipeline.Resolved[0].TargetId, "La cobra el atacante.");
            Assert.AreEqual(_bossGuid, _pipeline.Resolved[0].SourceId);
            Assert.AreEqual(Retaliation, _pipeline.Resolved[0].BaseDamage);
            Assert.AreEqual(AttackKind.Reaction, _pipeline.Resolved[0].Kind);
        }

        [Test]
        public void Hit_WithEvenNumberInTheAir_ChargesRetaliationToo()
        {
            // Arrange — la paridad ya no descuenta: era la regla invisible que hacía que la mitad de
            // los turnos pegarle fuera gratis sin que nada en pantalla lo dijera.
            _wheel.Sing(new List<int> { 4 });

            // Act
            HitBoss();

            // Assert
            Assert.AreEqual(1, _pipeline.Resolved.Count, "En los pares también se cobra.");
            Assert.AreEqual(Retaliation, _pipeline.Resolved[0].BaseDamage);
        }

        [Test]
        public void TwoHitsInTheSameTurn_ChargeTwice()
        {
            // Arrange — el candado es del corrimiento, no del cobro: cada golpe es una decisión aparte
            // y se paga aparte.
            _wheel.Sing(new List<int> { 3 });

            // Act
            HitBoss();
            HitBoss();

            // Assert
            Assert.AreEqual(2, _pipeline.Resolved.Count);
            Assert.AreEqual(Retaliation * 2, Total(_pipeline.Resolved));
        }

        [Test]
        public void RiggedWheel_StillChargesRetaliation()
        {
            // Arrange — fase 2: la rueda trucada apaga la palanca, no el precio de la casilla de melee.
            // Si también apagara el cobro, el jefe se quedaría sin daño directo justo en su fase fuerte.
            _wheel.SetMode(numbersPerTurn: 2, rigged: true, phaseIndex: 2);
            _wheel.Sing(new List<int> { 3, 4 });

            // Act
            HitBoss();

            // Assert
            Assert.AreEqual(1, _pipeline.Resolved.Count);
            Assert.AreEqual(Retaliation, _pipeline.Resolved[0].BaseDamage);
        }

        [Test]
        public void Hit_OutsideTheWindup_StillChargesRetaliation()
        {
            // Arrange — "siempre" incluye el hueco entre detonar y volver a cantar. Atarlo al windup
            // dejaría golpes gratis que el jugador no puede ver ni predecir.
            _wheel.Sing(new List<int> { 3 });
            _wheel.ConsumeWindup();

            // Act
            HitBoss();

            // Assert
            Assert.AreEqual(1, _pipeline.Resolved.Count);
        }

        [Test]
        public void LethalHit_DoesNotCharge()
        {
            // Arrange — un crupier muerto no manotea: sin esto la pelea se puede ganar y perder en el
            // mismo intercambio.
            _wheel.Sing(new List<int> { 3 });

            // Act
            TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                SourceGuid = _playerGuid,
                TargetGuid = _bossGuid,
                FinalDamage = 27,
                WasLethal = true,
            });

            // Assert
            Assert.IsEmpty(_pipeline.Resolved);
        }

        [Test]
        public void Hit_ThatDealtNoDamageAtAll_DoesNotCharge()
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
            Assert.IsEmpty(_pipeline.Resolved);
        }

        [Test]
        public void DamageToSomeoneElse_DoesNotCharge()
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
            Assert.IsEmpty(_pipeline.Resolved);
        }

        [Test]
        public void Hit_DoesNotMoveTheWheel()
        {
            // Arrange — la regresión que motivó el cambio: mover el número era un efecto secundario
            // gratis del único ataque que el jugador tiene.
            _wheel.Sing(new List<int> { 3 });

            // Act
            HitBoss();

            // Assert
            Assert.AreEqual(new[] { 3 }, _wheel.SungNumbers, "Pegarle no corre la rueda.");
        }

        // =====================================================================
        // El corrimiento: se paga con el cuerpo
        // =====================================================================

        [Test]
        public void EndTurnInsideTheCalledSector_MovesTheWheel()
        {
            // Arrange — el jugador está en el sector 4 y el jefe canta el 4: pararse bajo el hacha.
            _wheel.Sing(new List<int> { 4 });

            // Act
            EndPlayerTurn();

            // Assert
            Assert.AreEqual(new[] { 5 }, _wheel.SungNumbers, "El 4 tiene que pasar a 5.");
            Assert.IsEmpty(_pipeline.Resolved, "Correr la rueda con el cuerpo no cobra Represalia.");
        }

        [Test]
        public void EndTurnOutsideTheCalledSector_DoesNothing()
        {
            // Arrange — el jugador está en el sector 4 y el número cantado es el 3.
            _wheel.Sing(new List<int> { 3 });

            // Act
            EndPlayerTurn();

            // Assert
            Assert.AreEqual(new[] { 3 }, _wheel.SungNumbers, "Desde afuera la rueda no se toca.");
        }

        [Test]
        public void SecondTurnEndInsideTheSameWindup_DoesNotMoveItAgain()
        {
            // Arrange — la costura (x=7) pertenece al sector 5 y al 6 a la vez, así que sin el candado
            // el jugador parado ahí correría el número dos veces con el mismo cuerpo.
            MovePlayer(new GridCoord(7, 1));
            _wheel.Sing(new List<int> { 5 });

            // Act
            EndPlayerTurn();
            EndPlayerTurn();

            // Assert
            Assert.AreEqual(new[] { 6 }, _wheel.SungNumbers, "Un solo corrimiento por número.");
        }

        [Test]
        public void RiggedWheel_DoesNotMove()
        {
            // Arrange — fase 2: la palanca desaparece aunque el jugador se pare adentro.
            _wheel.SetMode(numbersPerTurn: 2, rigged: true, phaseIndex: 2);
            _wheel.Sing(new List<int> { 4, 5 });

            // Act
            EndPlayerTurn();

            // Assert
            Assert.AreEqual(new[] { 4, 5 }, _wheel.SungNumbers, "Con la rueda trucada el número no se mueve.");
        }

        [Test]
        public void EndTurnInsideOneOfTwoSectors_MovesOnlyThatNumber()
        {
            // Arrange — el criterio es por número, no por turno: el jugador corre el hacha bajo la que
            // se paró, no las dos. (Hoy fase 2 va trucada; esto fija el criterio si se destruca.)
            _wheel.SetMode(numbersPerTurn: 2, rigged: false, phaseIndex: 2);
            _wheel.Sing(new List<int> { 4, 3 });

            // Act
            EndPlayerTurn();

            // Assert
            Assert.AreEqual(new[] { 5, 3 }, _wheel.SungNumbers);
        }

        [Test]
        public void TurnEndOfSomeoneElse_DoesNotMoveTheWheel()
        {
            // Arrange — la rueda la corre el jugador con su cuerpo, no cualquier cosa que cierre turno
            // dentro del bloque.
            var otherGuid = Guid.NewGuid();
            _grid.Register(otherGuid, new GridCoord(1, 1)); // Sector 4, igual que el jugador.
            _wheel.Sing(new List<int> { 4 });

            // Act
            EventManager.Trigger(EventName.OnTurnFinished, otherGuid);

            // Assert
            Assert.AreEqual(new[] { 4 }, _wheel.SungNumbers);
        }

        [Test]
        public void EndTurn_OutsideTheWindup_DoesNothing()
        {
            // Arrange — el windup se cierra al detonar.
            _wheel.Sing(new List<int> { 4 });
            _wheel.ConsumeWindup();

            // Act
            EndPlayerTurn();

            // Assert
            Assert.IsEmpty(_wheel.SungNumbers);
        }

        [Test]
        public void Nudge_FromSix_WrapsToOne()
        {
            // Arrange — es una rueda, no una escalera.
            MovePlayer(new GridCoord(9, 1)); // Sector 6.
            _wheel.Sing(new List<int> { 6 });

            // Act
            EndPlayerTurn();

            // Assert
            Assert.AreEqual(new[] { 1 }, _wheel.SungNumbers);
        }

        // =====================================================================
        // El corrimiento mueve la marca
        // =====================================================================

        [Test]
        public void Nudge_MovesThePendingAreaToTheNewSector()
        {
            // Arrange — si el área no se moviera, la palanca no cambiaría nada de lo que va a pasar.
            _wheel.Sing(new List<int> { 4 });
            Assert.IsTrue(CroupierSectorTelegraph.Mark(_bossGuid, slot: 0, sector: 4, damage: 20, kind: AttackKind.BasicAttack));
            _wheel.RecordMark(0, 20, AttackKind.BasicAttack);

            // Act
            EndPlayerTurn();

            // Assert
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
            // Arrange
            _wheel.Sing(new List<int> { 4 });
            CroupierSectorTelegraph.Mark(_bossGuid, 0, 4, 20, AttackKind.BasicAttack);
            _wheel.RecordMark(0, 20, AttackKind.BasicAttack);

            // Act
            EndPlayerTurn();

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
        public void CombatEnd_UnhooksBothChannels()
        {
            // Arrange — los dos hooks viven fuera del turno del jefe, así que si sobrevivieran al
            // combate seguirían cobrando y corriendo una rueda que ya no existe.
            _wheel.Sing(new List<int> { 4 });
            EventManager.Trigger(EventName.OnCombatEnd);

            // Act
            HitBoss();
            EndPlayerTurn();

            // Assert
            Assert.IsEmpty(_pipeline.Resolved);
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
