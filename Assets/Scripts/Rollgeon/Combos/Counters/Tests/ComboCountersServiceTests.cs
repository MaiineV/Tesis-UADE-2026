using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Balance;
using Rollgeon.Combos.Counters;
using UnityEngine;

namespace Rollgeon.Combos.Counters.Tests
{
    /// <summary>
    /// Tests del <see cref="ComboCountersService"/> — cubre subscribe/unsubscribe,
    /// lifecycle OnRunStart/OnRunEnd, incremento por TypedEvent&lt;ComboMatchedPayload&gt;,
    /// multiplier formula y event fire. Plan §9.1.
    /// </summary>
    [TestFixture]
    public class ComboCountersServiceTests
    {
        private ComboCountersService _svc;
        private RulesetSO _ruleset;

        // ================================================================
        // Setup / Teardown
        // ================================================================

        [SetUp]
        public void Setup()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            TypedEvent<ComboMatchedPayload>.Clear();

            _ruleset = ScriptableObject.CreateInstance<RulesetSO>();
            // Config default: PerUseBonus=0.02, MaxBonus=0.20.
            ServiceLocator.AddService<RulesetSO>(_ruleset);

            _svc = new ComboCountersService();
            _svc.Register();
        }

        [TearDown]
        public void TearDown()
        {
            _svc?.Dispose();
            _svc = null;

            if (_ruleset != null)
            {
                UnityEngine.Object.DestroyImmediate(_ruleset);
                _ruleset = null;
            }

            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            TypedEvent<ComboMatchedPayload>.Clear();
        }

        // Helpers ----------------------------------------------------------

        private static void TriggerRunStart()
        {
            EventManager.Trigger(EventName.OnRunStart, Guid.NewGuid(), "test-ruleset");
        }

        private static void TriggerRunEnd()
        {
            EventManager.Trigger(EventName.OnRunEnd, Guid.NewGuid(), (object)null);
            // BootstrapHooks.OnRunEnd -> ClearScope(Run) no corre en tests (no instalamos
            // BootstrapHooks); el service por sí solo no limpia — simulamos el teardown
            // legítimo del harness llamando a ClearScope acá.
            ServiceLocator.ClearScope(ServiceScope.Run);
        }

        // ================================================================
        // Registration / Service lookup
        // ================================================================

        [Test]
        public void Register_SelfRegistersUnderInterface()
        {
            Assert.IsTrue(ServiceLocator.HasService<IComboCountersService>());
            Assert.AreSame(_svc, ServiceLocator.GetService<IComboCountersService>());
        }

        // ================================================================
        // Counters start at 0 (out of run)
        // ================================================================

        [Test]
        public void GetCount_OutOfRun_ReturnsZero()
        {
            // No OnRunStart triggered — no state.
            Assert.AreEqual(0, _svc.GetCount("combo.par"));
        }

        [Test]
        public void GetBonusMultiplier_OutOfRun_ReturnsOne()
        {
            Assert.AreEqual(1f, _svc.GetBonusMultiplier("combo.par"));
        }

        [Test]
        public void IncrementCount_OutOfRun_IsNoOp()
        {
            // No throws, and a subsequent OnRunStart starts from 0.
            _svc.IncrementCount("combo.par");

            TriggerRunStart();
            Assert.AreEqual(0, _svc.GetCount("combo.par"));
        }

        // ================================================================
        // OnRunStart lifecycle
        // ================================================================

        [Test]
        public void OnRunStart_CreatesFreshStateInRunScope()
        {
            TriggerRunStart();

            Assert.IsTrue(ServiceLocator.HasService<RunComboCounterState>());
            var state = ServiceLocator.GetService<RunComboCounterState>();
            Assert.IsNotNull(state);
            Assert.AreEqual(0, state.Counts.Count);
        }

        [Test]
        public void OnRunStart_AfterEnd_ResetsCounts()
        {
            TriggerRunStart();
            _svc.IncrementCount("combo.par");
            _svc.IncrementCount("combo.par");
            Assert.AreEqual(2, _svc.GetCount("combo.par"));

            TriggerRunEnd();

            // After ClearScope(Run), GetCount returns 0.
            Assert.AreEqual(0, _svc.GetCount("combo.par"));

            TriggerRunStart();
            Assert.AreEqual(0, _svc.GetCount("combo.par"));
        }

        // ================================================================
        // Increment
        // ================================================================

        [Test]
        public void IncrementCount_UpdatesStateAndFiresEvent()
        {
            TriggerRunStart();

            string capturedId = null;
            int capturedCount = -1;
            EventManager.Subscribe(EventName.OnComboCounterIncremented, args =>
            {
                capturedId = args[0] as string;
                capturedCount = (int)args[1];
            });

            _svc.IncrementCount("combo.par");

            Assert.AreEqual(1, _svc.GetCount("combo.par"));
            Assert.AreEqual("combo.par", capturedId);
            Assert.AreEqual(1, capturedCount);
        }

        [Test]
        public void IncrementCount_MultipleCalls_Accumulate()
        {
            TriggerRunStart();

            _svc.IncrementCount("combo.par");
            _svc.IncrementCount("combo.par");
            _svc.IncrementCount("combo.par");

            Assert.AreEqual(3, _svc.GetCount("combo.par"));
        }

        [Test]
        public void IncrementCount_NullOrEmpty_NoOpNoEvent()
        {
            TriggerRunStart();

            int calls = 0;
            EventManager.Subscribe(EventName.OnComboCounterIncremented, _ => calls++);

            _svc.IncrementCount(null);
            _svc.IncrementCount(string.Empty);

            Assert.AreEqual(0, calls);
            Assert.AreEqual(0, _svc.GetCount("combo.par"));
        }

        // ================================================================
        // TypedEvent<ComboMatchedPayload> subscription
        // ================================================================

        [Test]
        public void ComboMatchedPayload_Subscription_IncrementsCounter()
        {
            TriggerRunStart();

            TypedEvent<ComboMatchedPayload>.Raise(new ComboMatchedPayload
            {
                SourceGuid = Guid.NewGuid(),
                ComboId = "combo.trio",
                BaseDamage = 42,
            });

            Assert.AreEqual(1, _svc.GetCount("combo.trio"));
        }

        [Test]
        public void ComboMatchedPayload_Subscription_FiresIncrementEvent()
        {
            TriggerRunStart();

            string capturedId = null;
            int capturedCount = -1;
            EventManager.Subscribe(EventName.OnComboCounterIncremented, args =>
            {
                capturedId = args[0] as string;
                capturedCount = (int)args[1];
            });

            TypedEvent<ComboMatchedPayload>.Raise(new ComboMatchedPayload
            {
                SourceGuid = Guid.NewGuid(),
                ComboId = "combo.poker",
                BaseDamage = 60,
            });

            Assert.AreEqual("combo.poker", capturedId);
            Assert.AreEqual(1, capturedCount);
        }

        [Test]
        public void ComboMatchedPayload_WithNullId_NoOp()
        {
            TriggerRunStart();

            TypedEvent<ComboMatchedPayload>.Raise(new ComboMatchedPayload
            {
                SourceGuid = Guid.NewGuid(),
                ComboId = null,
                BaseDamage = 0,
            });

            Assert.AreEqual(0, _svc.GetCount(null));
        }

        // ================================================================
        // GetBonusMultiplier — formula + cap
        // ================================================================

        [Test]
        public void GetBonusMultiplier_Count0_ReturnsOne()
        {
            TriggerRunStart();
            Assert.AreEqual(1f, _svc.GetBonusMultiplier("combo.par"));
        }

        [Test]
        public void GetBonusMultiplier_MatchesConfigFormula()
        {
            TriggerRunStart();

            // Default ruleset: PerUseBonus=0.02, MaxBonus=0.20.
            for (int i = 0; i < 5; i++) _svc.IncrementCount("combo.par");

            // 1 + min(0.20, 5*0.02) = 1.10
            Assert.AreEqual(1.10f, _svc.GetBonusMultiplier("combo.par"), 1e-5f);
        }

        [Test]
        public void GetBonusMultiplier_CapEnforced()
        {
            TriggerRunStart();

            // 50 uses → raw = 1.00 → capped to 0.20 → mult = 1.20.
            for (int i = 0; i < 50; i++) _svc.IncrementCount("combo.par");

            Assert.AreEqual(1.20f, _svc.GetBonusMultiplier("combo.par"), 1e-5f);
        }

        [Test]
        public void GetBonusMultiplier_NoRuleset_DegradesToOne()
        {
            // Remove the ruleset so the service resolves null.
            ServiceLocator.RemoveService<RulesetSO>();
            _svc.ConfigureForTests(null);

            TriggerRunStart();
            _svc.IncrementCount("combo.par");

            // Count is 1 but multiplier path needs config → returns 1.0f.
            Assert.AreEqual(1, _svc.GetCount("combo.par"));
            Assert.AreEqual(1f, _svc.GetBonusMultiplier("combo.par"));
        }

        // ================================================================
        // RunEnd clears (via ClearScope)
        // ================================================================

        [Test]
        public void AfterRunEnd_StateIsGone()
        {
            TriggerRunStart();
            _svc.IncrementCount("combo.par");

            TriggerRunEnd();

            Assert.IsFalse(ServiceLocator.HasService<RunComboCounterState>());
            Assert.AreEqual(0, _svc.GetCount("combo.par"));
        }

        // ================================================================
        // Dispose unsubscribes
        // ================================================================

        [Test]
        public void Dispose_UnsubscribesFromComboMatchedPayload()
        {
            TriggerRunStart();

            _svc.Dispose();

            TypedEvent<ComboMatchedPayload>.Raise(new ComboMatchedPayload
            {
                SourceGuid = Guid.NewGuid(),
                ComboId = "combo.par",
                BaseDamage = 1,
            });

            // State still exists but service no longer listens.
            var state = ServiceLocator.GetService<RunComboCounterState>();
            Assert.AreEqual(0, state.Get("combo.par"));

            // Prevent TearDown from calling Dispose() a second time; nulling is enough.
            _svc = null;
        }
    }
}
