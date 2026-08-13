using NUnit.Framework;
using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Readers;
using Rollgeon.Upgrades;
using Rollgeon.Upgrades.Dice;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rollgeon.Effects.Tests
{
    /// <summary>
    /// Tests de los efectos que escriben al scratch de combo del trigger context
    /// (<see cref="EffAddComboBonus"/>, <see cref="EffMultiplyComboDamage"/>,
    /// <see cref="EffBlockComboDamage"/>).
    /// </summary>
    [TestFixture]
    public class ComboScratchEffectTests
    {
        private static EffectContext BuildScratchContext(out EnchantmentScratch scratch)
        {
            scratch = new EnchantmentScratch();
            return new EffectContext
            {
                TriggerContext = new ScratchTriggerContext
                {
                    Scratch = scratch,
                    ComboId = "combo.par",
                    Channel = ScratchChannel.ComboPlay,
                },
            };
        }

        [Test]
        public void EffAddComboBonus_WritesAmountToDispatchScratch()
        {
            // Arrange
            var ctx = BuildScratchContext(out var scratch);
            var eff = new EffAddComboBonus { Amount = new ReadConstantInt { Value = 7 } };

            // Act
            bool result = eff.Apply(ctx);

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(7, scratch.BonusComboDamage);
        }

        [Test]
        public void EffAddComboBonus_NegativeAmount_SubtractsFromBonus()
        {
            // Arrange
            var ctx = BuildScratchContext(out var scratch);
            scratch.BonusComboDamage = 10;
            var eff = new EffAddComboBonus { Amount = new ReadConstantInt { Value = -4 } };

            // Act
            eff.Apply(ctx);

            // Assert
            Assert.AreEqual(6, scratch.BonusComboDamage);
        }

        [Test]
        public void EffAddComboBonus_AccumulatesAcrossApplies()
        {
            // Arrange
            var ctx = BuildScratchContext(out var scratch);
            var eff = new EffAddComboBonus { Amount = new ReadConstantInt { Value = 3 } };

            // Act — dos triggers distintos escribiendo al mismo scratch del evento.
            eff.Apply(ctx);
            ctx.lastResult = true;
            eff.Apply(ctx);

            // Assert
            Assert.AreEqual(6, scratch.BonusComboDamage);
        }

        [Test]
        public void EffAddComboBonus_WithoutTriggerContext_ReturnsFalseAndWarns()
        {
            // Arrange — contexto de behavior común, sin dispatch de trigger de combo.
            var ctx = new EffectContext();
            var eff = new EffAddComboBonus { Amount = new ReadConstantInt { Value = 7 } };

            // Act
            LogAssert.Expect(LogType.Warning,
                "[EffAddComboBonus] sin ScratchTriggerContext — este efecto " +
                "solo funciona dentro de un dispatch de trigger de combo.");
            bool result = eff.Apply(ctx);

            // Assert — false corta el grupo (convención IRequiresTriggerContext).
            Assert.IsFalse(result);
            Assert.IsFalse(ctx.lastResult);
        }

        [Test]
        public void EffMultiplyComboDamage_ComposesMultiplicatively()
        {
            // Arrange
            var ctx = BuildScratchContext(out var scratch);
            var half = new EffMultiplyComboDamage { Multiplier = 1.5f };
            var dbl = new EffMultiplyComboDamage { Multiplier = 2f };

            // Act
            half.Apply(ctx);
            ctx.lastResult = true;
            dbl.Apply(ctx);

            // Assert
            Assert.AreEqual(3f, scratch.ComboDamageMultiplier, 0.0001f);
        }

        [Test]
        public void EffMultiplyComboDamage_WithoutTriggerContext_ReturnsFalse()
        {
            // Arrange
            var eff = new EffMultiplyComboDamage { Multiplier = 2f };

            // Act
            LogAssert.Expect(LogType.Warning,
                "[EffMultiplyComboDamage] sin ScratchTriggerContext — este efecto " +
                "solo funciona dentro de un dispatch de trigger de combo.");
            bool result = eff.Apply(new EffectContext());

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void EffBlockComboDamage_SetsFlag_Idempotent()
        {
            // Arrange
            var ctx = BuildScratchContext(out var scratch);
            var eff = new EffBlockComboDamage();

            // Act
            eff.Apply(ctx);
            ctx.lastResult = true;
            eff.Apply(ctx);

            // Assert
            Assert.IsTrue(scratch.BlockComboDamage);
        }

        [Test]
        public void EffBlockComboDamage_WithoutTriggerContext_ReturnsFalse()
        {
            // Arrange
            var eff = new EffBlockComboDamage();

            // Act
            LogAssert.Expect(LogType.Warning,
                "[EffBlockComboDamage] sin ScratchTriggerContext — este efecto " +
                "solo funciona dentro de un dispatch de trigger de combo.");
            bool result = eff.Apply(new EffectContext());

            // Assert
            Assert.IsFalse(result);
        }
    }
}
