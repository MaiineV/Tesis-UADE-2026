using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.ComboBlock;

namespace Rollgeon.Combat.ComboBlock.Tests
{
    /// <summary>
    /// Tests de <see cref="ComboBlockService"/> (plan §9.1). Cubre:
    /// Block / IsBlocked / TickDuration / Clear; duration &lt;= 0 no-op; re-block toma max;
    /// unblock dispara evento; Clear no dispara eventos; OnCombatEnd / OnRunEnd trigger Clear.
    /// </summary>
    [TestFixture]
    public class ComboBlockServiceTests
    {
        private ComboBlockService _svc;
        private List<object[]> _blockedLog;
        private List<object[]> _unblockedLog;
        private EventManager.EventReceiver _onBlocked;
        private EventManager.EventReceiver _onUnblocked;
        private Guid _playerGuid;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();

            _blockedLog = new List<object[]>();
            _unblockedLog = new List<object[]>();
            _onBlocked = args => _blockedLog.Add(args);
            _onUnblocked = args => _unblockedLog.Add(args);
            EventManager.Subscribe(EventName.OnComboBlocked, _onBlocked);
            EventManager.Subscribe(EventName.OnComboUnblocked, _onUnblocked);

            _playerGuid = Guid.NewGuid();
            _svc = new ComboBlockService();
            _svc.ConfigureForTests(() => _playerGuid);
        }

        [TearDown]
        public void TearDown()
        {
            EventManager.UnSubscribe(EventName.OnComboBlocked, _onBlocked);
            EventManager.UnSubscribe(EventName.OnComboUnblocked, _onUnblocked);
            _svc?.Dispose();
            ServiceLocator.Clear();
        }

        [Test]
        public void Block_RegistersAndFiresEvent()
        {
            _svc.Block("combo.par", 3);

            Assert.IsTrue(_svc.IsBlocked("combo.par"));
            Assert.AreEqual(3, _svc.GetRemainingTurns("combo.par"));
            Assert.AreEqual(1, _blockedLog.Count);
            Assert.AreEqual("combo.par", _blockedLog[0][0]);
            Assert.AreEqual(3, _blockedLog[0][1]);
        }

        [Test]
        public void Block_WithInvalidInputs_IsNoop()
        {
            _svc.Block(null, 3);
            _svc.Block("", 3);
            _svc.Block("combo.par", 0);
            _svc.Block("combo.par", -1);

            Assert.AreEqual(0, _svc.ActiveBlocks.Count);
            Assert.AreEqual(0, _blockedLog.Count);
        }

        [Test]
        public void Block_ReblockTakesMaxDuration()
        {
            _svc.Block("combo.par", 2);
            _svc.Block("combo.par", 5); // max-override
            _svc.Block("combo.par", 3); // menor → ignorado como override

            Assert.AreEqual(5, _svc.GetRemainingTurns("combo.par"));
            Assert.AreEqual(3, _blockedLog.Count, "Cada llamada a Block() dispara el evento aunque no cambie la duracion.");
        }

        [Test]
        public void TickDuration_DecrementsAll_AndFiresUnblockWhenZero()
        {
            _svc.Block("combo.par", 2);
            _svc.Block("combo.doble", 1);

            _svc.TickDuration(); // par:1 doble:0 → unblock doble.

            Assert.IsTrue(_svc.IsBlocked("combo.par"));
            Assert.IsFalse(_svc.IsBlocked("combo.doble"));
            Assert.AreEqual(1, _unblockedLog.Count);
            Assert.AreEqual("combo.doble", _unblockedLog[0][0]);

            _svc.TickDuration(); // par:0 → unblock par.
            Assert.IsFalse(_svc.IsBlocked("combo.par"));
            Assert.AreEqual(2, _unblockedLog.Count);
        }

        [Test]
        public void Clear_EmptiesDictWithoutFiringUnblockEvents()
        {
            _svc.Block("combo.par", 2);
            _svc.Block("combo.doble", 3);

            _svc.Clear();

            Assert.AreEqual(0, _svc.ActiveBlocks.Count);
            Assert.AreEqual(0, _unblockedLog.Count, "Clear NO dispara OnComboUnblocked por diseno.");
        }

        [Test]
        public void OnTurnFinished_FromPlayer_TicksDuration()
        {
            _svc.Block("combo.par", 1);

            // Simula que el CombatTurnFSM dispara OnTurnFinished con el Guid del player.
            EventManager.Trigger(EventName.OnTurnFinished, _playerGuid);

            Assert.IsFalse(_svc.IsBlocked("combo.par"),
                "OnTurnFinished(playerGuid) debe decrementar la duracion.");
            Assert.AreEqual(1, _unblockedLog.Count);
        }

        [Test]
        public void OnTurnFinished_FromNonPlayer_DoesNotTick()
        {
            _svc.Block("combo.par", 1);
            var enemyGuid = Guid.NewGuid();

            EventManager.Trigger(EventName.OnTurnFinished, enemyGuid);

            Assert.IsTrue(_svc.IsBlocked("combo.par"),
                "El tick solo corre si el turno terminado fue del player.");
            Assert.AreEqual(0, _unblockedLog.Count);
        }

        [Test]
        public void OnCombatEnd_FiresClear()
        {
            _svc.Block("combo.par", 5);
            EventManager.Trigger(EventName.OnCombatEnd, Guid.NewGuid(), new object());

            Assert.AreEqual(0, _svc.ActiveBlocks.Count);
            Assert.AreEqual(0, _unblockedLog.Count);
        }

        [Test]
        public void OnRunEnd_FiresClear()
        {
            _svc.Block("combo.par", 5);
            EventManager.Trigger(EventName.OnRunEnd, Guid.NewGuid(), new object());

            Assert.AreEqual(0, _svc.ActiveBlocks.Count);
        }

        [Test]
        public void IsBlocked_HandlesNullAndEmpty()
        {
            Assert.IsFalse(_svc.IsBlocked(null));
            Assert.IsFalse(_svc.IsBlocked(""));
            Assert.AreEqual(0, _svc.GetRemainingTurns(null));
        }

        [Test]
        public void TickDuration_EmptyDict_Noop()
        {
            Assert.DoesNotThrow(() => _svc.TickDuration());
            Assert.AreEqual(0, _unblockedLog.Count);
        }
    }
}
