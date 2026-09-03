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
        public void EffMultiplyComboDamage_WithReader_UsesReadFloatOverConstant()
        {
            // Arrange — la constante queda en 2 pero el reader (Eco Menguante) manda.
            var ctx = BuildScratchContext(out var scratch);
            var eff = new EffMultiplyComboDamage
            {
                Multiplier = 2f,
                MultiplierReader = new ReadAttackDecayMultiplier { Start = 4.9f, DecayPerAttack = 0.1f, Min = 1f },
            };

            // Act — sin IPlayerTurnStateService el reader devuelve Start.
            bool result = eff.Apply(ctx);

            // Assert — la fracción viaja entera al scratch.
            Assert.IsTrue(result);
            Assert.AreEqual(4.9f, scratch.ComboDamageMultiplier, 0.0001f);
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

        // ---- EffAddComboMultiplier (canal aditivo sobre M) ---------------------------

        [Test]
        public void EffAddComboMultiplier_Constant_AddsToComboMultiplierBonus()
        {
            var ctx = BuildScratchContext(out var scratch);
            var eff = new EffAddComboMultiplier { Amount = 2f };

            bool ok = eff.Apply(ctx);

            Assert.IsTrue(ok);
            Assert.AreEqual(2f, scratch.ComboMultiplierBonus, 0.0001f);
            Assert.AreEqual(1f, scratch.ComboDamageMultiplier, 0.0001f, "no toca el canal multiplicativo");
        }

        [Test]
        public void EffAddComboMultiplier_AccumulatesAdditivelyAcrossApplies()
        {
            var ctx = BuildScratchContext(out var scratch);

            new EffAddComboMultiplier { Amount = 2f }.Apply(ctx);
            ctx.lastResult = true;
            new EffAddComboMultiplier { Amount = 3f }.Apply(ctx);

            Assert.AreEqual(5f, scratch.ComboMultiplierBonus, 0.0001f, "+2 y +3 suman, nunca multiplican");
        }

        [Test]
        public void EffAddComboMultiplier_WithReader_UsesReadFloatTimesScale()
        {
            var ctx = BuildScratchContext(out var scratch);
            var eff = new EffAddComboMultiplier
            {
                Amount = 99f,
                AmountReader = new ReadConstantInt { Value = 4 },
                ReaderScale = 0.05f,
            };

            eff.Apply(ctx);

            Assert.AreEqual(0.2f, scratch.ComboMultiplierBonus, 0.0001f, "reader × scale pisa la constante");
        }

        [Test]
        public void EffAddComboMultiplier_WithoutTriggerContext_ReturnsFalseAndWarns()
        {
            var ctx = new EffectContext();
            var eff = new EffAddComboMultiplier { Amount = 2f };
            LogAssert.Expect(LogType.Warning,
                "[EffAddComboMultiplier] sin ScratchTriggerContext — este efecto solo funciona dentro de un dispatch de trigger de combo.");

            bool ok = eff.Apply(ctx);

            Assert.IsFalse(ok);
        }

        [Test]
        public void EffAddComboMultiplier_IsComboScratchWriter()
        {
            Assert.IsInstanceOf<IComboScratchWriter>(new EffAddComboMultiplier());
        }
    }
}
