using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat;
using Rollgeon.Combat.Actions;
using Rollgeon.Combat.EnergyLib;
using Rollgeon.Combat.Initiative;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Combat.FSM.Tests
{
    [TestFixture]
    public class CombatControllerFreezeTests
    {
        private GameObject _host;
        private CombatController _controller;

        private FakeEnergyService _energy;
        private TurnOrderService _turnOrder;
        private TurnManager _turnManager;
        private FakeInitiativeProvider _provider;
        private ServiceBootstrapSO _bootstrap;

        private Guid _playerId;
        private Guid _enemyId;

        [SetUp]
        public void Setup()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

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
            _provider.SetRoll(_playerId, 100);
            _provider.SetRoll(_enemyId, 10);

            _bootstrap = ScriptableObject.CreateInstance<ServiceBootstrapSO>();

            // Crear el host inactive para evitar que AddComponent dispare Awake con
            // _bootstrap = null. Seteamos _bootstrap via reflection e invocamos Awake
            // manualmente — en EditMode, SetActive(true) no dispara Awake de forma
            // confiable, entonces no lo usamos.
            _host = new GameObject("TestCombatController");
            _host.SetActive(false);
            _controller = _host.AddComponent<CombatController>();
            typeof(CombatController)
                .GetField("_bootstrap",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(_controller, _bootstrap);
            InvokeAwake(_controller);
        }

        private static void InvokeAwake(CombatController controller)
        {
            typeof(CombatController)
                .GetMethod("Awake",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(controller, null);
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) UnityEngine.Object.DestroyImmediate(_host);
            if (_bootstrap != null) UnityEngine.Object.DestroyImmediate(_bootstrap);
            _turnManager?.Dispose();
            _turnManager = null;
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        // ======================================================================
        // Freeze hooks
        // ======================================================================

        [Test]
        public void OnOverlayPushed_SetsFrozenTrue()
        {
            Assert.IsFalse(_controller.IsFrozen, "Pre: no freezed.");
            EventManager.Trigger(EventName.OnOverlayPushed, /*PhaseOverlay*/ (object)null);
            Assert.IsTrue(_controller.IsFrozen);
        }

        [Test]
        public void OnOverlayPopped_ClearsFrozenFlag()
        {
            EventManager.Trigger(EventName.OnOverlayPushed, (object)null);
            Assert.IsTrue(_controller.IsFrozen);
            EventManager.Trigger(EventName.OnOverlayPopped, (object)null);
            Assert.IsFalse(_controller.IsFrozen);
        }

        [Test]
        public void StartCombat_WithFsmRunning_ExposesFsm()
        {
            _controller.StartCombat(
                _playerId,
                new[] { _playerId, _enemyId },
                Guid.NewGuid(),
                enemyActionHandler: g => { /* no-op */ });

            Assert.IsNotNull(_controller.FSM);
            Assert.IsTrue(_controller.FSM.IsRunning);
            Assert.IsInstanceOf<States.PlayerTurnState>(_controller.FSM.Current);
        }

        [Test]
        public void NotifyCombatEnded_FiresOnCombatFinished()
        {
            CombatOutcome? captured = null;
            _controller.OnCombatFinished += o => captured = o;

            _controller.StartCombat(
                _playerId,
                new[] { _playerId, _enemyId },
                Guid.NewGuid(),
                enemyActionHandler: g => { });

            // HandleFsmFinished nullea _controller.FSM al terminar el combate
            // (teardown para permitir un proximo StartCombat). Guardamos la ref
            // local: Stop() no borra Current, asi podemos assertear el estado
            // terminal despues del teardown.
            var fsm = _controller.FSM;

            _controller.NotifyCombatEnded(CombatOutcome.Victory);

            Assert.AreEqual(CombatOutcome.Victory, captured);
            Assert.IsInstanceOf<States.CombatExitState>(fsm.Current);
            Assert.IsNull(_controller.FSM,
                "Post-combate la FSM debe quedar nulleada para habilitar el proximo StartCombat.");
        }

        [Test]
        public void Controller_WithoutBootstrap_DoesNotStartFSM()
        {
            // Nuevo host sin bootstrap asignado (activo desde el vamos = Awake
            // corre con _bootstrap == null).
            var orphanHost = new GameObject("Orphan");
            try
            {
                UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                    new System.Text.RegularExpressions.Regex(@"_bootstrap anchor es null"));
                var orphan = orphanHost.AddComponent<CombatController>();
                InvokeAwake(orphan);
                Assert.IsFalse(orphan.enabled,
                    "Controller sin bootstrap debe deshabilitarse en Awake.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(orphanHost);
            }
        }
    }
}
