using NUnit.Framework;
using Patterns;
using Rollgeon.Economy;
using Rollgeon.Effects.Readers;
using Rollgeon.PreConditions.Concretes;

namespace Rollgeon.PreConditions.Tests
{
    /// <summary>Tests de <see cref="PcGoldCompare"/>.</summary>
    [TestFixture]
    public class PcGoldCompareTests
    {
        [SetUp]
        public void SetUp() => ServiceLocator.Clear();

        [TearDown]
        public void TearDown() => ServiceLocator.Clear();

        private static void RegisterEconomy(int gold)
        {
            ServiceLocator.AddService<IEconomyService>(new FakeEconomy(gold), ServiceScope.Global);
        }

        private static PcGoldCompare Build(IntComparison comparison, int value)
        {
            return new PcGoldCompare
            {
                Comparison = comparison,
                Value = new ReadConstantInt { Value = value },
            };
        }

        [Test]
        public void Evaluate_GreaterOrEqual_PassesWhenAffordable()
        {
            // Arrange
            RegisterEconomy(10);
            var pc = Build(IntComparison.GreaterOrEqual, 5);

            // Act / Assert
            Assert.IsTrue(pc.Evaluate(new PreConditionContext()));
        }

        [Test]
        public void Evaluate_GreaterOrEqual_FailsWhenInsufficient()
        {
            // Arrange
            RegisterEconomy(3);
            var pc = Build(IntComparison.GreaterOrEqual, 5);

            // Act / Assert
            Assert.IsFalse(pc.Evaluate(new PreConditionContext()));
        }

        [Test]
        public void Evaluate_Less_GatesBlockCompositions()
        {
            // Arrange — el gate de "sin oro no hay daño": gold < threshold.
            RegisterEconomy(0);
            var pc = Build(IntComparison.Less, 1);

            // Act / Assert
            Assert.IsTrue(pc.Evaluate(new PreConditionContext()));
        }

        [Test]
        public void Evaluate_WithoutEconomyService_ReturnsFalse()
        {
            // Arrange — sin economía la comparación no se puede afirmar (NO permisivo:
            // un gate de oro inevaluable no habilita gastos ni bloqueos).
            var pc = Build(IntComparison.GreaterOrEqual, 0);

            // Act / Assert
            Assert.IsFalse(pc.Evaluate(new PreConditionContext()));
        }

        private sealed class FakeEconomy : IEconomyService
        {
            public FakeEconomy(int gold) { CurrentGold = gold; }
            public int CurrentGold { get; private set; }
            public void Add(int amount) { if (amount > 0) CurrentGold += amount; }
            public bool Spend(int amount)
            {
                if (amount > CurrentGold) return false;
                CurrentGold -= amount;
                return true;
            }
            public bool CanAfford(int amount) => amount <= 0 || CurrentGold >= amount;
            public void ResetTo(int amount) => CurrentGold = amount;
        }
    }
}
