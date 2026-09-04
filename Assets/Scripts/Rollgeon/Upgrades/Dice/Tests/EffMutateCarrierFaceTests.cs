using System;
using NUnit.Framework;
using Rollgeon.Dice;
using Rollgeon.Effects;
using Rollgeon.Effects.Readers;
using Rollgeon.Upgrades.Dice.Effects;
using Rollgeon.Upgrades.Dice.Readers;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rollgeon.Upgrades.Dice.Tests
{
    /// <summary>
    /// <see cref="EffMutateCarrierFace"/> (Fix#0053): la transformación de la cara del
    /// carrier va al canal <c>FaceDeltas</c> del scratch (cara efectiva del dado), no a
    /// <c>BonusComboDamage</c> — así el breakdown muestra al dado valiendo 0 / doble en vez
    /// de "+6" seguido de "−6".
    /// </summary>
    [TestFixture]
    public class EffMutateCarrierFaceTests
    {
        private static EffectContext Ctx(EnchantmentScratch scratch, int face, int carrier = 0, DiceType type = DiceType.D6)
        {
            return new EffectContext
            {
                SourceGuid = Guid.NewGuid(),
                // La cara pedida vive en el índice del carrier; el resto es relleno.
                DiceResult = new[] { face, face, face },
                TriggerContext = new ScratchTriggerContext
                {
                    Scratch = scratch,
                    Slot = new EnchantmentSlotRef(type, carrier, 0),
                    Channel = ScratchChannel.DiceEnchantment,
                },
            };
        }

        [Test]
        public void Exclude_WritesMinusFaceToTheCarrierSlot_NotToBonus()
        {
            var scratch = new EnchantmentScratch();
            var eff = new EffMutateCarrierFace { Delta = new ReadCarrierRollDelta { Op = CarrierRollDeltaOp.Exclude } };

            bool ok = eff.ApplyEffect(Ctx(scratch, face: 5));

            Assert.IsTrue(ok);
            Assert.AreEqual(-5, scratch.GetFaceDelta(0));
            Assert.AreEqual(0, scratch.BonusComboDamage, "la cara no pasa por bono_combo");
            Assert.AreEqual(0, scratch.GetFaceDelta(1), "solo toca al carrier");
        }

        [Test]
        public void Double_StacksWithOtherMutatorsOnTheSameSlot()
        {
            var scratch = new EnchantmentScratch();
            var twice = new EffMutateCarrierFace { Delta = new ReadCarrierRollDelta { Op = CarrierRollDeltaOp.Double } };
            var plusOne = new EffMutateCarrierFace { Delta = new ReadConstantInt { Value = 1 } };

            twice.ApplyEffect(Ctx(scratch, face: 4));
            plusOne.ApplyEffect(Ctx(scratch, face: 4));

            Assert.AreEqual(5, scratch.GetFaceDelta(0));
        }

        [Test]
        public void IsAScratchWriter_SoItIsAllowedInComboMatchedPreview()
        {
            // La auditoría BUG-017 solo deja IComboScratchWriter en ComboMatched.
            Assert.IsInstanceOf<IComboScratchWriter>(new EffMutateCarrierFace());
        }

        [Test]
        public void WithoutScratchTriggerContext_FailsWithoutThrowing()
        {
            var eff = new EffMutateCarrierFace();
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("EffMutateCarrierFace"));

            Assert.IsFalse(eff.ApplyEffect(new EffectContext { DiceResult = new[] { 3 } }));
        }

        [Test]
        public void Journal_AttributesTheFaceDeltaToTheSource()
        {
            // El dispatch del canal dados fotografía el slot antes/después de cada encantamiento.
            var scratch = new EnchantmentScratch();
            var before = ScratchSnapshot.Of(scratch, bagSlot: 2);
            new EffMutateCarrierFace { Delta = new ReadCarrierRollDelta { Op = CarrierRollDeltaOp.Exclude } }
                .ApplyEffect(Ctx(scratch, face: 6, carrier: 2));

            ScratchSnapshot.RecordDelta(scratch, in before, ScratchSourceKind.Enchantment, "ench.oxidado", null, bagSlot: 2);

            Assert.IsNotNull(scratch.Journal);
            Assert.AreEqual(1, scratch.Journal.Count);
            Assert.AreEqual(-6, scratch.Journal[0].FaceDelta);
            Assert.AreEqual(0, scratch.Journal[0].BonusDelta);
            Assert.AreEqual(2, scratch.Journal[0].BagSlot);
        }

        [Test]
        public void Reset_ClearsFaceDeltas()
        {
            var scratch = new EnchantmentScratch();
            scratch.AddFaceDelta(0, -3);

            scratch.Reset();

            Assert.AreEqual(0, scratch.GetFaceDelta(0));
            Assert.IsTrue(scratch.FaceDeltas == null || scratch.FaceDeltas.Count == 0);
        }
    }
}
