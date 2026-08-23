using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;

namespace Rollgeon.Combat.Status.Tests
{
    /// <summary>
    /// Tests de <see cref="TeleportCooldownService"/>: refresh-sin-stack, tick al inicio del
    /// turno del afectado, expiración exacta, y teardown silencioso por scope (incluida la
    /// entrada a otra sala, que Veneno no escucha pero este estado sí).
    /// </summary>
    [TestFixture]
    public class TeleportCooldownServiceTests
    {
        private TeleportCooldownService _svc;
        private Guid _entity;

        private List<object[]> _appliedLog;
        private List<object[]> _tickedLog;
        private List<object[]> _expiredLog;
        private EventManager.EventReceiver _onApplied;
        private EventManager.EventReceiver _onTicked;
        private EventManager.EventReceiver _onExpired;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _entity = Guid.NewGuid();

            _appliedLog = new List<object[]>();
            _tickedLog = new List<object[]>();
            _expiredLog = new List<object[]>();
            _onApplied = args => _appliedLog.Add(args);
            _onTicked = args => _tickedLog.Add(args);
            _onExpired = args => _expiredLog.Add(args);
            EventManager.Subscribe(EventName.OnTeleportCooldownApplied, _onApplied);
            EventManager.Subscribe(EventName.OnTeleportCooldownTicked, _onTicked);
            EventManager.Subscribe(EventName.OnTeleportCooldownExpired, _onExpired);

            _svc = new TeleportCooldownService();
            _svc.ConfigureForTests();
        }

        [TearDown]
        public void TearDown()
        {
            _svc?.Dispose();
            _svc = null;
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        private void StartTurn(Guid entity) => EventManager.Trigger(EventName.OnTurnStarted, entity);

        [Test]
        public void Apply_WithTwoTurns_IsOnCooldownWithThatCount()
        {
            _svc.Apply(_entity, 2);

            Assert.IsTrue(_svc.IsOnCooldown(_entity));
            Assert.AreEqual(2, _svc.GetTurns(_entity));
            Assert.AreEqual(1, _appliedLog.Count);
            Assert.AreEqual(_entity, _appliedLog[0][0]);
            Assert.AreEqual(2, _appliedLog[0][1]);
        }

        [Test]
        public void Apply_Twice_TakesMaxNotSum()
        {
            _svc.Apply(_entity, 2);
            StartTurn(_entity);
            Assert.AreEqual(1, _svc.GetTurns(_entity), "Setup: queda 1 turno.");

            _svc.Apply(_entity, 3);

            Assert.AreEqual(3, _svc.GetTurns(_entity),
                "Re-teleportar refresca al máximo, nunca acumula (no 1+3).");
        }

        [Test]
        public void Apply_WithZeroOrNegativeTurns_DoesNothing()
        {
            _svc.Apply(_entity, 0);
            _svc.Apply(_entity, -1);

            Assert.IsFalse(_svc.IsOnCooldown(_entity));
            Assert.AreEqual(0, _appliedLog.Count);
        }

        [Test]
        public void TurnStart_ForCooledEntity_DecrementsAndFiresTicked()
        {
            _svc.Apply(_entity, 2);

            StartTurn(_entity);

            Assert.AreEqual(1, _svc.GetTurns(_entity));
            Assert.AreEqual(1, _tickedLog.Count);
            Assert.AreEqual(_entity, _tickedLog[0][0]);
            Assert.AreEqual(1, _tickedLog[0][1]);
            Assert.AreEqual(0, _expiredLog.Count);
        }

        [Test]
        public void Cooldown_ExpiresAfterExactlyTwoTurns()
        {
            _svc.Apply(_entity, 2);

            StartTurn(_entity);
            StartTurn(_entity);
            StartTurn(_entity); // ya expirado: no debe tickear de más

            Assert.IsFalse(_svc.IsOnCooldown(_entity));
            Assert.AreEqual(2, _tickedLog.Count, "Exactamente 2 ticks — ni uno más.");
            Assert.AreEqual(1, _expiredLog.Count);
            Assert.AreEqual(_entity, _expiredLog[0][0]);
        }

        [Test]
        public void OtherEntityTurnStart_DoesNotTick()
        {
            _svc.Apply(_entity, 2);

            StartTurn(Guid.NewGuid());

            Assert.AreEqual(0, _tickedLog.Count);
            Assert.AreEqual(2, _svc.GetTurns(_entity));
        }

        [Test]
        public void Clear_RemovesCooldown_AndFiresExpired()
        {
            _svc.Apply(_entity, 2);

            _svc.Clear(_entity);

            Assert.IsFalse(_svc.IsOnCooldown(_entity));
            Assert.AreEqual(1, _expiredLog.Count);
        }

        [Test]
        public void Clear_WithoutCooldown_DoesNotFireExpired()
        {
            _svc.Clear(_entity);

            Assert.AreEqual(0, _expiredLog.Count);
        }

        [Test]
        public void CombatEnd_ClearsAll_WithoutExpiredEvents()
        {
            _svc.Apply(_entity, 2);

            EventManager.Trigger(EventName.OnCombatEnd);

            Assert.IsFalse(_svc.IsOnCooldown(_entity));
            Assert.AreEqual(0, _expiredLog.Count, "Teardown silencioso, como Veneno/Stun.");
        }

        [Test]
        public void RoomEntered_ClearsAll_WithoutExpiredEvents()
        {
            _svc.Apply(_entity, 2);

            EventManager.Trigger(EventName.OnRoomEntered);

            Assert.IsFalse(_svc.IsOnCooldown(_entity),
                "Cambiar de sala es el hard-reset: fuera de combate no hay turnos que expiren esto.");
            Assert.AreEqual(0, _expiredLog.Count);
        }

        // ------------------------------------------------------------------
        // Reloj de exploración: tick por movimiento completado
        // ------------------------------------------------------------------

        private sealed class StubPhaseService : Rollgeon.Phase.IPhaseService
        {
            public Rollgeon.Phase.GamePhase CurrentBase { get; set; }
            public Rollgeon.Phase.PhaseOverlay CurrentOverlay => default;
            public void ReplacePhase(Rollgeon.Phase.GamePhase next) => CurrentBase = next;
            public void PushOverlay(Rollgeon.Phase.PhaseOverlay overlay) { }
            public void PopOverlay() { }
        }

        private Rollgeon.Movement.MovementService ArrangeMovementWorld(
            Rollgeon.Phase.GamePhase phase, out Guid mover)
        {
            var stubPhase = new StubPhaseService { CurrentBase = phase };
            ServiceLocator.AddService<Rollgeon.Phase.IPhaseService>(stubPhase, ServiceScope.Global);

            var grid = new Rollgeon.Grid.GridManager();
            grid.LoadRoom(Rollgeon.Grid.NavGraph.Rect(6, 6));
            ServiceLocator.AddService<Rollgeon.Grid.IGridManager>(grid, ServiceScope.Global);

            var movement = new Rollgeon.Movement.MovementService(grid);
            ServiceLocator.AddService<Rollgeon.Movement.IMovementService>(movement, ServiceScope.Global);

            mover = Guid.NewGuid();
            grid.Register(mover, new Rollgeon.Grid.GridCoord(0, 0));
            return movement;
        }

        // El guard del mismo frame existe para la reubicación post-teleport; en EditMode
        // todo el test es un solo frame, así que se borra el stamp por reflection para
        // simular "frames posteriores".
        private void ForgetApplyFrame(Guid entity)
        {
            var field = typeof(TeleportCooldownService).GetField("_appliedFrame",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            var dict = (Dictionary<Guid, int>)field.GetValue(_svc);
            dict?.Remove(entity);
        }

        [Test]
        public void ExplorationMove_ForCooledEntity_DecrementsPerCompletedMove()
        {
            var movement = ArrangeMovementWorld(Rollgeon.Phase.GamePhase.Exploration, out var mover);
            _svc.Apply(mover, 2);
            ForgetApplyFrame(mover);

            Assert.IsTrue(movement.Move(mover, new Rollgeon.Grid.GridCoord(1, 0)));

            Assert.AreEqual(1, _svc.GetTurns(mover),
                "En exploración no hay turnos: cada movimiento completado descuenta 1.");
            Assert.AreEqual(1, _tickedLog.Count);
        }

        [Test]
        public void ExplorationMove_InSameFrameAsApply_DoesNotTick()
        {
            var movement = ArrangeMovementWorld(Rollgeon.Phase.GamePhase.Exploration, out var mover);
            _svc.Apply(mover, 2);

            Assert.IsTrue(movement.Move(mover, new Rollgeon.Grid.GridCoord(1, 0)));

            Assert.AreEqual(2, _svc.GetTurns(mover),
                "El movimiento del mismo frame es la reubicación del motor de cadenas, no un paso del jugador.");
        }

        [Test]
        public void CombatMove_ForCooledEntity_DoesNotTick_TurnClockOwnsIt()
        {
            var movement = ArrangeMovementWorld(Rollgeon.Phase.GamePhase.Combat, out var mover);
            _svc.Apply(mover, 2);
            ForgetApplyFrame(mover);

            Assert.IsTrue(movement.Move(mover, new Rollgeon.Grid.GridCoord(1, 0)));

            Assert.AreEqual(2, _svc.GetTurns(mover),
                "En combate el reloj es el turno: caminar no descuenta extra.");
            StartTurn(mover);
            Assert.AreEqual(1, _svc.GetTurns(mover));
        }
    }
}
