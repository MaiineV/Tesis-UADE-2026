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
    /// <summary>
    /// El peaje del mostrador: 20 por terminar el turno del mismo lado que el Cajero. Ficha de
    /// diseño "El Cajero" (piso 2), §El peaje.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Es lo que le pone precio a la abertura. Sin peaje, cruzar el mostrador es gratis y la sala
    /// —que es la mitad del jefe— no muerde: entrás por la puerta que te queda cómoda, le pegás y
    /// salís. Con el peaje, quedarte de su lado cuesta todos los turnos.
    /// </para>
    /// <para>
    /// <b>Coordenadas de la sala real</b> (<c>Boss_Room_Cajero</c>): 11×11 centrada en (0,0), el
    /// mostrador en la fila <c>Y = 0</c> con aberturas en <c>(-3,0)</c> y <c>(3,0)</c>, el jefe
    /// arriba en <c>(0,2)</c>. Las entidades se ubican con <c>Register</c> y sin
    /// <c>LoadRoom</c>: al peaje sólo le importa la coordenada, y hornear el grafo acá ataría el
    /// test a la sala en vez de a la regla.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class CajeroCounterTollTests
    {
        private const int CounterRow = 0;
        // El número de la ficha (CajeroAssetBuilder.CounterTollDamage). No se referencia la
        // constante porque vive en el assembly de Editor y este fixture es de runtime.
        private const int TollDamage = 20;

        /// <summary>Del lado de arriba del mostrador — el lado del jefe.</summary>
        private static readonly GridCoord BossCoord = new GridCoord(0, 2);

        /// <summary>Del lado de abajo — donde entra el jugador.</summary>
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

        // ---- Helpers -----------------------------------------------------

        private void Arm() => _toll.Arm(_boss, _player, CounterRow, TollDamage);

        private void EndTurnOf(Guid entityGuid) =>
            EventManager.Trigger(EventName.OnTurnFinished, entityGuid);

        private static AINode_CashierCounterToll NewNode() => new AINode_CashierCounterToll
        {
            Damage = TollDamage,
            CounterRow = CounterRow,
        };

        private AIContext NewContext() => new AIContext
        {
            SelfGuid = _boss,
            PlayerGuid = _player,
            Grid = _grid,
            DamagePipeline = _pipeline,
        };

        // ---- El cobro ----------------------------------------------------

        [Test]
        public void test_toll_playerEndsTurnOnHisSide_chargesTheSheetTen()
        {
            // Arrange — cruzó por una abertura y se quedó del lado de él.
            Arm();
            _grid.Register(_player, new GridCoord(1, 1));

            // Act
            EndTurnOf(_player);

            // Assert
            Assert.AreEqual(1, _pipeline.Resolved.Count, "Un peaje por turno cerrado de su lado.");
            Assert.AreEqual(TollDamage, _pipeline.Resolved[0].BaseDamage);
            Assert.AreEqual(_player, _pipeline.Resolved[0].TargetId);
        }

        [Test]
        public void test_toll_playerEndsTurnAcrossTheCounter_chargesNothing()
        {
            // Arrange — el jugador se queda de su lado.
            Arm();

            // Act
            EndTurnOf(_player);

            // Assert
            Assert.IsEmpty(_pipeline.Resolved,
                "Del otro lado del mostrador no se paga: el peaje compra estar cerca, no existir.");
        }

        [Test]
        public void test_toll_playerEndsTurnInAnOpening_chargesNothing()
        {
            // Arrange — parado en la puerta, sin comprometerse con ningún lado.
            Arm();
            _grid.Register(_player, Opening);

            // Act
            EndTurnOf(_player);

            // Assert
            Assert.IsEmpty(_pipeline.Resolved,
                "La fila del mostrador no es lado: asomarse por la abertura es gratis, quedarse " +
                "adentro no.");
        }

        [Test]
        public void test_toll_chargedDamage_carriesTheBossAndReadsAsEnvironmental()
        {
            // Arrange
            Arm();
            _grid.Register(_player, new GridCoord(0, 3));

            // Act
            EndTurnOf(_player);

            // Assert
            Assert.AreEqual(_boss, _pipeline.Resolved[0].SourceId,
                "El peaje es suyo aunque lo cobre la sala: sin fuente, el log de combate lo " +
                "atribuye a nadie.");
            Assert.AreEqual(AttackKind.Environmental, _pipeline.Resolved[0].Kind,
                "No es un ataque que él tira — es el mostrador cobrando, como una trampa de piso.");
        }

        [Test]
        public void test_toll_onlyThePlayerPays()
        {
            // Arrange — un refuerzo parado del lado del jefe.
            Arm();
            var ally = Guid.NewGuid();
            _grid.Register(ally, new GridCoord(2, 2));

            // Act
            EndTurnOf(ally);

            // Assert
            Assert.IsEmpty(_pipeline.Resolved, "La ficha cobra el peaje al jugador, no a la sala entera.");
        }

        [Test]
        public void test_toll_beforeTheBossArmsIt_chargesNothing()
        {
            // Arrange — turno de apertura del jugador: el árbol del jefe todavía no tickeó.
            _grid.Register(_player, new GridCoord(0, 1));

            // Act
            EndTurnOf(_player);

            // Assert
            Assert.IsFalse(_toll.IsArmed);
            Assert.IsEmpty(_pipeline.Resolved,
                "Desarmado el servicio es inerte: fuera de la sala del Cajero no hay mostrador.");
        }

        [Test]
        public void test_toll_afterTheBossCrossesToTheOtherSide_followsHim()
        {
            // Arrange — el kiteo lo metió del lado del jugador; el que no se movió es el jugador.
            Arm();
            _grid.Register(_boss, new GridCoord(0, -3));

            // Act
            EndTurnOf(_player);

            // Assert
            Assert.AreEqual(1, _pipeline.Resolved.Count,
                "El lado se resuelve con las posiciones vivas: si él cruza, el lado que cobra es " +
                "el de él, no el que tenía al armarse.");
        }

        [Test]
        public void test_toll_whenTheBossLeftTheGrid_chargesNothing()
        {
            // Arrange — CombatDeathWatcher saca al muerto de la grilla.
            Arm();
            _grid.Register(_player, new GridCoord(0, 1));
            _grid.Unregister(_boss);

            // Act
            EndTurnOf(_player);

            // Assert
            Assert.IsEmpty(_pipeline.Resolved, "Un mostrador sin cajero no cobra peaje.");
        }

        [Test]
        public void test_toll_onCombatEnd_disarmsItself()
        {
            // Arrange
            Arm();
            _grid.Register(_player, new GridCoord(0, 1));

            // Act
            EventManager.Trigger(EventName.OnCombatEnd);
            EndTurnOf(_player);

            // Assert
            Assert.IsFalse(_toll.IsArmed);
            Assert.IsEmpty(_pipeline.Resolved,
                "Un peaje que sobreviva a la pelea cobraría en la sala siguiente, sin mostrador " +
                "a la vista.");
        }

        [Test]
        public void test_toll_withoutDamagePipeline_skipsTheChargeWithoutBreakingTheTurn()
        {
            // Arrange
            Arm();
            _grid.Register(_player, new GridCoord(0, 1));
            ServiceLocator.Clear();
            ServiceLocator.AddService<IGridManager>(_grid);

            // Act — EventManager aísla y loguea lo que lance un subscriber, y un LogError hace
            // fallar el test en el runner: alcanza con cerrar el turno para cubrir el degradado.
            EndTurnOf(_player);

            // Assert
            Assert.IsTrue(_toll.IsArmed,
                "Sigue armado: el peaje se saltea el cobro, no se apaga por un servicio ausente.");
        }

        // ---- La regla del lado --------------------------------------------

        [TestCase(2, -2, false, TestName = "test_sameSide_acrossTheCounter_isFalse")]
        [TestCase(2, 1, true, TestName = "test_sameSide_bothAboveTheCounter_isTrue")]
        [TestCase(2, 4, true, TestName = "test_sameSide_bothAboveEvenFarApart_isTrue")]
        [TestCase(2, 0, false, TestName = "test_sameSide_standingInTheCounterRow_isFalse")]
        [TestCase(-2, -1, true, TestName = "test_sameSide_bothBelowTheCounter_isTrue")]
        public void test_sameSide_readsTheRealRoomGeometry(int bossRow, int playerRow, bool expected)
        {
            // El mostrador de Boss_Room_Cajero está en Y = 0 y el jefe spawnea en Y = 2.
            Assert.AreEqual(expected, CashierCounterTollService.IsSameSide(playerRow, bossRow, CounterRow));
        }

        // ---- El nodo que lo arma ------------------------------------------

        [Test]
        public void test_tollNode_armsTheServiceWithTheAuthoredRowAndDamage()
        {
            // Arrange
            var node = NewNode();

            // Act
            var result = node.Tick(NewContext());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.IsTrue(_toll.IsArmed);
            Assert.AreEqual(TollDamage, _toll.TollDamage);
            Assert.AreEqual(CounterRow, _toll.CounterRow);
        }

        [Test]
        public void test_tollNode_armsTheServiceEveryTurn_soAResetRecovers()
        {
            // Arrange — un fin de combate mal disparado a mitad de pelea deja el peaje mudo.
            var node = NewNode();
            node.Tick(NewContext());
            _toll.Disarm();

            // Act
            node.Tick(NewContext());

            // Assert
            Assert.IsTrue(_toll.IsArmed,
                "El re-armado por turno es lo que hace que el peaje se recupere solo.");
        }

        [Test]
        public void test_tollNode_withoutPlayerInContext_failsWithoutArming()
        {
            // Arrange
            var context = NewContext();
            context.PlayerGuid = Guid.Empty;

            // Act
            var result = NewNode().Tick(context);

            // Assert
            Assert.AreEqual(AIResult.Failed, result,
                "Failed benigno: en el árbol lo absorbe el Selector[peaje, Wait].");
            Assert.IsFalse(_toll.IsArmed);
        }

        [Test]
        public void test_tollNode_withZeroDamage_failsWithoutArming()
        {
            // Arrange — un asset mal autorado no debe dejar un peaje que cobra 0 por turno.
            var node = NewNode();
            node.Damage = 0;

            // Act
            var result = node.Tick(NewContext());

            // Assert
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
            // Arrange — el servicio es lazy: no hay bootstrap que lo levante.
            _toll.Dispose();
            ServiceLocator.Clear();
            ServiceLocator.AddService<IGridManager>(_grid);
            ServiceLocator.AddService<IDamagePipeline>(_pipeline);

            // Act
            var result = NewNode().Tick(NewContext());

            // Assert
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
