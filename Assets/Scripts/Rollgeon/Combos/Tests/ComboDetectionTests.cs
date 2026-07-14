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
            CollectionAssert.AreEqual(new[] { 0, 1 }, result.ContributingIndices);
        }

        [Test]
        public void Par_Positive_6_1_6_2_3_OrderAgnostic()
        {
            var result = _sut.Detect(new[] { 6, 1, 6, 2, 3 });
            Assert.IsTrue(result.IsMatch);
            Assert.AreEqual(2, result.CountUsed);
            CollectionAssert.AreEqual(new[] { 0, 2 }, result.ContributingIndices);
        }

        [Test]
        public void Par_PicksHighestValueGroup_WhenMultipleQualify_2_2_4_4_4()
        {
            var result = _sut.Detect(new[] { 2, 2, 4, 4, 4 });
            Assert.IsTrue(result.IsMatch);
            CollectionAssert.AreEqual(new[] { 2, 3 }, result.ContributingIndices);
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
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3 }, result.ContributingIndices);
        }

        [Test]
        public void DoblePar_Positive_2_2_6_6_1()
        {
            var result = _sut.Detect(new[] { 2, 2, 6, 6, 1 });
            Assert.IsTrue(result.IsMatch);
            Assert.AreEqual(4, result.CountUsed);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3 }, result.ContributingIndices);
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
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, result.ContributingIndices);
        }

        [Test]
        public void Trio_Positive_Poker_5_5_5_5_2_AlsoMatchesAsTrio()
        {
            var result = _sut.Detect(new[] { 5, 5, 5, 5, 2 });
            Assert.IsTrue(result.IsMatch, "Poker matches as Trio (count >= 3). Resolucion via Priority downstream.");
            Assert.AreEqual(3, result.CountUsed);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, result.ContributingIndices);
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
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, result.ContributingIndices);
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
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, result.ContributingIndices);
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
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, result.ContributingIndices);
        }

        [Test]
        public void Poker_Positive_Generala_5_5_5_5_5_AlsoMatchesAsPoker()
        {
            var result = _sut.Detect(new[] { 5, 5, 5, 5, 5 });
            Assert.IsTrue(result.IsMatch, "Generala matches as Poker (count >= 4). Priority resolves.");
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, result.ContributingIndices);
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
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, result.ContributingIndices);
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
            CollectionAssert.AreEqual(new[] { 0, 1, 4 }, result.ContributingIndices);
        }

        [Test]
        public void SumaX_Positive_OneFour_4_2_3_5_6()
        {
            var result = _sut.Detect(new[] { 4, 2, 3, 5, 6 });
            Assert.IsTrue(result.IsMatch);
            // 25 + (4 * 1) = 29
            Assert.AreEqual(29, result.BaseDamage);
            Assert.AreEqual(1, result.CountUsed);
            CollectionAssert.AreEqual(new[] { 0 }, result.ContributingIndices);
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
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, result.ContributingIndices);
        }
    }

    // =============================================================================
    // Combo_FuerzaBruta (spec Santi 2026-07-13: matchea solo si TODOS los dados caen
    // en la mitad superior de su propio rango — valor > MaxFace/2. No hay subconjunto
    // parcial: un solo dado por debajo del umbral anula el combo entero.)
    // =============================================================================
    [TestFixture]
    public class Combo_FuerzaBruta_Tests
    {
        private Combo_FuerzaBruta _sut;

        [SetUp]
        public void Setup()
        {
            _sut = ComboTestUtils.CreateCombo<Combo_FuerzaBruta>(ComboId.BruteForce, 5);
            ComboTestUtils.SetField(_sut, "_baseDamageConfigurable", 5);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_sut);
        }

        [Test]
        public void FuerzaBruta_Positive_TodosEnMitadSuperior_Heterogeneo()
        {
            // d6=5 (>3), d8=6 (>4), d4=3 (>2), d12=7 (>6), d20=11 (>10) — todos entran.
            var types = new[]
            {
                Rollgeon.Dice.DiceType.D6, Rollgeon.Dice.DiceType.D8, Rollgeon.Dice.DiceType.D4,
                Rollgeon.Dice.DiceType.D12, Rollgeon.Dice.DiceType.D20,
            };
            var result = _sut.Detect(new[] { 5, 6, 3, 7, 11 }, types, null);

            Assert.IsTrue(result.IsMatch);
            // 5 (piso) + 5 + 6 + 3 + 7 + 11 = 37
            Assert.AreEqual(37, result.BaseDamage);
            Assert.AreEqual(5, result.CountUsed);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, result.ContributingIndices);
            Assert.AreEqual(ComboId.BruteForce, result.ComboId);
        }

        [Test]
        public void FuerzaBruta_Negative_UnSoloDadoDebajoDelUmbral_AnulaElCombo()
        {
            // Los primeros 4 entran; el d12=6 (necesita 7+) no. Un solo fallo anula todo.
            var types = new[]
            {
                Rollgeon.Dice.DiceType.D6, Rollgeon.Dice.DiceType.D6, Rollgeon.Dice.DiceType.D8,
                Rollgeon.Dice.DiceType.D4, Rollgeon.Dice.DiceType.D12,
            };
            var result = _sut.Detect(new[] { 5, 4, 6, 3, 6 }, types, null);

            Assert.IsFalse(result.IsMatch);
            Assert.AreEqual(0, result.BaseDamage);
            Assert.AreEqual(0, result.CountUsed);
        }

        [Test]
        public void FuerzaBruta_Negative_TodosMitadInferior_1_2_3_2_1()
        {
            var types = new[]
            {
                Rollgeon.Dice.DiceType.D6, Rollgeon.Dice.DiceType.D6, Rollgeon.Dice.DiceType.D6,
                Rollgeon.Dice.DiceType.D6, Rollgeon.Dice.DiceType.D6,
            };
            var result = _sut.Detect(new[] { 1, 2, 3, 2, 1 }, types, null);
            Assert.IsFalse(result.IsMatch);
            Assert.AreEqual(0, result.BaseDamage);
        }

        [Test]
        public void FuerzaBruta_MitadEsRelativa_4EnD8_NoEntra()
        {
            // Disambiguator del fallback: 4 es mitad superior en d6 pero NO en d8 (necesita 5+).
            // Los otros 4 dados entran para aislar el disambiguator (bolsa completa de 5).
            var types = new[]
            {
                Rollgeon.Dice.DiceType.D8, Rollgeon.Dice.DiceType.D8, Rollgeon.Dice.DiceType.D6,
                Rollgeon.Dice.DiceType.D6, Rollgeon.Dice.DiceType.D6,
            };
            var result = _sut.Detect(new[] { 4, 5, 4, 4, 4 }, types, null);
            Assert.IsFalse(result.IsMatch);
        }

        [Test]
        public void FuerzaBruta_Bordes_TodosEnElUmbralExacto_Match()
        {
            // d3=2 (umbral 2, entra), d20=11 (umbral 11, entra), + 3 d6=4 (umbral 4, entra).
            var types = new[]
            {
                Rollgeon.Dice.DiceType.D3, Rollgeon.Dice.DiceType.D20, Rollgeon.Dice.DiceType.D6,
                Rollgeon.Dice.DiceType.D6, Rollgeon.Dice.DiceType.D6,
            };
            var result = _sut.Detect(new[] { 2, 11, 4, 4, 4 }, types, null);

            Assert.IsTrue(result.IsMatch);
            // 5 (piso) + 2 + 11 + 4 + 4 + 4 = 30
            Assert.AreEqual(30, result.BaseDamage);
            Assert.AreEqual(5, result.CountUsed);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, result.ContributingIndices);
        }

        [Test]
        public void FuerzaBruta_Bordes_UnoDebajoDelUmbralExacto_NoMatch()
        {
            // d3=2 entra; d20=10 no entra (necesita 11+, un punto debajo del umbral).
            var types = new[]
            {
                Rollgeon.Dice.DiceType.D3, Rollgeon.Dice.DiceType.D20, Rollgeon.Dice.DiceType.D6,
                Rollgeon.Dice.DiceType.D6, Rollgeon.Dice.DiceType.D6,
            };
            var result = _sut.Detect(new[] { 2, 10, 4, 4, 4 }, types, null);
            Assert.IsFalse(result.IsMatch);
        }

        [Test]
        public void FuerzaBruta_RequiereBolsaCompleta_SubsetDeTresTodosEnMitadSuperior_NoMatch()
        {
            // Regresion Bocco (2026-07-14): con solo 3 dados "kept" (subset como Par/Trio/Poker),
            // todos en mitad superior, el combo NO debe activarse — exige los 5 de la bolsa.
            var types = new[]
            {
                Rollgeon.Dice.DiceType.D6, Rollgeon.Dice.DiceType.D6, Rollgeon.Dice.DiceType.D6,
            };
            var result = _sut.Detect(new[] { 5, 6, 4 }, types, null);

            Assert.IsFalse(result.IsMatch);
            Assert.AreEqual(0, result.BaseDamage);
            Assert.AreEqual(0, result.CountUsed);
        }

        [Test]
        public void FuerzaBruta_Matches_RequiereBolsaCompleta_SubsetDeTres_NoMatch()
        {
            // Mismo caso via Matches() (el path que usa ContractSheet.MatchBest en combate).
            var types = new[]
            {
                Rollgeon.Dice.DiceType.D6, Rollgeon.Dice.DiceType.D6, Rollgeon.Dice.DiceType.D6,
            };
            Assert.IsFalse(_sut.Matches(new[] { 5, 6, 4 }, types));
        }

        [Test]
        public void FuerzaBruta_FallbackSinTipos_AsumeD6_TodosEntran()
        {
            // Sin tipos (path legacy/tests) asume d6: los 5 valores en {4,5,6} entran.
            var result = _sut.Detect(new[] { 4, 4, 5, 6, 4 });

            Assert.IsTrue(result.IsMatch);
            // 5 (piso) + 4+4+5+6+4 = 28
            Assert.AreEqual(28, result.BaseDamage);
            Assert.AreEqual(5, result.CountUsed);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, result.ContributingIndices);
        }

        [Test]
        public void FuerzaBruta_FallbackSinTipos_AsumeD6_UnoDebajoAnula()
        {
            // Sin tipos asume d6: el 3 no entra (necesita 4+) y anula el combo entero.
            var result = _sut.Detect(new[] { 4, 4, 1, 2, 3 });
            Assert.IsFalse(result.IsMatch);
        }

        [Test]
        public void FuerzaBruta_Matches_ConTipos_RespetaRango()
        {
            // Bolsa completa de 5 (D8 primero, 4 dados de relleno en mitad superior) para aislar
            // el disambiguator d8 en el primer slot.
            var typesD8 = new[]
            {
                Rollgeon.Dice.DiceType.D8, Rollgeon.Dice.DiceType.D6, Rollgeon.Dice.DiceType.D6,
                Rollgeon.Dice.DiceType.D6, Rollgeon.Dice.DiceType.D6,
            };
            Assert.IsFalse(_sut.Matches(new[] { 4, 4, 4, 4, 4 }, typesD8), "4 en d8 es mitad inferior.");
            Assert.IsTrue(_sut.Matches(new[] { 5, 4, 4, 4, 4 }, typesD8), "5 en d8 es mitad superior.");
        }

        [Test]
        public void FuerzaBruta_Empty_NoMatch()
        {
            var result = _sut.Detect(new int[0]);
            Assert.IsFalse(result.IsMatch);
        }

        [Test]
        public void FuerzaBruta_Null_NoMatch()
        {
            var result = _sut.Detect(null);
            Assert.IsFalse(result.IsMatch);
        }
    }
}
