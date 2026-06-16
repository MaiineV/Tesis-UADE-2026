using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Rollgeon.Dice.Tests
{
    [TestFixture]
    public class DiceBagPoolSOTests
    {
        private List<DiceBagPoolSO> _created;

        [SetUp]
        public void SetUp()
        {
            _created = new List<DiceBagPoolSO>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var pool in _created)
                if (pool != null) Object.DestroyImmediate(pool);
            _created = null;
        }

        private DiceBagPoolSO MakePool(int requiredSize, params (DiceType type, int max)[] offerings)
        {
            var pool = ScriptableObject.CreateInstance<DiceBagPoolSO>();
            pool.name = "TestPool";
            pool.RequiredBagSize = requiredSize;
            pool.Offerings = new List<DicePoolEntry>();
            foreach (var (t, m) in offerings)
                pool.Offerings.Add(new DicePoolEntry { Type = t, MaxInBag = m });
            _created.Add(pool);
            return pool;
        }

        [Test]
        public void Validate_AcceptsPoolWithEnoughCapacity()
        {
            var pool = MakePool(5, (DiceType.D6, 5), (DiceType.D8, 4));
            Assert.IsTrue(pool.Validate(out var error), "Expected valid; error='{0}'", error);
            Assert.IsNull(error);
        }

        [Test]
        public void Validate_RejectsEmptyOfferings()
        {
            var pool = MakePool(5);
            Assert.IsFalse(pool.Validate(out var error));
            StringAssert.Contains("Offerings", error);
        }

        [Test]
        public void Validate_RejectsInsufficientTotalCapacity()
        {
            // D6:2 + D8:1 = 3 dados maximos; pero el pool requiere 5.
            var pool = MakePool(5, (DiceType.D6, 2), (DiceType.D8, 1));
            Assert.IsFalse(pool.Validate(out var error));
            StringAssert.Contains("RequiredBagSize", error);
        }

        [Test]
        public void Validate_RejectsMaxInBagAboveHardCap()
        {
            // D20.MaxPerBag() == 1; un override de 3 es invalido.
            var pool = MakePool(5, (DiceType.D20, 3), (DiceType.D6, 4));
            Assert.IsFalse(pool.Validate(out var error));
            StringAssert.Contains("D20", error);
        }

        [Test]
        public void Validate_RejectsZeroMaxInBag()
        {
            var pool = MakePool(5, (DiceType.D6, 0), (DiceType.D8, 5));
            Assert.IsFalse(pool.Validate(out var error));
            StringAssert.Contains("MaxInBag", error);
        }

        [Test]
        public void MaxFor_ReturnsZeroWhenTypeNotInPool()
        {
            var pool = MakePool(5, (DiceType.D6, 5));
            Assert.AreEqual(0, pool.MaxFor(DiceType.D4));
        }

        [Test]
        public void MaxFor_ReturnsConfiguredCap()
        {
            var pool = MakePool(5, (DiceType.D6, 4), (DiceType.D8, 2));
            Assert.AreEqual(4, pool.MaxFor(DiceType.D6));
            Assert.AreEqual(2, pool.MaxFor(DiceType.D8));
        }
    }
}
