using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;

namespace Rollgeon.Combat.Status.Tests
{
    /// <summary>
    /// Tests de <see cref="StunTurnSkipper"/> sin FSM: se simula <c>OnTurnStarted</c> a mano y se
    /// verifica qué callback de cierre de turno se invoca y cuándo se consume el stun.
    /// </summary>
    [TestFixture]
    public class StunTurnSkipperTests
    {
        private StunService _stun;
        private StunTurnSkipper _skipper;

        private Guid _playerId;
        private Guid _enemyId;

        private int _endPlayerTurnCalls;
        private int _endEnemyTurnCalls;
        private List<object[]> _expiredLog;
        private EventManager.EventReceiver _onExpired;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _playerId = Guid.NewGuid();
            _enemyId = Guid.NewGuid();
            _endPlayerTurnCalls = 0;
            _endEnemyTurnCalls = 0;

            _expiredLog = new List<object[]>();
            _onExpired = args => _expiredLog.Add(args);
            EventManager.Subscribe(EventName.OnStunExpired, _onExpired);

            _stun = new StunService();
            _stun.ConfigureForTests(() => _playerId);
        }

        [TearDown]
        public void TearDown()
        {
            _skipper?.Dispose();
            _skipper = null;
            _stun?.Dispose();
            _stun = null;
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        private void AttachSkipper(Func<IStunService> resolver = null, bool withEnemyPath = true)
        {
            _skipper = new StunTurnSkipper(
                resolver ?? (() => _stun),
                () => _playerId,
                () => _endPlayerTurnCalls++,
                withEnemyPath ? (Action)(() => _endEnemyTurnCalls++) : null);
            _skipper.Attach();
        }

        // ======================================================================
        // Skip del player
        // ======================================================================

        [Test]
        public void TurnStarted_PlayerStunned_ConsumesOneTurnAndEndsTurn()
        {
            AttachSkipper();
            _stun.ApplyStun(_playerId, 1);

            EventManager.Trigger(EventName.OnTurnStarted, _playerId);

            Assert.AreEqual(1, _endPlayerTurnCalls, "El turno stuneado se cierra por PlayerEndTurn.");
            Assert.AreEqual(0, _endEnemyTurnCalls);
            Assert.IsFalse(_stun.IsStunned(_playerId), "El turno perdido consumio el stun.");
            Assert.AreEqual(1, _expiredLog.Count);
            Assert.AreEqual(1, _skipper.SkipsPerformed);
        }

        [Test]
        public void TurnStarted_PlayerNotStunned_DoesNothing()
        {
            AttachSkipper();

            EventManager.Trigger(EventName.OnTurnStarted, _playerId);

            Assert.AreEqual(0, _endPlayerTurnCalls,
                "Sin stun el turno del player sigue su curso normal.");
            Assert.AreEqual(0, _skipper.SkipsPerformed);
        }

        [Test]
        public void TurnStarted_TwoTurnStun_SkipsTwiceThenPlaysNormally()
        {
            AttachSkipper();
            _stun.ApplyStun(_playerId, 2);

            EventManager.Trigger(EventName.OnTurnStarted, _playerId);
            Assert.AreEqual(1, _endPlayerTurnCalls);
            Assert.AreEqual(1, _stun.GetStunTurns(_playerId));
            Assert.AreEqual(0, _expiredLog.Count);

            EventManager.Trigger(EventName.OnTurnStarted, _playerId);
            Assert.AreEqual(2, _endPlayerTurnCalls);
            Assert.IsFalse(_stun.IsStunned(_playerId));
            Assert.AreEqual(1, _expiredLog.Count);

            // Tercer turno: ya no hay stun, el player juega.
            EventManager.Trigger(EventName.OnTurnStarted, _playerId);
            Assert.AreEqual(2, _endPlayerTurnCalls,
                "Un stun de 2 turnos saltea exactamente 2 turnos.");
        }

        [Test]
        public void TurnStarted_OnlyDecrementsOncePerTurn()
        {
            AttachSkipper();
            _stun.ApplyStun(_playerId, 3);

            EventManager.Trigger(EventName.OnTurnStarted, _playerId);

            Assert.AreEqual(2, _stun.GetStunTurns(_playerId),
                "Un OnTurnStarted consume exactamente 1 turno de stun.");
        }

        // ======================================================================
        // Skip del enemy
        // ======================================================================

        [Test]
        public void TurnStarted_EnemyStunned_UsesEnemyDonePath()
        {
            AttachSkipper();
            _stun.ApplyStun(_enemyId, 1);

            EventManager.Trigger(EventName.OnTurnStarted, _enemyId);

            Assert.AreEqual(1, _endEnemyTurnCalls, "El enemy cierra su turno por EnemyDone.");
            Assert.AreEqual(0, _endPlayerTurnCalls);
            Assert.IsFalse(_stun.IsStunned(_enemyId));
        }

        [Test]
        public void TurnStarted_EnemyStunned_WithoutEnemyPath_DoesNotBurnStun()
        {
            AttachSkipper(withEnemyPath: false);
            _stun.ApplyStun(_enemyId, 1);

            EventManager.Trigger(EventName.OnTurnStarted, _enemyId);

            Assert.AreEqual(0, _endEnemyTurnCalls);
            Assert.IsTrue(_stun.IsStunned(_enemyId),
                "Sin via para cerrar el turno no se saltea NI se consume — el actor jugaria " +
                "gratis y con un turno menos de stun.");
            Assert.AreEqual(0, _skipper.SkipsPerformed);
        }

        // ======================================================================
        // Guards
        // ======================================================================

        [Test]
        public void TurnStarted_WithoutStunService_IsInert()
        {
            AttachSkipper(resolver: () => null);

            Assert.DoesNotThrow(() => EventManager.Trigger(EventName.OnTurnStarted, _playerId));
            Assert.AreEqual(0, _endPlayerTurnCalls,
                "Sin IStunService registrado el flujo de turnos queda intacto.");
        }

        [Test]
        public void TurnStarted_WithMalformedPayload_IsInert()
        {
            AttachSkipper();
            _stun.ApplyStun(_playerId, 1);

            Assert.DoesNotThrow(() =>
            {
                EventManager.Trigger(EventName.OnTurnStarted);
                EventManager.Trigger(EventName.OnTurnStarted, "not-a-guid");
                EventManager.Trigger(EventName.OnTurnStarted, Guid.Empty);
            });

            Assert.AreEqual(0, _endPlayerTurnCalls);
            Assert.AreEqual(1, _stun.GetStunTurns(_playerId), "Ningun payload invalido consume stun.");
        }

        [Test]
        public void Dispose_StopsSkipping()
        {
            AttachSkipper();
            _stun.ApplyStun(_playerId, 2);

            _skipper.Dispose();
            EventManager.Trigger(EventName.OnTurnStarted, _playerId);

            Assert.AreEqual(0, _endPlayerTurnCalls);
            Assert.AreEqual(2, _stun.GetStunTurns(_playerId));
        }

        [Test]
        public void Attach_IsIdempotent_SkipsOncePerTurn()
        {
            AttachSkipper();
            _skipper.Attach();
            _skipper.Attach();
            _stun.ApplyStun(_playerId, 3);

            EventManager.Trigger(EventName.OnTurnStarted, _playerId);

            Assert.AreEqual(1, _endPlayerTurnCalls, "Attach duplicado no debe duplicar el skip.");
            Assert.AreEqual(2, _stun.GetStunTurns(_playerId));
        }
    }
}
