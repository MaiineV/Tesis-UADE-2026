using System;
using NUnit.Framework;
using Rollgeon.Dice;
using Rollgeon.Effects;
using Rollgeon.Upgrades.Dice.Readers;

namespace Rollgeon.Upgrades.Dice.Tests
{
    /// <summary>
    /// Ops "por dado" de <see cref="ReadCarrierRollDelta"/> (Feature#0073): el delta que
    /// entra a N para que el dado carrier valga distinto de su cara sin tocar la detección
    /// del combo. El breakdown lo muestra como proc del dado (journal por BagSlot).
    /// </summary>
    [TestFixture]
    public class ReadCarrierRollDeltaOpsTests
    {
        private static EffectContext Ctx(DiceType type, int face, int carrier = 0)
        {
            return new EffectContext
            {
                SourceGuid = Guid.NewGuid(),
                DiceResult = new[] { face },
                TriggerContext = new ScratchTriggerContext
                {
                    Scratch = new EnchantmentScratch(),
                    Slot = new EnchantmentSlotRef(type, carrier, 0),
                    Channel = ScratchChannel.DiceEnchantment,
                },
            };
        }

        private static int Read(CarrierRollDeltaOp op, DiceType type, int face)
            => new ReadCarrierRollDelta { Op = op }.Read(Ctx(type, face));

        [Test]
        public void Exclude_NegatesFace_SoTheDieAddsZeroToN()
        {
            // Oxidado / Frágil (cara): cara + delta = 0.
            Assert.AreEqual(-4, Read(CarrierRollDeltaOp.Exclude, DiceType.D6, 4));
            Assert.AreEqual(-20, Read(CarrierRollDeltaOp.Exclude, DiceType.D20, 20));
        }

        [Test]
        public void Double_AddsFace_SoTheDieCountsTwice()
        {
            // Frágil (cruz): cara + delta = 2·cara. d20 con 20 → 40 (ejemplo del GDD).
            Assert.AreEqual(20, Read(CarrierRollDeltaOp.Double, DiceType.D20, 20));
            Assert.AreEqual(3, Read(CarrierRollDeltaOp.Double, DiceType.D6, 3));
        }

        [TestCase(DiceType.D6, 6, 6)]     // máximo → duplica
        [TestCase(DiceType.D6, 5, -2)]    // 5 → ceil(2.5)=3
        [TestCase(DiceType.D6, 4, -2)]    // 4 → 2
        [TestCase(DiceType.D6, 1, 0)]     // 1 → ceil(0.5)=1, nunca 0
        [TestCase(DiceType.D20, 20, 20)]
        [TestCase(DiceType.D20, 7, -3)]   // 7 → 4
        public void DoubleMaxHalveRest_MatchesVolatilGdd(DiceType type, int face, int expected)
        {
            Assert.AreEqual(expected, Read(CarrierRollDeltaOp.DoubleMaxHalveRest, type, face));
        }

        [TestCase(5, 10)]   // impar → ×3 ⇒ delta +2·cara
        [TestCase(1, 2)]
        [TestCase(4, -4)]   // par → 0
        [TestCase(6, -6)]
        public void TripleOddZeroEven_MatchesEnfiestadoGdd(int face, int expected)
        {
            Assert.AreEqual(expected, Read(CarrierRollDeltaOp.TripleOddZeroEven, DiceType.D6, face));
        }

        [Test]
        public void NewOps_AreAppendOnly_LegacyValuesUnchanged()
        {
            // Los SOs serializan el int: Invert/ClampMinToHalfMax/DoubleMaxZeroMin no se mueven.
            Assert.AreEqual(0, (int)CarrierRollDeltaOp.Invert);
            Assert.AreEqual(1, (int)CarrierRollDeltaOp.ClampMinToHalfMax);
            Assert.AreEqual(2, (int)CarrierRollDeltaOp.DoubleMaxZeroMin);
            Assert.AreEqual(3, (int)CarrierRollDeltaOp.Exclude);
            Assert.AreEqual(4, (int)CarrierRollDeltaOp.Double);
            Assert.AreEqual(5, (int)CarrierRollDeltaOp.DoubleMaxHalveRest);
            Assert.AreEqual(6, (int)CarrierRollDeltaOp.TripleOddZeroEven);
        }

        [Test]
        public void WithoutTriggerContext_ReturnsZero()
        {
            var ctx = new EffectContext { DiceResult = new[] { 6 } };
            Assert.AreEqual(0, new ReadCarrierRollDelta { Op = CarrierRollDeltaOp.Exclude }.Read(ctx));
        }
    }
}
