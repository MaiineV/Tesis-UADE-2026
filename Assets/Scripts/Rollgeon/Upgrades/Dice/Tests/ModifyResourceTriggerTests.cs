using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Attributes;
using Rollgeon.Combos;
using Rollgeon.Dice;
using Rollgeon.Effects;
using Rollgeon.Effects.Readers;
using Rollgeon.Upgrades.Dice.Triggers.Concretes;

namespace Rollgeon.Upgrades.Dice.Tests
{
    [TestFixture]
    public class ModifyResourceTriggerTests
    {
        private static ModifyResourceTrigger MakeTrigger(
            TriggerWhen when,
            ComboFilter filter,
            ResourceTarget target,
            ResourceOperation op,
            int amount)
        {
            return new ModifyResourceTrigger
            {
                When = when,
                Filter = filter,
                Target = target,
                Operation = op,
                Amount = new ReadConstantInt { Value = amount },
            };
        }

        private static EnchantmentTriggerContext MakeCtx(string comboId)
        {
            return new EnchantmentTriggerContext
            {
                Effect = null, // ReadConstantInt ignora el EffectContext.
                Scratch = new EnchantmentScratch(),
                ComboId = comboId,
            };
        }

        private static int ResolvedGold(EnchantmentScratch scratch)
        {
            return scratch.Resources.TryGetValue(ResourceTarget.Gold, out var acc)
                ? acc.Resolve(0)
                : 0;
        }

        [Test]
        public void OnComboMatched_AnyCombo_AddsGold()
        {
            var trigger = MakeTrigger(
                TriggerWhen.ComboMatched,
                new ComboFilter { Mode = ComboFilterMode.AnyCombo },
                ResourceTarget.Gold, ResourceOperation.Add, 3);
            var ctx = MakeCtx("combo.trio");

            trigger.OnComboMatched(ctx);

            Assert.AreEqual(3, ResolvedGold(ctx.Scratch));
        }

        [Test]
        public void OnComboMatched_ComboIdsFilter_AppliesOnlyToListedCombo()
        {
            var trigger = MakeTrigger(
                TriggerWhen.ComboMatched,
                new ComboFilter
                {
                    Mode = ComboFilterMode.ComboIds,
                    ComboIds = new List<string> { "combo.ladder" },
                },
                ResourceTarget.Gold, ResourceOperation.Add, 5);

            var matching = MakeCtx("combo.ladder");
            trigger.OnComboMatched(matching);
            Assert.AreEqual(5, ResolvedGold(matching.Scratch));

            var nonMatching = MakeCtx("combo.trio");
            trigger.OnComboMatched(nonMatching);
            Assert.AreEqual(0, ResolvedGold(nonMatching.Scratch));
        }

        [Test]
        public void OnComboMatched_DoesNotFireWhenWhenIsRollResolved()
        {
            var trigger = MakeTrigger(
                TriggerWhen.RollResolved,
                new ComboFilter { Mode = ComboFilterMode.AnyCombo },
                ResourceTarget.Gold, ResourceOperation.Add, 5);
            var ctx = MakeCtx("combo.trio");

            trigger.OnComboMatched(ctx);

            Assert.AreEqual(0, ResolvedGold(ctx.Scratch));
        }

        [Test]
        public void OnRollResolved_FiresWhenWhenIsRollResolved_IgnoringFilter()
        {
            var trigger = MakeTrigger(
                TriggerWhen.RollResolved,
                new ComboFilter { Mode = ComboFilterMode.ComboIds, ComboIds = new List<string>() },
                ResourceTarget.Gold, ResourceOperation.Add, 2);
            var ctx = MakeCtx(null); // roll no tiene combo

            trigger.OnRollResolved(ctx);

            Assert.AreEqual(2, ResolvedGold(ctx.Scratch));
        }

        [Test]
        public void OnRollResolved_DoesNotFireWhenWhenIsComboMatched()
        {
            var trigger = MakeTrigger(
                TriggerWhen.ComboMatched,
                new ComboFilter { Mode = ComboFilterMode.AnyCombo },
                ResourceTarget.Gold, ResourceOperation.Add, 2);
            var ctx = MakeCtx(null);

            trigger.OnRollResolved(ctx);

            Assert.AreEqual(0, ResolvedGold(ctx.Scratch));
        }

        [Test]
        public void OnDiceRolled_FiresWhenWhenIsDiceRolled()
        {
            var trigger = MakeTrigger(
                TriggerWhen.DiceRolled,
                new ComboFilter { Mode = ComboFilterMode.AnyCombo },
                ResourceTarget.Gold, ResourceOperation.Add, 4);
            var ctx = MakeCtx(null);

            trigger.OnDiceRolled(ctx);

            Assert.AreEqual(4, ResolvedGold(ctx.Scratch));
        }

        [Test]
        public void OnComboMatched_StatTarget_WritesToStatAccumulator()
        {
            var shield = ResourceTarget.OfStat(StatType.Shield);
            var trigger = MakeTrigger(
                TriggerWhen.ComboMatched,
                new ComboFilter
                {
                    Mode = ComboFilterMode.ComboIds,
                    ComboIds = new List<string> { "combo.full_house" },
                },
                shield, ResourceOperation.Add, 2);
            var ctx = MakeCtx("combo.full_house");

            trigger.OnComboMatched(ctx);

            Assert.IsTrue(ctx.Scratch.Resources.TryGetValue(shield, out var acc));
            Assert.AreEqual(2, acc.Resolve(0));
            Assert.IsFalse(ctx.Scratch.Resources.ContainsKey(ResourceTarget.Gold));
        }

        [Test]
        public void OnComboMatched_NullAmount_TreatedAsZero()
        {
            var trigger = new ModifyResourceTrigger
            {
                When = TriggerWhen.ComboMatched,
                Filter = new ComboFilter { Mode = ComboFilterMode.AnyCombo },
                Target = ResourceTarget.Gold,
                Operation = ResourceOperation.Add,
                Amount = null,
            };
            var ctx = MakeCtx("combo.trio");

            Assert.DoesNotThrow(() => trigger.OnComboMatched(ctx));
            Assert.AreEqual(0, ResolvedGold(ctx.Scratch));
        }

        // ── Condición NoComboMatched (Ench_GoldOnRoll / Greed) ──────────

        private static EnchantmentTriggerContext MakeRollCtx(ComboDetectionResult? combo)
        {
            return new EnchantmentTriggerContext
            {
                Effect = new EffectContext { ComboResult = combo },
                Scratch = new EnchantmentScratch(),
                ComboId = null,
            };
        }

        private static ModifyResourceTrigger MakeGoldRollTrigger(TriggerCondition condition)
        {
            var t = MakeTrigger(
                TriggerWhen.RollResolved,
                new ComboFilter { Mode = ComboFilterMode.None },
                ResourceTarget.Gold, ResourceOperation.Add, 2);
            t.Condition = condition;
            return t;
        }

        [Test]
        public void NoComboMatched_FiresWhenNoComboPresent()
        {
            var trigger = MakeGoldRollTrigger(TriggerCondition.NoComboMatched);
            var ctx = MakeRollCtx(null);

            trigger.OnRollResolved(ctx);

            Assert.AreEqual(2, ResolvedGold(ctx.Scratch));
        }

        [Test]
        public void NoComboMatched_FiresWhenComboIsNoMatch()
        {
            var trigger = MakeGoldRollTrigger(TriggerCondition.NoComboMatched);
            var ctx = MakeRollCtx(ComboDetectionResult.NoMatch());

            trigger.OnRollResolved(ctx);

            Assert.AreEqual(2, ResolvedGold(ctx.Scratch));
        }

        [Test]
        public void NoComboMatched_DoesNotFireWhenComboMatched()
        {
            var trigger = MakeGoldRollTrigger(TriggerCondition.NoComboMatched);
            var ctx = MakeRollCtx(ComboDetectionResult.Match(20, 2));

            trigger.OnRollResolved(ctx);

            Assert.AreEqual(0, ResolvedGold(ctx.Scratch));
        }

        // ── Condición DieOnMaxFace (Ench_Fortaleza) ─────────────────────

        private static EnchantmentTriggerContext MakeMaxFaceCtx(
            List<int> diceResult, int bagSlotIndex, DiceType type)
        {
            return new EnchantmentTriggerContext
            {
                Effect = new EffectContext { DiceResult = diceResult },
                Scratch = new EnchantmentScratch(),
                ComboId = "combo.trio",
                Slot = new EnchantmentSlotRef(type, bagSlotIndex, 0),
            };
        }

        private static ModifyResourceTrigger MakeShieldMaxFaceTrigger()
        {
            var t = MakeTrigger(
                TriggerWhen.ComboMatched,
                new ComboFilter { Mode = ComboFilterMode.AnyCombo },
                ResourceTarget.OfStat(StatType.Shield), ResourceOperation.Add, 2);
            t.Condition = TriggerCondition.DieOnMaxFace;
            return t;
        }

        [Test]
        public void DieOnMaxFace_FiresWhenCarrierShowsMaxFace()
        {
            var shield = ResourceTarget.OfStat(StatType.Shield);
            var trigger = MakeShieldMaxFaceTrigger();
            // d6 en el slot 1 mostrando 6 (su cara máxima).
            var ctx = MakeMaxFaceCtx(new List<int> { 3, 6, 1 }, 1, DiceType.D6);

            trigger.OnComboMatched(ctx);

            Assert.IsTrue(ctx.Scratch.Resources.TryGetValue(shield, out var acc));
            Assert.AreEqual(2, acc.Resolve(0));
        }

        [Test]
        public void DieOnMaxFace_DoesNotFireWhenCarrierBelowMaxFace()
        {
            var shield = ResourceTarget.OfStat(StatType.Shield);
            var trigger = MakeShieldMaxFaceTrigger();
            var ctx = MakeMaxFaceCtx(new List<int> { 3, 5, 1 }, 1, DiceType.D6);

            trigger.OnComboMatched(ctx);

            Assert.IsFalse(ctx.Scratch.Resources.ContainsKey(shield));
        }

        [Test]
        public void DieOnMaxFace_DoesNotFireWhenSlotIndexOutOfRange()
        {
            var shield = ResourceTarget.OfStat(StatType.Shield);
            var trigger = MakeShieldMaxFaceTrigger();
            var ctx = MakeMaxFaceCtx(new List<int> { 6, 6 }, 5, DiceType.D6);

            trigger.OnComboMatched(ctx);

            Assert.IsFalse(ctx.Scratch.Resources.ContainsKey(shield));
        }
    }
}
