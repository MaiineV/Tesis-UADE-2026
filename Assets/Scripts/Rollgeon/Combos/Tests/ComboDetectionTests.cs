using NUnit.Framework;
using Rollgeon.Combos;
using Rollgeon.Combos.Concretes;
using UnityEngine;

namespace Rollgeon.Combos.Tests
{
    // =============================================================================
    // Combo_Par
    // =============================================================================
    [TestFixture]
    public class Combo_Par_Tests
    {
        private Combo_Par _sut;

        [SetUp]
        public void Setup()
        {
            _sut = ComboTestUtils.CreateCombo<Combo_Par>(ComboId.Par, 10);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_sut);
        }

        [Test]
        public void Par_Positive_3_3_1_2_5()
        {
            var result = _sut.Detect(new[] { 3, 3, 1, 2, 5 });
            Assert.IsTrue(result.IsMatch);
            Assert.AreEqual(10, result.BaseDamage);
            Assert.AreEqual(2, result.CountUsed);
        }

        [Test]
        public void Par_Positive_6_1_6_2_3_OrderAgnostic()
        {
            var result = _sut.Detect(new[] { 6, 1, 6, 2, 3 });
            Assert.IsTrue(result.IsMatch);
            Assert.AreEqual(2, result.CountUsed);
        }

        [Test]
        public void Par_Negative_1_2_3_4_5()
        {
            var result = _sut.Detect(new[] { 1, 2, 3, 4, 5 });
            Assert.IsFalse(result.IsMatch);
            Assert.AreEqual(0, result.BaseDamage);
        }

        [Test]
        public void Par_Negative_1_2_3_4_6()
        {
            var result = _sut.Detect(new[] { 1, 2, 3, 4, 6 });
            Assert.IsFalse(result.IsMatch);
        }

        [Test]
        public void Par_Null_NoMatch()
        {
            var result = _sut.Detect(null);
            Assert.IsFalse(result.IsMatch);
        }
    }

    // =============================================================================
    // Combo_DoblePar (incl. disambiguator critico vs FullHouse)
    // =============================================================================
    [TestFixture]
    public class Combo_DoblePar_Tests
    {
        private Combo_DoblePar _sut;

        [SetUp]
        public void Setup()
        {
            _sut = ComboTestUtils.CreateCombo<Combo_DoblePar>(ComboId.DoublePair, 18);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_sut);
        }

        [Test]
        public void DoblePar_Positive_3_3_5_5_1()
        {
            var result = _sut.Detect(new[] { 3, 3, 5, 5, 1 });
            Assert.IsTrue(result.IsMatch);
            Assert.AreEqual(18, result.BaseDamage);
            Assert.AreEqual(4, result.CountUsed);
        }

        [Test]
        public void DoblePar_Positive_2_2_6_6_1()
        {
            var result = _sut.Detect(new[] { 2, 2, 6, 6, 1 });
            Assert.IsTrue(result.IsMatch);
            Assert.AreEqual(4, result.CountUsed);
        }

        /// <summary>Disambiguator critico (hard rule #7): FullHouse NO debe matchear como DoblePar.</summary>
        [Test]
        public void DoblePar_Disambiguator_FullHouse_3_3_3_5_5_DoesNotMatch()
        {
            var result = _sut.Detect(new[] { 3, 3, 3, 5, 5 });
            Assert.IsFalse(result.IsMatch, "FullHouse [3,3,3,5,5] NO debe matchear como DoblePar.");
        }

        [Test]
        public void DoblePar_Negative_Trio_3_3_3_5_1()
        {
            var result = _sut.Detect(new[] { 3, 3, 3, 5, 1 });
            Assert.IsFalse(result.IsMatch);
        }

        [Test]
        public void DoblePar_Negative_Poker_4_4_4_4_1()
        {
            var result = _sut.Detect(new[] { 4, 4, 4, 4, 1 });
            Assert.IsFalse(result.IsMatch, "Poker tiene un solo grupo de 4+, no dos pares distintos.");
        }

        [Test]
        public void DoblePar_Negative_Straight_1_2_3_4_5()
        {
            var result = _sut.Detect(new[] { 1, 2, 3, 4, 5 });
            Assert.IsFalse(result.IsMatch);
        }
    }

    // =============================================================================
    // Combo_Trio
    // =============================================================================
    [TestFixture]
    public class Combo_Trio_Tests
    {
        private Combo_Trio _sut;

        [SetUp]
        public void Setup()
        {
            _sut = ComboTestUtils.CreateCombo<Combo_Trio>(ComboId.Triple, 28);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_sut);
        }

        [Test]
        public void Trio_Positive_3_3_3_1_2()
        {
            var result = _sut.Detect(new[] { 3, 3, 3, 1, 2 });
            Assert.IsTrue(result.IsMatch);
            Assert.AreEqual(28, result.BaseDamage);
            Assert.AreEqual(3, result.CountUsed);
        }

        [Test]
        public void Trio_Positive_Poker_5_5_5_5_2_AlsoMatchesAsTrio()
        {
            var result = _sut.Detect(new[] { 5, 5, 5, 5, 2 });
            Assert.IsTrue(result.IsMatch, "Poker matches as Trio (count >= 3). Resolucion via Priority downstream.");
            Assert.AreEqual(3, result.CountUsed);
        }

        [Test]
        public void Trio_Negative_DoblePar_1_1_2_2_3()
        {
            var result = _sut.Detect(new[] { 1, 1, 2, 2, 3 });
            Assert.IsFalse(result.IsMatch);
        }

        [Test]
        public void Trio_Negative_Straight_1_2_3_4_5()
        {
            var result = _sut.Detect(new[] { 1, 2, 3, 4, 5 });
            Assert.IsFalse(result.IsMatch);
        }
    }

    // =============================================================================
    // Combo_Escalera (incl. normalizacion de orden)
    // =============================================================================
    [TestFixture]
    public class Combo_Escalera_Tests
    {
        private Combo_Escalera _sut;

        [SetUp]
        public void Setup()
        {
            _sut = ComboTestUtils.CreateCombo<Combo_Escalera>(ComboId.Straight, 35);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_sut);
        }

        [Test]
        public void Escalera_Positive_1_2_3_4_5()
        {
            var result = _sut.Detect(new[] { 1, 2, 3, 4, 5 });
            Assert.IsTrue(result.IsMatch);
            Assert.AreEqual(35, result.BaseDamage);
            Assert.AreEqual(5, result.CountUsed);
        }

        [Test]
        public void Escalera_Positive_2_3_4_5_6()
        {
            var result = _sut.Detect(new[] { 2, 3, 4, 5, 6 });
            Assert.IsTrue(result.IsMatch);
        }

        /// <summary>Plan §9.2: debe matchear sin importar el orden del input.</summary>
        [Test]
        public void Escalera_Detects_Regardless_Of_Order_5_1_3_4_2()
        {
            var result = _sut.Detect(new[] { 5, 1, 3, 4, 2 });
            Assert.IsTrue(result.IsMatch);
        }

        [Test]
        public void Escalera_Detects_Regardless_Of_Order_6_2_4_5_3()
        {
            var result = _sut.Detect(new[] { 6, 2, 4, 5, 3 });
            Assert.IsTrue(result.IsMatch);
        }

        [Test]
        public void Escalera_Negative_Gap_1_2_3_4_6()
        {
            var result = _sut.Detect(new[] { 1, 2, 3, 4, 6 });
            Assert.IsFalse(result.IsMatch);
        }

        [Test]
        public void Escalera_Negative_Duplicate_1_1_2_3_4()
        {
            var result = _sut.Detect(new[] { 1, 1, 2, 3, 4 });
            Assert.IsFalse(result.IsMatch);
        }
    }

    // =============================================================================
    // Combo_FullHouse
    // =============================================================================
    [TestFixture]
    public class Combo_FullHouse_Tests
    {
        private Combo_FullHouse _sut;

        [SetUp]
        public void Setup()
        {
            _sut = ComboTestUtils.CreateCombo<Combo_FullHouse>(ComboId.FullHouse, 40);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_sut);
        }

        [Test]
        public void FullHouse_Positive_3_3_3_5_5()
        {
            var result = _sut.Detect(new[] { 3, 3, 3, 5, 5 });
            Assert.IsTrue(result.IsMatch);
            Assert.AreEqual(40, result.BaseDamage);
            Assert.AreEqual(5, result.CountUsed);
        }

        [Test]
        public void FullHouse_Positive_2_2_6_6_6()
        {
            var result = _sut.Detect(new[] { 2, 2, 6, 6, 6 });
            Assert.IsTrue(result.IsMatch);
        }

        [Test]
        public void FullHouse_Negative_DoblePar_1_1_2_2_3()
        {
            var result = _sut.Detect(new[] { 1, 1, 2, 2, 3 });
            Assert.IsFalse(result.IsMatch);
        }

        [Test]
        public void FullHouse_Negative_Poker_4_4_4_4_5()
        {
            var result = _sut.Detect(new[] { 4, 4, 4, 4, 5 });
            Assert.IsFalse(result.IsMatch);
        }
    }

    // =============================================================================
    // Combo_Poker
    // =============================================================================
    [TestFixture]
    public class Combo_Poker_Tests
    {
        private Combo_Poker _sut;

        [SetUp]
        public void Setup()
        {
            _sut = ComboTestUtils.CreateCombo<Combo_Poker>(ComboId.Poker, 60);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_sut);
        }

        [Test]
        public void Poker_Positive_4_4_4_4_1()
        {
            var result = _sut.Detect(new[] { 4, 4, 4, 4, 1 });
            Assert.IsTrue(result.IsMatch);
            Assert.AreEqual(60, result.BaseDamage);
            Assert.AreEqual(4, result.CountUsed);
        }

        [Test]
        public void Poker_Positive_Generala_5_5_5_5_5_AlsoMatchesAsPoker()
        {
            var result = _sut.Detect(new[] { 5, 5, 5, 5, 5 });
            Assert.IsTrue(result.IsMatch, "Generala matches as Poker (count >= 4). Priority resolves.");
        }

        [Test]
        public void Poker_Negative_Trio_3_3_3_1_2()
        {
            var result = _sut.Detect(new[] { 3, 3, 3, 1, 2 });
            Assert.IsFalse(result.IsMatch);
        }

        [Test]
        public void Poker_Negative_Straight_1_2_3_4_5()
        {
            var result = _sut.Detect(new[] { 1, 2, 3, 4, 5 });
            Assert.IsFalse(result.IsMatch);
        }
    }

    // =============================================================================
    // Combo_Generala
    // =============================================================================
    [TestFixture]
    public class Combo_Generala_Tests
    {
        private Combo_Generala _sut;

        [SetUp]
        public void Setup()
        {
            _sut = ComboTestUtils.CreateCombo<Combo_Generala>(ComboId.Generala, 100);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_sut);
        }

        [Test]
        public void Generala_Positive_5_5_5_5_5()
        {
            var result = _sut.Detect(new[] { 5, 5, 5, 5, 5 });
            Assert.IsTrue(result.IsMatch);
            Assert.AreEqual(100, result.BaseDamage);
            Assert.AreEqual(5, result.CountUsed);
        }

        [Test]
        public void Generala_Positive_1_1_1_1_1()
        {
            var result = _sut.Detect(new[] { 1, 1, 1, 1, 1 });
            Assert.IsTrue(result.IsMatch);
        }

        [Test]
        public void Generala_Negative_5_5_5_5_6()
        {
            var result = _sut.Detect(new[] { 5, 5, 5, 5, 6 });
            Assert.IsFalse(result.IsMatch);
        }

        [Test]
        public void Generala_Negative_Straight_1_2_3_4_5()
        {
            var result = _sut.Detect(new[] { 1, 2, 3, 4, 5 });
            Assert.IsFalse(result.IsMatch);
        }

        [Test]
        public void Generala_Empty_NoMatch()
        {
            var result = _sut.Detect(new int[0]);
            Assert.IsFalse(result.IsMatch);
        }

        [Test]
        public void Generala_Null_NoMatch()
        {
            var result = _sut.Detect(null);
            Assert.IsFalse(result.IsMatch);
        }
    }

    // =============================================================================
    // Combo_SumaX (override Detect con formula dinamica)
    // =============================================================================
    [TestFixture]
    public class Combo_SumaX_Tests
    {
        private Combo_SumaX _sut;

        [SetUp]
        public void Setup()
        {
            // X=4 (Warrior), BaseDamageConfigurable=25 (GD default).
            _sut = ScriptableObject.CreateInstance<Combo_SumaX>();
            ComboTestUtils.SetField(_sut, "_comboId", ComboId.SumX);
            ComboTestUtils.SetField(_sut, "_x", 4);
            ComboTestUtils.SetField(_sut, "_baseDamageConfigurable", 25);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_sut);
        }

        [Test]
        public void SumaX_Positive_ThreeFours_4_4_1_6_4()
        {
            var result = _sut.Detect(new[] { 4, 4, 1, 6, 4 });
            Assert.IsTrue(result.IsMatch);
            // 25 + (4 * 3) = 37
            Assert.AreEqual(37, result.BaseDamage);
            Assert.AreEqual(3, result.CountUsed);
        }

        [Test]
        public void SumaX_Positive_OneFour_4_2_3_5_6()
        {
            var result = _sut.Detect(new[] { 4, 2, 3, 5, 6 });
            Assert.IsTrue(result.IsMatch);
            // 25 + (4 * 1) = 29
            Assert.AreEqual(29, result.BaseDamage);
            Assert.AreEqual(1, result.CountUsed);
        }

        [Test]
        public void SumaX_Negative_NoFours_1_2_3_5_6()
        {
            var result = _sut.Detect(new[] { 1, 2, 3, 5, 6 });
            Assert.IsFalse(result.IsMatch);
        }

        [Test]
        public void SumaX_Negative_AllFives_5_5_5_5_5()
        {
            var result = _sut.Detect(new[] { 5, 5, 5, 5, 5 });
            Assert.IsFalse(result.IsMatch, "Generala de 5s no tiene ningun 4, no matchea Suma-4.");
        }

        [Test]
        public void SumaX_Empty_NoMatch()
        {
            var result = _sut.Detect(new int[0]);
            Assert.IsFalse(result.IsMatch);
        }

        [Test]
        public void SumaX_Null_NoMatch()
        {
            var result = _sut.Detect(null);
            Assert.IsFalse(result.IsMatch);
        }

        [Test]
        public void SumaX_Parametric_X6_AllSixes()
        {
            ComboTestUtils.SetField(_sut, "_x", 6);
            var result = _sut.Detect(new[] { 6, 6, 6, 1, 2 });
            Assert.IsTrue(result.IsMatch);
            // 25 + (6 * 3) = 43
            Assert.AreEqual(43, result.BaseDamage);
            Assert.AreEqual(3, result.CountUsed);
        }
    }
}
