using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Combos;
using Rollgeon.Dice;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Readers;
using Rollgeon.Upgrades.Dice.PreConditions;
using Rollgeon.Upgrades.Dice.Readers;
using Rollgeon.Upgrades.Dice.Triggers;
using Rollgeon.Upgrades.Dice.Triggers.Concretes;

namespace Rollgeon.Upgrades.Dice.Tests
{
    /// <summary>
    /// Tests de PARIDAD: cada trigger legacy de scratch-math contra su composición
    /// Eff/PC equivalente, ejecutados sobre contextos idénticos — el scratch resultante
    /// debe ser igual. Son el oráculo de la migración de assets (Etapa 3); cuando los
    /// legacy se borren (Etapa 4), el lado legacy se reemplaza por el valor literal.
    /// </summary>
    [TestFixture]
    public class ComboBonusCompositionParityTests
    {
        // ================================================================
        // Harness
        // ================================================================

        private static EnchantmentTriggerContext BuildCtx(
            string comboId,
            int[] faces,
            int carrierIndex = 0)
        {
            return new EnchantmentTriggerContext
            {
                Effect = new EffectContext
                {
                    SourceGuid = Guid.NewGuid(),
                    DiceResult = faces,
                    ComboResult = comboId != null
                        ? ComboDetectionResult.Match(comboId, baseDamage: 10, countUsed: 2,
                            contributingIndices: new[] { 0 })
                        : (ComboDetectionResult?)null,
                },
                Scratch = new EnchantmentScratch(),
                Slot = new EnchantmentSlotRef(DiceType.D6, carrierIndex, 0),
                ComboId = comboId,
            };
        }

        private static EffectData Group(IEffect effect, params Rollgeon.PreConditions.BasePreCondition[] pcs)
        {
            var data = new EffectData();
            foreach (var pc in pcs) data.PreConditions.Add(pc);
            data.Effects.Add(effect);
            return data;
        }

        private static ExecuteEffectsOnDiceEvent Bridge(EnchantmentHookEvent evt, params EffectData[] groups)
        {
            return new ExecuteEffectsOnDiceEvent
            {
                Event = evt,
                Effects = new List<EffectData>(groups),
            };
        }

        /// <summary>Dispatcha legacy y composición sobre contextos gemelos y compara el scratch.</summary>
        private static void AssertScratchParity(
            Action<EnchantmentTriggerContext> legacyDispatch,
            Action<EnchantmentTriggerContext> composedDispatch,
            Func<EnchantmentTriggerContext> ctxFactory,
            string caseName)
        {
            var legacyCtx = ctxFactory();
            var composedCtx = ctxFactory();

            legacyDispatch(legacyCtx);
            composedDispatch(composedCtx);

            Assert.AreEqual(legacyCtx.Scratch.BonusComboDamage, composedCtx.Scratch.BonusComboDamage,
                $"{caseName}: BonusComboDamage difiere entre legacy y composición.");
            Assert.AreEqual(legacyCtx.Scratch.ComboDamageMultiplier, composedCtx.Scratch.ComboDamageMultiplier, 0.0001f,
                $"{caseName}: ComboDamageMultiplier difiere.");
            Assert.AreEqual(legacyCtx.Scratch.BlockComboDamage, composedCtx.Scratch.BlockComboDamage,
                $"{caseName}: BlockComboDamage difiere.");
        }

        // ================================================================
        // AddComboDamage → EEODE(ComboMatched, Filter) + EffAddComboBonus
        // ================================================================

        [Test]
        public void Parity_AddComboDamage_WithComboIdRestriction()
        {
            var legacy = new AddComboDamage
            {
                Bonus = new ReadConstantInt { Value = 5 },
                RestrictToComboIds = new List<string> { "combo.par" },
            };
            var composed = Bridge(EnchantmentHookEvent.ComboMatched,
                Group(new EffAddComboBonus { Amount = new ReadConstantInt { Value = 5 } }));
            composed.Filter = new ComboFilter
            {
                Mode = ComboFilterMode.ComboIds,
                ComboIds = new List<string> { "combo.par" },
            };

            AssertScratchParity(legacy.OnComboMatched, composed.OnComboMatched,
                () => BuildCtx("combo.par", new[] { 2, 2, 5 }), "AddComboDamage/par");
            AssertScratchParity(legacy.OnComboMatched, composed.OnComboMatched,
                () => BuildCtx("combo.trio", new[] { 3, 3, 3 }), "AddComboDamage/trio (filtrado)");
        }

        // ================================================================
        // Deltas de roll (aprox MVP) → EffAddComboBonus(ReadCarrierRollDelta | const)
        // ================================================================

        [Test]
        public void Parity_AddFlatToResult_ConstantBonus()
        {
            var legacy = new AddFlatToResult { Bonus = new ReadConstantInt { Value = 2 } };
            var composed = Bridge(EnchantmentHookEvent.RollResolved,
                Group(new EffAddComboBonus { Amount = new ReadConstantInt { Value = 2 } }));

            AssertScratchParity(legacy.OnRollResolved, composed.OnRollResolved,
                () => BuildCtx(null, new[] { 4 }), "AddFlatToResult");
        }

        [Test]
        public void Parity_SubtractFromResult_NegativeConstant()
        {
            var legacy = new SubtractFromResult { Amount = new ReadConstantInt { Value = 2 } };
            var composed = Bridge(EnchantmentHookEvent.RollResolved,
                Group(new EffAddComboBonus { Amount = new ReadConstantInt { Value = -2 } }));

            AssertScratchParity(legacy.OnRollResolved, composed.OnRollResolved,
                () => BuildCtx(null, new[] { 4 }), "SubtractFromResult");
        }

        [Test]
        public void Parity_InvertResult_DeltaReader()
        {
            var legacy = new InvertResult();
            var composed = Bridge(EnchantmentHookEvent.RollResolved,
                Group(new EffAddComboBonus
                {
                    Amount = new ReadCarrierRollDelta { Op = CarrierRollDeltaOp.Invert },
                }));

            // D6 cara 2 → +3; cara 5 → -3.
            AssertScratchParity(legacy.OnRollResolved, composed.OnRollResolved,
                () => BuildCtx(null, new[] { 2 }), "Invert/2");
            AssertScratchParity(legacy.OnRollResolved, composed.OnRollResolved,
                () => BuildCtx(null, new[] { 5 }), "Invert/5");
        }

        [Test]
        public void Parity_ClampMinToHalfMax_DeltaReader()
        {
            var legacy = new ClampMinToHalfMax();
            var composed = Bridge(EnchantmentHookEvent.RollResolved,
                Group(new EffAddComboBonus
                {
                    Amount = new ReadCarrierRollDelta { Op = CarrierRollDeltaOp.ClampMinToHalfMax },
                }));

            // D6 mínimo 3: cara 2 → +1; cara 4 → 0.
            AssertScratchParity(legacy.OnRollResolved, composed.OnRollResolved,
                () => BuildCtx(null, new[] { 2 }), "Clamp/2");
            AssertScratchParity(legacy.OnRollResolved, composed.OnRollResolved,
                () => BuildCtx(null, new[] { 4 }), "Clamp/4");
        }

        [Test]
        public void Parity_DoubleMaxZeroMin_DeltaReader()
        {
            var legacy = new DoubleMaxZeroMin();
            var composed = Bridge(EnchantmentHookEvent.RollResolved,
                Group(new EffAddComboBonus
                {
                    Amount = new ReadCarrierRollDelta { Op = CarrierRollDeltaOp.DoubleMaxZeroMin },
                }));

            AssertScratchParity(legacy.OnRollResolved, composed.OnRollResolved,
                () => BuildCtx(null, new[] { 6 }), "DoubleMaxZeroMin/6");
            AssertScratchParity(legacy.OnRollResolved, composed.OnRollResolved,
                () => BuildCtx(null, new[] { 1 }), "DoubleMaxZeroMin/1");
            AssertScratchParity(legacy.OnRollResolved, composed.OnRollResolved,
                () => BuildCtx(null, new[] { 3 }), "DoubleMaxZeroMin/3");
        }

        // ================================================================
        // Face-conditional → PcCarrierFace + Eff*
        // ================================================================

        [Test]
        public void Parity_TwinBonus_HasDuplicateMultiplier()
        {
            var legacy = new TwinBonus { BonusMultiplier = 1.5f };
            var composed = Bridge(EnchantmentHookEvent.ComboMatched,
                Group(new EffMultiplyComboDamage { Multiplier = 1.5f },
                    new PcCarrierFace { Mode = CarrierFaceMode.HasDuplicate }));

            AssertScratchParity(legacy.OnComboMatched, composed.OnComboMatched,
                () => BuildCtx("combo.par", new[] { 3, 3, 5 }), "TwinBonus/gemelo");
            AssertScratchParity(legacy.OnComboMatched, composed.OnComboMatched,
                () => BuildCtx("combo.par", new[] { 3, 4, 5 }), "TwinBonus/sin gemelo");
        }

        [Test]
        public void Parity_ResonantDoubleCount_CarrierFaceBonus()
        {
            var legacy = new ResonantDoubleCount();
            var composed = Bridge(EnchantmentHookEvent.ComboMatched,
                Group(new EffAddComboBonus { Amount = new ReadCarrierFace() },
                    new PcCarrierFace { Mode = CarrierFaceMode.HasDuplicate }));

            AssertScratchParity(legacy.OnComboMatched, composed.OnComboMatched,
                () => BuildCtx("combo.par", new[] { 4, 4, 2 }), "Resonante/duplicado");
            AssertScratchParity(legacy.OnComboMatched, composed.OnComboMatched,
                () => BuildCtx("combo.par", new[] { 4, 5, 2 }), "Resonante/sin duplicado");
        }

        [Test]
        public void Parity_ParityScoreMultiplier_TwoGatedGroups()
        {
            var legacy = new ParityScoreMultiplier { MultiplierOdd = 3f, MultiplierEven = 0f };
            var composed = Bridge(EnchantmentHookEvent.ComboMatched,
                Group(new EffMultiplyComboDamage { Multiplier = 3f },
                    new PcCarrierFace { Mode = CarrierFaceMode.Odd }),
                Group(new EffMultiplyComboDamage { Multiplier = 0f },
                    new PcCarrierFace { Mode = CarrierFaceMode.Even }));

            AssertScratchParity(legacy.OnComboMatched, composed.OnComboMatched,
                () => BuildCtx("combo.par", new[] { 3 }), "ParityGamble/impar");
            AssertScratchParity(legacy.OnComboMatched, composed.OnComboMatched,
                () => BuildCtx("combo.par", new[] { 4 }), "ParityGamble/par");
        }
    }
}
