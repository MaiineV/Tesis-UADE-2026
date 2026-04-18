using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Combos;
using Rollgeon.Combos.Concretes;
using Rollgeon.Combos.Tests;
using UnityEngine;

namespace Rollgeon.Heroes.Tests
{
    /// <summary>
    /// Cobertura del DoD §9.1 de Content#0097b (plan §9). Cubre:
    /// - MatchBest picks highest priority (Generala > Poker > ... > Par).
    /// - MatchBest returns null on empty/invalid/no-match dice.
    /// - CrossCombo makes a previously-matching combo invisible.
    /// - Validate enforces the 8-entries + Generala-last rule.
    /// </summary>
    [TestFixture]
    public class ContractSheetTests
    {
        private Combo_Par _par;
        private Combo_DoblePar _doblePar;
        private Combo_SumaX _sumaX;
        private Combo_Trio _trio;
        private Combo_Escalera _escalera;
        private Combo_FullHouse _fullHouse;
        private Combo_Poker _poker;
        private Combo_Generala _generala;

        [SetUp]
        public void SetUp()
        {
            _par = ComboTestUtils.CreateCombo<Combo_Par>(ComboId.Par, 10);
            _doblePar = ComboTestUtils.CreateCombo<Combo_DoblePar>(ComboId.DoublePair, 18);
            _sumaX = ComboTestUtils.CreateCombo<Combo_SumaX>(ComboId.SumX, 25);
            _trio = ComboTestUtils.CreateCombo<Combo_Trio>(ComboId.Triple, 28);
            _escalera = ComboTestUtils.CreateCombo<Combo_Escalera>(ComboId.Straight, 35);
            _fullHouse = ComboTestUtils.CreateCombo<Combo_FullHouse>(ComboId.FullHouse, 40);
            _poker = ComboTestUtils.CreateCombo<Combo_Poker>(ComboId.Poker, 60);
            _generala = ComboTestUtils.CreateCombo<Combo_Generala>(ComboId.Generala, 100);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_par);
            Object.DestroyImmediate(_doblePar);
            Object.DestroyImmediate(_sumaX);
            Object.DestroyImmediate(_trio);
            Object.DestroyImmediate(_escalera);
            Object.DestroyImmediate(_fullHouse);
            Object.DestroyImmediate(_poker);
            Object.DestroyImmediate(_generala);
        }

        private ContractSheet BuildWarriorSheet()
        {
            return new ContractSheet
            {
                Combos = new List<BaseComboSO>
                {
                    _par, _doblePar, _sumaX, _trio, _escalera, _fullHouse, _poker, _generala,
                },
            };
        }

        // ---- MatchBest priority -----------------------------------------

        [Test]
        public void MatchBest_Generala_Wins_On_FiveOfAKind()
        {
            var sheet = BuildWarriorSheet();
            var result = sheet.MatchBest(new[] { 5, 5, 5, 5, 5 });
            Assert.IsNotNull(result, "Generala must match five-of-a-kind.");
            Assert.AreEqual(ComboId.Generala, result.ComboId,
                "Generala has Priority == int.MaxValue, must beat Poker/Trio/Par on [5,5,5,5,5].");
        }

        [Test]
        public void MatchBest_Poker_Wins_On_FourOfAKind()
        {
            var sheet = BuildWarriorSheet();
            var result = sheet.MatchBest(new[] { 5, 5, 5, 5, 1 });
            Assert.IsNotNull(result);
            Assert.AreEqual(ComboId.Poker, result.ComboId,
                "Poker (priority 60) must beat Trio (28) and Par (10) on four-of-a-kind.");
        }

        [Test]
        public void MatchBest_Par_Picks_Only_Valid_On_SinglePair()
        {
            var sheet = BuildWarriorSheet();
            // [1,1,2,3,6] — only Par matches. SumaX(X=4) needs a 4; no straight (missing 4/5).
            var result = sheet.MatchBest(new[] { 1, 1, 2, 3, 6 });
            Assert.IsNotNull(result);
            Assert.AreEqual(ComboId.Par, result.ComboId,
                "Only Par matches [1,1,2,3,6] — highest-priority-that-matches = Par.");
        }

        // ---- MatchBest null / empty / no-match --------------------------

        [Test]
        public void MatchBest_Null_Dice_Returns_Null()
        {
            var sheet = BuildWarriorSheet();
            Assert.IsNull(sheet.MatchBest(null));
        }

        [Test]
        public void MatchBest_Empty_Dice_Returns_Null()
        {
            var sheet = BuildWarriorSheet();
            Assert.IsNull(sheet.MatchBest(new int[0]));
        }

        [Test]
        public void MatchBest_NoMatch_Returns_Null()
        {
            var sheet = BuildWarriorSheet();
            // [1,2,3,4,6] — no pair, no straight (missing 5), no trio, no sum_x (x=4 needs a single 4; but SumaX
            // matches on any 4 appearing once, so we must use dice that don't trigger any combo).
            // Use [1,2,3,5,6] — guarantees no Par, no Trio, no Escalera (missing 4), no FullHouse, no Poker,
            // no Generala. SumaX with X=4 would need a 4. DoblePar needs 2 pairs.
            var result = sheet.MatchBest(new[] { 1, 2, 3, 5, 6 });
            Assert.IsNull(result, "No combo should match [1,2,3,5,6].");
        }

        // ---- Cross combo -------------------------------------------------

        [Test]
        public void CrossCombo_Skips_Crossed_Combo()
        {
            var sheet = BuildWarriorSheet();
            Assert.IsFalse(sheet.IsCrossed(_par));
            sheet.CrossCombo(_par);
            Assert.IsTrue(sheet.IsCrossed(_par));

            // With Par crossed, [1,1,2,3,5] can't match anything else in the canonical Warrior sheet.
            var result = sheet.MatchBest(new[] { 1, 1, 2, 3, 5 });
            Assert.IsNull(result, "Par is crossed — MatchBest should return null for pair-only dice.");
        }

        [Test]
        public void CrossCombo_Idempotent()
        {
            var sheet = BuildWarriorSheet();
            sheet.CrossCombo(_par);
            sheet.CrossCombo(_par);
            Assert.IsTrue(sheet.IsCrossed(_par));
        }

        // ---- Validate ----------------------------------------------------

        [Test]
        public void Validate_Warrior_Sheet_Passes()
        {
            var sheet = BuildWarriorSheet();
            bool ok = sheet.Validate(out var error);
            Assert.IsTrue(ok, $"Warrior sheet should validate. Error: {error}");
        }

        [Test]
        public void Validate_Wrong_Count_Fails()
        {
            var sheet = new ContractSheet
            {
                Combos = new List<BaseComboSO> { _par, _trio },
            };
            bool ok = sheet.Validate(out var error);
            Assert.IsFalse(ok);
            StringAssert.Contains("8", error);
        }

        [Test]
        public void Validate_Last_Must_Be_Generala()
        {
            var sheet = new ContractSheet
            {
                Combos = new List<BaseComboSO>
                {
                    _par, _doblePar, _sumaX, _trio, _escalera, _fullHouse, _generala, _poker,
                },
            };
            bool ok = sheet.Validate(out var error);
            Assert.IsFalse(ok, "Sheet with Poker-last should fail (Generala must be last).");
            StringAssert.Contains("Generala", error);
        }

        [Test]
        public void Validate_Duplicate_Ids_Fails()
        {
            var parDup = ComboTestUtils.CreateCombo<Combo_Par>(ComboId.Par, 10);
            try
            {
                var sheet = new ContractSheet
                {
                    Combos = new List<BaseComboSO>
                    {
                        _par, parDup, _sumaX, _trio, _escalera, _fullHouse, _poker, _generala,
                    },
                };
                bool ok = sheet.Validate(out var error);
                Assert.IsFalse(ok);
                StringAssert.Contains("duplicate", error.ToLowerInvariant());
            }
            finally
            {
                Object.DestroyImmediate(parDup);
            }
        }

        // ---- Instantiate -------------------------------------------------

        [Test]
        public void Instantiate_Clones_Crossed_Set_Empty()
        {
            var sheet = BuildWarriorSheet();
            sheet.CrossCombo(_par);
            Assert.IsTrue(sheet.IsCrossed(_par));

            var clone = sheet.Instantiate();
            Assert.IsFalse(clone.IsCrossed(_par), "Clone must have empty crossed set.");
            Assert.AreEqual(8, clone.Combos.Count);
        }
    }
}
