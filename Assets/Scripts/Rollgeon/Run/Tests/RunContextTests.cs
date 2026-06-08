using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Heroes;
using UnityEngine;

namespace Rollgeon.Run.Tests
{
    [TestFixture]
    public class RunContextTests
    {
        private ClassHeroSO _hero;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            _hero = ScriptableObject.CreateInstance<ClassHeroSO>();
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            if (_hero != null)
                UnityEngine.Object.DestroyImmediate(_hero);
        }

        [Test]
        public void Constructor_SetsRunIdAndHero()
        {
            var runId = Guid.NewGuid();

            var ctx = new RunContext(runId, _hero);

            Assert.AreEqual(runId, ctx.RunId);
            Assert.AreSame(_hero, ctx.SelectedHero);
        }

        [Test]
        public void FloorIndex_StartsAtZero()
        {
            var ctx = new RunContext(Guid.NewGuid(), _hero);

            Assert.AreEqual(0, ctx.FloorIndex);
        }

        [Test]
        public void IsRunActive_StartsTrue()
        {
            var ctx = new RunContext(Guid.NewGuid(), _hero);

            Assert.IsTrue(ctx.IsRunActive);
        }

        [Test]
        public void AdvanceFloor_IncrementsFloorIndex()
        {
            var ctx = new RunContext(Guid.NewGuid(), _hero);

            ctx.AdvanceFloor();
            Assert.AreEqual(1, ctx.FloorIndex);

            ctx.AdvanceFloor();
            Assert.AreEqual(2, ctx.FloorIndex);
        }

        [Test]
        public void AdvanceFloor_FiresOnFloorChangedEvent()
        {
            var runId = Guid.NewGuid();
            var ctx = new RunContext(runId, _hero);
            Guid receivedRunId = Guid.Empty;
            int receivedFloor = -1;

            EventManager.Subscribe(EventName.OnFloorChanged, args =>
            {
                receivedRunId = (Guid)args[0];
                receivedFloor = (int)args[1];
            });

            ctx.AdvanceFloor();

            Assert.AreEqual(runId, receivedRunId);
            Assert.AreEqual(1, receivedFloor);
        }

        [Test]
        public void EndRun_SetsIsRunActiveFalse()
        {
            var ctx = new RunContext(Guid.NewGuid(), _hero);

            ctx.EndRun();

            Assert.IsFalse(ctx.IsRunActive);
        }

        [Test]
        public void Constructor_NullHero_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new RunContext(Guid.NewGuid(), null));
        }

        // ---- ISaveable (#158) ------------------------------------------------

        [Test]
        public void SaveKey_IsStable()
        {
            var ctx = new RunContext(Guid.NewGuid(), _hero);

            Assert.AreEqual("run.floor_index", ctx.SaveKey);
        }

        [Test]
        public void CaptureState_ReturnsCurrentFloorIndex()
        {
            var ctx = new RunContext(Guid.NewGuid(), _hero);
            ctx.AdvanceFloor();
            ctx.AdvanceFloor();

            Assert.AreEqual(2, (int)ctx.CaptureState());
        }

        [Test]
        public void RestoreState_RestoresFloorIndex()
        {
            var ctx = new RunContext(Guid.NewGuid(), _hero);

            ctx.RestoreState(5);

            Assert.AreEqual(5, ctx.FloorIndex);
        }

        [Test]
        public void RestoreState_NonIntPayload_IsIgnored()
        {
            var ctx = new RunContext(Guid.NewGuid(), _hero);
            ctx.AdvanceFloor();

            ctx.RestoreState("not-an-int");

            Assert.AreEqual(1, ctx.FloorIndex, "Un payload inválido no debe corromper el estado.");
        }
    }
}
