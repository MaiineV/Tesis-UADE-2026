using NUnit.Framework;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Threat;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Entities;
using Rollgeon.Entities.Behaviors;
using Rollgeon.PreConditions;
using Rollgeon.PreConditions.Concretes;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    [TestFixture]
    public class EnemyTreeSummaryTests
    {
        EnemyDataSO _so;

        [SetUp] public void SetUp() => _so = ScriptableObject.CreateInstance<EnemyDataSO>();
        [TearDown] public void TearDown() => Object.DestroyImmediate(_so);

        [Test]
        public void Build_NullRoot_ReturnsEmptySummaryWithHasTreeFalse()
        {
            var s = EnemyTreeSummary.Build(_so);
            Assert.IsFalse(s.HasTree);
            Assert.AreEqual(0, s.NodeCount);
            Assert.IsFalse(s.HasMovement);
        }

        [Test]
        public void Build_CountsNodesAndCollectsTelegraphShapes()
        {
            var seq = new AINode_Sequence();
            seq.Children.Add(new AINode_TelegraphMark { Shape = ThreatShape.Row });
            seq.Children.Add(new AINode_AuxTelegraph { Shape = ThreatShape.SquareAroundSelf });
            _so.AIRoot = seq;
            _so.AIDetachedNodes.Add(new AINode_Wait());

            var s = EnemyTreeSummary.Build(_so);

            Assert.IsTrue(s.HasTree);
            Assert.AreEqual(3, s.NodeCount, "los sueltos no cuentan como nodos activos");
            Assert.AreEqual(1, s.DetachedCount);
            Assert.IsTrue(s.HasTelegraph);
            CollectionAssert.AreEquivalent(new[] { ThreatShape.Row, ThreatShape.SquareAroundSelf }, s.TelegraphShapes);
        }

        [Test]
        public void Build_CollectsPreConditionsThroughCompositeAndEffectData()
        {
            var composite = new PCComposite();
            composite.Children.Add(new PcOwnerHpBelow());
            var ifNode = new AINode_If();
            ifNode.Conditions.Add(composite);

            var group = new EffectData();
            group.PreConditions.Add(new PcChance());
            group.Effects.Add(new EffDealDamage());
            var behavior = new EnemyActionBehavior();
            behavior.Effects.Add(group);
            ifNode.Then = new AINode_Behavior { Behavior = behavior };
            _so.AIRoot = ifNode;

            var s = EnemyTreeSummary.Build(_so);

            CollectionAssert.Contains(s.PreConditionTypes, typeof(PCComposite));
            CollectionAssert.Contains(s.PreConditionTypes, typeof(PcOwnerHpBelow));
            CollectionAssert.Contains(s.PreConditionTypes, typeof(PcChance));
            CollectionAssert.Contains(s.EffectTypes, typeof(EffDealDamage));
            Assert.IsFalse(s.HasHeal);
        }

        [Test]
        public void Build_FlagsHealFromSupportHealBehaviorInBehaviorsList()
        {
            _so.AIRoot = new AINode_Wait();
            _so.Behaviors.Add(new SupportHealBehavior());

            var s = EnemyTreeSummary.Build(_so);

            Assert.IsTrue(s.HasHeal);
            Assert.IsTrue(s.UsesBehaviorsList);
        }

        [Test]
        public void Build_MovementAndRangedShot_AreDetected()
        {
            var sel = new AINode_Selector();
            sel.Children.Add(new AINode_RangedShot());
            sel.Children.Add(new AINode_KeepDistance());
            _so.AIRoot = sel;

            var s = EnemyTreeSummary.Build(_so);

            Assert.IsTrue(s.HasRangedShot);
            Assert.IsTrue(s.KeepsDistance);
            Assert.AreEqual("KeepDistance", EnemyTreeSummary.Names(s.MovementNodes));
        }
    }
}
