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

        // --- Bonus de pool (reward "Rolls +1", BUG-85) --------------------------

        [Test]
        public void should_raise_max_when_roll_pool_bonus_added()
        {
            // Arrange
            _service.InitializeForEntity(_player);

            // Act
            _service.AddRollPoolBonus(2);

            // Assert — el bonus sube el TECHO del pool, solo para el player.
            Assert.AreEqual(17, _service.GetMax(_player));
            Assert.AreEqual(15, _service.GetMax(Guid.NewGuid()),
                "El bonus es exclusivo del player.");
        }

        [Test]
        public void should_start_combat_with_base_start_plus_bonus()
        {
            // Arrange — reward reclamado en exploración, antes del combate.
            _service.InitializeForEntity(_player);
            _service.AddRollPoolBonus(2);

            // Act
            EventManager.Trigger(EventName.OnCombatStart, Guid.NewGuid());

            // Assert — visible desde el primer turno: 5 base + 2 bonus.
            Assert.AreEqual(7, _service.GetCurrent(_player));
        }

        [Test]
        public void should_clamp_combat_start_to_raised_max()
        {
            // Arrange — arranque base ya en el cap: el bonus levanta ambos.
            _ruleset.RollPool.RollsAtCombatStart = 15;
            _service.InitializeForEntity(_player);
            _service.AddRollPoolBonus(1);

            // Act
            EventManager.Trigger(EventName.OnCombatStart, Guid.NewGuid());

            // Assert — 15 + 1 clampeado al max nuevo (16).
            Assert.AreEqual(16, _service.GetCurrent(_player));
        }

        [Test]
        public void should_trigger_rolls_changed_with_new_max_when_bonus_added()
        {
            // Arrange
            _service.InitializeForEntity(_player);
            _rollsChangedArgs.Clear();

            // Act — el claim ocurre en exploración; el HUD refresca al instante.
            _service.AddRollPoolBonus(1);

            // Assert
            Assert.AreEqual(1, _rollsChangedArgs.Count);
            Assert.AreEqual(16, (int)_rollsChangedArgs[0][2]);
        }

        [Test]
        public void should_not_grant_bonus_per_turn_anymore()
        {
            // Arrange — regresión del cambio de semántica: el bonus ya no infla el
            // grant por turno (antes era su único efecto y resultaba invisible).
            InitAndStartCombat();
            _service.AddRollPoolBonus(3);
            _service.TrySpendRolls(_player, _service.GetCurrent(_player)); // 0

            // Act
            FinishPlayerTurn();

            // Assert — el grant sigue siendo el base del ruleset.
            Assert.AreEqual(5, _service.GetRollsPerTurn(_player));
            Assert.AreEqual(5, _service.GetCurrent(_player));
        }

        [Test]
        public void RunStart_ResetsPlayerPoolAndBonus()
        {
            InitAndStartCombat();
            _service.AddRollPoolBonus(2);

            EventManager.Trigger(EventName.OnRunStart, Guid.NewGuid(), "default");

            Assert.AreEqual(0, _service.GetCurrent(_player));
            Assert.AreEqual(15, _service.GetMax(Guid.NewGuid()), "bonus reseteado (cap base)");
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
