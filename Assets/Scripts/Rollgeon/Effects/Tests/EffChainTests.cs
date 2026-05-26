using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Effects.Concretes;
using Rollgeon.Heroes;
using Rollgeon.PreConditions;

namespace Rollgeon.Effects.Tests
{
    [TestFixture]
    public class EffChainTests
    {
        private EffectContext MakeCtx()
        {
            return new EffectContext
            {
                SourceGuid = Guid.NewGuid(),
                TargetGuid = Guid.Empty,
                lastResult = true,
            };
        }

        private PreConditionContext MakePreCtx()
        {
            return new PreConditionContext
            {
                OwnerGuid = Guid.NewGuid(),
                OpponentGuid = Guid.Empty,
            };
        }

        // ───── Fallback execution ─────────────────────────────────────────────

        [Test]
        public void ApplyEffect_ZeroPhases_ReturnsFalse()
        {
            var chain = new EffChain { Phases = new List<ChainPhase>() };
            var ctx = MakeCtx();

            bool result = chain.ApplyEffect(ctx);

            Assert.IsFalse(result);
        }

        [Test]
        public void ApplyEffect_NullPhases_ReturnsFalse()
        {
            var chain = new EffChain { Phases = null };
            var ctx = MakeCtx();

            bool result = chain.ApplyEffect(ctx);

            Assert.IsFalse(result);
        }

        [Test]
        public void ApplyEffect_TwoPhases_BothExecute()
        {
            var eff0 = new Eff_ReturnsConfigured { ReturnValue = true };
            var eff1 = new Eff_ReturnsConfigured { ReturnValue = true };

            var chain = new EffChain
            {
                Phases = new List<ChainPhase>
                {
                    new ChainPhase
                    {
                        Label = "Phase 0",
                        Effects = new EffectData { Effects = new List<IEffect> { eff0 } },
                    },
                    new ChainPhase
                    {
                        Label = "Phase 1",
                        Effects = new EffectData { Effects = new List<IEffect> { eff1 } },
                    },
                },
            };

            var ctx = MakeCtx();
            bool result = chain.ApplyEffect(ctx);

            Assert.IsTrue(result);
            Assert.AreEqual(1, eff0.ExecutionCount);
            Assert.AreEqual(1, eff1.ExecutionCount);
        }

        [Test]
        public void ApplyEffect_Phase0Fails_Phase1Skipped()
        {
            var eff0 = new Eff_ReturnsConfigured { ReturnValue = false };
            var eff1 = new Eff_ReturnsConfigured { ReturnValue = true };

            var chain = new EffChain
            {
                Phases = new List<ChainPhase>
                {
                    new ChainPhase
                    {
                        Effects = new EffectData { Effects = new List<IEffect> { eff0 } },
                    },
                    new ChainPhase
                    {
                        Effects = new EffectData { Effects = new List<IEffect> { eff1 } },
                    },
                },
            };

            var ctx = MakeCtx();
            bool result = chain.ApplyEffect(ctx);

            Assert.IsFalse(result);
            Assert.AreEqual(1, eff0.ExecutionCount);
            Assert.AreEqual(0, eff1.ExecutionCount);
        }

        [Test]
        public void ApplyEffect_PhasePreConditionsFail_PhaseSkippedButChainContinues()
        {
            var eff0 = new Eff_ReturnsConfigured { ReturnValue = true };
            var eff1 = new Eff_ReturnsConfigured { ReturnValue = true };

            var chain = new EffChain
            {
                Phases = new List<ChainPhase>
                {
                    new ChainPhase
                    {
                        Effects = new EffectData
                        {
                            PreConditions = new List<BasePreCondition> { new PC_AlwaysFalse() },
                            Effects = new List<IEffect> { eff0 },
                        },
                    },
                    new ChainPhase
                    {
                        Effects = new EffectData { Effects = new List<IEffect> { eff1 } },
                    },
                },
            };

            var ctx = MakeCtx();
            bool result = chain.ApplyEffect(ctx);

            Assert.IsTrue(result);
            Assert.AreEqual(0, eff0.ExecutionCount);
            Assert.AreEqual(1, eff1.ExecutionCount);
        }

        [Test]
        public void PhaseCount_ReflectsListSize()
        {
            var chain = new EffChain
            {
                Phases = new List<ChainPhase>
                {
                    new ChainPhase(),
                    new ChainPhase(),
                    new ChainPhase(),
                },
            };

            Assert.AreEqual(3, chain.PhaseCount);
        }

        [Test]
        public void PhaseCount_NullPhases_ReturnsZero()
        {
            var chain = new EffChain { Phases = null };
            Assert.AreEqual(0, chain.PhaseCount);
        }

        [Test]
        public void GetEffectName_ReturnsChain()
        {
            var chain = new EffChain();
            Assert.AreEqual("Chain", chain.GetEffectName());
        }

        // ───── FindChainEffect ────────────────────────────────────────────────

        [Test]
        public void FindChainEffect_ReturnsChain_WhenPresent()
        {
            var chain = new EffChain
            {
                Phases = new List<ChainPhase> { new ChainPhase() },
            };

            var behavior = new HeroActionBehavior
            {
                ActionName = "TestChain",
                Effects = new List<EffectData>
                {
                    new EffectData { Effects = new List<IEffect> { chain } },
                },
            };

            var found = behavior.FindChainEffect();

            Assert.IsNotNull(found);
            Assert.AreSame(chain, found);
        }

        [Test]
        public void FindChainEffect_ReturnsNull_WhenNoChain()
        {
            var behavior = new HeroActionBehavior
            {
                ActionName = "NoChain",
                Effects = new List<EffectData>
                {
                    new EffectData
                    {
                        Effects = new List<IEffect>
                        {
                            new Eff_ReturnsConfigured { ReturnValue = true },
                        },
                    },
                },
            };

            var found = behavior.FindChainEffect();

            Assert.IsNull(found);
        }

        [Test]
        public void FindChainEffect_ReturnsNull_WhenEffectsNull()
        {
            var behavior = new HeroActionBehavior
            {
                ActionName = "NullEffects",
                Effects = null,
            };

            var found = behavior.FindChainEffect();

            Assert.IsNull(found);
        }
    }
}
