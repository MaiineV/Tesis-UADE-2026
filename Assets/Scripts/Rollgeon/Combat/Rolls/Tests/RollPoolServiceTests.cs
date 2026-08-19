using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Balance;
using UnityEngine;

namespace Rollgeon.Combat.Rolls.Tests
{
    [TestFixture]
    public class RollPoolServiceTests
    {
        private RulesetSO _ruleset;
        private RollPoolService _service;
        private Guid _player;
        private List<object[]> _rollsChangedArgs;
        private EventManager.EventReceiver _rollsRec;

        [SetUp]
        public void Setup()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _ruleset = ScriptableObject.CreateInstance<RulesetSO>();
            // defaults del SO: AtCombatStart=5, PerTurn=5, Cap=15.

            _service = new RollPoolService();
            _service.ConfigureForTests(_ruleset);

            _player = Guid.NewGuid();

            _rollsChangedArgs = new List<object[]>();
            _rollsRec = args => _rollsChangedArgs.Add(args);
            EventManager.Subscribe(EventName.OnPlayerRollsChanged, _rollsRec);
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();
            if (_ruleset != null)
            {
                UnityEngine.Object.DestroyImmediate(_ruleset);
                _ruleset = null;
            }
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        private void InitAndStartCombat()
        {
            _service.InitializeForEntity(_player);
            EventManager.Trigger(EventName.OnCombatStart, Guid.NewGuid());
        }

        private void FinishPlayerTurn()
        {
            EventManager.Trigger(EventName.OnTurnFinished, _player);
        }

        // --- Lifecycle: combate --------------------------------------------

        [Test]
        public void Initialize_CachesPlayer_PoolStaysAtZeroUntilCombat()
        {
            _service.InitializeForEntity(_player);

            Assert.AreEqual(0, _service.GetCurrent(_player));
            Assert.AreEqual(15, _service.GetMax(_player));
        }

        [Test]
        public void CombatStart_SetsPoolToRollsAtCombatStart_AndFiresEvent()
        {
            InitAndStartCombat();

            Assert.AreEqual(5, _service.GetCurrent(_player));
            var last = _rollsChangedArgs[_rollsChangedArgs.Count - 1];
            Assert.AreEqual(_player, (Guid)last[0]);
            Assert.AreEqual(5, (int)last[1]);
            Assert.AreEqual(15, (int)last[2]);
        }

        [Test]
        public void CombatStart_ClampsToCap_WhenStartExceedsCap()
        {
            _ruleset.RollPool.RollsAtCombatStart = 99;
            _ruleset.RollPool.RollPoolCap = 15;

            InitAndStartCombat();

            Assert.AreEqual(15, _service.GetCurrent(_player));
        }

        [Test]
        public void CombatStart_WithoutInitialize_IsNoOp()
        {
            EventManager.Trigger(EventName.OnCombatStart, Guid.NewGuid());

            Assert.AreEqual(0, _service.GetCurrent(_player));
            Assert.AreEqual(0, _rollsChangedArgs.Count);
        }

        [Test]
        public void CombatEnd_ZeroesPool_PoolDoesNotExistOutsideCombat()
        {
            InitAndStartCombat();

            EventManager.Trigger(EventName.OnCombatEnd);

            Assert.AreEqual(0, _service.GetCurrent(_player));
        }

        [Test]
        public void CombatEnd_BeforeCombatStart_DoesNotFireRedundantEvent()
        {
            _service.InitializeForEntity(_player);
            _rollsChangedArgs.Clear();

            EventManager.Trigger(EventName.OnCombatEnd);

            Assert.AreEqual(0, _rollsChangedArgs.Count);
        }

        // --- Grant por turno ------------------------------------------------

        [Test]
        public void TurnFinished_GrantsRollsPerTurn_AccumulatingLeftover()
        {
            InitAndStartCombat();
            _service.TrySpendRolls(_player, 3); // 5 -> 2

            FinishPlayerTurn(); // 2 + 5

            Assert.AreEqual(7, _service.GetCurrent(_player));
        }

        [Test]
        public void TurnFinished_AccumulationClampsAtCap()
        {
            InitAndStartCombat(); // 5

            FinishPlayerTurn(); // 10
            FinishPlayerTurn(); // 15
            FinishPlayerTurn(); // clamp 15

            Assert.AreEqual(15, _service.GetCurrent(_player));
        }

        [Test]
        public void TurnFinished_AtCap_DoesNotFireRedundantEvent()
        {
            InitAndStartCombat();
            FinishPlayerTurn();
            FinishPlayerTurn(); // 15 = cap
            _rollsChangedArgs.Clear();

            FinishPlayerTurn(); // no-op

            Assert.AreEqual(0, _rollsChangedArgs.Count);
        }

        [Test]
        public void TurnFinished_OutsideCombat_DoesNotAccumulate()
        {
            _service.InitializeForEntity(_player);

            FinishPlayerTurn();

            Assert.AreEqual(0, _service.GetCurrent(_player));
        }

        [Test]
        public void TurnFinished_AfterCombatEnd_DoesNotAccumulate()
        {
            InitAndStartCombat();
            EventManager.Trigger(EventName.OnCombatEnd);

            FinishPlayerTurn();

            Assert.AreEqual(0, _service.GetCurrent(_player));
        }

        [Test]
        public void TurnFinished_OtherGuid_Ignored()
        {
            InitAndStartCombat();

            EventManager.Trigger(EventName.OnTurnFinished, Guid.NewGuid());

            Assert.AreEqual(5, _service.GetCurrent(_player));
        }

        // --- TrySpendRolls ---------------------------------------------------

        [Test]
        public void TrySpend_WithEnough_DecrementsAndFiresEvent()
        {
            InitAndStartCombat();
            _rollsChangedArgs.Clear();

            bool ok = _service.TrySpendRolls(_player, 1);

            Assert.IsTrue(ok);
            Assert.AreEqual(4, _service.GetCurrent(_player));
            Assert.AreEqual(1, _rollsChangedArgs.Count);
            Assert.AreEqual(4, (int)_rollsChangedArgs[0][1]);
        }

        [Test]
        public void TrySpend_ExactBalance_SucceedsToZero()
        {
            InitAndStartCombat();

            bool ok = _service.TrySpendRolls(_player, 5);

            Assert.IsTrue(ok);
            Assert.AreEqual(0, _service.GetCurrent(_player));
        }

        [Test]
        public void TrySpend_Insufficient_FalseWithoutMutationOrEvent()
        {
            InitAndStartCombat();
            _rollsChangedArgs.Clear();

            bool ok = _service.TrySpendRolls(_player, 6);

            Assert.IsFalse(ok);
            Assert.AreEqual(5, _service.GetCurrent(_player));
            Assert.AreEqual(0, _rollsChangedArgs.Count);
        }

        [Test]
        public void TrySpend_NegativeCount_False()
        {
            InitAndStartCombat();

            Assert.IsFalse(_service.TrySpendRolls(_player, -1));
        }

        [Test]
        public void TrySpend_OtherGuid_False()
        {
            InitAndStartCombat();

            Assert.IsFalse(_service.TrySpendRolls(Guid.NewGuid(), 1));
            Assert.AreEqual(5, _service.GetCurrent(_player));
        }

        // --- Drain (ReelToll) -------------------------------------------------

        [Test]
        public void Drain_WithEnough_ReturnsRequestedAmount()
        {
            InitAndStartCombat();

            int drained = _service.Drain(_player, 2);

            Assert.AreEqual(2, drained);
            Assert.AreEqual(3, _service.GetCurrent(_player));
        }

        [Test]
        public void Drain_PartialBalance_FloorsAtZeroAndReturnsActual()
        {
            InitAndStartCombat();
            _service.TrySpendRolls(_player, 3); // queda 2

            int drained = _service.Drain(_player, 5);

            Assert.AreEqual(2, drained);
            Assert.AreEqual(0, _service.GetCurrent(_player));
        }

        [Test]
        public void Drain_EmptyPool_ReturnsZeroWithoutEvent()
        {
            InitAndStartCombat();
            _service.TrySpendRolls(_player, 5);
            _rollsChangedArgs.Clear();

            int drained = _service.Drain(_player, 3);

            Assert.AreEqual(0, drained);
            Assert.AreEqual(0, _rollsChangedArgs.Count);
        }

        // --- AddRolls / RestoreCurrent ----------------------------------------

        [Test]
        public void AddRolls_ClampsToCap()
        {
            InitAndStartCombat(); // 5

            _service.AddRolls(_player, 99);

            Assert.AreEqual(15, _service.GetCurrent(_player));
        }

        [Test]
        public void RestoreCurrent_SetsSavedValue_OverridingCombatStart()
        {
            InitAndStartCombat(); // 5 (el resume corre después de OnCombatStart)

            _service.RestoreCurrent(_player, 11);

            Assert.AreEqual(11, _service.GetCurrent(_player));
        }

        [Test]
        public void RestoreCurrent_ClampsToRange()
        {
            InitAndStartCombat();

            _service.RestoreCurrent(_player, 99);
            Assert.AreEqual(15, _service.GetCurrent(_player));

            _service.RestoreCurrent(_player, -3);
            Assert.AreEqual(0, _service.GetCurrent(_player));
        }

        // --- Bonus por turno (reward "+1 Roll por turno") ----------------------

        [Test]
        public void PerTurnGrantBonus_RaisesTurnGrant()
        {
            InitAndStartCombat();
            _service.TrySpendRolls(_player, 5); // 0

            _service.AddPerTurnGrantBonus(1);
            FinishPlayerTurn();

            Assert.AreEqual(6, _service.GetCurrent(_player));
            Assert.AreEqual(6, _service.GetRollsPerTurn(_player));
        }

        [Test]
        public void RunStart_ResetsPlayerPoolAndBonus()
        {
            InitAndStartCombat();
            _service.AddPerTurnGrantBonus(2);

            EventManager.Trigger(EventName.OnRunStart, Guid.NewGuid(), "default");

            Assert.AreEqual(0, _service.GetCurrent(_player));
            Assert.AreEqual(5, _service.GetRollsPerTurn(_player)); // bonus reseteado
            // El player quedó descacheado: gastar ya no es posible hasta re-init.
            Assert.IsFalse(_service.TrySpendRolls(_player, 1));
        }

        // --- Getters ------------------------------------------------------------

        [Test]
        public void GetCurrent_OtherGuid_ReturnsZero()
        {
            InitAndStartCombat();

            Assert.AreEqual(0, _service.GetCurrent(Guid.NewGuid()));
        }
    }
}
