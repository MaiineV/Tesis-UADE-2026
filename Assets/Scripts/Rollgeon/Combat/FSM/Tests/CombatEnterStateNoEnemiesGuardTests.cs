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
    /// BUG-078: si <c>DefaultEnemySpawnResolver</c> no logra resolver ningún enemigo en el
    /// resume (ej. el boss se pierde en <c>LookupEnemyData</c>), <c>CachedParticipants</c>
    /// llega a <see cref="CombatEnterState"/> con SOLO el player — sin este guard, la FSM
    /// arma una cola de 1 y el player cicla su propio turno para siempre (softlock, más
    /// grave en boss room porque <c>EffForceDoor</c> bloquea el escape).
    /// </summary>
    [TestFixture]
    public class CombatEnterStateNoEnemiesGuardTests
    {
        private TurnOrderService _turnOrder;
        private FakeInitiativeProvider _provider;
        private FakeRollPoolService _energy;
        private TurnManager _turnManager;

        private Guid _playerId;
        private Guid _enemyId;
        private Guid _roomId;

        private readonly List<string> _eventLog = new List<string>();

        [SetUp]
        public void Setup()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            _eventLog.Clear();

            _provider = new FakeInitiativeProvider();
            ServiceLocator.AddService<IInitiativeProvider>(_provider);

            _turnOrder = new TurnOrderService();
            _energy = new FakeRollPoolService();

            _turnManager = new TurnManager();
            _turnManager.ConfigureForTests(_energy, actions: null, ruleset: null);

            _playerId = Guid.NewGuid();
            _enemyId = Guid.NewGuid();
            _roomId = Guid.NewGuid();

            _energy.Current[_playerId] = _energy.RollsPerTurn;
            _energy.Current[_enemyId] = _energy.RollsPerTurn;

            EventManager.Subscribe(EventName.OnCombatEnd,
                args => _eventLog.Add($"OnCombatEnd:{args[0]}:{args[1]}"));
            EventManager.Subscribe(EventName.OnCombatStart,
                args => _eventLog.Add($"OnCombatStart:{args[0]}"));
            EventManager.Subscribe(EventName.OnTurnQueueBuilt,
                args => _eventLog.Add("OnTurnQueueBuilt"));
        }

        [TearDown]
        public void TearDown()
        {
            _turnManager?.Dispose();
            _turnManager = null;
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

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

        [Test]
        public void StartCombat_ParticipantsOnlyPlayer_ClosesCombatAsAbortedInsteadOfEnteringTurnLoop()
        {
            // Arrange
            var fsm = new CombatTurnFSM(BuildContext());
            fsm.SetParticipants(new[] { _playerId });

            // Act
            fsm.Start();
            fsm.SendInput(CombatInput.StartCombat);

            // Assert
            Assert.IsInstanceOf<CombatExitState>(fsm.Current,
                "sin enemigos, StartCombat debe cerrar el combate en vez de ir a PlayerTurn/EnemyTurn.");
            Assert.AreEqual(CombatOutcome.Aborted, fsm.Context.PendingOutcome,
                "el cierre defensivo debe marcar Aborted (no se peleó nada).");
        }

        [Test]
        public void StartCombat_ParticipantsOnlyPlayer_FiresOnCombatEndWithAbortedOutcome()
        {
            // Arrange
            var fsm = new CombatTurnFSM(BuildContext());
            fsm.SetParticipants(new[] { _playerId });

            // Act
            fsm.Start();
            fsm.SendInput(CombatInput.StartCombat);

            // Assert
            Assert.AreEqual(1, _eventLog.Count, "OnCombatEnd debe dispararse exactamente una vez.");
            StringAssert.Contains($"OnCombatEnd:{_roomId}:{CombatOutcome.Aborted}", _eventLog[0]);
        }

        [Test]
        public void StartCombat_ParticipantsOnlyPlayer_DoesNotBuildTurnOrderQueue()
        {
            // Arrange
            var fsm = new CombatTurnFSM(BuildContext());
            fsm.SetParticipants(new[] { _playerId });

            // Act
            fsm.Start();
            fsm.SendInput(CombatInput.StartCombat);

            // Assert — la cola nunca se armó. TurnOrderService.Current lanza si no hay
            // participantes (el assert viejo comparaba contra Guid.Empty y quedó roto
            // cuando Current se volvió throwing): la señal correcta es el count en 0.
            Assert.AreEqual(0, _turnOrder.ParticipantCount,
                "BuildForCombat no debió correr para una cola sin enemigos.");
            Assert.Throws<InvalidOperationException>(() => _ = _turnOrder.Current,
                "sin cola armada, Current debe seguir siendo un estado inválido explícito.");
        }

        [Test]
        public void StartCombat_ParticipantsOnlyPlayer_DoesNotEmitOnCombatStart()
        {
            // Arrange
            var fsm = new CombatTurnFSM(BuildContext());
            fsm.SetParticipants(new[] { _playerId });

            // Act
            fsm.Start();
            fsm.SendInput(CombatInput.StartCombat);

            // Assert — el abort defensivo NO debe emitir OnCombatStart: un par
            // start/end en el mismo frame deja a RollPoolService con _inCombat=false
            // y _current=0 (BUG-078: sin UI de energía + acciones gratis).
            Assert.IsFalse(_eventLog.Exists(e => e.StartsWith("OnCombatStart")),
                "el cierre defensivo no debe emitir OnCombatStart.");
        }

        [Test]
        public void StartCombat_WithEnemyParticipant_EmitsOnCombatStartOnceBeforeQueueBuilt()
        {
            // Arrange
            _provider.SetRoll(_playerId, 100);
            _provider.SetRoll(_enemyId, 10);
            var fsm = new CombatTurnFSM(BuildContext());
            fsm.SetParticipants(new[] { _playerId, _enemyId });

            // Act
            fsm.Start();
            fsm.SendInput(CombatInput.StartCombat);

            // Assert — contrato de orden: listeners de "combat init" esperan
            // OnCombatStart antes del turn queue wiring.
            int startCount = _eventLog.FindAll(e => e.StartsWith("OnCombatStart")).Count;
            Assert.AreEqual(1, startCount, "OnCombatStart debe emitirse exactamente una vez.");
            int startIndex = _eventLog.FindIndex(e => e.StartsWith("OnCombatStart"));
            int queueIndex = _eventLog.FindIndex(e => e == "OnTurnQueueBuilt");
            Assert.GreaterOrEqual(queueIndex, 0, "el camino feliz debe armar la cola de turnos.");
            Assert.Less(startIndex, queueIndex,
                "OnCombatStart debe emitirse ANTES de OnTurnQueueBuilt.");
        }

        [Test]
        public void StartCombat_WithEnemyParticipant_StillReachesPlayerTurn_RegressionGuard()
        {
            // Arrange — mismo contexto que el resto de la suite, para probar que el guard
            // nuevo no rompe el camino normal (al menos un enemigo además del player).
            _provider.SetRoll(_playerId, 100);
            _provider.SetRoll(_enemyId, 10);
            var fsm = new CombatTurnFSM(BuildContext());
            fsm.SetParticipants(new[] { _playerId, _enemyId });

            // Act
            fsm.Start();
            fsm.SendInput(CombatInput.StartCombat);

            // Assert
            Assert.IsInstanceOf<PlayerTurnState>(fsm.Current,
                "con un enemigo real en la lista, el combate debe seguir arrancando normal.");
        }
    }
}
