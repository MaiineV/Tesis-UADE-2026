using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.AI.Targeting;
using Rollgeon.Effects;
using Rollgeon.Entities.Behaviors;
using Rollgeon.PreConditions;

namespace Rollgeon.Combat.AI.Tests
{
    [TestFixture]
    public class EnemyActionBehaviorTests
    {
        private static AIContext NewCtx(Guid self, Guid player) => new AIContext
        {
            SelfGuid = self,
            PlayerGuid = player,
            SelfMaxHp = 50,
        };

        [Test]
        public void Execute_NoEffects_NoOp()
        {
            var behavior = new EnemyActionBehavior();
            var bctx = new EnemyAIBehaviorContext { AI = NewCtx(Guid.NewGuid(), Guid.NewGuid()) };
            Assert.DoesNotThrow(() => behavior.Execute(bctx));
        }

        [Test]
        public void Execute_EmptyPreConditions_RunsEffects()
        {
            var spy = new SpyEffect();
            var behavior = new EnemyActionBehavior
            {
                Effects = new List<EffectData>
                {
                    new EffectData { Effects = new List<IEffect> { spy } },
                }
            };
            behavior.Execute(new EnemyAIBehaviorContext { AI = NewCtx(Guid.NewGuid(), Guid.NewGuid()) });
            Assert.AreEqual(1, spy.AppliedCount, "PC vacía → effect debe correr (regla AND-empty=true).");
        }

        [Test]
        public void Execute_FailingPreCondition_SkipsGroup()
        {
            var spy = new SpyEffect();
            var behavior = new EnemyActionBehavior
            {
                Effects = new List<EffectData>
                {
                    new EffectData
                    {
                        PreConditions = new List<BasePreCondition> { new ConstPC { Value = false } },
                        Effects = new List<IEffect> { spy },
                    }
                }
            };
            behavior.Execute(new EnemyAIBehaviorContext { AI = NewCtx(Guid.NewGuid(), Guid.NewGuid()) });
            Assert.AreEqual(0, spy.AppliedCount);
        }

        [Test]
        public void Execute_OneGroupFails_NextGroupStillRuns()
        {
            // Distinto del héroe: cada EffectData se evalúa por separado, sin short-circuit
            // global. El plan reusa EffectData.TryExecute que no encadena lastResult entre sets.
            var spyA = new SpyEffect();
            var spyB = new SpyEffect();
            var behavior = new EnemyActionBehavior
            {
                Effects = new List<EffectData>
                {
                    new EffectData
                    {
                        PreConditions = new List<BasePreCondition> { new ConstPC { Value = false } },
                        Effects = new List<IEffect> { spyA },
                    },
                    new EffectData { Effects = new List<IEffect> { spyB } },
                }
            };
            behavior.Execute(new EnemyAIBehaviorContext { AI = NewCtx(Guid.NewGuid(), Guid.NewGuid()) });
            Assert.AreEqual(0, spyA.AppliedCount);
            Assert.AreEqual(1, spyB.AppliedCount);
        }

        [Test]
        public void Execute_BehaviorSelectorOverride_PassesCustomTarget()
        {
            var captured = Guid.Empty;
            var spy = new SpyEffect { OnApply = ctx => captured = ctx.TargetGuid };
            var customTarget = Guid.NewGuid();
            var behavior = new EnemyActionBehavior
            {
                TargetSelector = new ConstSelector { Pick = customTarget },
                Effects = new List<EffectData>
                {
                    new EffectData { Effects = new List<IEffect> { spy } },
                }
            };
            behavior.Execute(new EnemyAIBehaviorContext { AI = NewCtx(Guid.NewGuid(), Guid.NewGuid()) });
            Assert.AreEqual(customTarget, captured);
        }

        [Test]
        public void Execute_EffectDataSelector_OverridesBehaviorSelector()
        {
            var captured = Guid.Empty;
            var spy = new SpyEffect { OnApply = ctx => captured = ctx.TargetGuid };
            var behaviorTarget = Guid.NewGuid();
            var groupTarget = Guid.NewGuid();
            var behavior = new EnemyActionBehavior
            {
                TargetSelector = new ConstSelector { Pick = behaviorTarget },
                Effects = new List<EffectData>
                {
                    new EffectData
                    {
                        TargetSelector = new ConstSelector { Pick = groupTarget },
                        Effects = new List<IEffect> { spy },
                    }
                }
            };
            behavior.Execute(new EnemyAIBehaviorContext { AI = NewCtx(Guid.NewGuid(), Guid.NewGuid()) });
            Assert.AreEqual(groupTarget, captured);
        }

        [Test]
        public void Execute_NullSelector_FallsBackToPlayer()
        {
            var captured = Guid.Empty;
            var spy = new SpyEffect { OnApply = ctx => captured = ctx.TargetGuid };
            var playerId = Guid.NewGuid();
            var behavior = new EnemyActionBehavior
            {
                Effects = new List<EffectData>
                {
                    new EffectData { Effects = new List<IEffect> { spy } },
                }
            };
            behavior.Execute(new EnemyAIBehaviorContext { AI = NewCtx(Guid.NewGuid(), playerId) });
            Assert.AreEqual(playerId, captured);
        }

        [Test]
        public void Execute_WrongContextType_NoOp()
        {
            // El behavior espera EnemyAIBehaviorContext; cualquier otro subtipo loguea
            // warning y retorna sin ejecutar effects.
            var spy = new SpyEffect();
            var behavior = new EnemyActionBehavior
            {
                Effects = new List<EffectData>
                {
                    new EffectData { Effects = new List<IEffect> { spy } },
                }
            };
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            behavior.Execute(new Rollgeon.Heroes.HeroBehaviorContext());
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
            Assert.AreEqual(0, spy.AppliedCount);
        }

        // ---- Fakes ------------------------------------------------------

        private sealed class ConstPC : BasePreCondition
        {
            public bool Value;
            public override string ConditionName => $"Const({Value})";
            public override bool Evaluate(PreConditionContext context) => Value;
        }

        private sealed class ConstSelector : BaseEnemyTargetSelector
        {
            public Guid Pick;
            public override Guid PickTarget(AIContext ctx, Guid ownerGuid) => Pick;
        }

        private sealed class SpyEffect : IEffect
        {
            public int AppliedCount;
            public Action<EffectContext> OnApply;
            public bool Apply(EffectContext context)
            {
                AppliedCount++;
                OnApply?.Invoke(context);
                return true;
            }
            public string GetEffectName() => "Spy";
            public Rollgeon.Effects.Selection.SelectionSettings GetSelection() => null;
            public bool HasSelectionRequirement() => false;
            public bool RequiresSelectionAt(Rollgeon.Effects.Selection.SelectionTiming timing) => false;
            public bool ValidateSelection(
                Rollgeon.Effects.Selection.TargetSelectionResult result, Guid ownerGuid, out string error)
            {
                error = null;
                return true;
            }
        }
    }
}
