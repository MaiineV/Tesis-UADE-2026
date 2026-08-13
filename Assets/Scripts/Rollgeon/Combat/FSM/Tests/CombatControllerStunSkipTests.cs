using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Actions;
using Rollgeon.Combat.EnergyLib;
using Rollgeon.Combat.Initiative;
using Rollgeon.Combat.Status;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Combat.FSM.Tests
{
    /// <summary>
    /// Integración del skip por stun con la FSM real (mismo harness reflectivo que
    /// <see cref="CombatControllerFreezeTests"/>: host inactivo + Awake invocado a mano).
    /// Verifica que el turno stuneado se cierra por el input normal, que
    /// <c>OnTurnFinished</c> sigue saliendo (la UI de End Turn se re-deshabilita sola) y que
    /// sin <c>IStunService</c> registrado nada cambia.
    /// </summary>
    [TestFixture]
    public class CombatControllerStunSkipTests
    {
        private GameObject _host;
        private CombatController _controller;

        private FakeEnergyService _energy;
        private TurnOrderService _turnOrder;
        private TurnManager _turnManager;
        private FakeInitiativeProvider _provider;
        private ServiceBootstrapSO _bootstrap;
        private StunService _stun;

        private Guid _playerId;
        private Guid _enemyId;

        private readonly List<string> _eventLog = new List<string>();

        [SetUp]
        public void Setup()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            _eventLog.Clear();

            _provider = new FakeInitiativeProvider();
            _turnOrder = new TurnOrderService();
            _energy = new FakeEnergyService();
            _turnManager = new TurnManager();
            _turnManager.ConfigureForTests(_energy, actions: null, ruleset: null);

            ServiceLocator.AddService<IInitiativeProvider>(_provider);
            ServiceLocator.AddService<TurnOrderService>(_turnOrder);
            ServiceLocator.AddService<TurnManager>(_turnManager);
            ServiceLocator.AddService<IEnergyService>(_energy);

            _playerId = Guid.NewGuid();
            _enemyId = Guid.NewGuid();
            _energy.Current[_playerId] = _energy.MaxPerEntity;
            _energy.Current[_enemyId] = _energy.MaxPerEntity;

            EventManager.Subscribe(EventName.OnTurnStarted,
                args => _eventLog.Add($"OnTurnStarted:{args[0]}"));
            EventManager.Subscribe(EventName.OnTurnFinished,
                args => _eventLog.Add($"OnTurnFinished:{args[0]}"));

            _bootstrap = ScriptableObject.CreateInstance<ServiceBootstrapSO>();

            // Host inactivo para que AddComponent no dispare Awake con _bootstrap == null;
            // seteamos el anchor por reflection e invocamos Awake a mano (ver
            // CombatControllerFreezeTests).
            _host = new GameObject("TestCombatControllerStun");
            _host.SetActive(false);
            _controller = _host.AddComponent<CombatController>();
            typeof(CombatController)
                .GetField("_bootstrap",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(_controller, _bootstrap);
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) UnityEngine.Object.DestroyImmediate(_host);
            if (_bootstrap != null) UnityEngine.Object.DestroyImmediate(_bootstrap);
            _stun?.Dispose();
            _stun = null;
            _turnManager?.Dispose();
            _turnManager = null;
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        // --- Helpers ------------------------------------------------------

        private static void InvokeAwake(CombatController controller)
        {
            typeof(CombatController)
                .GetMethod("Awake",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(controller, null);
        }

        /// <summary>Registra el StunService real. El orden respecto de <see cref="InvokeAwake"/>
        /// es indistinto: el controller resuelve <c>IStunService</c> lazy, por turno.</summary>
        private StunService RegisterStunService()
        {
            _stun = new StunService();
            _stun.Register();
            return _stun;
        }

        private void StackOrderPlayerFirst()
        {
            _provider.SetRoll(_playerId, 100);
            _provider.SetRoll(_enemyId, 10);
        }

        private void StackOrderEnemyFirst()
        {
            _provider.SetRoll(_playerId, 10);
            _provider.SetRoll(_enemyId, 100);
        }

        // ======================================================================
        // Player stuneado
        // ======================================================================

        [Test]
        public void PlayerStunned_TurnIsSkipped_FsmLandsOnEnemyTurn()
        {
            StackOrderPlayerFirst();
            var stun = RegisterStunService();
            InvokeAwake(_controller);
            stun.ApplyStun(_playerId, 1);

            _controller.StartCombat(_playerId, new[] { _playerId, _enemyId }, Guid.NewGuid(),
                enemyActionHandler: g => { });

            Assert.IsInstanceOf<States.EnemyTurnState>(_controller.FSM.Current,
                "El turno stuneado del player debe cerrarse solo y pasarle el turno al enemy.");
            Assert.IsFalse(stun.IsStunned(_playerId), "El turno perdido consumio el stun.");
        }

        [Test]
        public void PlayerStunned_SkippedTurnStillFiresOnTurnFinished()
        {
            StackOrderPlayerFirst();
            var stun = RegisterStunService();
            InvokeAwake(_controller);
            stun.ApplyStun(_playerId, 1);

            _controller.StartCombat(_playerId, new[] { _playerId, _enemyId }, Guid.NewGuid(),
                enemyActionHandler: g => { });

            Assert.Contains($"OnTurnStarted:{_playerId}", _eventLog);
            Assert.Contains($"OnTurnFinished:{_playerId}", _eventLog,
                "El skip pasa por PlayerEndTurn justamente para que OnTurnFinished salga — de ahi " +
                "cuelgan energia, duracion de modificadores y el re-disable del boton End Turn.");
            Assert.Contains($"OnTurnStarted:{_enemyId}", _eventLog);
        }

        [Test]
        public void PlayerStunned_AdvancesTurnOrderCursorExactlyOnce()
        {
            StackOrderPlayerFirst();
            var stun = RegisterStunService();
            InvokeAwake(_controller);
            stun.ApplyStun(_playerId, 1);

            _controller.StartCombat(_playerId, new[] { _playerId, _enemyId }, Guid.NewGuid(),
                enemyActionHandler: g => { });

            Assert.AreEqual(_enemyId, _turnOrder.Current,
                "Un solo Advance: el cursor queda en el enemy, no se saltea de mas.");
        }

        [Test]
        public void PlayerNotStunned_TurnIsNotSkipped()
        {
            StackOrderPlayerFirst();
            RegisterStunService();
            InvokeAwake(_controller);

            _controller.StartCombat(_playerId, new[] { _playerId, _enemyId }, Guid.NewGuid(),
                enemyActionHandler: g => { });

            Assert.IsInstanceOf<States.PlayerTurnState>(_controller.FSM.Current,
                "Sin stun el player conserva su turno.");
        }

        [Test]
        public void WithoutStunService_TurnFlowIsUnchanged()
        {
            StackOrderPlayerFirst();
            // Sin RegisterStunService(): el skipper queda inerte.
            InvokeAwake(_controller);

            _controller.StartCombat(_playerId, new[] { _playerId, _enemyId }, Guid.NewGuid(),
                enemyActionHandler: g => { });

            Assert.IsInstanceOf<States.PlayerTurnState>(_controller.FSM.Current,
                "Escenas/tests sin el bootstrap del StunService conservan el flujo actual.");
        }

        [Test]
        public void PlayerStunnedTwoTurns_SkipsTwoTurnsThenPlays()
        {
            StackOrderPlayerFirst();
            var stun = RegisterStunService();
            InvokeAwake(_controller);
            stun.ApplyStun(_playerId, 2);

            _controller.StartCombat(_playerId, new[] { _playerId, _enemyId }, Guid.NewGuid(),
                enemyActionHandler: g => { });

            // Turno 1 del player: salteado.
            Assert.IsInstanceOf<States.EnemyTurnState>(_controller.FSM.Current);
            Assert.AreEqual(1, stun.GetStunTurns(_playerId));

            // El enemy cierra y vuelve el player: sigue stuneado, se saltea de nuevo.
            _controller.SendEnemyDone();
            Assert.IsInstanceOf<States.EnemyTurnState>(_controller.FSM.Current,
                "Segundo turno stuneado: el player vuelve a perderlo.");
            Assert.IsFalse(stun.IsStunned(_playerId));

            // Tercera vez: ya sin stun, el player se queda en su turno.
            _controller.SendEnemyDone();
            Assert.IsInstanceOf<States.PlayerTurnState>(_controller.FSM.Current,
                "Un stun de 2 turnos saltea exactamente 2 turnos.");
        }

        // ======================================================================
        // Enemy stuneado (capacidad generica — hoy ningun sistema stunea enemigos)
        // ======================================================================

        [Test]
        public void EnemyStunned_TurnIsSkipped_WithoutInvokingAiHandler()
        {
            StackOrderEnemyFirst();
            var stun = RegisterStunService();
            InvokeAwake(_controller);
            stun.ApplyStun(_enemyId, 1);

            int handlerCalls = 0;
            _controller.StartCombat(_playerId, new[] { _playerId, _enemyId }, Guid.NewGuid(),
                enemyActionHandler: g => handlerCalls++);

            Assert.IsInstanceOf<States.PlayerTurnState>(_controller.FSM.Current,
                "El turno del enemy stuneado se cierra por EnemyDone y pasa al player.");
            Assert.AreEqual(0, handlerCalls,
                "El grace period (CNF-006) difiere el handler al Update, y el EnemyDone encolado " +
                "corre Exit antes: el enemigo stuneado no actua.");
            Assert.IsFalse(stun.IsStunned(_enemyId));
        }

        // ======================================================================
        // Teardown
        // ======================================================================

        [Test]
        public void OnDestroy_DetachesSkipper()
        {
            StackOrderPlayerFirst();
            var stun = RegisterStunService();
            InvokeAwake(_controller);

            typeof(CombatController)
                .GetMethod("OnDestroy",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(_controller, null);

            stun.ApplyStun(_playerId, 1);
            Assert.DoesNotThrow(() => EventManager.Trigger(EventName.OnTurnStarted, _playerId));
            Assert.AreEqual(1, stun.GetStunTurns(_playerId),
                "Tras OnDestroy el skipper ya no consume stun.");
        }
    }
}
