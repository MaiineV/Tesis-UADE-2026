using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combos;
using Rollgeon.Dice;
using Rollgeon.Effects;
using Rollgeon.PreConditions;
using Rollgeon.PreConditions.Concretes;
using Rollgeon.Upgrades.Dice.Effects;
using Rollgeon.Upgrades.Dice.PreConditions;
using Rollgeon.Upgrades.Dice.Readers;

namespace Rollgeon.Upgrades.Dice.Tests
{
    /// <summary>
    /// Tests de las piezas carrier-aware de la migración: <see cref="PcCarrierFace"/>,
    /// <see cref="ReadCarrierFace"/>, <see cref="ReadCarrierRollDelta"/>,
    /// <see cref="PcSlotCounterCompare"/>, <see cref="EffSlotCounter"/> y
    /// <see cref="EffRemoveEnchantment"/>.
    /// </summary>
    [TestFixture]
    public class CarrierPieceTests
    {
        [SetUp]
        public void SetUp() => ServiceLocator.Clear();

        [TearDown]
        public void TearDown() => ServiceLocator.Clear();

        private static EffectContext BuildCarrierContext(int[] faces, int carrierIndex,
            DiceType type = DiceType.D6)
        {
            return new EffectContext
            {
                DiceResult = faces,
                TriggerContext = new ScratchTriggerContext
                {
                    Scratch = new EnchantmentScratch(),
                    Slot = new EnchantmentSlotRef(type, carrierIndex, 0),
                    Channel = ScratchChannel.DiceEnchantment,
                },
            };
        }

        /// <summary>
        /// Variante con datos de combo (hook ComboMatched/ComboPlayed) — <paramref name="contributingIndices"/>
        /// son bag slots directos (identidad, <c>keptDiceOriginalIndices</c> null), mismo
        /// encoding que <c>DiceEnchantmentService.OnComboMatchedHandler</c> arma para el
        /// canal dados.
        /// </summary>
        private static EffectContext BuildComboCarrierContext(
            int[] faces, int carrierIndex, int[] contributingIndices, DiceType type = DiceType.D6)
        {
            var ctx = BuildCarrierContext(faces, carrierIndex, type);
            ctx.ComboResult = ComboDetectionResult.Match("combo.par", baseDamage: 10,
                countUsed: contributingIndices.Length, contributingIndices: contributingIndices);
            return ctx;
        }

        private static PreConditionContext WrapPre(EffectContext eff) =>
            new PreConditionContext { Effect = eff };

        // ================================================================
        // PcCarrierFace
        // ================================================================

        [Test]
        public void PcCarrierFace_OnMaxFace_TrueOnlyOnMax()
        {
            var pc = new PcCarrierFace { Mode = CarrierFaceMode.OnMaxFace };

            Assert.IsTrue(pc.Evaluate(WrapPre(BuildCarrierContext(new[] { 6, 2, 3 }, 0))));
            Assert.IsFalse(pc.Evaluate(WrapPre(BuildCarrierContext(new[] { 5, 2, 3 }, 0))));
        }

        [Test]
        public void PcCarrierFace_Parity_MatchesLegacyPredicate()
        {
            var even = new PcCarrierFace { Mode = CarrierFaceMode.Even };
            var odd = new PcCarrierFace { Mode = CarrierFaceMode.Odd };
            var ctx = WrapPre(BuildCarrierContext(new[] { 4, 3, 1 }, 0));

            Assert.IsTrue(even.Evaluate(ctx));
            Assert.IsFalse(odd.Evaluate(ctx));
        }

        [Test]
        public void PcCarrierFace_HasDuplicate_RequiresAnotherDieWithSameFace()
        {
            var pc = new PcCarrierFace { Mode = CarrierFaceMode.HasDuplicate };

            Assert.IsTrue(pc.Evaluate(WrapPre(BuildCarrierContext(new[] { 3, 3, 5 }, 0))),
                "Otro dado comparte la cara del carrier.");
            Assert.IsFalse(pc.Evaluate(WrapPre(BuildCarrierContext(new[] { 3, 4, 5 }, 0))),
                "Sin gemelo no pasa.");
        }

        // ================================================================
        // PcCarrierFace.HasDuplicate — carrier-scope (bug fix)
        // ================================================================

        [Test]
        public void PcCarrierFace_HasDuplicate_ComboContext_PairOnlyOutsideContributors_ReturnsFalse()
        {
            // Tirada [3,3,5] — hay un par de 3s, pero el combo (bag slots 1,2 → caras 3,5)
            // no incluye al carrier (bag slot 0) NI forma el par entre sí. Antes del fix,
            // HasDuplicate escaneaba TODA la tirada y esto disparaba en falso.
            var pc = new PcCarrierFace { Mode = CarrierFaceMode.HasDuplicate };
            var ctx = BuildComboCarrierContext(
                faces: new[] { 3, 3, 5 }, carrierIndex: 0, contributingIndices: new[] { 1, 2 });

            Assert.IsFalse(pc.Evaluate(WrapPre(ctx)),
                "El carrier no participó del combo — el par entre los otros dos dados no debe contar.");
        }

        [Test]
        public void PcCarrierFace_HasDuplicate_ComboContext_PairAmongContributors_ReturnsTrue()
        {
            // Tirada [3,3,5] — el combo usa los bag slots 0 y 1 (los dos 3s) y el carrier
            // es el slot 0: participa Y tiene un gemelo dentro del subset contribuyente.
            var pc = new PcCarrierFace { Mode = CarrierFaceMode.HasDuplicate };
            var ctx = BuildComboCarrierContext(
                faces: new[] { 3, 3, 5 }, carrierIndex: 0, contributingIndices: new[] { 0, 1 });

            Assert.IsTrue(pc.Evaluate(WrapPre(ctx)));
        }

        [Test]
        public void PcCarrierFace_HasDuplicate_ComboContext_CarrierOutsideContributors_ReturnsFalse()
        {
            // Combo = bag slots 1,2 (caras 3,3 — sí forman gemelo entre ellos), pero el
            // carrier vive en el slot 0 y no es parte del combo: no dispara aunque su
            // propia cara (3) también coincida con la de los contribuyentes.
            var pc = new PcCarrierFace { Mode = CarrierFaceMode.HasDuplicate };
            var ctx = BuildComboCarrierContext(
                faces: new[] { 3, 3, 3 }, carrierIndex: 0, contributingIndices: new[] { 1, 2 });

            Assert.IsFalse(pc.Evaluate(WrapPre(ctx)),
                "El carrier debe estar ENTRE los contribuyentes, no solo compartir la cara.");
        }

        [Test]
        public void PcCarrierFace_HasDuplicate_WithoutComboContext_FallsBackToFullRoll()
        {
            // Regresión: hooks sin noción de combo (RollResolved/DiceRolled/TurnFinished)
            // no setean ComboResult — debe preservarse el comportamiento legado (toda la tirada).
            var pc = new PcCarrierFace { Mode = CarrierFaceMode.HasDuplicate };

            Assert.IsTrue(pc.Evaluate(WrapPre(BuildCarrierContext(new[] { 3, 3, 5 }, 0))));
        }

        [Test]
        public void PcCarrierFace_WithoutCarrierContext_ReturnsFalse()
        {
            // Gate inevaluable → veto (paridad con el early-return legacy).
            var pc = new PcCarrierFace { Mode = CarrierFaceMode.Even };

            Assert.IsFalse(pc.Evaluate(new PreConditionContext { Effect = new EffectContext() }));
            Assert.IsFalse(pc.Evaluate(null));
        }

        // ================================================================
        // ReadCarrierFace / ReadCarrierRollDelta
        // ================================================================

        [Test]
        public void ReadCarrierFace_ReturnsCarrierDieFace()
        {
            var reader = new ReadCarrierFace();

            Assert.AreEqual(5, reader.Read(BuildCarrierContext(new[] { 2, 5, 3 }, 1)));
            Assert.AreEqual(0, reader.Read(new EffectContext { DiceResult = new[] { 2 } }),
                "Sin trigger context devuelve 0.");
        }

        [Test]
        public void ReadCarrierRollDelta_Invert_MatchesLegacyMath()
        {
            var reader = new ReadCarrierRollDelta { Op = CarrierRollDeltaOp.Invert };

            // D6: cara 2 → invertida 5 → delta +3; cara 5 → invertida 2 → delta -3.
            Assert.AreEqual(3, reader.Read(BuildCarrierContext(new[] { 2 }, 0)));
            Assert.AreEqual(-3, reader.Read(BuildCarrierContext(new[] { 5 }, 0)));
        }

        [Test]
        public void ReadCarrierRollDelta_ClampMinToHalfMax_MatchesLegacyMath()
        {
            var reader = new ReadCarrierRollDelta { Op = CarrierRollDeltaOp.ClampMinToHalfMax };

            // D6: mínimo = (6+1)/2 = 3. Cara 2 → +1; cara 4 → 0.
            Assert.AreEqual(1, reader.Read(BuildCarrierContext(new[] { 2 }, 0)));
            Assert.AreEqual(0, reader.Read(BuildCarrierContext(new[] { 4 }, 0)));
        }

        [Test]
        public void ReadCarrierRollDelta_DoubleMaxZeroMin_MatchesLegacyMath()
        {
            var reader = new ReadCarrierRollDelta { Op = CarrierRollDeltaOp.DoubleMaxZeroMin };

            // D6: cara 6 → +6 (duplica); cara 1 → -1 (anula); cara 3 → 0.
            Assert.AreEqual(6, reader.Read(BuildCarrierContext(new[] { 6 }, 0)));
            Assert.AreEqual(-1, reader.Read(BuildCarrierContext(new[] { 1 }, 0)));
            Assert.AreEqual(0, reader.Read(BuildCarrierContext(new[] { 3 }, 0)));
        }

        // ================================================================
        // Counters per-slot + self-remove
        // ================================================================

        [Test]
        public void EffSlotCounter_IncrementAndReset_AgainstRuntime()
        {
            var runtime = new FakeRuntime();
            ServiceLocator.AddService<IDiceEnchantmentRuntime>(runtime, ServiceScope.Global);
            var ctx = BuildCarrierContext(new[] { 2 }, 0);

            new EffSlotCounter { Operation = SlotCounterOperation.Increment, Key = "k" }.Apply(ctx);
            ctx.lastResult = true;
            new EffSlotCounter { Operation = SlotCounterOperation.Increment, Key = "k" }.Apply(ctx);

            Assert.AreEqual(2, runtime.GetCounter(default, "k"));

            ctx.lastResult = true;
            new EffSlotCounter { Operation = SlotCounterOperation.Reset, Key = "k" }.Apply(ctx);
            Assert.AreEqual(0, runtime.GetCounter(default, "k"));
        }

        [Test]
        public void PcSlotCounterCompare_GatesOnRuntimeCounter()
        {
            var runtime = new FakeRuntime();
            ServiceLocator.AddService<IDiceEnchantmentRuntime>(runtime, ServiceScope.Global);
            runtime.IncrementCounter(default, "k", 3);
            var pc = new PcSlotCounterCompare { Key = "k", Comparison = IntComparison.GreaterOrEqual, Value = 3 };

            Assert.IsTrue(pc.Evaluate(WrapPre(BuildCarrierContext(new[] { 2 }, 0))));

            runtime.ResetCounter(default, "k");
            Assert.IsFalse(pc.Evaluate(WrapPre(BuildCarrierContext(new[] { 2 }, 0))));
        }

        [Test]
        public void EffRemoveEnchantment_RemovesCarrierSlot()
        {
            var runtime = new FakeRuntime();
            ServiceLocator.AddService<IDiceEnchantmentRuntime>(runtime, ServiceScope.Global);
            var ctx = BuildCarrierContext(new[] { 2 }, carrierIndex: 1);

            var result = new EffRemoveEnchantment().Apply(ctx);

            Assert.IsTrue(result);
            Assert.AreEqual(1, runtime.Removed.Count);
            Assert.AreEqual(1, runtime.Removed[0].BagSlotIndex);
        }

        /// <summary>Counters keyed solo por key (los tests usan un único slot).</summary>
        private sealed class FakeRuntime : IDiceEnchantmentRuntime
        {
            private readonly Dictionary<string, int> _counters = new Dictionary<string, int>();
            public readonly List<EnchantmentSlotRef> Removed = new List<EnchantmentSlotRef>();

            public int GetCounter(EnchantmentSlotRef slot, string key)
                => _counters.TryGetValue(key, out var v) ? v : 0;

            public int IncrementCounter(EnchantmentSlotRef slot, string key, int delta = 1)
            {
                _counters[key] = GetCounter(slot, key) + delta;
                return _counters[key];
            }

            public void ResetCounter(EnchantmentSlotRef slot, string key) => _counters[key] = 0;

            public void RemoveEnchantment(EnchantmentSlotRef slot) => Removed.Add(slot);
        }
    }
}
