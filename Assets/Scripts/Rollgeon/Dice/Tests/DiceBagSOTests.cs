using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Rollgeon.Dice.Tests
{
    [TestFixture]
    public class DiceBagSOTests
    {
        private List<DiceBagSO> _created;

        [SetUp]
        public void SetUp()
        {
            _created = new List<DiceBagSO>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var bag in _created)
            {
                if (bag != null) Object.DestroyImmediate(bag);
            }
            _created = null;
        }

        private DiceBagSO MakeBag(params DiceType[] dice)
        {
            var bag = ScriptableObject.CreateInstance<DiceBagSO>();
            bag.name = "TestBag";
            bag.Dice = new List<DiceType>(dice);
            _created.Add(bag);
            return bag;
        }

        [Test]
        public void Validate_AcceptsExactlyFiveDice_AllD6()
        {
            var bag = MakeBag(DiceType.D6, DiceType.D6, DiceType.D6, DiceType.D6, DiceType.D6);
            Assert.IsTrue(bag.Validate(out var error), "Expected valid; error='{0}'", error);
            Assert.IsNull(error);
        }

        [Test]
        public void Validate_RejectsTooFewDice()
        {
            var bag = MakeBag(DiceType.D6, DiceType.D6);
            Assert.IsFalse(bag.Validate(out var error));
            StringAssert.Contains("5 dados", error);
        }

        [Test]
        public void Validate_RejectsTooManyDice()
        {
            var bag = MakeBag(DiceType.D6, DiceType.D6, DiceType.D6, DiceType.D6, DiceType.D6, DiceType.D6);
            Assert.IsFalse(bag.Validate(out var error));
            StringAssert.Contains("5 dados", error);
        }

        [Test]
        public void Validate_RejectsExcessOfD20()
        {
            // D20 tiene MaxPerBag = 1. Dos D20 + 3 D6 viola la regla.
            var bag = MakeBag(DiceType.D20, DiceType.D20, DiceType.D6, DiceType.D6, DiceType.D6);
            Assert.IsFalse(bag.Validate(out var error));
            StringAssert.Contains("D20", error);
        }

        [Test]
        public void Validate_AcceptsBoundaryD8()
        {
            // D8 tiene MaxPerBag = 4. 4xD8 + 1xD6 está en el límite — válido.
            var bag = MakeBag(DiceType.D8, DiceType.D8, DiceType.D8, DiceType.D8, DiceType.D6);
            Assert.IsTrue(bag.Validate(out var error), "Expected valid; error='{0}'", error);
        }

        [Test]
        public void Clone_ProducesIndependentInstance()
        {
            var bag = MakeBag(DiceType.D6, DiceType.D8, DiceType.D10, DiceType.D12, DiceType.D20);
            var clone = bag.Clone();
            _created.Add(clone);

            Assert.AreNotSame(bag, clone);
            Assert.AreNotSame(bag.Dice, clone.Dice);
            CollectionAssert.AreEqual(bag.Dice, clone.Dice);

            // Mutar el original no afecta el clon.
            bag.Dice[0] = DiceType.D4;
            Assert.AreEqual(DiceType.D6, clone.Dice[0]);
        }
    }
}
