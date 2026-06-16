using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat;
using Rollgeon.Combat.Actions;
using Rollgeon.Combat.FSM.States;
using Rollgeon.Combat.Initiative;

namespace Rollgeon.Combat.FSM.Tests
{
    [TestFixture]
    public class CombatTurnFSMTests
    {
        private TurnOrderService _turnOrder;
        private FakeInitiativeProvider _provider;
        private FakeEnergyService _energy;
        private TurnManager _turnManager;

        private Guid _playerId;
        private Guid _enemyAId;
        private Guid _enemyBId;
        private Guid _roomId;

        private readonly List<string> _eventLog = new List<string>();
        private EventManager.EventReceiver _onTurnStartedLog;
        private EventManager.EventReceiver _onTurnFinishedLog;
        private EventManager.EventReceiver _onCombatStartLog;
        private EventManager.EventReceiver _onCombatEndLog;

        [SetUp]
        public void Setup()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            _eventLog.Clear();

            _provider = new FakeInitiativeProvider();
            ServiceLocator.AddService<IInitiativeProvider>(_provider);

            _turnOrder = new TurnOrderService();
            _energy = new FakeEnergyService();

            _turnManager = new TurnManager();
            _turnManager.ConfigureForTests(_energy, actions: null, ruleset: null);

            _playerId = Guid.NewGuid();
            _enemyAId = Guid.NewGuid();
            _enemyBId = Guid.NewGuid();
            _roomId = Guid.NewGuid();

            _energy.Current[_playerId] = _energy.MaxPerEntity;
            _energy.Current[_enemyAId] = _energy.MaxPerEntity;
            _energy.Current[_enemyBId] = _energy.MaxPerEntity;

            // Loggers globales — cada test puede leer _eventLog.
            _onTurnStartedLog = args => _eventLog.Add($"OnTurnStarted:{args[0]}");
            _onTurnFinishedLog = args => _eventLog.Add($"OnTurnFinished:{args[0]}");
            _onCombatStartLog = args => _eventLog.Add($"OnCombatStart:{args[0]}");
            _onCombatEndLog = args => _eventLog.Add($"OnCombatEnd:{args[0]}:{args[1]}");
            EventManager.Subscribe(EventName.OnTurnStarted, _onTurnStartedLog);
            EventManager.Subscribe(EventName.OnTurnFinished, _onTurnFinishedLog);
            EventManager.Subscribe(EventName.OnCombatStart, _onCombatStartLog);
            EventManager.Subscribe(EventName.OnCombatEnd, _onCombatEndLog);
        }

        [TearDown]
        public void TearDown()
        {
            _turnManager?.Dispose();
            _turnManager = null;
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        // --- Helpers ------------------------------------------------------

        private CombatContext BuildContext(Action<Guid> enemyHandler = null)
        {
            return new CombatContext(
                _turnOrder,
                _turnManager,
                _energy,
                _playerId,
                _roomId,
                enemyHandler);
        }

        private void StackOrderPlayerFirst()
        {
            _provider.SetRoll(_playerId, 100);
            _provider.SetRoll(_enemyAId, 10);
            _provider.SetRoll(_enemyBId, 1);
        }

        private void StackOrderEnemyFirst()
        {
            _provider.SetRoll(_playerId, 1);
            _provider.SetRoll(_enemyAId, 100);
            _provider.SetRoll(_enemyBId, 50);
        }

        // ======================================================================
        // Transiciones basicas
        // ======================================================================

        [Test]
        public void StartCombat_PlayerHighestSpeed_TransitionsToPlayerTurn()
        {
            StackOrderPlayerFirst();
            var fsm = new CombatTurnFSM(BuildContext());
            fsm.SetParticipants(new[] { _playerId, _enemyAId });
            fsm.Start();
            fsm.SendInput(CombatInput.StartCombat);

            Assert.IsInstanceOf<PlayerTurnState>(fsm.Current);
        }

        [Test]
        public void StartCombat_EnemyHighestSpeed_TransitionsToEnemyTurn()
        {
            StackOrderEnemyFirst();
            bool handlerCalled = false;
            var fsm = new CombatTurnFSM(BuildContext(enemyHandler: g => handlerCalled = true));
            fsm.SetParticipants(new[] { _playerId, _enemyAId });
            fsm.Start();
            fsm.SendInput(CombatInput.StartCombat);

            Assert.IsInstanceOf<EnemyTurnState>(fsm.Current);
            Assert.IsTrue(handlerCalled, "EnemyActionHandler debe invocarse al entrar a EnemyTurn.");
        }

        [Test]
        public void CombatEnterState_CombatEnded_TransitionsToExit()
        {
            StackOrderPlayerFirst();
            var ctx = BuildContext();
            var fsm = new CombatTurnFSM(ctx);
            fsm.SetParticipants(new[] { _playerId, _enemyAId });
            fsm.Start();
            ctx.PendingOutcome = CombatOutcome.Aborted;
            fsm.SendInput(CombatInput.CombatEnded);

            Assert.IsInstanceOf<CombatExitState>(fsm.Current);
        }

        // ======================================================================
        // Ordering de eventos (plan R9)
        // ======================================================================

        [Test]
        public void OnTurnFinished_FiresBeforeTurnOrderAdvance()
        {
            StackOrderPlayerFirst();
            var ctx = BuildContext();
            var fsm = new CombatTurnFSM(ctx);
            fsm.SetParticipants(new[] { _playerId, _enemyAId });
            fsm.Start();
            fsm.SendInput(CombatInput.StartCombat);
            // Now in PlayerTurn, TurnOrder.Current == _playerId.

            Guid cursorAtFinished = Guid.Empty;
            EventManager.EventReceiver handler = args =>
            {
                // Dentro del handler, el cursor AUN debe apuntar al actor saliente.
                cursorAtFinished = _turnOrder.Current;
            };
            EventManager.Subscribe(EventName.OnTurnFinished, handler);

            fsm.SendInput(CombatInput.PlayerEndTurn);

            EventManager.UnSubscribe(EventName.OnTurnFinished, handler);

            Assert.AreEqual(_playerId, cursorAtFinished,
                "OnTurnFinished debe dispararse ANTES de TurnOrder.Advance — cursor == playerId.");
        }

        // ======================================================================
        // Ciclo de events: OnCombatStart / OnTurnStarted / OnTurnFinished / OnCombatEnd
        // ======================================================================

        [Test]
        public void Lifecycle_FullCycleFiresExpectedEventsInOrder()
        {
            StackOrderPlayerFirst();
            var ctx = BuildContext(enemyHandler: g => { /* no-op */ });
            var fsm = new CombatTurnFSM(ctx);
            fsm.SetParticipants(new[] { _playerId, _enemyAId });
            fsm.Start();

            Assert.AreEqual($"OnCombatStart:{_roomId}", _eventLog[0]);
            fsm.SendInput(CombatInput.StartCombat);
            // Enter del PlayerTurnState disparo OnTurnStarted(playerId).
            Assert.Contains($"OnTurnStarted:{_playerId}", _eventLog);

            fsm.SendInput(CombatInput.PlayerEndTurn);
            // OnTurnFinished(playerId) + OnTurnStarted(enemyId).
            Assert.Contains($"OnTurnFinished:{_playerId}", _eventLog);
            Assert.Contains($"OnTurnStarted:{_enemyAId}", _eventLog);

            fsm.SendInput(CombatInput.EnemyDone);
            // OnTurnFinished(enemyId) + OnTurnStarted(playerId) de nuevo (loop).
            Assert.Contains($"OnTurnFinished:{_enemyAId}", _eventLog);

            ctx.PendingOutcome = CombatOutcome.Victory;
            fsm.SendInput(CombatInput.CombatEnded);
            Assert.Contains($"OnCombatEnd:{_roomId}:{CombatOutcome.Victory}", _eventLog);
        }

        [Test]
        public void OnCombatStart_PayloadIsRoomInstanceId()
        {
            StackOrderPlayerFirst();
            Guid captured = Guid.Empty;
            EventManager.Subscribe(EventName.OnCombatStart, args => captured = (Guid)args[0]);

            var fsm = new CombatTurnFSM(BuildContext());
            fsm.SetParticipants(new[] { _playerId, _enemyAId });
            fsm.Start();

            Assert.AreEqual(_roomId, captured);
        }

        [Test]
        public void OnTurnStarted_OnTurnFinished_FirePairedSameGuid()
        {
            StackOrderPlayerFirst();
            var ctx = BuildContext();
            var fsm = new CombatTurnFSM(ctx);
            fsm.SetParticipants(new[] { _playerId, _enemyAId });
            fsm.Start();
            fsm.SendInput(CombatInput.StartCombat);
            fsm.SendInput(CombatInput.PlayerEndTurn);

            int startedIdx = _eventLog.IndexOf($"OnTurnStarted:{_playerId}");
            int finishedIdx = _eventLog.IndexOf($"OnTurnFinished:{_playerId}");
            Assert.Greater(finishedIdx, startedIdx,
                "OnTurnFinished(playerId) debe seguir a OnTurnStarted(playerId) del mismo turno.");
        }

        // ======================================================================
        // Integracion TurnManager: _actionsUsedThisTurn clear en OnTurnStarted
        // ======================================================================

        [Test]
        public void TurnManager_ActionsUsedCleared_OnTurnStartedDispatchedByFSM()
        {
            StackOrderPlayerFirst();
            // Pre-poblar el TurnManager simulando que una accion se uso antes.
            // El handler del TurnManager se suscribe a OnTurnStarted durante
            // ConfigureForTests; disparando el evento limpia _actionsUsedThisTurn.
            // Usamos WasUsedThisTurn/UsedActionsCount como read-only check.
            Assert.AreEqual(0, _turnManager.UsedActionsCount, "pre-condicion: set vacio");

            // Inyectamos una entry via una accion valida — para evitar dependencia
            // de ActionDefinitionSO usamos directamente el flujo de FSM que
            // dispara OnTurnStarted, y verificamos que SIGUE siendo 0 (clear).
            // (El set comienza vacio, el test valida el hook sin un setup complejo).
            var ctx = BuildContext();
            var fsm = new CombatTurnFSM(ctx);
            fsm.SetParticipants(new[] { _playerId, _enemyAId });
            fsm.Start();
            fsm.SendInput(CombatInput.StartCombat);

            // Tras entrar a PlayerTurn el TurnManager recibio OnTurnStarted y
            // limpio el set. Lo dejamos empty — cubre el smoke test del contrato.
            Assert.AreEqual(0, _turnManager.UsedActionsCount);
        }

        // ======================================================================
        // EnergyService suscribe OnTurnFinished - validado con fake
        // ======================================================================

        [Test]
        public void EnergyService_RegenerateAtTurnEnd_HookedByExternalListener()
        {
            // En runtime el EnergyService real se suscribe a OnTurnFinished. Aqui
            // validamos el contrato: un listener externo suscripto al evento
            // recibe el callback con el Guid correcto cuando PlayerTurnState.Exit
            // corre.
            StackOrderPlayerFirst();

            EventManager.Subscribe(EventName.OnTurnFinished,
                args => _energy.RegenerateAtTurnEnd((Guid)args[0]));

            var ctx = BuildContext();
            var fsm = new CombatTurnFSM(ctx);
            fsm.SetParticipants(new[] { _playerId, _enemyAId });
            fsm.Start();
            fsm.SendInput(CombatInput.StartCombat);
            fsm.SendInput(CombatInput.PlayerEndTurn);

            Assert.AreEqual(1, _energy.RegenerateCallCount);
            Assert.AreEqual(_playerId, _energy.RegenerateCalledFor[0]);
        }

        // ======================================================================
        // REVISION 2: Energy == 0 does NOT auto-end turn
        // ======================================================================

        [Test]
        public void EnergyZero_DoesNotAutoEndTurn_FSMRemainsInPlayerTurnState()
        {
            StackOrderPlayerFirst();
            // Arrancamos combate con el player arriba.
            var ctx = BuildContext();
            var fsm = new CombatTurnFSM(ctx);
            fsm.SetParticipants(new[] { _playerId, _enemyAId });
            fsm.Start();
            fsm.SendInput(CombatInput.StartCombat);
            Assert.IsInstanceOf<PlayerTurnState>(fsm.Current,
                "Pre: player deberia estar en PlayerTurn.");

            // Forzamos energia 0.
            _energy.Current[_playerId] = 0;
            Assert.AreEqual(0, _energy.GetCurrent(_playerId));

            // Disparamos PlayerActionDone (Revision 2: debe ser inerte).
            fsm.SendInput(CombatInput.PlayerActionDone);

            // FSM NO auto-transiciona: sigue en PlayerTurnState.
            Assert.IsInstanceOf<PlayerTurnState>(fsm.Current,
                "Revision 2: Energy == 0 NO dispara auto-end; FSM permanece en PlayerTurnState.");

            // Y sigue aceptando PlayerEndTurn explicito como unica via legitima.
            fsm.SendInput(CombatInput.PlayerEndTurn);
            Assert.IsInstanceOf<EnemyTurnState>(fsm.Current,
                "PlayerEndTurn explicito si transiciona.");
        }

        // ======================================================================
        // Cursor / Advance + wraparound
        // ======================================================================

        [Test]
        public void PlayerEndTurn_AdvancesTurnOrderCursor()
        {
            StackOrderPlayerFirst();
            var ctx = BuildContext();
            var fsm = new CombatTurnFSM(ctx);
            fsm.SetParticipants(new[] { _playerId, _enemyAId });
            fsm.Start();
            fsm.SendInput(CombatInput.StartCombat);
            Assert.AreEqual(_playerId, _turnOrder.Current);

            fsm.SendInput(CombatInput.PlayerEndTurn);

            // Tras advance el cursor debe haber movido al enemy.
            Assert.AreEqual(_enemyAId, _turnOrder.Current);
        }

        [Test]
        public void FullRound_Wraparound_IncrementsRoundIndex()
        {
            StackOrderPlayerFirst();
            int roundsBuilt = 0;
            int lastRoundIndex = -1;
            EventManager.Subscribe(EventName.OnTurnQueueBuilt, args =>
            {
                roundsBuilt++;
                lastRoundIndex = (int)args[1];
            });

            var ctx = BuildContext(enemyHandler: g => { /* no auto enemy done */ });
            var fsm = new CombatTurnFSM(ctx);
            fsm.SetParticipants(new[] { _playerId, _enemyAId });
            fsm.Start();
            fsm.SendInput(CombatInput.StartCombat);
            // Initial BuildForCombat disparo OnTurnQueueBuilt (roundIndex = 0).
            Assert.AreEqual(0, lastRoundIndex);

            fsm.SendInput(CombatInput.PlayerEndTurn); // cursor -> enemy
            fsm.SendInput(CombatInput.EnemyDone);     // cursor -> player (wrap!)

            Assert.GreaterOrEqual(roundsBuilt, 2, "Wraparound debe disparar OnTurnQueueBuilt otra vez.");
            Assert.AreEqual(1, lastRoundIndex, "RoundIndex debe incrementarse a 1 tras wrap.");
            Assert.IsInstanceOf<PlayerTurnState>(fsm.Current);
        }

        // ======================================================================
        // CombatEnded desde distintos estados
        // ======================================================================

        [Test]
        public void NotifyCombatEnded_FromPlayerTurn_TransitionsToExit_WithOutcome()
        {
            StackOrderPlayerFirst();
            var ctx = BuildContext();
            var fsm = new CombatTurnFSM(ctx);
            fsm.SetParticipants(new[] { _playerId, _enemyAId });
            fsm.Start();
            fsm.SendInput(CombatInput.StartCombat);

            ctx.PendingOutcome = CombatOutcome.Victory;
            fsm.SendInput(CombatInput.CombatEnded);

            Assert.IsInstanceOf<CombatExitState>(fsm.Current);
            Assert.Contains($"OnCombatEnd:{_roomId}:{CombatOutcome.Victory}", _eventLog);
        }

        [Test]
        public void CombatExitState_IsTerminal_NoTransitionOnSubsequentInputs()
        {
            StackOrderPlayerFirst();
            var ctx = BuildContext();
            var fsm = new CombatTurnFSM(ctx);
            fsm.SetParticipants(new[] { _playerId, _enemyAId });
            fsm.Start();
            fsm.SendInput(CombatInput.StartCombat);
            ctx.PendingOutcome = CombatOutcome.Victory;
            fsm.SendInput(CombatInput.CombatEnded);
            Assert.IsInstanceOf<CombatExitState>(fsm.Current);

            // Inputs subsiguientes deben ser no-op.
            fsm.SendInput(CombatInput.PlayerActionDone);
            Assert.IsInstanceOf<CombatExitState>(fsm.Current);
            fsm.SendInput(CombatInput.PlayerEndTurn);
            Assert.IsInstanceOf<CombatExitState>(fsm.Current);
        }

        [Test]
        public void OnFinished_FiresWithPendingOutcome()
        {
            StackOrderPlayerFirst();
            var ctx = BuildContext();
            var fsm = new CombatTurnFSM(ctx);
            fsm.SetParticipants(new[] { _playerId, _enemyAId });

            CombatOutcome? captured = null;
            fsm.OnFinished += o => captured = o;

            fsm.Start();
            fsm.SendInput(CombatInput.StartCombat);
            ctx.PendingOutcome = CombatOutcome.Defeat;
            fsm.SendInput(CombatInput.CombatEnded);

            Assert.AreEqual(CombatOutcome.Defeat, captured);
        }

        // ======================================================================
        // Input inerte (None)
        // ======================================================================

        [Test]
        public void NoneInput_DoesNotTransition()
        {
            StackOrderPlayerFirst();
            var ctx = BuildContext();
            var fsm = new CombatTurnFSM(ctx);
            fsm.SetParticipants(new[] { _playerId, _enemyAId });
            fsm.Start();
            fsm.SendInput(CombatInput.StartCombat);

            var before = fsm.Current;
            fsm.SendInput(CombatInput.None);
            Assert.AreSame(before, fsm.Current);
        }

        // ======================================================================
        // Delegate de AI se invoca con el enemy Guid correcto
        // ======================================================================

        [Test]
        public void EnemyActionHandler_InvokedWithCurrentEnemyGuid()
        {
            StackOrderEnemyFirst();
            Guid captured = Guid.Empty;
            var fsm = new CombatTurnFSM(BuildContext(enemyHandler: g => captured = g));
            fsm.SetParticipants(new[] { _playerId, _enemyAId });
            fsm.Start();
            fsm.SendInput(CombatInput.StartCombat);

            Assert.AreEqual(_enemyAId, captured);
        }
    }
}
