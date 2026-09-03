using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Patterns.Save;
using Rollgeon.Combos;
using Rollgeon.Dice;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.PreConditions;
using Rollgeon.PreConditions.Concretes;
using Rollgeon.Upgrades.Dice.Effects;
using Rollgeon.Upgrades.Dice.PreConditions;
using Rollgeon.Upgrades.Dice.Readers;
using Rollgeon.Upgrades.Dice.Triggers;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Tests
{
    /// <summary>
    /// Feature#0073 — <see cref="PcCarrierParticipates"/> (Solitario / Racha) y la
    /// composición de Frágil: moneda por tirada (DiceRolled → counter) leída en cada
    /// preview (ComboMatched), así el resultado no cambia entre toggles de hold.
    /// </summary>
    [TestFixture]
    public class PcCarrierParticipatesTests
    {
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();
        private DiceEnchantmentService _runtime;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            SaveSystem.ResetForTests();
            _runtime = new DiceEnchantmentService(config: null);
            var bag = ScriptableObject.CreateInstance<DiceBagSO>();
            bag.Dice = new List<DiceType> { DiceType.D6, DiceType.D6 };
            _created.Add(bag);
            _runtime.InitializeFromBag(bag);
            ServiceLocator.AddService<IDiceEnchantmentRuntime>(_runtime, ServiceScope.Global);
        }

        [TearDown]
        public void TearDown()
        {
            PcChance.ResetRandomSource();
            foreach (var obj in _created)
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            _created.Clear();
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            SaveSystem.ResetForTests();
        }

        // ================================================================
        // Harness
        // ================================================================

        private static EnchantmentTriggerContext Ctx(int[] faces, int carrier, int[] contributing, string comboId = "combo.par")
        {
            return new EnchantmentTriggerContext
            {
                Effect = new EffectContext
                {
                    SourceGuid = Guid.NewGuid(),
                    DiceResult = faces,
                    ComboResult = comboId != null
                        ? ComboDetectionResult.Match(comboId, baseDamage: 10, countUsed: contributing?.Length ?? 0,
                            contributingIndices: contributing)
                        : (ComboDetectionResult?)null,
                },
                Scratch = new EnchantmentScratch(),
                Slot = new EnchantmentSlotRef(DiceType.D6, carrier, 0),
                ComboId = comboId,
            };
        }

        private static PreConditionContext Pre(EnchantmentTriggerContext ctx)
        {
            ctx.Effect.TriggerContext = new ScratchTriggerContext
            {
                Scratch = ctx.Scratch,
                ComboId = ctx.ComboId,
                Slot = ctx.Slot,
                Channel = ScratchChannel.DiceEnchantment,
            };
            return new PreConditionContext { OwnerGuid = ctx.Effect.SourceGuid, Effect = ctx.Effect };
        }

        private static EffectData Group(IEffect effect, params BasePreCondition[] pcs)
        {
            var data = new EffectData();
            foreach (var pc in pcs) data.PreConditions.Add(pc);
            data.Effects.Add(effect);
            return data;
        }

        // ================================================================
        // PcCarrierParticipates
        // ================================================================

        [Test]
        public void Participates_WhenCarrierIsAContributingIndex()
        {
            var pc = new PcCarrierParticipates();
            Assert.IsTrue(pc.Evaluate(Pre(Ctx(new[] { 4, 4 }, carrier: 1, contributing: new[] { 0, 1 }))));
            Assert.IsFalse(pc.Evaluate(Pre(Ctx(new[] { 4, 2 }, carrier: 1, contributing: new[] { 0 }))));
        }

        [Test]
        public void Negate_InvertsTheAnswer_ButStaysFalseWithoutCombo()
        {
            var pc = new PcCarrierParticipates { Negate = true };
            Assert.IsTrue(pc.Evaluate(Pre(Ctx(new[] { 4, 2 }, carrier: 1, contributing: new[] { 0 }))),
                "Solitario: el dado quedó afuera del combo");
            Assert.IsFalse(pc.Evaluate(Pre(Ctx(new[] { 4, 4 }, carrier: 1, contributing: new[] { 0, 1 }))));
            // Sin combo resuelto no hay "afuera de un combo": conservador en ambos sentidos.
            Assert.IsFalse(new PcCarrierParticipates().Evaluate(Pre(Ctx(new[] { 4, 2 }, 1, null, comboId: null))));
            Assert.IsFalse(pc.Evaluate(Pre(Ctx(new[] { 4, 2 }, 1, null, comboId: null))));
        }

        [Test]
        public void WithoutTriggerContext_IsFalse()
        {
            var ctx = new PreConditionContext { Effect = new EffectContext { DiceResult = new[] { 1 } } };
            Assert.IsFalse(new PcCarrierParticipates().Evaluate(ctx));
            Assert.IsFalse(new PcCarrierParticipates { Negate = true }.Evaluate(ctx));
        }

        // ================================================================
        // Frágil: la moneda se tira en DiceRolled, el preview solo la lee
        // ================================================================

        private static ExecuteEffectsOnDiceEvent FragilCoin()
        {
            return new ExecuteEffectsOnDiceEvent
            {
                Event = EnchantmentHookEvent.DiceRolled,
                Effects = new List<EffectData>
                {
                    Group(new EffSlotCounter { Operation = SlotCounterOperation.Reset, Key = "fragil" }),
                    Group(new EffSlotCounter { Operation = SlotCounterOperation.Increment, Key = "fragil", Delta = 1 },
                        new PcChance { Mode = ChanceMode.Percent01, Chance = 0.5f }),
                },
            };
        }

        private static ExecuteEffectsOnDiceEvent FragilPreview()
        {
            return new ExecuteEffectsOnDiceEvent
            {
                Event = EnchantmentHookEvent.ComboMatched,
                RequireCarrierParticipates = true,
                Effects = new List<EffectData>
                {
                    Group(new EffAddComboBonus { Amount = new ReadCarrierRollDelta { Op = CarrierRollDeltaOp.Double } },
                        new PcSlotCounterCompare { Key = "fragil", Comparison = IntComparison.GreaterOrEqual, Value = 1 }),
                    Group(new EffAddComboBonus { Amount = new ReadCarrierRollDelta { Op = CarrierRollDeltaOp.Exclude } },
                        new PcSlotCounterCompare { Key = "fragil", Comparison = IntComparison.Less, Value = 1 }),
                },
            };
        }

        [TestCase(0.1f, 4)]    // cara: el dado vale el doble → +cara
        [TestCase(0.9f, -4)]   // cruz: el dado no cuenta → -cara
        public void Fragil_CoinIsStableAcrossPreviews_UntilNextRoll(float roll, int expectedDelta)
        {
            PcChance.RandomSource = () => roll;
            var coin = FragilCoin();
            var preview = FragilPreview();
            var faces = new[] { 4, 4 };

            coin.OnDiceRolled(Ctx(faces, carrier: 0, contributing: null, comboId: null));

            // Dos previews (dos toggles de hold) con la misma tirada: mismo resultado.
            var first = Ctx(faces, 0, new[] { 0, 1 });
            preview.OnComboMatched(first);
            var second = Ctx(faces, 0, new[] { 0, 1 });
            preview.OnComboMatched(second);

            Assert.AreEqual(expectedDelta, first.Scratch.BonusComboDamage);
            Assert.AreEqual(expectedDelta, second.Scratch.BonusComboDamage);
        }

        [Test]
        public void Fragil_NewRoll_ReflipsTheCoin()
        {
            var coin = FragilCoin();
            var preview = FragilPreview();
            var faces = new[] { 4, 4 };

            PcChance.RandomSource = () => 0.1f;
            coin.OnDiceRolled(Ctx(faces, 0, null, comboId: null));
            var a = Ctx(faces, 0, new[] { 0, 1 });
            preview.OnComboMatched(a);

            PcChance.RandomSource = () => 0.9f;
            coin.OnDiceRolled(Ctx(faces, 0, null, comboId: null));
            var b = Ctx(faces, 0, new[] { 0, 1 });
            preview.OnComboMatched(b);

            Assert.AreEqual(4, a.Scratch.BonusComboDamage);
            Assert.AreEqual(-4, b.Scratch.BonusComboDamage);
        }
    }
}
