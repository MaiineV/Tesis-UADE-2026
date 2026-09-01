using NUnit.Framework;
using Rollgeon.Attributes;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.AI.Targeting;
using Rollgeon.Editor.Tools.Enemy.AITree;
using Rollgeon.Editor.Tools.Enemy.Templates;
using Rollgeon.Effects.Concretes;
using Rollgeon.PreConditions.Concretes;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    [TestFixture]
    public class EnemyTreeKitTests
    {
        [Test]
        public void EnergyLoop_BuildsCanonicalSkeleton()
        {
            var body = new AINode_Wait();
            var root = EnemyTreeKit.EnergyLoop(body, 4);

            Assert.AreEqual(2, root.Children.Count);
            var reset = (AINode_Behavior)root.Children[0];
            Assert.AreEqual(EnemyTreeKit.ResetEnergyName, reset.Behavior.ActionName);
            Assert.IsTrue(reset.Behavior.IsEnergyBookkeeping, "Recargar no cuenta como acción del turno");
            Assert.AreEqual(4, (int)EffectAuthoring.Get(reset.Behavior.Effects[0].Effects[0], "_baseAmount"));

            var loop = (AINode_While)root.Children[1];
            var pc = (PcOwnerStatCompare)loop.Conditions[0];
            Assert.AreEqual(StatType.Energy, pc.Stat);
            Assert.AreEqual(IntComparison.Greater, pc.Comparison);
            Assert.AreEqual(0, pc.Value);

            var iteration = (AINode_Sequence)loop.Body;
            var spend = (AINode_Behavior)iteration.Children[0];
            Assert.IsTrue(spend.Behavior.IsEnergyBookkeeping);
            Assert.AreEqual(IntOperation.Subtract, ((EffModifyIntAttribute)spend.Behavior.Effects[0].Effects[0]).Operation);
            Assert.AreSame(body, iteration.Children[1]);
        }

        [Test]
        public void AttackMelee_HasAnimDamageImpactInOrder()
        {
            var attack = EnemyTreeKit.AttackMelee();
            var effects = attack.Behavior.Effects[0].Effects;
            Assert.AreEqual(3, effects.Count);
            Assert.IsInstanceOf<EffPlaySequence>(effects[0]);
            Assert.IsInstanceOf<EffDealDamage>(effects[1]);
            Assert.IsInstanceOf<EffPlaySequence>(effects[2]);
            Assert.AreEqual("anim.enemy.melee.attack", ((EffPlaySequence)effects[0]).Steps[0].FeedbackRefId);
            Assert.IsInstanceOf<TargetSelector_AlwaysPlayer>(attack.Behavior.TargetSelector);
            Assert.IsFalse(attack.Behavior.IsEnergyBookkeeping, "Atacar sí cuenta como acción");
        }

        [Test]
        public void IfTargetInRange_UsesPcTargetInRange_WithMetric()
        {
            var then = new AINode_Wait();
            var node = EnemyTreeKit.IfTargetInRange(4, then, metric: DistanceMetric.Chebyshev);
            var pc = (PcTargetInRange)node.Conditions[0];
            Assert.AreEqual(4, pc.Range);
            Assert.AreEqual(DistanceMetric.Chebyshev, pc.Metric);
            Assert.AreSame(then, node.Then);
            Assert.IsNull(node.Else);
        }

        [Test]
        public void Kit_TreesPassTheTreeValidator()
        {
            var root = EnemyTreeKit.EnergyLoop(
                EnemyTreeKit.IfTargetInRange(1, EnemyTreeKit.AttackMelee(), EnemyTreeKit.Chase()));
            var issues = AITreeValidator.Validate(AITreeSerializer.Load(root));
            Assert.IsEmpty(issues, string.Join("\n", issues.ConvertAll(i => i.Message)));
        }
    }
}
