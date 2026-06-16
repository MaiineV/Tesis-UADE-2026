using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Combos.Counters;

namespace Rollgeon.Combos.Counters.Tests
{
    /// <summary>
    /// Tests del POCO <see cref="RunComboCounterState"/> — <c>Get</c>/<c>Increment</c>/<c>Reset</c>
    /// + ISaveable round-trip. Plan §9.1.
    /// </summary>
    [TestFixture]
    public class RunComboCounterStateTests
    {
        private RunComboCounterState _state;

        [SetUp]
        public void Setup()
        {
            _state = new RunComboCounterState();
        }

        [Test]
        public void Get_UnknownCombo_ReturnsZero()
        {
            Assert.AreEqual(0, _state.Get("combo.par"));
        }

        [Test]
        public void Get_NullOrEmpty_ReturnsZero()
        {
            Assert.AreEqual(0, _state.Get(null));
            Assert.AreEqual(0, _state.Get(string.Empty));
        }

        [Test]
        public void Increment_FirstCall_ReturnsOne()
        {
            Assert.AreEqual(1, _state.Increment("combo.par"));
            Assert.AreEqual(1, _state.Get("combo.par"));
        }

        [Test]
        public void Increment_MultipleCalls_Accumulate()
        {
            _state.Increment("combo.par");
            _state.Increment("combo.par");
            _state.Increment("combo.par");

            Assert.AreEqual(3, _state.Get("combo.par"));
        }

        [Test]
        public void Increment_DifferentCombos_AreIndependent()
        {
            _state.Increment("combo.par");
            _state.Increment("combo.trio");
            _state.Increment("combo.trio");

            Assert.AreEqual(1, _state.Get("combo.par"));
            Assert.AreEqual(2, _state.Get("combo.trio"));
        }

        [Test]
        public void Increment_NullOrEmpty_NoOp()
        {
            _state.Increment(null);
            _state.Increment(string.Empty);

            Assert.AreEqual(0, _state.Counts.Count);
        }

        [Test]
        public void Reset_ClearsAllCounts()
        {
            _state.Increment("combo.par");
            _state.Increment("combo.trio");

            _state.Reset();

            Assert.AreEqual(0, _state.Get("combo.par"));
            Assert.AreEqual(0, _state.Get("combo.trio"));
            Assert.AreEqual(0, _state.Counts.Count);
        }

        // ====================================================================
        // ISaveable
        // ====================================================================

        [Test]
        public void SaveKey_IsStableConstant()
        {
            Assert.AreEqual(RunComboCounterState.SaveKeyConst, _state.SaveKey);
            Assert.AreEqual("run.combo_counter_state", _state.SaveKey);
        }

        [Test]
        public void CaptureState_ReturnsIndependentClone()
        {
            _state.Increment("combo.par");
            _state.Increment("combo.trio");

            var snapshot = _state.CaptureState() as IDictionary<string, int>;
            Assert.IsNotNull(snapshot);

            // Mutar el state vivo no debe afectar al snapshot.
            _state.Increment("combo.par");
            Assert.AreEqual(1, snapshot["combo.par"]);
            Assert.AreEqual(1, snapshot["combo.trio"]);
        }

        [Test]
        public void RestoreState_ReplacesExistingDict()
        {
            _state.Increment("combo.par");

            var restore = new Dictionary<string, int>
            {
                { "combo.trio", 5 },
                { "combo.poker", 2 },
            };
            _state.RestoreState(restore);

            // Original combo.par got wiped; restored values present.
            Assert.AreEqual(0, _state.Get("combo.par"));
            Assert.AreEqual(5, _state.Get("combo.trio"));
            Assert.AreEqual(2, _state.Get("combo.poker"));
        }

        [Test]
        public void RestoreState_Null_ClearsDict()
        {
            _state.Increment("combo.par");
            _state.RestoreState(null);

            Assert.AreEqual(0, _state.Counts.Count);
        }

        [Test]
        public void Capture_Restore_RoundTrip_Preserves_All_Keys()
        {
            _state.Increment("combo.par");
            _state.Increment("combo.par");
            _state.Increment("combo.trio");
            _state.Increment("combo.poker");

            var snapshot = _state.CaptureState();

            var restored = new RunComboCounterState();
            restored.RestoreState(snapshot);

            Assert.AreEqual(2, restored.Get("combo.par"));
            Assert.AreEqual(1, restored.Get("combo.trio"));
            Assert.AreEqual(1, restored.Get("combo.poker"));
        }
    }
}
