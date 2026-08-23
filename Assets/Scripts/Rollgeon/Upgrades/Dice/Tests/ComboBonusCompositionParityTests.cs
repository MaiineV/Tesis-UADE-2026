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

namespace Rollgeon.Upgrades.Dice.Tests
{
    /// <summary>
    /// Oráculo de paridad de la migración (Etapa 4): las composiciones que reemplazaron
    /// a los triggers legacy de scratch-math, contra los valores literales que el legacy
    /// producía (verificados con ambas implementaciones vivas antes del borrado —
    /// Feature#0035). Cambiar estas expectativas es cambiar el balance del juego.
    /// Excepción deliberada (23/08): los face-conditional en hooks de combo ahora exigen
    /// que el duplicado esté entre los contribuyentes — el legacy evaluaba la tirada
    /// entera y ese era el bug de "afecta aunque no participe".
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
            int carrierIndex = 0,
            int[] contributingIndices = null)
        {
            return new EnchantmentTriggerContext
            {
                Effect = new EffectContext
                {
                    SourceGuid = Guid.NewGuid(),
                    DiceResult = faces,
                    ComboResult = comboId != null
                        ? ComboDetectionResult.Match(comboId, baseDamage: 10, countUsed: 2,
                            contributingIndices: contributingIndices ?? new[] { 0 })
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

        private static EnchantmentScratch RunComboMatched(ExecuteEffectsOnDiceEvent bridge, string comboId,
            int[] faces, int[] contributingIndices = null)
        {
            var ctx = BuildCtx(comboId, faces, contributingIndices: contributingIndices);
            bridge.OnComboMatched(ctx);
            return ctx.Scratch;
        }

        private static EnchantmentScratch RunRollResolved(ExecuteEffectsOnDiceEvent bridge, int[] faces)
        {
            var ctx = BuildCtx(null, faces);
            bridge.OnRollResolved(ctx);
            return ctx.Scratch;
        }

        // ================================================================
        // AddComboDamage legacy → EEODE(ComboMatched, Filter) + EffAddComboBonus
        // ================================================================

        [Test]
        public void Composition_AddComboBonus_RespectsComboIdRestriction()
        {
            var bridge = Bridge(EnchantmentHookEvent.ComboMatched,
                Group(new EffAddComboBonus { Amount = new ReadConstantInt { Value = 5 } }));
            bridge.Filter = new ComboFilter
            {
                Mode = ComboFilterMode.ComboIds,
                ComboIds = new List<string> { "combo.par" },
            };

            Assert.AreEqual(5, RunComboMatched(bridge, "combo.par", new[] { 2, 2, 5 }).BonusComboDamage);
            Assert.AreEqual(0, RunComboMatched(bridge, "combo.trio", new[] { 3, 3, 3 }).BonusComboDamage);
        }

        // ================================================================
        // Deltas de roll (Pesado / Oxidado / Invertido / Afilado / Volátil legacy)
        // ================================================================

        [Test]
        public void Composition_FlatBonus_AddsConstant()
        {
            // Ench_Pesado: +2 plano en RollResolved.
            var bridge = Bridge(EnchantmentHookEvent.RollResolved,
                Group(new EffAddComboBonus { Amount = new ReadConstantInt { Value = 2 } }));

            Assert.AreEqual(2, RunRollResolved(bridge, new[] { 4 }).BonusComboDamage);
        }

        [Test]
        public void Composition_NegativeConstant_Subtracts()
        {
            // Ench_Oxidado: -2 en RollResolved (constante negada por el migrador).
            var bridge = Bridge(EnchantmentHookEvent.RollResolved,
                Group(new EffAddComboBonus { Amount = new ReadConstantInt { Value = -2 } }));

            Assert.AreEqual(-2, RunRollResolved(bridge, new[] { 4 }).BonusComboDamage);
        }

        [Test]
        public void Composition_InvertDelta_MatchesLegacyMath()
        {
            // Ench_Invertido (D6): cara 2 → +3; cara 5 → -3.
            var bridge = Bridge(EnchantmentHookEvent.RollResolved,
                Group(new EffAddComboBonus
                {
                    Amount = new ReadCarrierRollDelta { Op = CarrierRollDeltaOp.Invert },
                }));

            Assert.AreEqual(3, RunRollResolved(bridge, new[] { 2 }).BonusComboDamage);
            Assert.AreEqual(-3, RunRollResolved(bridge, new[] { 5 }).BonusComboDamage);
        }

        [Test]
        public void Composition_ClampDelta_MatchesLegacyMath()
        {
            // Ench_Afilado (D6, mínimo 3): cara 2 → +1; cara 4 → 0.
            var bridge = Bridge(EnchantmentHookEvent.RollResolved,
                Group(new EffAddComboBonus
                {
                    Amount = new ReadCarrierRollDelta { Op = CarrierRollDeltaOp.ClampMinToHalfMax },
                }));

            Assert.AreEqual(1, RunRollResolved(bridge, new[] { 2 }).BonusComboDamage);
            Assert.AreEqual(0, RunRollResolved(bridge, new[] { 4 }).BonusComboDamage);
        }

        [Test]
        public void Composition_DoubleMaxZeroMinDelta_MatchesLegacyMath()
        {
            // Ench_Volatil (D6): cara 6 → +6; cara 1 → -1; cara 3 → 0.
            var bridge = Bridge(EnchantmentHookEvent.RollResolved,
                Group(new EffAddComboBonus
                {
                    Amount = new ReadCarrierRollDelta { Op = CarrierRollDeltaOp.DoubleMaxZeroMin },
                }));

            Assert.AreEqual(6, RunRollResolved(bridge, new[] { 6 }).BonusComboDamage);
            Assert.AreEqual(-1, RunRollResolved(bridge, new[] { 1 }).BonusComboDamage);
            Assert.AreEqual(0, RunRollResolved(bridge, new[] { 3 }).BonusComboDamage);
        }

        // ================================================================
        // Face-conditional (Gemelo / Resonante / ParityGamble legacy)
        // ================================================================

        [Test]
        public void Composition_TwinMultiplier_OnlyWithDuplicateFace()
        {
            // Ench_Gemelo: ×1.5 si otro dado comparte la cara del carrier. Desde el fix del
            // 23/08 el gemelo tiene que estar entre los CONTRIBUYENTES del combo — un par
            // fuera de la combinación confirmada ya no dispara (ese era el bug).
            var bridge = Bridge(EnchantmentHookEvent.ComboMatched,
                Group(new EffMultiplyComboDamage { Multiplier = 1.5f },
                    new PcCarrierFace { Mode = CarrierFaceMode.HasDuplicate }));

            Assert.AreEqual(1.5f, RunComboMatched(bridge, "combo.par", new[] { 3, 3, 5 },
                contributingIndices: new[] { 0, 1 }).ComboDamageMultiplier, 0.0001f);
            Assert.AreEqual(1f, RunComboMatched(bridge, "combo.par", new[] { 3, 4, 5 },
                contributingIndices: new[] { 0, 1 }).ComboDamageMultiplier, 0.0001f);
            Assert.AreEqual(1f, RunComboMatched(bridge, "combo.par", new[] { 3, 3, 5 },
                contributingIndices: new[] { 0 }).ComboDamageMultiplier, 0.0001f,
                "El gemelo (índice 1) quedó fuera del combo: no multiplica — regresión del bug 23/08.");
        }

        [Test]
        public void Composition_ResonantBonus_AddsCarrierFaceWhenDuplicated()
        {
            // Ench_Resonante: +cara del carrier si está duplicada. Mismo cambio de semántica
            // que Gemelo (23/08): el duplicado tiene que participar del combo.
            var bridge = Bridge(EnchantmentHookEvent.ComboMatched,
                Group(new EffAddComboBonus { Amount = new ReadCarrierFace() },
                    new PcCarrierFace { Mode = CarrierFaceMode.HasDuplicate }));

            Assert.AreEqual(4, RunComboMatched(bridge, "combo.par", new[] { 4, 4, 2 },
                contributingIndices: new[] { 0, 1 }).BonusComboDamage);
            Assert.AreEqual(0, RunComboMatched(bridge, "combo.par", new[] { 4, 5, 2 },
                contributingIndices: new[] { 0, 1 }).BonusComboDamage);
            Assert.AreEqual(0, RunComboMatched(bridge, "combo.par", new[] { 4, 4, 2 },
                contributingIndices: new[] { 0 }).BonusComboDamage,
                "El duplicado (índice 1) quedó fuera del combo: sin bonus — regresión del bug 23/08.");
        }

        [Test]
        public void Composition_ParityGamble_TwoGatedGroups()
        {
            // Ench_ParityGamble: impar → ×3; par → ×0.
            var bridge = Bridge(EnchantmentHookEvent.ComboMatched,
                Group(new EffMultiplyComboDamage { Multiplier = 3f },
                    new PcCarrierFace { Mode = CarrierFaceMode.Odd }),
                Group(new EffMultiplyComboDamage { Multiplier = 0f },
                    new PcCarrierFace { Mode = CarrierFaceMode.Even }));

            Assert.AreEqual(3f, RunComboMatched(bridge, "combo.par", new[] { 3 }).ComboDamageMultiplier, 0.0001f);
            Assert.AreEqual(0f, RunComboMatched(bridge, "combo.par", new[] { 4 }).ComboDamageMultiplier, 0.0001f);
        }
    }
}
