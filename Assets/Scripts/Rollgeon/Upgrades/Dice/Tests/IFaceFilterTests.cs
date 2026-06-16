using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Dice;
using Rollgeon.Upgrades.Dice.Filters;

namespace Rollgeon.Upgrades.Dice.Tests
{
    /// <summary>
    /// Cobertura de los <see cref="IFaceFilter"/> concretos. El roller los compone
    /// en cascada — cada filter recibe el set ya filtrado por los anteriores. Por
    /// eso testeamos solo la operación atómica.
    /// </summary>
    [TestFixture]
    public class IFaceFilterTests
    {
        private static HashSet<int> Faces(params int[] values) => new HashSet<int>(values);

        // ---- ParityFilter ----------------------------------------------------

        [Test]
        public void ParityFilter_Even_RetainsOnlyEven()
        {
            var filter = new ParityFilter { Allowed = Parity.Even };
            var input = Faces(1, 2, 3, 4, 5, 6);

            var result = filter.GetAllowedFaces(DiceType.D6, input);

            CollectionAssert.AreEquivalent(new[] { 2, 4, 6 }, result);
        }

        [Test]
        public void ParityFilter_Odd_RetainsOnlyOdd()
        {
            var filter = new ParityFilter { Allowed = Parity.Odd };
            var input = Faces(1, 2, 3, 4, 5, 6);

            var result = filter.GetAllowedFaces(DiceType.D6, input);

            CollectionAssert.AreEquivalent(new[] { 1, 3, 5 }, result);
        }

        [Test]
        public void ParityFilter_EmptyInput_ReturnsEmpty()
        {
            var filter = new ParityFilter { Allowed = Parity.Even };
            var result = filter.GetAllowedFaces(DiceType.D6, Faces());
            Assert.AreEqual(0, result.Count);
        }

        // ---- FaceRangeFilter -------------------------------------------------

        [Test]
        public void FaceRangeFilter_RangeOneToSix_OnD20_RetainsLowSix()
        {
            var filter = new FaceRangeFilter { Min = 1, Max = 6 };
            var input = Faces(1, 5, 6, 10, 15, 20);

            var result = filter.GetAllowedFaces(DiceType.D20, input);

            CollectionAssert.AreEquivalent(new[] { 1, 5, 6 }, result);
        }

        [Test]
        public void FaceRangeFilter_RangeBeyondDieMax_ReturnsEmpty()
        {
            var filter = new FaceRangeFilter { Min = 21, Max = 30 };
            var input = Faces(1, 5, 10, 20);

            var result = filter.GetAllowedFaces(DiceType.D20, input);

            Assert.AreEqual(0, result.Count);
        }

        // ---- OnlyPrimesFilter ------------------------------------------------

        [Test]
        public void OnlyPrimesFilter_OnD20_RetainsPrimesOnly()
        {
            var filter = new OnlyPrimesFilter();
            var input = Faces(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 13, 15, 17, 19);

            var result = filter.GetAllowedFaces(DiceType.D20, input);

            // primos en [1..19] válidos: 2, 3, 5, 7, 11, 13, 17, 19
            CollectionAssert.AreEquivalent(new[] { 2, 3, 5, 7, 11, 13, 17, 19 }, result);
        }

        [Test]
        public void OnlyPrimesFilter_OnD3_RetainsTwoAndThree()
        {
            var filter = new OnlyPrimesFilter();
            var input = Faces(1, 2, 3);

            var result = filter.GetAllowedFaces(DiceType.D3, input);

            CollectionAssert.AreEquivalent(new[] { 2, 3 }, result);
        }

        // ---- SpecificValuesFilter --------------------------------------------

        [Test]
        public void SpecificValuesFilter_AllowsConfiguredValuesOnly()
        {
            var filter = new SpecificValuesFilter
            {
                AllowedFaces = new List<int> { 3, 7, 13 },
            };
            var input = Faces(1, 2, 3, 5, 7, 11, 13, 17);

            var result = filter.GetAllowedFaces(DiceType.D20, input);

            CollectionAssert.AreEquivalent(new[] { 3, 7, 13 }, result);
        }

        [Test]
        public void SpecificValuesFilter_NoIntersection_ReturnsEmpty()
        {
            var filter = new SpecificValuesFilter
            {
                AllowedFaces = new List<int> { 100, 200 },
            };
            var result = filter.GetAllowedFaces(DiceType.D20, Faces(1, 2, 3));
            Assert.AreEqual(0, result.Count);
        }

        // ---- Composición — verifica que el caller puede componer en cascada ----

        [Test]
        public void Composition_ParityThenRange_AppliesBothCorrectly()
        {
            var parity = new ParityFilter { Allowed = Parity.Even };
            var range = new FaceRangeFilter { Min = 1, Max = 10 };
            IReadOnlyCollection<int> current = Faces(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 14, 16, 18, 20);

            // Cascade — primero parity (impares fuera), después range (>10 fuera).
            current = parity.GetAllowedFaces(DiceType.D20, current);
            current = range.GetAllowedFaces(DiceType.D20, current);

            CollectionAssert.AreEquivalent(new[] { 2, 4, 6, 8, 10 }, current);
        }
    }
}
