using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combos;
using Rollgeon.Dice;
using Rollgeon.Economy;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Readers;
using Rollgeon.PreConditions.Concretes;
using Rollgeon.Upgrades.Dice.Triggers;
using Rollgeon.Upgrades.Dice.Triggers.Concretes;

namespace Rollgeon.Upgrades.Dice.Tests
{
    /// <summary>
    /// Paridad OBSERVABLE de los triggers gold-gated: legacy (scratch diferido +
    /// applier) vs composición (PcGoldCompare + EffModifyGold inmediato). Se compara
    /// el oro final y el flag de block — el "gold efectivo" legacy y el apply
    /// inmediato nuevo coinciden mientras ningún trigger sume y gaste oro en el
    /// mismo evento (ningún asset lo hace — documentado en el plan §B4).
    /// </summary>
    [TestFixture]
    public class GoldGatedCompositionTests
    {
        private FakeEconomy _economy;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _economy = new FakeEconomy(0);
            ServiceLocator.AddService<IEconomyService>(_economy, ServiceScope.Global);
        }

        [TearDown]
        public void TearDown() => ServiceLocator.Clear();

        // ================================================================
        // Harness
        // ================================================================

        private static EnchantmentTriggerContext BuildCtx()
        {
            return new EnchantmentTriggerContext
            {
                Effect = new EffectContext
                {
                    SourceGuid = Guid.NewGuid(),
                    DiceResult = new[] { 2, 2, 5 },
                    ComboResult = ComboDetectionResult.Match("combo.par", 10, 2, new[] { 0 }),
                },
                Scratch = new EnchantmentScratch(),
                Slot = new EnchantmentSlotRef(DiceType.D6, 0, 0),
                ComboId = "combo.par",
            };
        }

        /// <summary>Camino legacy completo: dispatch + applier (como hace el service).</summary>
        private (int gold, bool block) RunLegacy(IOnComboMatchedTrigger legacy, int startingGold)
        {
            _economy.ResetTo(startingGold);
            var ctx = BuildCtx();
            legacy.OnComboMatched(ctx);
            EnchantmentScratchApplier.Apply(ctx.Scratch, ctx.Effect.SourceGuid);
            return (_economy.CurrentGold, ctx.Scratch.BlockComboDamage);
        }

        /// <summary>Camino nuevo: bridge con apply inmediato de EffModifyGold.</summary>
        private (int gold, bool block) RunComposed(ExecuteEffectsOnDiceEvent bridge, int startingGold)
        {
            _economy.ResetTo(startingGold);
            var ctx = BuildCtx();
            bridge.OnComboMatched(ctx);
            return (_economy.CurrentGold, ctx.Scratch.BlockComboDamage);
        }

        private static EffectData Group(Rollgeon.PreConditions.BasePreCondition pc, params IEffect[] effects)
        {
            var data = new EffectData();
            if (pc != null) data.PreConditions.Add(pc);
            foreach (var eff in effects) data.Effects.Add(eff);
            return data;
        }

        private static PcGoldCompare GoldPc(IntComparison cmp, int value) =>
            new PcGoldCompare { Comparison = cmp, Value = new ReadConstantInt { Value = value } };

        // ================================================================
        // SpendGoldOnComboParticipation ("Sediento")
        // ================================================================

        private static ExecuteEffectsOnDiceEvent SedientoComposition(int cost)
        {
            return new ExecuteEffectsOnDiceEvent
            {
                Event = EnchantmentHookEvent.ComboMatched,
                Effects = new List<EffectData>
                {
                    Group(GoldPc(IntComparison.GreaterOrEqual, cost),
                        new EffModifyGold
                        {
                            Operation = GoldOperation.Spend,
                            Amount = new ReadConstantInt { Value = cost },
                        }),
                    Group(GoldPc(IntComparison.Less, cost), new EffBlockComboDamage()),
                },
            };
        }

        [Test]
        public void Parity_Sediento_WithEnoughGold_SpendsWithoutBlocking()
        {
            var legacy = new SpendGoldOnComboParticipation { Cost = new ReadConstantInt { Value = 3 } };

            var legacyResult = RunLegacy(legacy, startingGold: 10);
            var composedResult = RunComposed(SedientoComposition(3), startingGold: 10);

            Assert.AreEqual(legacyResult, composedResult);
            Assert.AreEqual((7, false), composedResult);
        }

        [Test]
        public void Parity_Sediento_WithoutGold_BlocksWithoutSpending()
        {
            var legacy = new SpendGoldOnComboParticipation { Cost = new ReadConstantInt { Value = 3 } };

            var legacyResult = RunLegacy(legacy, startingGold: 2);
            var composedResult = RunComposed(SedientoComposition(3), startingGold: 2);

            Assert.AreEqual(legacyResult, composedResult);
            Assert.AreEqual((2, true), composedResult);
        }

        // ================================================================
        // SpendGoldForComboBonus ("pagá y pegá más fuerte")
        // ================================================================

        private static ExecuteEffectsOnDiceEvent SpendForBonusComposition(int cost, int bonus)
        {
            // Cortocircuito de EffectData: si el Spend falla, el bonus no corre.
            return new ExecuteEffectsOnDiceEvent
            {
                Event = EnchantmentHookEvent.ComboMatched,
                Effects = new List<EffectData>
                {
                    Group(GoldPc(IntComparison.GreaterOrEqual, cost),
                        new EffModifyGold
                        {
                            Operation = GoldOperation.Spend,
                            Amount = new ReadConstantInt { Value = cost },
                        },
                        new EffAddComboBonus { Amount = new ReadConstantInt { Value = bonus } }),
                },
            };
        }

        [Test]
        public void Parity_SpendForBonus_PaysAndAddsBonus()
        {
            var legacy = new SpendGoldForComboBonus
            {
                Cost = new ReadConstantInt { Value = 3 },
                Bonus = new ReadConstantInt { Value = 5 },
            };

            _economy.ResetTo(10);
            var legacyCtx = BuildCtx();
            legacy.OnComboMatched(legacyCtx);
            EnchantmentScratchApplier.Apply(legacyCtx.Scratch, legacyCtx.Effect.SourceGuid);
            int legacyGold = _economy.CurrentGold;

            _economy.ResetTo(10);
            var composedCtx = BuildCtx();
            SpendForBonusComposition(3, 5).OnComboMatched(composedCtx);

            Assert.AreEqual(legacyGold, _economy.CurrentGold);
            Assert.AreEqual(legacyCtx.Scratch.BonusComboDamage, composedCtx.Scratch.BonusComboDamage);
            Assert.AreEqual(7, _economy.CurrentGold);
            Assert.AreEqual(5, composedCtx.Scratch.BonusComboDamage);
        }

        [Test]
        public void Parity_SpendForBonus_InsufficientGold_NoBonusNoSpend()
        {
            var legacy = new SpendGoldForComboBonus
            {
                Cost = new ReadConstantInt { Value = 3 },
                Bonus = new ReadConstantInt { Value = 5 },
            };

            _economy.ResetTo(2);
            var legacyCtx = BuildCtx();
            legacy.OnComboMatched(legacyCtx);
            EnchantmentScratchApplier.Apply(legacyCtx.Scratch, legacyCtx.Effect.SourceGuid);
            int legacyGold = _economy.CurrentGold;
            int legacyBonus = legacyCtx.Scratch.BonusComboDamage;

            _economy.ResetTo(2);
            var composedCtx = BuildCtx();
            SpendForBonusComposition(3, 5).OnComboMatched(composedCtx);

            Assert.AreEqual(legacyGold, _economy.CurrentGold);
            Assert.AreEqual(legacyBonus, composedCtx.Scratch.BonusComboDamage);
            Assert.AreEqual(2, _economy.CurrentGold);
            Assert.AreEqual(0, composedCtx.Scratch.BonusComboDamage);
        }

        // ================================================================
        // BlockComboIfBelowGold ("sin oro no hay daño")
        // ================================================================

        private static ExecuteEffectsOnDiceEvent BlockBelowComposition(int threshold)
        {
            return new ExecuteEffectsOnDiceEvent
            {
                Event = EnchantmentHookEvent.ComboMatched,
                Effects = new List<EffectData>
                {
                    Group(GoldPc(IntComparison.Less, threshold), new EffBlockComboDamage()),
                },
            };
        }

        [Test]
        public void Parity_BlockBelowGold_BlocksOnlyWhenPoor()
        {
            var legacy = new BlockComboIfBelowGold { Threshold = new ReadConstantInt { Value = 1 } };

            Assert.AreEqual(
                RunLegacy(legacy, startingGold: 0),
                RunComposed(BlockBelowComposition(1), startingGold: 0),
                "Sin oro: ambos bloquean.");
            Assert.AreEqual(
                RunLegacy(legacy, startingGold: 5),
                RunComposed(BlockBelowComposition(1), startingGold: 5),
                "Con oro: ninguno bloquea.");
        }

        private sealed class FakeEconomy : IEconomyService
        {
            public FakeEconomy(int gold) { CurrentGold = gold; }
            public int CurrentGold { get; private set; }
            public void Add(int amount) { if (amount > 0) CurrentGold += amount; }
            public bool Spend(int amount)
            {
                if (amount > CurrentGold) return false;
                CurrentGold -= amount;
                return true;
            }
            public bool CanAfford(int amount) => amount <= 0 || CurrentGold >= amount;
            public void ResetTo(int amount) => CurrentGold = amount;
        }
    }
}
