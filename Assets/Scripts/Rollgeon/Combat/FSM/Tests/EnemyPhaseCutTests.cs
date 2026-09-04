using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Actions;
using Rollgeon.Combat.FSM.States;
using Rollgeon.Combat.Initiative;

namespace Rollgeon.Combat.FSM.Tests
{
    /// <summary>
    /// Feature#0075 — Segundo Aliento corta la fase enemiga: con
    /// <see cref="CombatContext.EnemyPhaseCutRequested"/> levantado, el próximo
    /// <c>EnemyDone</c> devuelve el turno al jugador y los enemigos que faltaban no actúan.
    /// </summary>
    [TestFixture]
    public class EnemyPhaseCutTests
    {
        private TurnOrderService _turnOrder;
        private FakeInitiativeProvider _provider;
        private FakeRollPoolService _energy;
        private TurnManager _turnManager;

        private Guid _playerId;
        private Guid _enemyAId;
        private Guid _enemyBId;

        private readonly List<string> _eventLog = new List<string>();
        private readonly List<Guid> _handled = new List<Guid>();
        private int _queuesBuilt;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            _eventLog.Clear();
            _handled.Clear();
            _queuesBuilt = 0;

            _provider = new FakeInitiativeProvider();
            ServiceLocator.AddService<IInitiativeProvider>(_provider);
            _turnOrder = new TurnOrderService();
            _energy = new FakeRollPoolService();
            _turnManager = new TurnManager();
            _turnManager.ConfigureForTests(_energy, actions: null, ruleset: null);

            _playerId = Guid.NewGuid();
            _enemyAId = Guid.NewGuid();
            _enemyBId = Guid.NewGuid();
            _energy.Current[_playerId] = _energy.RollsPerTurn;
            _energy.Current[_enemyAId] = _energy.RollsPerTurn;
            _energy.Current[_enemyBId] = _energy.RollsPerTurn;

            EventManager.Subscribe(EventName.OnTurnStarted, args => _eventLog.Add($"Started:{Name(args[0])}"));
            EventManager.Subscribe(EventName.OnTurnFinished, args => _eventLog.Add($"Finished:{Name(args[0])}"));
            EventManager.Subscribe(EventName.OnTurnQueueBuilt, _ => _queuesBuilt++);
        }

        [TearDown]
        public void TearDown()
        {
            _turnManager?.Dispose();
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        private string Name(object guid)
        {
            if (!(guid is Guid g)) return "?";
            if (g == _playerId) return "player";
            if (g == _enemyAId) return "A";
            if (g == _enemyBId) return "B";
            return "?";
        }

        /// <summary>
        /// Arranca un combate con el orden dado y deja la FSM en el turno de A (el primer
        /// enemigo). El handler enemigo registra quién actuó y, si es A, pide el corte.
        /// </summary>
        private (CombatTurnFSM fsm, CombatContext ctx) StartWithCutOnA(bool playerFirst)
        {
            if (playerFirst)
            {
                _provider.SetRoll(_playerId, 100);
                _provider.SetRoll(_enemyAId, 10);
                _provider.SetRoll(_enemyBId, 1);
            }
            else
            {
                _provider.SetRoll(_enemyAId, 100);
                _provider.SetRoll(_enemyBId, 50);
                _provider.SetRoll(_playerId, 1);
            }

            CombatContext ctx = null;
            ctx = new CombatContext(_turnOrder, _turnManager, _energy, _playerId, Guid.NewGuid(), g =>
            {
                _handled.Add(g);
                // Simula el OnSecondWindTriggered que dispara la Ficha en medio del golpe de A.
                if (g == _enemyAId) ctx.EnemyPhaseCutRequested = true;
            });
            var fsm = new CombatTurnFSM(ctx);
            fsm.SetParticipants(new[] { _playerId, _enemyAId, _enemyBId });
            fsm.Start();
            fsm.SendInput(CombatInput.StartCombat);
            if (fsm.Current is PlayerTurnState) fsm.SendInput(CombatInput.PlayerEndTurn);

            Assert.IsInstanceOf<EnemyTurnState>(fsm.Current, "precondición: A está actuando");
            Assert.AreEqual(_enemyAId, _turnOrder.Current, "precondición: el cursor está en A");
            return (fsm, ctx);
        }

        [Test]
        public void Cut_WithEnemiesRemaining_ReturnsToPlayer_AndSkipsThem()
        {
            // Orden: A, B, player — B queda salteado.
            var (fsm, ctx) = StartWithCutOnA(playerFirst: false);
            _eventLog.Clear();

            fsm.SendInput(CombatInput.EnemyDone);

            Assert.IsInstanceOf<PlayerTurnState>(fsm.Current);
            Assert.AreEqual(_playerId, _turnOrder.Current, "el cursor tiene que quedar en el jugador");
            CollectionAssert.DoesNotContain(_handled, _enemyBId, "B no actúa");
            CollectionAssert.AreEqual(new[] { "Finished:A", "Started:player" }, _eventLog,
                "B no recibe OnTurnStarted/Finished");
            Assert.IsFalse(ctx.EnemyPhaseCutRequested, "el flag se consume");
        }

        [Test]
        public void Cut_ThatWrapsTheRound_IncrementsRoundIndexOnce()
        {
            // Orden: player, A, B — el corte en A salta B y da la vuelta.
            var (fsm, _) = StartWithCutOnA(playerFirst: true);
            int roundBefore = _turnOrder.RoundIndex;
            int queuesBefore = _queuesBuilt;

            fsm.SendInput(CombatInput.EnemyDone);

            Assert.IsInstanceOf<PlayerTurnState>(fsm.Current);
            Assert.AreEqual(_playerId, _turnOrder.Current);
            Assert.AreEqual(roundBefore + 1, _turnOrder.RoundIndex, "una sola vuelta");
            Assert.AreEqual(queuesBefore + 1, _queuesBuilt, "OnTurnQueueBuilt una vez, como en cualquier wrap");
            CollectionAssert.DoesNotContain(_handled, _enemyBId);
        }

        [Test]
        public void Cut_WhenPlayerWasNextAnyway_BehavesLikeANormalTurn()
        {
            // Orden: B, A, player — corte en A, pero A ya era el último enemigo.
            _provider.SetRoll(_enemyBId, 100);
            _provider.SetRoll(_enemyAId, 50);
            _provider.SetRoll(_playerId, 1);
            CombatContext ctx = null;
            ctx = new CombatContext(_turnOrder, _turnManager, _energy, _playerId, Guid.NewGuid(), g =>
            {
                _handled.Add(g);
                if (g == _enemyAId) ctx.EnemyPhaseCutRequested = true;
            });
            var fsm = new CombatTurnFSM(ctx);
            fsm.SetParticipants(new[] { _playerId, _enemyAId, _enemyBId });
            fsm.Start();
            fsm.SendInput(CombatInput.StartCombat);
            if (fsm.Current is PlayerTurnState) fsm.SendInput(CombatInput.PlayerEndTurn);
            Assert.AreEqual(_enemyBId, _turnOrder.Current, "precondición: B actúa primero");
            fsm.SendInput(CombatInput.EnemyDone); // B → A (A pide el corte al actuar)
            Assert.AreEqual(_enemyAId, _turnOrder.Current);
            int roundBefore = _turnOrder.RoundIndex;
            int queuesBefore = _queuesBuilt;
            // El servicio decide dónde queda el jugador en el orden; un Advance único
            // wrappea (RoundIndex+1) solo si el jugador está en el índice 0.
            var order = new List<Guid>(_turnOrder.OrderForRound);
            int expectedWraps = order.IndexOf(_playerId) == 0 ? 1 : 0;

            fsm.SendInput(CombatInput.EnemyDone);

            Assert.IsInstanceOf<PlayerTurnState>(fsm.Current);
            Assert.AreEqual(_playerId, _turnOrder.Current);
            Assert.AreEqual(roundBefore + expectedWraps, _turnOrder.RoundIndex, "idéntico a un Advance normal");
            Assert.AreEqual(queuesBefore + expectedWraps, _queuesBuilt);
            Assert.IsFalse(ctx.EnemyPhaseCutRequested);
        }

        [Test]
        public void CombatEnded_WithCutPending_ExitsWithoutAdvancing()
        {
            var (fsm, ctx) = StartWithCutOnA(playerFirst: false);
            ctx.PendingOutcome = CombatOutcome.Aborted;

            fsm.SendInput(CombatInput.CombatEnded);

            Assert.IsNotInstanceOf<PlayerTurnState>(fsm.Current);
            Assert.IsNotInstanceOf<EnemyTurnState>(fsm.Current);
            Assert.IsFalse(ctx.EnemyPhaseCutRequested, "el flag no sobrevive al fin del combate");
            CollectionAssert.DoesNotContain(_handled, _enemyBId);
        }

        [Test]
        public void WithoutCut_EnemyChainIsUnchanged()
        {
            _provider.SetRoll(_enemyAId, 100);
            _provider.SetRoll(_enemyBId, 50);
            _provider.SetRoll(_playerId, 1);
            var ctx = new CombatContext(_turnOrder, _turnManager, _energy, _playerId, Guid.NewGuid(), g => _handled.Add(g));
            var fsm = new CombatTurnFSM(ctx);
            fsm.SetParticipants(new[] { _playerId, _enemyAId, _enemyBId });
            fsm.Start();
            fsm.SendInput(CombatInput.StartCombat);
            if (fsm.Current is PlayerTurnState) fsm.SendInput(CombatInput.PlayerEndTurn);

            fsm.SendInput(CombatInput.EnemyDone); // A → B

            Assert.IsInstanceOf<EnemyTurnState>(fsm.Current);
            Assert.AreEqual(_enemyBId, _turnOrder.Current);
            CollectionAssert.AreEqual(new[] { _enemyAId, _enemyBId }, _handled);
        }
    }
}
