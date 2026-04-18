using NUnit.Framework;

namespace Rollgeon.Dice.Tests
{
    /// <summary>
    /// Unit tests de la value class <see cref="RerollBudget"/> (sin dependencias
    /// del servicio ni de Unity).
    /// </summary>
    [TestFixture]
    public class RerollBudgetTests
    {
        [Test]
        public void ConsumeFree_WithRemaining_DecrementsAndReturnsTrue()
        {
            var b = new RerollBudget { FreeRollsRemaining = 2 };

            bool ok = b.ConsumeFree();

            Assert.IsTrue(ok);
            Assert.AreEqual(1, b.FreeRollsRemaining);
        }

        [Test]
        public void ConsumeFree_WithZero_ReturnsFalseAndStaysAtZero()
        {
            var b = new RerollBudget { FreeRollsRemaining = 0 };

            bool ok = b.ConsumeFree();

            Assert.IsFalse(ok);
            Assert.AreEqual(0, b.FreeRollsRemaining);
        }

        [Test]
        public void ConsumeFree_DoesNotGoNegative()
        {
            var b = new RerollBudget { FreeRollsRemaining = 1 };

            b.ConsumeFree();
            bool ok = b.ConsumeFree();

            Assert.IsFalse(ok);
            Assert.AreEqual(0, b.FreeRollsRemaining);
        }

        [Test]
        public void ConsumePaid_IncrementsCounter()
        {
            var b = new RerollBudget();

            b.ConsumePaid();
            b.ConsumePaid();

            Assert.AreEqual(2, b.PaidRollsUsed);
        }

        [Test]
        public void Reset_ZerosCountersAndClearsAction()
        {
            var b = new RerollBudget
            {
                FreeRollsRemaining = 5,
                PaidRollsUsed = 3,
                Action = null,
            };

            b.Reset();

            Assert.AreEqual(0, b.FreeRollsRemaining);
            Assert.AreEqual(0, b.PaidRollsUsed);
            Assert.IsNull(b.Action);
        }
    }
}
