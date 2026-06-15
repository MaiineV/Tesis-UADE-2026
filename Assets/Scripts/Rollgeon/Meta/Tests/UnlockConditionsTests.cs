using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Dice;
using Rollgeon.Meta.Conditions;

namespace Rollgeon.Meta.Tests
{
    /// <summary>
    /// Tests unitarios de todos los bloques condicionales (#164): build, combos,
    /// clase, ejecución y composites AND/OR, incluyendo semántica de
    /// <c>RunEnded</c> e invalidación de consistencia.
    /// </summary>
    [TestFixture]
    public class UnlockConditionsTests
    {
        private static UnlockEvaluationContext Ctx() => new UnlockEvaluationContext
        {
            ComboCounts = new Dictionary<string, int>(),
            UsedActiveItemIds = new List<string>(),
            ClassesPlayed = new List<string>(),
        };

        // ── Build ───────────────────────────────────────────────

        [Test]
        public void DiceCountOfType_ExactMatch_ReturnsTrue()
        {
            var ctx = Ctx();
            ctx.DiceBuild = new List<DiceType> { DiceType.D6, DiceType.D6, DiceType.D6, DiceType.D6, DiceType.D6 };
            var condition = new DiceCountOfTypeCondition { Type = DiceType.D6, Count = 5 };

            Assert.IsTrue(condition.Evaluate(ctx));
        }

        [Test]
        public void DiceCountOfType_CountMismatch_ReturnsFalse()
        {
            var ctx = Ctx();
            ctx.DiceBuild = new List<DiceType> { DiceType.D6, DiceType.D6, DiceType.D4, DiceType.D4, DiceType.D3 };
            var condition = new DiceCountOfTypeCondition { Type = DiceType.D6, Count = 5 };

            Assert.IsFalse(condition.Evaluate(ctx));
        }

        [Test]
        public void DiceCountOfType_NullBuild_ReturnsFalse()
        {
            var condition = new DiceCountOfTypeCondition { Type = DiceType.D6, Count = 0 };

            Assert.IsFalse(condition.Evaluate(Ctx()));
        }

        [Test]
        public void DiceCombination_SameMultisetDifferentOrder_ReturnsTrue()
        {
            var ctx = Ctx();
            ctx.DiceBuild = new List<DiceType> { DiceType.D8, DiceType.D4, DiceType.D6, DiceType.D4, DiceType.D6 };
            var condition = new DiceCombinationCondition
            {
                Combination = new List<DiceType> { DiceType.D4, DiceType.D4, DiceType.D6, DiceType.D6, DiceType.D8 },
            };

            Assert.IsTrue(condition.Evaluate(ctx));
        }

        [Test]
        public void DiceCombination_DifferentCounts_ReturnsFalse()
        {
            var ctx = Ctx();
            ctx.DiceBuild = new List<DiceType> { DiceType.D4, DiceType.D4, DiceType.D4, DiceType.D6, DiceType.D8 };
            var condition = new DiceCombinationCondition
            {
                Combination = new List<DiceType> { DiceType.D4, DiceType.D4, DiceType.D6, DiceType.D6, DiceType.D8 },
            };

            Assert.IsFalse(condition.Evaluate(ctx));
        }

        [Test]
        public void OnlyDiceType_AllSameType_ReturnsTrue()
        {
            var ctx = Ctx();
            ctx.DiceBuild = new List<DiceType> { DiceType.D4, DiceType.D4, DiceType.D4, DiceType.D4, DiceType.D4 };
            var condition = new OnlyDiceTypeCondition { Type = DiceType.D4 };

            Assert.IsTrue(condition.Evaluate(ctx));
        }

        [Test]
        public void OnlyDiceType_OneIntruder_ReturnsFalse()
        {
            var ctx = Ctx();
            ctx.DiceBuild = new List<DiceType> { DiceType.D4, DiceType.D4, DiceType.D6, DiceType.D4, DiceType.D4 };
            var condition = new OnlyDiceTypeCondition { Type = DiceType.D4 };

            Assert.IsFalse(condition.Evaluate(ctx));
        }

        // ── Combos ──────────────────────────────────────────────

        [Test]
        public void ComboExecutedTimes_CountReached_ReturnsTrueMidRun()
        {
            var ctx = Ctx();
            ctx.ComboCounts = new Dictionary<string, int> { ["combo.par"] = 3 };
            var condition = new ComboExecutedTimesCondition { ComboId = "combo.par", Times = 3 };

            Assert.IsTrue(condition.Evaluate(ctx), "Monotónica: debe cumplirse aún con RunEnded == false");
        }

        [Test]
        public void ComboExecutedTimes_CountBelow_ReturnsFalse()
        {
            var ctx = Ctx();
            ctx.ComboCounts = new Dictionary<string, int> { ["combo.par"] = 2 };
            var condition = new ComboExecutedTimesCondition { ComboId = "combo.par", Times = 3 };

            Assert.IsFalse(condition.Evaluate(ctx));
        }

        [Test]
        public void ComboNeverExecuted_MidRun_ReturnsFalseEvenIfClean()
        {
            var ctx = Ctx();
            var condition = new ComboNeverExecutedCondition { ComboId = "combo.generala" };

            Assert.IsFalse(condition.Evaluate(ctx), "Consistencia total: solo evaluable al cierre");
        }

        [Test]
        public void ComboNeverExecuted_RunEndedClean_ReturnsTrue()
        {
            var ctx = Ctx();
            ctx.RunEnded = true;
            var condition = new ComboNeverExecutedCondition { ComboId = "combo.generala" };

            Assert.IsTrue(condition.Evaluate(ctx));
        }

        [Test]
        public void ComboNeverExecuted_ComboFired_IsInvalidated()
        {
            var ctx = Ctx();
            ctx.ComboCounts = new Dictionary<string, int> { ["combo.generala"] = 1 };
            var condition = new ComboNeverExecutedCondition { ComboId = "combo.generala" };

            Assert.IsTrue(condition.IsInvalidated(ctx));
            ctx.RunEnded = true;
            Assert.IsFalse(condition.Evaluate(ctx));
        }

        [Test]
        public void AllContractCombos_AllExecutedOnce_ReturnsTrue()
        {
            var ctx = Ctx();
            ctx.ContractComboIds = new List<string> { "combo.par", "combo.trio" };
            ctx.ComboCounts = new Dictionary<string, int> { ["combo.par"] = 1, ["combo.trio"] = 4 };
            var condition = new AllContractCombosExecutedCondition();

            Assert.IsTrue(condition.Evaluate(ctx));
        }

        [Test]
        public void AllContractCombos_OneMissing_ReturnsFalse()
        {
            var ctx = Ctx();
            ctx.ContractComboIds = new List<string> { "combo.par", "combo.trio" };
            ctx.ComboCounts = new Dictionary<string, int> { ["combo.par"] = 1 };
            var condition = new AllContractCombosExecutedCondition();

            Assert.IsFalse(condition.Evaluate(ctx));
        }

        // ── Clase ───────────────────────────────────────────────

        [Test]
        public void ClassIs_MatchingClass_ReturnsTrue()
        {
            var ctx = Ctx();
            ctx.ClassId = "Warrior";
            var condition = new ClassIsCondition { ClassId = "Warrior" };

            Assert.IsTrue(condition.Evaluate(ctx));
        }

        [Test]
        public void ClassIs_DifferentClass_ReturnsFalse()
        {
            var ctx = Ctx();
            ctx.ClassId = "Berserker";
            var condition = new ClassIsCondition { ClassId = "Warrior" };

            Assert.IsFalse(condition.Evaluate(ctx));
        }

        [Test]
        public void ConsecutiveWins_StreakReached_ReturnsTrueOnlyAtRunEnd()
        {
            var ctx = Ctx();
            ctx.ConsecutiveWins = 3;
            var condition = new ConsecutiveWinsCondition { Wins = 3 };

            Assert.IsFalse(condition.Evaluate(ctx), "Solo evaluable al cierre");
            ctx.RunEnded = true;
            Assert.IsTrue(condition.Evaluate(ctx));
        }

        [Test]
        public void DistinctClassesPlayed_EnoughClasses_ReturnsTrue()
        {
            var ctx = Ctx();
            ctx.ClassesPlayed = new List<string> { "Warrior", "Berserker" };
            var condition = new DistinctClassesPlayedCondition { Count = 2 };

            Assert.IsTrue(condition.Evaluate(ctx));
        }

        // ── Ejecución ───────────────────────────────────────────

        [Test]
        public void NoPotionUsed_CleanRunEnded_ReturnsTrue()
        {
            var ctx = Ctx();
            ctx.RunEnded = true;
            var condition = new NoPotionUsedCondition();

            Assert.IsTrue(condition.Evaluate(ctx));
        }

        [Test]
        public void NoPotionUsed_AnyItemUsedWithEmptyFilter_IsInvalidated()
        {
            var ctx = Ctx();
            ctx.UsedActiveItemIds = new List<string> { "item.healing_potion" };
            var condition = new NoPotionUsedCondition();

            Assert.IsTrue(condition.IsInvalidated(ctx));
            ctx.RunEnded = true;
            Assert.IsFalse(condition.Evaluate(ctx));
        }

        [Test]
        public void NoPotionUsed_FilteredIds_IgnoresOtherItems()
        {
            var ctx = Ctx();
            ctx.RunEnded = true;
            ctx.UsedActiveItemIds = new List<string> { "item.bomb" };
            var condition = new NoPotionUsedCondition
            {
                PotionItemIds = new List<string> { "item.healing_potion" },
            };

            Assert.IsFalse(condition.IsInvalidated(ctx));
            Assert.IsTrue(condition.Evaluate(ctx));
        }

        [Test]
        public void FlawlessCombats_ThresholdReached_ReturnsTrueMidRun()
        {
            var ctx = Ctx();
            ctx.FlawlessCombats = 3;
            var condition = new FlawlessCombatsCondition { MinCombats = 3 };

            Assert.IsTrue(condition.Evaluate(ctx));
        }

        [Test]
        public void DefeatAllBossesNoFlee_CleanFullRun_ReturnsTrueAtEnd()
        {
            var ctx = Ctx();
            ctx.RunEnded = true;
            ctx.FloorsVisited = 2;
            ctx.BossesDefeated = 2;
            var condition = new DefeatAllBossesNoFleeCondition();

            Assert.IsTrue(condition.Evaluate(ctx));
        }

        [Test]
        public void DefeatAllBossesNoFlee_FledOnce_IsInvalidated()
        {
            var ctx = Ctx();
            ctx.CombatsFled = 1;
            ctx.FloorsVisited = 2;
            ctx.BossesDefeated = 2;
            var condition = new DefeatAllBossesNoFleeCondition();

            Assert.IsTrue(condition.IsInvalidated(ctx));
            ctx.RunEnded = true;
            Assert.IsFalse(condition.Evaluate(ctx));
        }

        [Test]
        public void DefeatAllBossesNoFlee_MissingBoss_ReturnsFalse()
        {
            var ctx = Ctx();
            ctx.RunEnded = true;
            ctx.FloorsVisited = 3;
            ctx.BossesDefeated = 2;
            var condition = new DefeatAllBossesNoFleeCondition();

            Assert.IsFalse(condition.Evaluate(ctx));
        }

        // ── Composites ──────────────────────────────────────────

        [Test]
        public void And_AllChildrenTrue_ReturnsTrue()
        {
            var ctx = Ctx();
            ctx.ClassId = "Warrior";
            ctx.FlawlessCombats = 2;
            var condition = new AndCondition
            {
                Children = new List<IUnlockCondition>
                {
                    new ClassIsCondition { ClassId = "Warrior" },
                    new FlawlessCombatsCondition { MinCombats = 2 },
                },
            };

            Assert.IsTrue(condition.Evaluate(ctx));
        }

        [Test]
        public void And_OneChildFalse_ReturnsFalse()
        {
            var ctx = Ctx();
            ctx.ClassId = "Warrior";
            ctx.FlawlessCombats = 1;
            var condition = new AndCondition
            {
                Children = new List<IUnlockCondition>
                {
                    new ClassIsCondition { ClassId = "Warrior" },
                    new FlawlessCombatsCondition { MinCombats = 2 },
                },
            };

            Assert.IsFalse(condition.Evaluate(ctx));
        }

        [Test]
        public void And_EmptyChildren_ReturnsFalse()
        {
            Assert.IsFalse(new AndCondition().Evaluate(Ctx()));
        }

        [Test]
        public void And_OneChildInvalidated_IsInvalidated()
        {
            var ctx = Ctx();
            ctx.CombatsFled = 1;
            var condition = new AndCondition
            {
                Children = new List<IUnlockCondition>
                {
                    new ClassIsCondition { ClassId = "Warrior" },
                    new DefeatAllBossesNoFleeCondition(),
                },
            };

            Assert.IsTrue(condition.IsInvalidated(ctx));
        }

        [Test]
        public void Or_OneChildTrue_ReturnsTrue()
        {
            var ctx = Ctx();
            ctx.ClassId = "Berserker";
            var condition = new OrCondition
            {
                Children = new List<IUnlockCondition>
                {
                    new ClassIsCondition { ClassId = "Warrior" },
                    new ClassIsCondition { ClassId = "Berserker" },
                },
            };

            Assert.IsTrue(condition.Evaluate(ctx));
        }

        [Test]
        public void Or_OnlySomeChildrenInvalidated_IsNotInvalidated()
        {
            var ctx = Ctx();
            ctx.CombatsFled = 1;
            var condition = new OrCondition
            {
                Children = new List<IUnlockCondition>
                {
                    new DefeatAllBossesNoFleeCondition(),
                    new FlawlessCombatsCondition { MinCombats = 2 },
                },
            };

            Assert.IsFalse(condition.IsInvalidated(ctx), "OR sigue vivo mientras quede un hijo posible");
        }

        [Test]
        public void Or_AllChildrenInvalidated_IsInvalidated()
        {
            var ctx = Ctx();
            ctx.CombatsFled = 1;
            ctx.UsedActiveItemIds = new List<string> { "item.healing_potion" };
            var condition = new OrCondition
            {
                Children = new List<IUnlockCondition>
                {
                    new DefeatAllBossesNoFleeCondition(),
                    new NoPotionUsedCondition(),
                },
            };

            Assert.IsTrue(condition.IsInvalidated(ctx));
        }
    }
}
