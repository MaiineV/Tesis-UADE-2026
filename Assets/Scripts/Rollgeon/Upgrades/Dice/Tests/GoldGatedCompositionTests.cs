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

namespace Rollgeon.Upgrades.Dice.Tests
{
    /// <summary>
    /// Oráculo de los encantamientos gold-gated migrados (Sediento / SpendForBonus /
    /// BlockBelowGold legacy): PcGoldCompare + EffModifyGold inmediato, con los valores
    /// observables que el legacy producía (verificados con ambas implementaciones vivas
    /// antes del borrado — Feature#0035). El orden de los grupos ES la semántica.
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

        private (int gold, bool block, int bonus) Run(ExecuteEffectsOnDiceEvent bridge, int startingGold)
        {
            _economy.ResetTo(startingGold);
            var ctx = BuildCtx();
            bridge.OnComboMatched(ctx);
            return (_economy.CurrentGold, ctx.Scratch.BlockComboDamage, ctx.Scratch.BonusComboDamage);
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
        // "Sediento": paga el costo o bloquea el combo
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
        public void Sediento_WithEnoughGold_SpendsWithoutBlocking()
        {
            var result = Run(SedientoComposition(3), startingGold: 10);

            Assert.AreEqual((7, false, 0), result);
        }

        [Test]
        public void Sediento_WithoutGold_BlocksWithoutSpending()
        {
            var result = Run(SedientoComposition(3), startingGold: 2);

            Assert.AreEqual((2, true, 0), result);
        }

        // ================================================================
        // "Pagá y pegá más fuerte": Spend + bonus con atomicidad por cortocircuito
        // ================================================================

        private static ExecuteEffectsOnDiceEvent SpendForBonusComposition(int cost, int bonus)
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
                        },
                        new EffAddComboBonus { Amount = new ReadConstantInt { Value = bonus } }),
                },
            };
        }

        [Test]
        public void SpendForBonus_PaysAndAddsBonus()
        {
            var result = Run(SpendForBonusComposition(3, 5), startingGold: 10);

            Assert.AreEqual((7, false, 5), result);
        }

        [Test]
        public void SpendForBonus_InsufficientGold_NoBonusNoSpend()
        {
            var result = Run(SpendForBonusComposition(3, 5), startingGold: 2);

            Assert.AreEqual((2, false, 0), result);
        }

        // ================================================================
        // "Sin oro no hay daño"
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
        public void BlockBelowGold_BlocksOnlyWhenPoor()
        {
            Assert.AreEqual((0, true, 0), Run(BlockBelowComposition(1), startingGold: 0));
            Assert.AreEqual((5, false, 0), Run(BlockBelowComposition(1), startingGold: 5));
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
