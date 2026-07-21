using NUnit.Framework;
using Rollgeon.PreConditions.Concretes;

namespace Rollgeon.PreConditions.Tests
{
    /// <summary>Tests de <see cref="PcChance"/> con la seam de RNG determinística.</summary>
    [TestFixture]
    public class PcChanceTests
    {
        [TearDown]
        public void TearDown()
        {
            PcChance.ResetRandomSource();
        }

        [Test]
        public void Evaluate_Percent01_PassesWhenRollBelowChance()
        {
            // Arrange
            PcChance.RandomSource = () => 0.2f;
            var pc = new PcChance { Mode = ChanceMode.Percent01, Chance = 0.5f };

            // Act / Assert
            Assert.IsTrue(pc.Evaluate(new PreConditionContext()));
        }

        [Test]
        public void Evaluate_Percent01_FailsWhenRollAboveChance()
        {
            // Arrange
            PcChance.RandomSource = () => 0.9f;
            var pc = new PcChance { Mode = ChanceMode.Percent01, Chance = 0.5f };

            // Act / Assert
            Assert.IsFalse(pc.Evaluate(new PreConditionContext()));
        }

        [Test]
        public void Evaluate_OneInN_UsesReciprocalProbability()
        {
            // Arrange — 1 en 5 = 0.2: roll 0.19 pasa, 0.21 no.
            var pc = new PcChance { Mode = ChanceMode.OneInN, OneIn = 5 };

            PcChance.RandomSource = () => 0.19f;
            Assert.IsTrue(pc.Evaluate(new PreConditionContext()));

            PcChance.RandomSource = () => 0.21f;
            Assert.IsFalse(pc.Evaluate(new PreConditionContext()));
        }

        [Test]
        public void Evaluate_ChanceZero_NeverPasses()
        {
            // Arrange
            PcChance.RandomSource = () => 0f;
            var pc = new PcChance { Mode = ChanceMode.Percent01, Chance = 0f };

            // Act / Assert — roll 0 < chance 0 es falso: 0% nunca pasa.
            Assert.IsFalse(pc.Evaluate(new PreConditionContext()));
        }

        [Test]
        public void Evaluate_NullContext_StillEvaluates()
        {
            // Arrange — la chance no depende del contexto.
            PcChance.RandomSource = () => 0f;
            var pc = new PcChance { Mode = ChanceMode.Percent01, Chance = 1f };

            // Act / Assert
            Assert.IsTrue(pc.Evaluate(null));
        }
    }
}
