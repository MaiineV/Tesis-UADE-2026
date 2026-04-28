using NUnit.Framework;
using Rollgeon.PreConditions.Concretes;

namespace Rollgeon.PreConditions.Tests
{
    [TestFixture]
    public class PcRoundNumberTests
    {
        private static PreConditionContext Ctx(int? round) =>
            new PreConditionContext { RoundIndex = round };

        [Test]
        public void Evaluate_Equal_MatchesExact()
        {
            var pc = new PcRoundNumber { Mode = PcRoundNumber.CompareMode.Equal, Value = 3 };
            Assert.IsTrue(pc.Evaluate(Ctx(3)));
            Assert.IsFalse(pc.Evaluate(Ctx(4)));
        }

        [Test]
        public void Evaluate_GreaterOrEqual_MatchesAtAndAbove()
        {
            var pc = new PcRoundNumber { Mode = PcRoundNumber.CompareMode.GreaterOrEqual, Value = 5 };
            Assert.IsFalse(pc.Evaluate(Ctx(4)));
            Assert.IsTrue(pc.Evaluate(Ctx(5)));
            Assert.IsTrue(pc.Evaluate(Ctx(7)));
        }

        [Test]
        public void Evaluate_Multiple_MatchesEveryN()
        {
            var pc = new PcRoundNumber { Mode = PcRoundNumber.CompareMode.Multiple, Value = 3 };
            Assert.IsTrue(pc.Evaluate(Ctx(3)));
            Assert.IsTrue(pc.Evaluate(Ctx(6)));
            Assert.IsFalse(pc.Evaluate(Ctx(5)));
            Assert.IsFalse(pc.Evaluate(Ctx(0)));
        }

        [Test]
        public void Evaluate_NoRoundProvided_ReturnsTrue()
        {
            // Semántica permisiva: si el caller no provee round (ej. flujo héroe),
            // la PC no debe vetar — devuelve true.
            var pc = new PcRoundNumber { Mode = PcRoundNumber.CompareMode.Equal, Value = 3 };
            Assert.IsTrue(pc.Evaluate(Ctx(null)));
        }

        [Test]
        public void Evaluate_NullContext_ReturnsTrue()
        {
            var pc = new PcRoundNumber { Mode = PcRoundNumber.CompareMode.Equal, Value = 3 };
            Assert.IsTrue(pc.Evaluate(null));
        }
    }
}
