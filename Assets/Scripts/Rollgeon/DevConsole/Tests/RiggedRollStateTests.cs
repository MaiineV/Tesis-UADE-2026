using NUnit.Framework;
using Rollgeon.Dice;

namespace Rollgeon.DevConsole.Tests
{
    public class RiggedRollStateTests
    {
        [Test]
        public void should_have_no_pending_initially()
        {
            var rig = new RiggedRollState();
            Assert.IsFalse(rig.HasPending);
        }

        [Test]
        public void should_consume_next_once_when_set()
        {
            var rig = new RiggedRollState();
            rig.SetNext(new[] { 3, 4, 5 });

            Assert.IsTrue(rig.HasPending);
            Assert.IsTrue(rig.TryConsumeNext(out var faces));
            Assert.AreEqual(new[] { 3, 4, 5 }, faces);

            // One-shot: el segundo consume falla.
            Assert.IsFalse(rig.HasPending);
            Assert.IsFalse(rig.TryConsumeNext(out _));
        }

        [Test]
        public void should_clear_pending_when_set_empty()
        {
            var rig = new RiggedRollState();
            rig.SetNext(new[] { 1, 2 });
            rig.SetNext(null);

            Assert.IsFalse(rig.HasPending);
        }
    }
}
