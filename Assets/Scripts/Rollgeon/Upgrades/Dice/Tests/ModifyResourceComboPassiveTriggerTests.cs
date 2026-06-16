using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Attributes;
using Rollgeon.Effects.Readers;
using Rollgeon.Upgrades.Combos;
using Rollgeon.Upgrades.Combos.Triggers.Concretes;
using Rollgeon.Upgrades.Dice;

namespace Rollgeon.Upgrades.Dice.Tests
{
    [TestFixture]
    public class ModifyResourceComboPassiveTriggerTests
    {
        private static ComboPassiveContext MakeCtx(string comboId)
        {
            return new ComboPassiveContext
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
            var trigger = new ModifyResourceComboPassiveTrigger
            {
                Filter = new ComboFilter { Mode = ComboFilterMode.AnyCombo },
                Target = ResourceTarget.Gold,
                Operation = ResourceOperation.Add,
                Amount = new ReadConstantInt { Value = 6 },
            };
            var ctx = MakeCtx("combo.trio");

            trigger.OnComboMatched(ctx);

            Assert.AreEqual(6, ResolvedGold(ctx.Scratch));
        }

        [Test]
        public void OnComboMatched_ComboIdsFilter_RestrictsToListedCombo()
        {
            var trigger = new ModifyResourceComboPassiveTrigger
            {
                Filter = new ComboFilter
                {
                    Mode = ComboFilterMode.ComboIds,
                    ComboIds = new List<string> { "combo.generala" },
                },
                Target = ResourceTarget.Gold,
                Operation = ResourceOperation.Add,
                Amount = new ReadConstantInt { Value = 6 },
            };

            var nonMatching = MakeCtx("combo.trio");
            trigger.OnComboMatched(nonMatching);
            Assert.AreEqual(0, ResolvedGold(nonMatching.Scratch));

            var matching = MakeCtx("combo.generala");
            trigger.OnComboMatched(matching);
            Assert.AreEqual(6, ResolvedGold(matching.Scratch));
        }

        [Test]
        public void OnComboMatched_StatTarget_WritesToStatAccumulator()
        {
            var shield = ResourceTarget.OfStat(StatType.Shield);
            var trigger = new ModifyResourceComboPassiveTrigger
            {
                Filter = new ComboFilter { Mode = ComboFilterMode.AnyCombo },
                Target = shield,
                Operation = ResourceOperation.Add,
                Amount = new ReadConstantInt { Value = 3 },
            };
            var ctx = MakeCtx("combo.full_house");

            trigger.OnComboMatched(ctx);

            Assert.IsTrue(ctx.Scratch.Resources.TryGetValue(shield, out var acc));
            Assert.AreEqual(3, acc.Resolve(0));
        }
    }
}
