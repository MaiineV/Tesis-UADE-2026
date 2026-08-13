using NUnit.Framework;
using Rollgeon.Dice.Throw;

namespace Rollgeon.Dice.Tests
{
    [TestFixture]
    public class DiceThrowStateMachineTests
    {
        private DiceThrowStateMachine _sm;

        [SetUp]
        public void SetUp() => _sm = new DiceThrowStateMachine();

        [Test]
        public void Begin_AllParticipants_StartNotThrown_PhasePending()
        {
            _sm.Begin(5);

            for (int i = 0; i < 5; i++)
                Assert.AreEqual(DieThrowState.NotThrown, _sm.GetState(i));
            Assert.AreEqual(DiceThrowPhase.Pending, _sm.Phase);
            Assert.IsFalse(_sm.AllSettled);
        }

        [Test]
        public void Begin_ExcludedMask_MarksExcluded_AndTheyNeverBlockSettle()
        {
            _sm.Begin(5, new[] { false, true, false, true, false });

            Assert.AreEqual(DieThrowState.Excluded, _sm.GetState(1));
            Assert.AreEqual(DieThrowState.Excluded, _sm.GetState(3));

            // Solo los participantes (0,2,4) necesitan asentarse.
            foreach (int i in new[] { 0, 2, 4 })
            {
                _sm.TryGrab(i);
                _sm.ReleaseGrabbed();
                _sm.MarkSettled(i);
            }
            Assert.IsTrue(_sm.AllSettled);
            Assert.AreEqual(DiceThrowPhase.Settled, _sm.Phase);
        }

        [Test]
        public void TryGrab_FromNotThrown_Succeeds_AndPhaseIsGrabbing()
        {
            _sm.Begin(5);

            Assert.IsTrue(_sm.TryGrab(0));
            Assert.AreEqual(DieThrowState.Grabbed, _sm.GetState(0));
            Assert.AreEqual(DiceThrowPhase.Grabbing, _sm.Phase);
        }

        [Test]
        public void TryGrab_Excluded_Fails()
        {
            _sm.Begin(5, new[] { true, false, false, false, false });
            Assert.IsFalse(_sm.TryGrab(0));
        }

        [Test]
        public void TryGrab_Flying_Fails()
        {
            _sm.Begin(5);
            _sm.TryGrab(0);
            _sm.ReleaseGrabbed();

            Assert.IsFalse(_sm.TryGrab(0));
            Assert.AreEqual(DieThrowState.Flying, _sm.GetState(0));
        }

        [Test]
        public void CancelGrab_ReturnsEachDieToItsPreviousState()
        {
            _sm.Begin(5);

            // 0 se tira y asienta; después se re-agarra junto al 1 (virgen).
            _sm.TryGrab(0);
            _sm.ReleaseGrabbed();
            _sm.MarkSettled(0);
            _sm.TryGrab(0);
            _sm.TryGrab(1);

            var cancelled = _sm.CancelGrab();

            CollectionAssert.AreEquivalent(new[] { 0, 1 }, cancelled);
            Assert.AreEqual(DieThrowState.Settled, _sm.GetState(0), "asentado vuelve a Settled");
            Assert.AreEqual(DieThrowState.NotThrown, _sm.GetState(1), "sin tirar vuelve a NotThrown");
        }

        [Test]
        public void ReleaseGrabbed_PartialThrow_OnlyGrabbedFly()
        {
            _sm.Begin(5);
            _sm.TryGrab(0);
            _sm.TryGrab(2);

            var released = _sm.ReleaseGrabbed();

            CollectionAssert.AreEquivalent(new[] { 0, 2 }, released);
            Assert.AreEqual(DieThrowState.Flying, _sm.GetState(0));
            Assert.AreEqual(DieThrowState.NotThrown, _sm.GetState(1));
            Assert.AreEqual(DiceThrowPhase.Flying, _sm.Phase);
        }

        [Test]
        public void AllSettled_RequiresEveryParticipant_NotJustTheThrownOnes()
        {
            _sm.Begin(2);
            _sm.TryGrab(0);
            _sm.ReleaseGrabbed();
            _sm.MarkSettled(0);

            // El dado 1 sigue NotThrown — la sesión no puede cerrar.
            Assert.IsFalse(_sm.AllSettled);
            Assert.AreEqual(DiceThrowPhase.Pending, _sm.Phase);
        }

        [Test]
        public void MarkSettled_OnlyValidFromFlying()
        {
            _sm.Begin(2);
            Assert.IsFalse(_sm.MarkSettled(0), "NotThrown no puede asentarse");
            _sm.TryGrab(0);
            Assert.IsFalse(_sm.MarkSettled(0), "Grabbed no puede asentarse");
            _sm.ReleaseGrabbed();
            Assert.IsTrue(_sm.MarkSettled(0));
        }

        [Test]
        public void Reset_ReturnsToIdle()
        {
            _sm.Begin(5);
            _sm.TryGrab(0);
            _sm.Reset();

            Assert.IsFalse(_sm.IsActive);
            Assert.AreEqual(DiceThrowPhase.Idle, _sm.Phase);
        }
    }
}
