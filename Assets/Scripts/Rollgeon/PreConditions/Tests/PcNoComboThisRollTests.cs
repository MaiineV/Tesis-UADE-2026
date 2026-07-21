using NUnit.Framework;
using Rollgeon.Combos;
using Rollgeon.Effects;
using Rollgeon.PreConditions.Concretes;

namespace Rollgeon.PreConditions.Tests
{
    /// <summary>Tests de <see cref="PcNoComboThisRoll"/> (predicado de ModifyResourceTrigger legacy).</summary>
    [TestFixture]
    public class PcNoComboThisRollTests
    {
        [Test]
        public void Evaluate_NoContext_ReturnsTrue()
        {
            Assert.IsTrue(new PcNoComboThisRoll().Evaluate(null));
        }

        [Test]
        public void Evaluate_ContextWithoutComboResult_ReturnsTrue()
        {
            var ctx = new PreConditionContext { Effect = new EffectContext() };

            Assert.IsTrue(new PcNoComboThisRoll().Evaluate(ctx));
        }

        [Test]
        public void Evaluate_NoMatchResult_ReturnsTrue()
        {
            var ctx = new PreConditionContext
            {
                Effect = new EffectContext { ComboResult = ComboDetectionResult.NoMatch() },
            };

            Assert.IsTrue(new PcNoComboThisRoll().Evaluate(ctx));
        }

        [Test]
        public void Evaluate_MatchedCombo_ReturnsFalse()
        {
            var ctx = new PreConditionContext
            {
                Effect = new EffectContext
                {
                    ComboResult = ComboDetectionResult.Match(baseDamage: 10, countUsed: 2),
                },
            };

            Assert.IsFalse(new PcNoComboThisRoll().Evaluate(ctx));
        }
    }
}
