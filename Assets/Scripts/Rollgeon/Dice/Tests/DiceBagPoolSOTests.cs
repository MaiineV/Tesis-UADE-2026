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

        private DiceBagPoolSO MakePool(int requiredSize, params DiceType[] offerings)
        {
            var pool = ScriptableObject.CreateInstance<DiceBagPoolSO>();
            pool.name = "TestPool";
            pool.RequiredBagSize = requiredSize;
            pool.Offerings = new List<DicePoolEntry>();
            foreach (var t in offerings)
                pool.Offerings.Add(new DicePoolEntry { Type = t });
            _created.Add(pool);
            return pool;
        }

        [Test]
        public void Validate_AcceptsPoolWithOfferings()
        {
            var pool = MakePool(5, DiceType.D6, DiceType.D8);
            Assert.IsTrue(pool.Validate(out var error), "Expected valid; error='{0}'", error);
            Assert.IsNull(error);
        }

        [Test]
        public void Validate_AcceptsSingleOffering_NoPerTypeCap()
        {
            // Sin tope por tipo, un único tipo ofrecido alcanza para llenar la bolsa (5×D20).
            var pool = MakePool(5, DiceType.D20);
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
        public void Offers_ReturnsFalseWhenTypeNotInPool()
        {
            var pool = MakePool(5, DiceType.D6);
            Assert.IsFalse(pool.Offers(DiceType.D4));
        }

        [Test]
        public void Offers_ReturnsTrueForOfferedTypes()
        {
            var pool = MakePool(5, DiceType.D6, DiceType.D8);
            Assert.IsTrue(pool.Offers(DiceType.D6));
            Assert.IsTrue(pool.Offers(DiceType.D8));
        }
    }
}
