using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Effects;
using Rollgeon.Entities.Behaviors;

namespace Rollgeon.Combat.AI.Tests
{
    [TestFixture]
    public class AINode_BehaviorTests
    {
        [Test]
        public void Tick_NullBehavior_ReturnsFailed()
        {
            var node = new AINode_Behavior();
            Assert.AreEqual(AIResult.Failed, node.Tick(new AIContext()));
        }

        [Test]
        public void Tick_NullContext_ReturnsFailed()
        {
            var node = new AINode_Behavior { Behavior = new EnemyActionBehavior() };
            Assert.AreEqual(AIResult.Failed, node.Tick(null));
        }

        [Test]
        public void Tick_WrapsAIContext_AndReturnsSucceeded()
        {
            var spy = new SpyEffect();
            var behavior = new EnemyActionBehavior
            {
                Effects = new List<EffectData>
                {
                    new EffectData { Effects = new List<IEffect> { spy } },
                }
            };
            var node = new AINode_Behavior { Behavior = behavior };
            var ctx = new AIContext { SelfGuid = Guid.NewGuid(), PlayerGuid = Guid.NewGuid() };

            Assert.AreEqual(AIResult.Succeeded, node.Tick(ctx));
            Assert.AreEqual(1, spy.AppliedCount, "El behavior debe haber recibido el AIContext y ejecutado el effect.");
            Assert.AreEqual(ctx.PlayerGuid, spy.LastTarget, "Default selector debe apuntar al player.");
        }

        private sealed class SpyEffect : IEffect
        {
            public int AppliedCount;
            public Guid LastTarget;
            public bool Apply(EffectContext context)
            {
                AppliedCount++;
                LastTarget = context.TargetGuid;
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
