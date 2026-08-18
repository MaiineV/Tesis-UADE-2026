using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;

namespace Rollgeon.Combat.Status.Tests
{
    /// <summary>
    /// Tests de <see cref="StunService"/>. Cubre: ApplyStun con max() (no suma), IsStunned /
    /// GetStunTurns, consumo con decremento + OnStunExpired en 0, Clear puntual, y ClearAll
    /// disparado por OnCombatEnd / OnRunEnd.
    /// </summary>
    [TestFixture]
    public class StunServiceTests
    {
        private StunService _svc;
        private List<object[]> _appliedLog;
        private List<object[]> _expiredLog;
        private EventManager.EventReceiver _onApplied;
        private EventManager.EventReceiver _onExpired;
        private Guid _playerGuid;
        private Guid _enemyGuid;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _appliedLog = new List<object[]>();
            _expiredLog = new List<object[]>();
            _onApplied = args => _appliedLog.Add(args);
            _onExpired = args => _expiredLog.Add(args);
            EventManager.Subscribe(EventName.OnStunApplied, _onApplied);
            EventManager.Subscribe(EventName.OnStunExpired, _onExpired);

            _playerGuid = Guid.NewGuid();
            _enemyGuid = Guid.NewGuid();

            _svc = new StunService();
            _svc.ConfigureForTests(() => _playerGuid);
        }

        [TearDown]
        public void TearDown()
        {
            _svc?.Dispose();
            _svc = null;
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        // ======================================================================
        // ApplyStun
        // ======================================================================

        [Test]
        public void ApplyStun_DefaultsToOneTurn_AndFiresEvent()
        {
            _svc.ApplyStun(_playerGuid);

            Assert.IsTrue(_svc.IsStunned(_playerGuid));
            Assert.AreEqual(1, _svc.GetStunTurns(_playerGuid));
            Assert.AreEqual(1, _appliedLog.Count);
            Assert.AreEqual(_playerGuid, _appliedLog[0][0]);
            Assert.AreEqual(1, _appliedLog[0][1]);
        }

        [Test]
        public void ApplyStun_TakesMax_DoesNotStack()
        {
            _svc.ApplyStun(_playerGuid, 1);
            _svc.ApplyStun(_playerGuid, 1);
            _svc.ApplyStun(_playerGuid, 1);

            Assert.AreEqual(1, _svc.GetStunTurns(_playerGuid),
                "Tres stuns de 1 turno siguen siendo 1 — max(), no suma (el hielo se derrite).");
            Assert.AreEqual(3, _appliedLog.Count,
                "Cada ApplyStun dispara OnStunApplied aunque el max() no mueva el contador.");
        }

        [Test]
        public void ApplyStun_HigherValueOverridesLower()
        {
            _svc.ApplyStun(_playerGuid, 1);
            _svc.ApplyStun(_playerGuid, 3);

            Assert.AreEqual(3, _svc.GetStunTurns(_playerGuid));
            Assert.AreEqual(3, _appliedLog[1][1], "El payload es el total restante tras el max().");
        }

        [Test]
        public void ApplyStun_LowerValueDoesNotShortenStun()
        {
            _svc.ApplyStun(_playerGuid, 3);
            _svc.ApplyStun(_playerGuid, 1);

            Assert.AreEqual(3, _svc.GetStunTurns(_playerGuid),
                "Un stun mas corto no debe recortar uno mas largo ya activo.");
        }

        [Test]
        public void ApplyStun_WithInvalidInputs_IsNoop()
        {
            _svc.ApplyStun(Guid.Empty, 2);
            _svc.ApplyStun(_playerGuid, 0);
            _svc.ApplyStun(_playerGuid, -1);

            Assert.AreEqual(0, _svc.ActiveStuns.Count);
            Assert.AreEqual(0, _appliedLog.Count);
        }

        [Test]
        public void ApplyStun_IsPerEntity()
        {
            _svc.ApplyStun(_playerGuid, 1);
            _svc.ApplyStun(_enemyGuid, 2);

            Assert.AreEqual(1, _svc.GetStunTurns(_playerGuid));
            Assert.AreEqual(2, _svc.GetStunTurns(_enemyGuid));
        }

        // ======================================================================
        // IsStunned / GetStunTurns
        // ======================================================================

        [Test]
        public void IsStunned_UnknownEntity_IsFalse()
        {
            Assert.IsFalse(_svc.IsStunned(Guid.NewGuid()));
            Assert.IsFalse(_svc.IsStunned(Guid.Empty));
            Assert.AreEqual(0, _svc.GetStunTurns(Guid.NewGuid()));
            Assert.AreEqual(0, _svc.GetStunTurns(Guid.Empty));
        }

        [Test]
        public void IsPlayerStunned_UsesInjectedResolver()
        {
            Assert.IsFalse(_svc.IsPlayerStunned());
            _svc.ApplyStun(_playerGuid);
            Assert.IsTrue(_svc.IsPlayerStunned());
        }

        // ======================================================================
        // ConsumeTurn
        // ======================================================================

        [Test]
        public void ConsumeTurn_DecrementsAndFiresExpiredAtZero()
        {
            _svc.ApplyStun(_playerGuid, 2);

            Assert.IsTrue(_svc.ConsumeTurn(_playerGuid));
            Assert.AreEqual(1, _svc.GetStunTurns(_playerGuid));
            Assert.AreEqual(0, _expiredLog.Count, "Todavia queda 1 turno — no expiro.");

            Assert.IsTrue(_svc.ConsumeTurn(_playerGuid));
            Assert.AreEqual(0, _svc.GetStunTurns(_playerGuid));
            Assert.IsFalse(_svc.IsStunned(_playerGuid));
            Assert.AreEqual(1, _expiredLog.Count);
            Assert.AreEqual(_playerGuid, _expiredLog[0][0]);
        }

        [Test]
        public void ConsumeTurn_OnUnstunnedEntity_ReturnsFalseAndFiresNothing()
        {
            Assert.IsFalse(_svc.ConsumeTurn(_playerGuid));
            Assert.IsFalse(_svc.ConsumeTurn(Guid.Empty));
            Assert.AreEqual(0, _expiredLog.Count);
        }

        [Test]
        public void ConsumeTurn_DoesNotLeaveZeroedEntries()
        {
            _svc.ApplyStun(_playerGuid, 1);
            _svc.ConsumeTurn(_playerGuid);

            Assert.AreEqual(0, _svc.ActiveStuns.Count,
                "Al expirar la entry se borra — no queda un 0 colgado en el dict.");
        }

        [Test]
        public void ConsumeTurn_OnlyAffectsTargetEntity()
        {
            _svc.ApplyStun(_playerGuid, 1);
            _svc.ApplyStun(_enemyGuid, 1);

            _svc.ConsumeTurn(_playerGuid);

            Assert.IsFalse(_svc.IsStunned(_playerGuid));
            Assert.IsTrue(_svc.IsStunned(_enemyGuid),
                "Consumir el turno de una entidad no toca el stun de las demas.");
        }

        // ======================================================================
        // Clear / ClearAll
        // ======================================================================

        [Test]
        public void Clear_RemovesEntityAndFiresExpired()
        {
            _svc.ApplyStun(_playerGuid, 3);

            _svc.Clear(_playerGuid);

            Assert.IsFalse(_svc.IsStunned(_playerGuid));
            Assert.AreEqual(1, _expiredLog.Count,
                "Clear puntual (cura) SI dispara OnStunExpired — es la unica via de enterarse.");
        }

        [Test]
        public void Clear_OnUnstunnedEntity_FiresNothing()
        {
            _svc.Clear(_playerGuid);
            _svc.Clear(Guid.Empty);

            Assert.AreEqual(0, _expiredLog.Count);
        }

        [Test]
        public void ClearAll_EmptiesWithoutFiringExpired()
        {
            _svc.ApplyStun(_playerGuid, 2);
            _svc.ApplyStun(_enemyGuid, 2);

            _svc.ClearAll();

            Assert.AreEqual(0, _svc.ActiveStuns.Count);
            Assert.AreEqual(0, _expiredLog.Count,
                "ClearAll es teardown — no dispara OnStunExpired (mismo criterio que ComboBlockService).");
        }

        [Test]
        public void OnCombatEnd_FiresClearAll()
        {
            _svc.ApplyStun(_playerGuid, 5);
            _svc.ApplyStun(_enemyGuid, 5);

            EventManager.Trigger(EventName.OnCombatEnd, Guid.NewGuid(), new object());

            Assert.AreEqual(0, _svc.ActiveStuns.Count,
                "El stun es combat-scoped: nadie arrastra stun a la sala siguiente.");
        }

        [Test]
        public void OnRunEnd_FiresClearAll()
        {
            _svc.ApplyStun(_playerGuid, 5);

            EventManager.Trigger(EventName.OnRunEnd, Guid.NewGuid(), new object());

            Assert.AreEqual(0, _svc.ActiveStuns.Count);
        }

        [Test]
        public void OnTurnFinished_DoesNotDecrement()
        {
            _svc.ApplyStun(_playerGuid, 2);

            EventManager.Trigger(EventName.OnTurnFinished, _playerGuid);

            Assert.AreEqual(2, _svc.GetStunTurns(_playerGuid),
                "El servicio NO se cuelga de OnTurnFinished: el decremento es del skip, si no " +
                "los turnos jugados normalmente tambien quemarian stun.");
        }

        // ======================================================================
        // Lifecycle
        // ======================================================================

        [Test]
        public void Dispose_UnsubscribesLifecycleHandlers()
        {
            _svc.ApplyStun(_playerGuid, 2);
            _svc.Dispose();

            _svc.ApplyStun(_playerGuid, 2);
            EventManager.Trigger(EventName.OnCombatEnd, Guid.NewGuid(), new object());

            Assert.AreEqual(2, _svc.GetStunTurns(_playerGuid),
                "Tras Dispose el servicio ya no reacciona a OnCombatEnd.");
        }

        [Test]
        public void Register_RegistersBothInterfaceAndConcreteType()
        {
            var svc = new StunService();
            try
            {
                svc.Register();

                Assert.IsTrue(ServiceLocator.TryGetService<IStunService>(out var byInterface));
                Assert.AreSame(svc, byInterface);
                Assert.IsTrue(ServiceLocator.TryGetService<StunService>(out var byConcrete));
                Assert.AreSame(svc, byConcrete);
            }
            finally
            {
                svc.Dispose();
            }
        }

        [Test]
        public void SubscribeHandlers_IsIdempotent_AcrossRegisterAndConfigureForTests()
        {
            var svc = new StunService();
            try
            {
                svc.Register();
                svc.ConfigureForTests(() => _playerGuid);
                svc.ApplyStun(_playerGuid, 1);

                // Un unico ClearAll suscripto: el segundo Subscribe no debe duplicar handlers.
                EventManager.Trigger(EventName.OnCombatEnd, Guid.NewGuid(), new object());
                Assert.AreEqual(0, svc.ActiveStuns.Count);

                svc.Dispose();
                svc.ApplyStun(_playerGuid, 1);
                EventManager.Trigger(EventName.OnCombatEnd, Guid.NewGuid(), new object());
                Assert.AreEqual(1, svc.GetStunTurns(_playerGuid),
                    "Un solo Dispose debe alcanzar para desenganchar todo — sin handlers duplicados.");
            }
            finally
            {
                svc.Dispose();
            }
        }
    }
}
