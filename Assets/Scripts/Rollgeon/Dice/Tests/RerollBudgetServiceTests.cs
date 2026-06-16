using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Actions;
using Rollgeon.Combat.EnergyLib;
using Rollgeon.Effects;
using UnityEngine;

namespace Rollgeon.Dice.Tests
{
    /// <summary>
    /// Fake minimalista de <see cref="IEnergyService"/> — in-memory dict, sin
    /// <c>AttributesManager</c> ni Unity events. Basado en el fake del test suite
    /// de <c>TurnManager</c>.
    /// </summary>
    internal sealed class FakeEnergyService : IEnergyService
    {
        public readonly Dictionary<Guid, int> Current = new Dictionary<Guid, int>();
        public int MaxPerEntity = 4;
        public int SpendCallCount { get; private set; }
        public int LastSpendCost { get; private set; }

        public void InitializeForEntity(Guid entityId) => Current[entityId] = MaxPerEntity;

        public bool SpendEnergy(Guid entityId, int cost)
        {
            SpendCallCount++;
            LastSpendCost = cost;
            if (cost < 0) return false;
            if (!Current.TryGetValue(entityId, out var have)) return false;
            if (cost > have) return false;
            Current[entityId] = have - cost;
            return true;
        }

        public void RegenerateAtTurnEnd(Guid entityId) { /* no-op */ }

        public int GetCurrent(Guid entityId) => Current.TryGetValue(entityId, out var v) ? v : 0;

        public int GetMax(Guid entityId) => MaxPerEntity;
    }

    [TestFixture]
    public class RerollBudgetServiceTests
    {
        private RerollBudgetService _svc;
        private FakeEnergyService _energy;
        private List<ActionDefinitionSO> _createdDefs;
        private Guid _player;

        [SetUp]
        public void Setup()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _energy = new FakeEnergyService();
            _player = Guid.NewGuid();
            _energy.Current[_player] = 4;

            _svc = new RerollBudgetService();
            _svc.ConfigureForTests(_energy, ruleset: null);

            _createdDefs = new List<ActionDefinitionSO>();
        }

        [TearDown]
        public void TearDown()
        {
            _svc?.Dispose();
            _svc = null;

            foreach (var def in _createdDefs)
            {
                if (def != null) UnityEngine.Object.DestroyImmediate(def);
            }
            _createdDefs = null;

            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        // --- Helpers -----------------------------------------------------

        private ActionDefinitionSO MakeAction(int freeRollCount = 3, bool allowsEnergyReroll = true,
            string id = "test.action")
        {
            var def = ScriptableObject.CreateInstance<ActionDefinitionSO>();
            def.ActionId = id;
            def.Type = ActionType.Attack;
            def.EnergyCost = 0;
            def.FreeRollCount = freeRollCount;
            def.AllowsEnergyReroll = allowsEnergyReroll;
            def.Effect = new EffectData();
            _createdDefs.Add(def);
            return def;
        }

        // ==================================================================
        // StartBudget / EndBudget
        // ==================================================================

        [Test]
        public void StartBudget_SetsCurrentAndMirrorsFreeRollCount()
        {
            // FreeRollCount=3 (total) → FreeRollsRemaining=3. El primer roll
            // tambien consume del budget en el flow manual.
            var action = MakeAction(freeRollCount: 3);

            _svc.StartBudget(action);

            Assert.IsNotNull(_svc.Current);
            Assert.AreSame(action, _svc.Current.Action);
            Assert.AreEqual(3, _svc.Current.FreeRollsRemaining);
            Assert.AreEqual(0, _svc.Current.PaidRollsUsed);
        }

        [Test]
        public void StartBudget_WithFreeRollCount1_GivesOneFreeRoll()
        {
            var action = MakeAction(freeRollCount: 1);

            _svc.StartBudget(action);

            Assert.AreEqual(1, _svc.Current.FreeRollsRemaining);
        }

        [Test]
        public void StartBudget_WithFreeRollCount0_ClampsToZero()
        {
            var action = MakeAction(freeRollCount: 0);

            _svc.StartBudget(action);

            Assert.AreEqual(0, _svc.Current.FreeRollsRemaining);
        }

        [Test]
        public void StartBudget_FiresOnBudgetStartedWithCurrentBudget()
        {
            var action = MakeAction(freeRollCount: 3);
            RerollBudget captured = null;
            _svc.OnBudgetStarted += b => captured = b;

            _svc.StartBudget(action);

            Assert.IsNotNull(captured);
            Assert.AreSame(_svc.Current, captured);
            Assert.AreEqual(3, captured.FreeRollsRemaining);
            Assert.AreSame(action, captured.Action);
        }

        [Test]
        public void StartBudget_NullAction_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _svc.StartBudget(null));
        }

        [Test]
        public void StartBudget_TwiceWithoutEnd_Throws()
        {
            var a = MakeAction();
            _svc.StartBudget(a);

            var b = MakeAction(id: "test.other");
            Assert.Throws<InvalidOperationException>(() => _svc.StartBudget(b));
        }

        [Test]
        public void EndBudget_ClearsCurrent()
        {
            var action = MakeAction();
            _svc.StartBudget(action);

            _svc.EndBudget();

            Assert.IsNull(_svc.Current);
        }

        [Test]
        public void EndBudget_WithoutStart_IsNoOp()
        {
            Assert.DoesNotThrow(() => _svc.EndBudget());
            Assert.IsNull(_svc.Current);
        }

        [Test]
        public void EndBudget_ThenStartBudget_HasFreshCounters()
        {
            var first = MakeAction(freeRollCount: 3);
            _svc.StartBudget(first);
            _svc.TryExtraRoll(_player); // consume uno gratis → 2 left
            _svc.EndBudget();

            var second = MakeAction(freeRollCount: 3, id: "test.other");
            _svc.StartBudget(second);

            Assert.AreEqual(3, _svc.Current.FreeRollsRemaining);
            Assert.AreEqual(0, _svc.Current.PaidRollsUsed);
            Assert.AreSame(second, _svc.Current.Action);
        }

        // ==================================================================
        // QueryExtraRoll
        // ==================================================================

        [Test]
        public void Query_WithNoActiveBudget_ReturnsBlockedNoActiveBudget()
        {
            var result = _svc.QueryExtraRoll(_player);

            Assert.IsFalse(result.IsAvailable);
            Assert.AreEqual(RerollBudgetService.BlockedReasonNoActiveBudget, result.BlockedReason);
        }

        [Test]
        public void Query_WithFreeRerollsLeft_ReturnsFree()
        {
            _svc.StartBudget(MakeAction(freeRollCount: 3));

            var result = _svc.QueryExtraRoll(_player);

            Assert.IsTrue(result.IsAvailable);
            Assert.IsTrue(result.IsFreeRoll);
            Assert.IsFalse(result.CostsEnergy);
            Assert.IsNull(result.BlockedReason);
        }

        [Test]
        public void Query_NoFreeButEnergyAvailable_ReturnsPaid()
        {
            // freeRollCount=0 → FreeRollsRemaining=0 desde el inicio. Primer query es paid.
            _svc.StartBudget(MakeAction(freeRollCount: 0, allowsEnergyReroll: true));

            var result = _svc.QueryExtraRoll(_player);

            Assert.IsTrue(result.IsAvailable);
            Assert.IsFalse(result.IsFreeRoll);
            Assert.IsTrue(result.CostsEnergy);
        }

        [Test]
        public void Query_NoFreeAndActionForbids_ReturnsBlockedActionForbids()
        {
            _svc.StartBudget(MakeAction(freeRollCount: 0, allowsEnergyReroll: false));

            var result = _svc.QueryExtraRoll(_player);

            Assert.IsFalse(result.IsAvailable);
            Assert.AreEqual(RerollBudgetService.BlockedReasonActionForbidsEnergyReroll, result.BlockedReason);
        }

        [Test]
        public void Query_NoFreeAndNoEnergy_ReturnsBlockedNoEnergy()
        {
            _energy.Current[_player] = 0;
            _svc.StartBudget(MakeAction(freeRollCount: 0, allowsEnergyReroll: true));

            var result = _svc.QueryExtraRoll(_player);

            Assert.IsFalse(result.IsAvailable);
            Assert.AreEqual(RerollBudgetService.BlockedReasonNoEnergy, result.BlockedReason);
        }

        // ==================================================================
        // TryExtraRoll — free path
        // ==================================================================

        [Test]
        public void TryExtra_FreePath_GrantsAndDoesNotSpendEnergy()
        {
            _svc.StartBudget(MakeAction(freeRollCount: 3));
            int energyBefore = _energy.Current[_player];

            bool ok = _svc.TryExtraRoll(_player);

            Assert.IsTrue(ok);
            Assert.AreEqual(2, _svc.Current.FreeRollsRemaining); // 3 → 2
            Assert.AreEqual(0, _svc.Current.PaidRollsUsed);
            Assert.AreEqual(0, _energy.SpendCallCount);
            Assert.AreEqual(energyBefore, _energy.Current[_player]);
        }

        [Test]
        public void TryExtra_ExhaustsAllFree_ThenPaidKicksIn()
        {
            _svc.StartBudget(MakeAction(freeRollCount: 3, allowsEnergyReroll: true));

            // 3 free rolls (incluye el primer roll).
            Assert.IsTrue(_svc.TryExtraRoll(_player));
            Assert.IsTrue(_svc.TryExtraRoll(_player));
            Assert.IsTrue(_svc.TryExtraRoll(_player));
            Assert.AreEqual(0, _svc.Current.FreeRollsRemaining);
            Assert.AreEqual(0, _energy.SpendCallCount);

            // 4to try → paid.
            Assert.IsTrue(_svc.TryExtraRoll(_player));
            Assert.AreEqual(1, _svc.Current.PaidRollsUsed);
            Assert.AreEqual(1, _energy.SpendCallCount);
            Assert.AreEqual(3, _energy.Current[_player]); // 4 → 3
        }

        // ==================================================================
        // TryExtraRoll — paid path
        // ==================================================================

        [Test]
        public void TryExtra_PaidPath_SpendsOneEnergy()
        {
            _svc.StartBudget(MakeAction(freeRollCount: 0, allowsEnergyReroll: true));
            // FreeRollsRemaining=0 → primer try es paid.

            bool ok = _svc.TryExtraRoll(_player);

            Assert.IsTrue(ok);
            Assert.AreEqual(1, _svc.Current.PaidRollsUsed);
            Assert.AreEqual(1, _energy.SpendCallCount);
            Assert.AreEqual(1, _energy.LastSpendCost);
            Assert.AreEqual(3, _energy.Current[_player]); // 4 → 3
        }

        [Test]
        public void TryExtra_PaidWithNoEnergy_ReturnsFalseAndDoesNotMutate()
        {
            _energy.Current[_player] = 0;
            _svc.StartBudget(MakeAction(freeRollCount: 0, allowsEnergyReroll: true));

            bool ok = _svc.TryExtraRoll(_player);

            Assert.IsFalse(ok);
            Assert.AreEqual(0, _svc.Current.PaidRollsUsed);
            Assert.AreEqual(0, _svc.Current.FreeRollsRemaining); // sigue en 0
            Assert.AreEqual(1, _energy.SpendCallCount); // se intento.
            Assert.AreEqual(0, _energy.Current[_player]);
        }

        [Test]
        public void TryExtra_ActionForbidsEnergyReroll_BlocksPaidEvenWithEnergy()
        {
            _svc.StartBudget(MakeAction(freeRollCount: 0, allowsEnergyReroll: false));
            // Hay 4 de energia pero la accion prohibe paid rerolls.

            bool ok = _svc.TryExtraRoll(_player);

            Assert.IsFalse(ok);
            Assert.AreEqual(0, _svc.Current.PaidRollsUsed);
            Assert.AreEqual(0, _energy.SpendCallCount);
            Assert.AreEqual(4, _energy.Current[_player]);
        }

        [Test]
        public void TryExtra_NoActiveBudget_ReturnsFalse()
        {
            // No StartBudget.
            // LogAssert: el servicio logea error pero retorna false.
            UnityEngine.TestTools.LogAssert.Expect(
                LogType.Error,
                new System.Text.RegularExpressions.Regex("RerollBudgetService.*no active budget"));

            bool ok = _svc.TryExtraRoll(_player);

            Assert.IsFalse(ok);
        }

        // ==================================================================
        // OnRerollStarted event
        // ==================================================================

        [Test]
        public void TryExtra_Free_FiresOnRerollStartedWithIsFreeTrueAndPostSpendCounts()
        {
            var action = MakeAction(freeRollCount: 3);
            _svc.StartBudget(action);

            RerollStartedPayload? captured = null;
            _svc.OnRerollStarted += p => captured = p;

            _svc.TryExtraRoll(_player);

            Assert.IsTrue(captured.HasValue);
            var p = captured.Value;
            Assert.AreEqual(_player, p.PlayerGuid);
            Assert.AreSame(action, p.Action);
            Assert.IsTrue(p.IsFree);
            Assert.AreEqual(2, p.FreeRollsRemaining); // post-consume: 3-1=2
            Assert.AreEqual(0, p.PaidRollsUsed);
        }

        [Test]
        public void TryExtra_Paid_FiresOnRerollStartedWithIsFreeFalse()
        {
            var action = MakeAction(freeRollCount: 0, allowsEnergyReroll: true);
            _svc.StartBudget(action);

            RerollStartedPayload? captured = null;
            _svc.OnRerollStarted += p => captured = p;

            _svc.TryExtraRoll(_player);

            Assert.IsTrue(captured.HasValue);
            var p = captured.Value;
            Assert.IsFalse(p.IsFree);
            Assert.AreEqual(0, p.FreeRollsRemaining);
            Assert.AreEqual(1, p.PaidRollsUsed);
            Assert.AreSame(action, p.Action);
        }

        [Test]
        public void TryExtra_BlockedPaid_DoesNotFireOnRerollStarted()
        {
            _energy.Current[_player] = 0;
            _svc.StartBudget(MakeAction(freeRollCount: 0, allowsEnergyReroll: true));

            int fireCount = 0;
            _svc.OnRerollStarted += _ => fireCount++;

            _svc.TryExtraRoll(_player);

            Assert.AreEqual(0, fireCount);
        }

        [Test]
        public void TryExtra_AlsoFiresLegacyEventManagerOnRerollStarted()
        {
            _svc.StartBudget(MakeAction(freeRollCount: 2));

            var received = new List<object[]>();
            EventManager.EventReceiver rec = args => received.Add(args);
            EventManager.Subscribe(EventName.OnRerollStarted, rec);

            _svc.TryExtraRoll(_player);

            try
            {
                Assert.AreEqual(1, received.Count);
                Assert.AreEqual(_player, (Guid)received[0][0]);
                Assert.AreEqual(1, (int)received[0][1]); // rerollIndex 1 (primer reroll).
            }
            finally
            {
                EventManager.UnSubscribe(EventName.OnRerollStarted, rec);
            }
        }
    }
}
