using NUnit.Framework;
using Patterns;
using Rollgeon.Economy;
using Rollgeon.Effects;
using Rollgeon.Upgrades.Dice.Readers;

namespace Rollgeon.Upgrades.Dice.Tests
{
    /// <summary>
    /// BUG-080: <see cref="ReadCurrentGoldSqrtScaled"/> — bono de daño de "El Egoísta"
    /// con retornos decrecientes: <c>floor(sqrt(oro_actual × Factor))</c>. Reemplaza el
    /// viejo <c>ReadCurrentGold</c> 1:1 que, sumado directo al Attack BASE, escalaba
    /// linealmente y de forma permanente.
    /// </summary>
    [TestFixture]
    public class ReadCurrentGoldSqrtScaledTests
    {
        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
        }

        // Factor default = 5 (diseño BUG-080): oro 0/1/5/20/45 → bono 0/2/5/10/15.
        [TestCase(0, 0)]
        [TestCase(1, 2)]
        [TestCase(5, 5)]
        [TestCase(20, 10)]
        [TestCase(45, 15)]
        public void Read_WithDefaultFactor_ReturnsFloorOfSqrtGoldTimesFive(int gold, int expectedBonus)
        {
            // Arrange
            ServiceLocator.AddService<IEconomyService>(new FakeEconomy(gold));
            var reader = new ReadCurrentGoldSqrtScaled(); // Factor default = 5

            // Act
            int bonus = reader.Read(new EffectContext());

            // Assert
            Assert.AreEqual(expectedBonus, bonus);
        }

        [Test]
        public void Read_NoEconomyServiceRegistered_ReturnsZero()
        {
            // Arrange — sin ServiceLocator.AddService<IEconomyService>.
            var reader = new ReadCurrentGoldSqrtScaled();

            // Act
            int bonus = reader.Read(new EffectContext());

            // Assert
            Assert.AreEqual(0, bonus);
        }

        [Test]
        public void Read_NegativeGold_ClampsToZeroBeforeSqrt()
        {
            // Arrange — defensivo: IEconomyService no debería devolver negativo, pero el
            // reader no debe explotar (Sqrt de negativo = NaN) si algún día lo hace.
            ServiceLocator.AddService<IEconomyService>(new FakeEconomy(-10));
            var reader = new ReadCurrentGoldSqrtScaled();

            int bonus = reader.Read(new EffectContext());

            Assert.AreEqual(0, bonus);
        }

        private sealed class FakeEconomy : IEconomyService
        {
            public FakeEconomy(int gold) { CurrentGold = gold; }
            public int CurrentGold { get; private set; }
            public void Add(int amount) { CurrentGold += amount; }
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
