using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Cashier;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Grid;

namespace Rollgeon.Combat.AI.Tests
{
    // Coordenadas de Boss_Room_Cajero: mostrador en Y = 0, aberturas en (±3,0),
    // jefe en (0,2). Sin LoadRoom — al peaje sólo le importa la coordenada.
    [TestFixture]
    public class CajeroCounterTollTests
    {
        private const int CounterRow = 0;
        private const int TollDamage = 20;
        private const int EveryNRounds = 2;

        private static readonly GridCoord BossCoord = new GridCoord(0, 2);
        private static readonly GridCoord PlayerSide = new GridCoord(0, -2);

        /// <summary>Una de las dos aberturas: parado ahí estás en la puerta, no de un lado.</summary>
        private static readonly GridCoord Opening = new GridCoord(3, 0);

        private GridManager _grid;
        private SpyDamagePipeline _pipeline;
        private CashierCounterTollService _toll;
        private Guid _boss;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            ServiceLocator.AddService<IGridManager>(_grid);

            _pipeline = new SpyDamagePipeline();
            ServiceLocator.AddService<IDamagePipeline>(_pipeline);

            _boss = Guid.NewGuid();
            _player = Guid.NewGuid();
            _grid.Register(_boss, BossCoord);
            _grid.Register(_player, PlayerSide);

            _toll = new CashierCounterTollService();
            ServiceLocator.AddService<ICashierCounterTollService>(_toll);
        }

        [TearDown]
        public void TearDown()
        {
            // El servicio se suscribe a EventManager, que ServiceLocator.Clear() no desengancha.
            _toll.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private void Arm() => _toll.Arm(_boss, _player, CounterRow, TollDamage);

        private void ArmIntermittent() => _toll.Arm(_boss, _player, CounterRow, TollDamage, EveryNRounds);

        private void EndTurnOf(Guid entityGuid) =>
            EventManager.Trigger(EventName.OnTurnFinished, entityGuid);

        /// <summary><paramref name="round"/> es 1-based; <c>RoundIndex</c> es 0-based.</summary>
        private void PutPlayerInRound(int round)
        {
            var turnOrder = new TurnOrderService();
            turnOrder.RestoreState(new[] { _player, _boss }, cursor: 0, roundIndex: round - 1);
            ServiceLocator.AddService<TurnOrderService>(turnOrder);
        }

        private void StandOnHisSide() => _grid.Register(_player, new GridCoord(0, 1));

        private static AINode_CashierCounterToll NewNode() => new AINode_CashierCounterToll
        {
            Damage = TollDamage,
            CounterRow = CounterRow,
            ChargesEveryNRounds = EveryNRounds,
        };

        private AIContext NewContext() => new AIContext
        {
            SelfGuid = _boss,
            PlayerGuid = _player,
            Grid = _grid,
            DamagePipeline = _pipeline,
        };

        [Test]
        public void test_toll_playerEndsTurnOnHisSide_chargesTheSheetTen()
        {
            Arm();
            _grid.Register(_player, new GridCoord(1, 1));

            EndTurnOf(_player);

            Assert.AreEqual(1, _pipeline.Resolved.Count, "Un peaje por turno cerrado de su lado.");
            Assert.AreEqual(TollDamage, _pipeline.Resolved[0].BaseDamage);
            Assert.AreEqual(_player, _pipeline.Resolved[0].TargetId);
        }

        [Test]
        public void test_toll_playerEndsTurnAcrossTheCounter_chargesNothing()
        {
            Arm();

            EndTurnOf(_player);

            Assert.IsEmpty(_pipeline.Resolved,
                "Del otro lado del mostrador no se paga: el peaje compra estar cerca, no existir.");
        }

        [Test]
        public void test_toll_playerEndsTurnInAnOpening_chargesNothing()
        {
            Arm();
            _grid.Register(_player, Opening);

            EndTurnOf(_player);

            Assert.IsEmpty(_pipeline.Resolved,
                "La fila del mostrador no es lado: asomarse por la abertura es gratis, quedarse " +
                "adentro no.");
        }

        [Test]
        public void test_toll_chargedDamage_carriesTheBossAndReadsAsEnvironmental()
        {
            Arm();
            _grid.Register(_player, new GridCoord(0, 3));

            EndTurnOf(_player);

            Assert.AreEqual(_boss, _pipeline.Resolved[0].SourceId,
                "El peaje es suyo aunque lo cobre la sala: sin fuente, el log de combate lo " +
                "atribuye a nadie.");
            Assert.AreEqual(AttackKind.Environmental, _pipeline.Resolved[0].Kind,
                "No es un ataque que él tira — es el mostrador cobrando, como una trampa de piso.");
        }

        [Test]
        public void test_toll_onlyThePlayerPays()
        {
            Arm();
            var ally = Guid.NewGuid();
            _grid.Register(ally, new GridCoord(2, 2));

            EndTurnOf(ally);

            Assert.IsEmpty(_pipeline.Resolved, "La ficha cobra el peaje al jugador, no a la sala entera.");
        }

        [Test]
        public void test_toll_beforeTheBossArmsIt_chargesNothing()
        {
            // Turno de apertura del jugador: el árbol del jefe todavía no tickeó.
            _grid.Register(_player, new GridCoord(0, 1));

            EndTurnOf(_player);

            Assert.IsFalse(_toll.IsArmed);
            Assert.IsEmpty(_pipeline.Resolved,
                "Desarmado el servicio es inerte: fuera de la sala del Cajero no hay mostrador.");
        }

        [Test]
        public void test_toll_afterTheBossCrossesToTheOtherSide_followsHim()
        {
            Arm();
            _grid.Register(_boss, new GridCoord(0, -3));

            EndTurnOf(_player);

            Assert.AreEqual(1, _pipeline.Resolved.Count,
                "El lado se resuelve con las posiciones vivas: si él cruza, el lado que cobra es " +
                "el de él, no el que tenía al armarse.");
        }

        [Test]
        public void test_toll_whenTheBossLeftTheGrid_chargesNothing()
        {
            // CombatDeathWatcher saca al muerto de la grilla.
            Arm();
            _grid.Register(_player, new GridCoord(0, 1));
            _grid.Unregister(_boss);

            EndTurnOf(_player);

            Assert.IsEmpty(_pipeline.Resolved, "Un mostrador sin cajero no cobra peaje.");
        }

        [Test]
        public void test_toll_onCombatEnd_disarmsItself()
        {
            Arm();
            _grid.Register(_player, new GridCoord(0, 1));

            EventManager.Trigger(EventName.OnCombatEnd);
            EndTurnOf(_player);

            Assert.IsFalse(_toll.IsArmed);
            Assert.IsEmpty(_pipeline.Resolved,
                "Un peaje que sobreviva a la pelea cobraría en la sala siguiente, sin mostrador " +
                "a la vista.");
        }

        [Test]
        public void test_toll_withoutDamagePipeline_skipsTheChargeWithoutBreakingTheTurn()
        {
            Arm();
            _grid.Register(_player, new GridCoord(0, 1));
            ServiceLocator.Clear();
            ServiceLocator.AddService<IGridManager>(_grid);

            // Un LogError de subscriber hace fallar el test, así que cerrar el turno alcanza.
            EndTurnOf(_player);

            Assert.IsTrue(_toll.IsArmed,
                "Sigue armado: el peaje se saltea el cobro, no se apaga por un servicio ausente.");
        }

        [Test]
        public void test_toll_onAChargingRound_charges()
        {
            ArmIntermittent();
            PutPlayerInRound(2);
            StandOnHisSide();

            EndTurnOf(_player);

            Assert.AreEqual(1, _pipeline.Resolved.Count);
            Assert.AreEqual(TollDamage, _pipeline.Resolved[0].BaseDamage);
        }

        [Test]
        public void test_toll_onTheFreeRound_chargesNothing()
        {
            ArmIntermittent();
            PutPlayerInRound(1);
            StandOnHisSide();

            EndTurnOf(_player);

            Assert.IsEmpty(_pipeline.Resolved,
                "La ronda franca es la ventana para acercarse: sin ella, la respuesta correcta a " +
                "este jefe es no entrar nunca a su lado.");
            Assert.IsTrue(_toll.IsArmed,
                "Franca no es desarmado — la ronda que viene vuelve a cobrar.");
        }

        [Test]
        public void test_toll_theFreeRoundIsNotADiscount_itAlternates()
        {
            ArmIntermittent();
            StandOnHisSide();

            for (int round = 1; round <= 4; round++)
            {
                PutPlayerInRound(round);
                EndTurnOf(_player);
            }

            // Rondas 2 y 4 cobran; 1 y 3 no.
            Assert.AreEqual(2, _pipeline.Resolved.Count,
                "Una de cada dos: quedarse plantado sigue costando, sólo la mitad de seguido.");
        }

        [Test]
        public void test_toll_chargesThisRound_tracksTheCadence_forTheOverlay()
        {
            ArmIntermittent();

            PutPlayerInRound(1);
            Assert.IsFalse(_toll.ChargesThisRound, "Ronda impar: el overlay no pinta nada.");

            PutPlayerInRound(2);
            Assert.IsTrue(_toll.ChargesThisRound, "Ronda par: el lado del jefe se pinta.");
        }

        [Test]
        public void test_toll_disarmed_neverChargesThisRound()
        {
            PutPlayerInRound(2);

            Assert.IsFalse(_toll.ChargesThisRound,
                "Sin armar no hay mostrador, y el overlay no tiene lado que pintar.");
        }

        [Test]
        public void test_toll_withoutTurnOrderService_chargesEveryRound()
        {
            // Sin la cola de turnos no hay ronda que leer.
            ArmIntermittent();
            StandOnHisSide();

            EndTurnOf(_player);

            Assert.AreEqual(1, _pipeline.Resolved.Count,
                "Sin ronda conocida cobra: degrada al comportamiento viejo, no a mudo.");
        }

        [Test]
        public void test_toll_armedWithCadenceOne_chargesEveryRound()
        {
            // Cadencia 1 es el default de Arm: "sin intermitencia".
            Arm();
            StandOnHisSide();

            for (int round = 1; round <= 3; round++)
            {
                PutPlayerInRound(round);
                EndTurnOf(_player);
            }

            Assert.AreEqual(3, _pipeline.Resolved.Count);
            Assert.AreEqual(1, _toll.ChargesEveryNRounds);
        }

        [Test]
        public void test_toll_disarm_resetsTheCadence()
        {
            // El servicio es Global: una cadencia pegada la heredaría el próximo jefe.
            ArmIntermittent();

            _toll.Disarm();

            Assert.AreEqual(1, _toll.ChargesEveryNRounds);
        }

        [TestCase(2, -2, false, TestName = "test_sameSide_acrossTheCounter_isFalse")]
        [TestCase(2, 1, true, TestName = "test_sameSide_bothAboveTheCounter_isTrue")]
        [TestCase(2, 4, true, TestName = "test_sameSide_bothAboveEvenFarApart_isTrue")]
        [TestCase(2, 0, false, TestName = "test_sameSide_standingInTheCounterRow_isFalse")]
        [TestCase(-2, -1, true, TestName = "test_sameSide_bothBelowTheCounter_isTrue")]
        public void test_sameSide_readsTheRealRoomGeometry(int bossRow, int playerRow, bool expected)
        {
            Assert.AreEqual(expected, CashierCounterTollService.IsSameSide(playerRow, bossRow, CounterRow));
        }

        [Test]
        public void test_tollNode_armsTheServiceWithTheAuthoredRowAndDamage()
        {
            var node = NewNode();

            var result = node.Tick(NewContext());

            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.IsTrue(_toll.IsArmed);
            Assert.AreEqual(TollDamage, _toll.TollDamage);
            Assert.AreEqual(CounterRow, _toll.CounterRow);
        }

        [Test]
        public void test_tollNode_armsTheCadence_soTheFreeRoundComesFromTheSheet()
        {
            var node = NewNode();

            node.Tick(NewContext());

            Assert.AreEqual(EveryNRounds, _toll.ChargesEveryNRounds,
                "Sin esto el peaje vuelve a cobrar todas las rondas y acercarse deja de ser posible.");
        }

        [Test]
        public void test_tollNode_fromAnAssetAuthoredBeforeTheField_chargesEveryRound()
        {
            // Odin no corre los inicializadores de campo al deserializar, así que un
            // ED_Boss_Cajero.asset viejo trae ChargesEveryNRounds en 0.
            var node = NewNode();
            node.ChargesEveryNRounds = 0;

            node.Tick(NewContext());

            // Degrada a cobrar todas las rondas, no a un peaje apagado.
            Assert.AreEqual(1, _toll.ChargesEveryNRounds);
            Assert.IsTrue(_toll.IsArmed);
        }

        [Test]
        public void test_tollNode_armsTheServiceEveryTurn_soAResetRecovers()
        {
            var node = NewNode();
            node.Tick(NewContext());
            _toll.Disarm();

            node.Tick(NewContext());

            Assert.IsTrue(_toll.IsArmed,
                "El re-armado por turno es lo que hace que el peaje se recupere solo.");
        }

        [Test]
        public void test_tollNode_withoutPlayerInContext_failsWithoutArming()
        {
            var context = NewContext();
            context.PlayerGuid = Guid.Empty;

            var result = NewNode().Tick(context);

            Assert.AreEqual(AIResult.Failed, result,
                "Failed benigno: en el árbol lo absorbe el Selector[peaje, Wait].");
            Assert.IsFalse(_toll.IsArmed);
        }

        [Test]
        public void test_tollNode_withZeroDamage_failsWithoutArming()
        {
            // Un asset mal autorado no debe dejar un peaje que cobra 0 por turno.
            var node = NewNode();
            node.Damage = 0;

            var result = node.Tick(NewContext());

            Assert.AreEqual(AIResult.Failed, result);
            Assert.IsFalse(_toll.IsArmed);
        }

        [Test]
        public void test_tollNode_nullContext_fails()
        {
            Assert.AreEqual(AIResult.Failed, NewNode().Tick(null));
        }

        [Test]
        public void test_tollNode_createsTheServiceWhenNobodyRegisteredIt()
        {
            // El servicio es lazy: no hay bootstrap que lo levante.
            _toll.Dispose();
            ServiceLocator.Clear();
            ServiceLocator.AddService<IGridManager>(_grid);
            ServiceLocator.AddService<IDamagePipeline>(_pipeline);

            var result = NewNode().Tick(NewContext());

            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.IsTrue(ServiceLocator.TryGetService<ICashierCounterTollService>(out var created) && created != null,
                "El nodo tiene que dejar el servicio registrado — sin él nadie cobra el peaje.");
            Assert.IsTrue(created.IsArmed);

            // Cleanup — el servicio creado por el nodo también está suscripto a EventManager.
            (created as IDisposable)?.Dispose();
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
