using NUnit.Framework;

namespace Rollgeon.Upgrades.Dice.Tests
{
    [TestFixture]
    public class ResourceAccumulatorTests
    {
        [Test]
        public void Identity_ResolvesToCurrentValue()
        {
            var acc = ResourceAccumulator.Identity;

            Assert.AreEqual(50, acc.Resolve(50));
        }

        [Test]
        public void Add_AddsToCurrentValue()
        {
            var acc = ResourceAccumulator.Identity.Apply(ResourceOperation.Add, 5);

            Assert.AreEqual(15, acc.Resolve(10));
        }

        [Test]
        public void Subtract_SubtractsFromCurrentValue()
        {
            var acc = ResourceAccumulator.Identity.Apply(ResourceOperation.Subtract, 4);

            Assert.AreEqual(6, acc.Resolve(10));
        }

        [Test]
        public void Multiply_ScalesCurrentValue()
        {
            var acc = ResourceAccumulator.Identity.Apply(ResourceOperation.Multiply, 3);

            Assert.AreEqual(30, acc.Resolve(10));
        }

        [Test]
        public void Set_OverridesBaseValue()
        {
            var acc = ResourceAccumulator.Identity.Apply(ResourceOperation.Set, 100);

            // El valor actual del jugador es irrelevante: Set pisa la base.
            Assert.AreEqual(100, acc.Resolve(10));
        }

        [Test]
        public void AddThenMultiply_AppliesAddBeforeMultiply()
        {
            var acc = ResourceAccumulator.Identity
                .Apply(ResourceOperation.Add, 5)
                .Apply(ResourceOperation.Multiply, 2);

            // (10 + 5) * 2 = 30
            Assert.AreEqual(30, acc.Resolve(10));
        }

        [Test]
        public void MultiplyThenAdd_IsOrderIndependent()
        {
            // Mismo set de operaciones que AddThenMultiply pero en orden inverso de
            // dispatch: el resultado debe ser idéntico (suma → producto fijo).
            var acc = ResourceAccumulator.Identity
                .Apply(ResourceOperation.Multiply, 2)
                .Apply(ResourceOperation.Add, 5);

            Assert.AreEqual(30, acc.Resolve(10));
        }

        [Test]
        public void SetThenAdd_AddsOnTopOfSetBase()
        {
            var acc = ResourceAccumulator.Identity
                .Apply(ResourceOperation.Set, 100)
                .Apply(ResourceOperation.Add, 5);

            // (100 + 5) * 1 = 105
            Assert.AreEqual(105, acc.Resolve(10));
        }

        [Test]
        public void MultipleAdds_Accumulate()
        {
            var acc = ResourceAccumulator.Identity
                .Apply(ResourceOperation.Add, 3)
                .Apply(ResourceOperation.Add, 4);

            Assert.AreEqual(17, acc.Resolve(10));
        }

        [Test]
        public void MultipleMultiplies_Compose()
        {
            var acc = ResourceAccumulator.Identity
                .Apply(ResourceOperation.Multiply, 2)
                .Apply(ResourceOperation.Multiply, 3);

            // 5 * (2 * 3) = 30
            Assert.AreEqual(30, acc.Resolve(5));
        }
    }
}
