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
    /// CNF-006 — grace period configurable antes de CADA acción enemiga.
    /// Cubre el deferral implementado en <see cref="EnemyTurnState"/>: el
    /// <c>EnemyActionHandler</c> no corre en <c>Enter</c> cuando hay delay
    /// configurado, sólo tras acumular <see cref="CombatContext.EnemyActionDelaySeconds"/>
    /// de deltaTime vía <see cref="CombatTurnFSM.Update"/>.
    /// </summary>
    [TestFixture]
    public class EnemyTurnDelayTests
    {
        private TurnOrderService _turnOrder;
        private FakeInitiativeProvider _provider;
        private FakeEnergyService _energy;
        private TurnManager _turnManager;

        private Guid _playerId;
        private Guid _enemyAId;
        private Guid _enemyBId;
        private Guid _roomId;

        private float _fakeDelta;

        [SetUp]
        public void Setup()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

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

            _fakeDelta = 0.5f;
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

        private CombatContext BuildContext(
            Action<Guid> enemyHandler,
            float delaySeconds = 0.8f,
            Func<float> deltaTimeProvider = null)
        {
            return new CombatContext(
                _turnOrder,
                _turnManager,
                _energy,
                _playerId,
                _roomId,
                enemyHandler,
                enemyActionDelaySeconds: delaySeconds,
                deltaTimeProvider: deltaTimeProvider ?? (() => _fakeDelta));
        }

        private void StackOrderPlayerFirst()
        {
            _provider.SetRoll(_playerId, 100);
            _provider.SetRoll(_enemyAId, 10);
            _provider.SetRoll(_enemyBId, 1);
        }

        // ======================================================================
        // Enter no invoca el handler cuando hay grace period configurado.
        // ======================================================================

        [Test]
        public void EnemyTurn_Enter_DoesNotInvokeHandlerImmediately()
        {
            StackOrderPlayerFirst();
            bool invoked = false;
            var fsm = new CombatTurnFSM(BuildContext(g => invoked = true));
            fsm.SetParticipants(new[] { _playerId, _enemyAId });
            fsm.Start();
            fsm.SendInput(CombatInput.StartCombat);
            fsm.SendInput(CombatInput.PlayerEndTurn); // -> EnemyTurnState.Enter

            Assert.IsInstanceOf<EnemyTurnState>(fsm.Current);
            Assert.IsFalse(invoked, "El handler no debe invocarse en Enter cuando hay grace period.");
        }

        [Test]
        public void EnemyTurn_OnTurnStarted_FiresImmediatelyInEnter()
        {
            StackOrderPlayerFirst();
            Guid captured = Guid.Empty;
            EventManager.Subscribe(EventName.OnTurnStarted, args => captured = (Guid)args[0]);

            var fsm = new CombatTurnFSM(BuildContext(g => { }));
            fsm.SetParticipants(new[] { _playerId, _enemyAId });
            fsm.Start();
            fsm.SendInput(CombatInput.StartCombat);
            fsm.SendInput(CombatInput.PlayerEndTurn);

            Assert.AreEqual(_enemyAId, captured,
                "OnTurnStarted debe disparar inmediato, sin esperar el grace period.");
        }

        // ======================================================================
        // Update() acumula deltaTime y dispara el handler una única vez.
        // ======================================================================

        [Test]
        public void EnemyTurn_Update_InvokesHandlerExactlyOnceAfterAccumulatingDelay()
        {
            StackOrderPlayerFirst();
            int invokeCount = 0;
            var fsm = new CombatTurnFSM(BuildContext(g => invokeCount++, delaySeconds: 0.8f));
            fsm.SetParticipants(new[] { _playerId, _enemyAId });
            fsm.Start();
            fsm.SendInput(CombatInput.StartCombat);
            fsm.SendInput(CombatInput.PlayerEndTurn);

            fsm.Update(); // delta 0.5 -> remaining 0.3, no alcanza.
            Assert.AreEqual(0, invokeCount, "1 update (0.5s) no debe alcanzar para 0.8s de delay.");

            fsm.Update(); // delta 0.5 -> remaining -0.2, dispara.
            Assert.AreEqual(1, invokeCount, "2 updates (1.0s acumulado) deben disparar el handler.");

            fsm.Update(); // ya invocado — no debe re-disparar.
            Assert.AreEqual(1, invokeCount, "Updates posteriores no deben re-invocar el handler.");
        }

        // ======================================================================
        // CombatEnded durante el delay invalida el countdown para siempre.
        // ======================================================================

        [Test]
        public void EnemyTurn_CombatEndedDuringDelay_HandlerNeverInvoked_EvenIfTickedAfter()
        {
            StackOrderPlayerFirst();
            int invokeCount = 0;
            var ctx = BuildContext(g => invokeCount++, delaySeconds: 0.8f);
            var fsm = new CombatTurnFSM(ctx);
            fsm.SetParticipants(new[] { _playerId, _enemyAId });
            fsm.Start();
            fsm.SendInput(CombatInput.StartCombat);
            fsm.SendInput(CombatInput.PlayerEndTurn);
            Assert.IsInstanceOf<EnemyTurnState>(fsm.Current, "pre: debe estar en EnemyTurn con delay pendiente.");

            ctx.PendingOutcome = CombatOutcome.Aborted;
            fsm.SendInput(CombatInput.CombatEnded);
            Assert.IsInstanceOf<CombatExitState>(fsm.Current);

            // Seguimos tickeando la FSM (ya en CombatExitState) — el handler jamas debe correr.
            fsm.Update();
            fsm.Update();
            fsm.Update();

            Assert.AreEqual(0, invokeCount, "CombatEnded durante el delay invalida el countdown para siempre.");
        }

        // ======================================================================
        // delay == 0 preserva el path sincrono legacy.
        // ======================================================================

        [Test]
        public void EnemyTurn_ZeroDelay_InvokesHandlerSynchronouslyInEnter_LegacyPath()
        {
            StackOrderPlayerFirst();
            bool invoked = false;
            var fsm = new CombatTurnFSM(BuildContext(g => invoked = true, delaySeconds: 0f));
            fsm.SetParticipants(new[] { _playerId, _enemyAId });
            fsm.Start();
            fsm.SendInput(CombatInput.StartCombat);
            fsm.SendInput(CombatInput.PlayerEndTurn);

            Assert.IsTrue(invoked, "delay=0 debe preservar el path sincrono legacy.");
        }

        // ======================================================================
        // Cadena de 2 enemigos: cada uno espera su propio countdown.
        // ======================================================================

        [Test]
        public void EnemyTurn_TwoChainedEnemies_EachWaitsItsOwnCountdown()
        {
            StackOrderPlayerFirst();
            _provider.SetRoll(_enemyAId, 10);
            _provider.SetRoll(_enemyBId, 5); // enemyA actua antes que enemyB.

            var invokedOrder = new List<Guid>();
            var fsm = new CombatTurnFSM(BuildContext(g => invokedOrder.Add(g), delaySeconds: 0.8f));
            fsm.SetParticipants(new[] { _playerId, _enemyAId, _enemyBId });
            fsm.Start();
            fsm.SendInput(CombatInput.StartCombat);
            fsm.SendInput(CombatInput.PlayerEndTurn); // -> EnemyTurnState (enemyA)

            fsm.Update(); // 0.5s acumulado, no alcanza para enemyA.
            Assert.AreEqual(0, invokedOrder.Count);

            fsm.Update(); // 1.0s acumulado, dispara enemyA.
            CollectionAssert.AreEqual(new[] { _enemyAId }, invokedOrder);

            fsm.SendInput(CombatInput.EnemyDone); // -> EnemyTurnState.Self (enemyB), nuevo countdown.

            fsm.Update(); // 0.5s del countdown de enemyB, no alcanza.
            CollectionAssert.AreEqual(new[] { _enemyAId }, invokedOrder);

            fsm.Update(); // 1.0s acumulado para enemyB, dispara.
            CollectionAssert.AreEqual(new[] { _enemyAId, _enemyBId }, invokedOrder);
        }
    }
}
