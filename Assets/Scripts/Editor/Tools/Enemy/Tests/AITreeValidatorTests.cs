using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Editor.Tools.Enemy.AITree;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Entities.Behaviors;
using Rollgeon.PreConditions;
using Rollgeon.PreConditions.Concretes;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    [TestFixture]
    public class AITreeValidatorTests
    {
        static List<ValidationIssue> Of(IssueSeverity sev, List<ValidationIssue> issues)
            => issues.FindAll(i => i.Severity == sev);

        static bool Mentions(List<ValidationIssue> issues, string fragment)
            => issues.Exists(i => i.Message.Contains(fragment));

        static AINode_Behavior BehaviorWith(params IEffect[] effects)
        {
            var group = new EffectData();
            group.Effects.AddRange(effects);
            var behavior = new EnemyActionBehavior();
            behavior.Effects.Add(group);
            return new AINode_Behavior { Behavior = behavior };
        }

        // ---- errores ------------------------------------------------------

        [Test]
        public void Validate_Cycle_IsError()
        {
            var a = new AINode_Sequence();
            var b = new AINode_Sequence();
            var snap = new GraphSnapshot { Root = a };
            snap.Nodes.Add(a); snap.Nodes.Add(b);
            snap.Edges.Add(new GraphSnapshot.Edge(a, 0, b));
            snap.Edges.Add(new GraphSnapshot.Edge(b, 0, a));

            var issues = AITreeValidator.Validate(snap);

            Assert.IsTrue(AITreeValidator.HasErrors(issues));
            Assert.IsTrue(Mentions(issues, "ciclo"));
        }

        [Test]
        public void Validate_CycleInDetachedSubtree_IsError()
        {
            var root = new AINode_Wait();
            var a = new AINode_Sequence();
            var b = new AINode_Sequence();
            var snap = new GraphSnapshot { Root = root };
            snap.Nodes.AddRange(new AIDecisionNode[] { root, a, b });
            snap.Edges.Add(new GraphSnapshot.Edge(a, 0, b));
            snap.Edges.Add(new GraphSnapshot.Edge(b, 0, a));

            // a y b se apuntan entre sí: ninguno es raíz suelta, pero el ciclo sigue siendo
            // imposible de serializar. Multi-padre no aplica (un inbound cada uno).
            Assert.IsTrue(AITreeValidator.HasErrors(AITreeValidator.Validate(snap)));
        }

        [Test]
        public void Validate_MultiParent_IsError()
        {
            var a = new AINode_Sequence();
            var b = new AINode_Sequence();
            var leaf = new AINode_Wait();
            var snap = new GraphSnapshot { Root = a };
            snap.Nodes.AddRange(new AIDecisionNode[] { a, b, leaf });
            snap.Edges.Add(new GraphSnapshot.Edge(a, 0, b));
            snap.Edges.Add(new GraphSnapshot.Edge(a, 0, leaf));
            snap.Edges.Add(new GraphSnapshot.Edge(b, 0, leaf));

            var issues = AITreeValidator.Validate(snap);

            Assert.IsTrue(AITreeValidator.HasErrors(issues));
            Assert.IsTrue(Mentions(issues, "más de un padre"));
        }

        // ---- avisos -------------------------------------------------------

        [Test]
        public void Validate_IfWithoutThen_IsWarning()
        {
            var ifNode = new AINode_If();
            ifNode.Conditions.Add(new PcOwnerHpBelow());
            var snap = AITreeSerializer.Load(ifNode);

            var issues = AITreeValidator.Validate(snap);

            Assert.IsFalse(AITreeValidator.HasErrors(issues));
            Assert.IsTrue(Mentions(issues, "Entonces"));
            Assert.AreSame(ifNode, Of(IssueSeverity.Warning, issues)[0].Node);
        }

        [Test]
        public void Validate_IfWithoutConditions_IsWarning()
        {
            var ifNode = new AINode_If { Then = new AINode_Wait() };
            var issues = AITreeValidator.Validate(AITreeSerializer.Load(ifNode));
            Assert.IsTrue(Mentions(issues, "siempre pasa"));
        }

        [Test]
        public void Validate_WhileWithoutConditions_IsWarning()
        {
            var w = new AINode_While { Body = new AINode_Wait() };
            var issues = AITreeValidator.Validate(AITreeSerializer.Load(w));
            Assert.IsTrue(Mentions(issues, "MaxIterations"));
        }

        [Test]
        public void Validate_CompositeWithoutChildren_IsWarning()
        {
            var issues = AITreeValidator.Validate(AITreeSerializer.Load(new AINode_Selector()));
            Assert.AreEqual(1, Of(IssueSeverity.Warning, issues).Count);
            Assert.IsTrue(Mentions(issues, "sin hijos"));
        }

        [Test]
        public void Validate_BehaviorWithoutEffects_IsWarning()
        {
            var node = new AINode_Behavior { Behavior = new EnemyActionBehavior() };
            var issues = AITreeValidator.Validate(AITreeSerializer.Load(node));
            Assert.IsTrue(Mentions(issues, "sin efectos"));

            var empty = new AINode_Behavior();
            Assert.IsTrue(Mentions(AITreeValidator.Validate(AITreeSerializer.Load(empty)), "Behavior vacío"));
        }

        [Test]
        public void Validate_RandomWithSingleOption_IsWarning()
        {
            var rnd = new AINode_Random();
            rnd.Options.Add(new AINode_Random.Option { Node = new AINode_Wait(), Weight = 1f });
            var issues = AITreeValidator.Validate(AITreeSerializer.Load(rnd));
            Assert.IsTrue(Mentions(issues, "menos de dos opciones"));
        }

        [Test]
        public void Validate_RandomWithZeroTotalWeight_IsWarning()
        {
            var rnd = new AINode_Random();
            rnd.Options.Add(new AINode_Random.Option { Node = new AINode_Wait(), Weight = 0f });
            rnd.Options.Add(new AINode_Random.Option { Node = new AINode_Wait(), Weight = 0f });
            var issues = AITreeValidator.Validate(AITreeSerializer.Load(rnd));
            Assert.IsTrue(Mentions(issues, "peso total 0"));
        }

        [Test]
        public void Validate_PcReadingTriggerEffectInIf_IsWarning()
        {
            var ifNode = new AINode_If { Then = new AINode_Wait() };
            ifNode.Conditions.Add(new PcNoComboThisRoll());
            var issues = AITreeValidator.Validate(AITreeSerializer.Load(ifNode));
            Assert.IsTrue(Mentions(issues, "PcNoComboThisRoll"));
        }

        [Test]
        public void Validate_PcNestedInComposite_IsWarning()
        {
            var composite = new PCComposite();
            composite.Children.Add(new PcNoComboThisRoll());
            var ifNode = new AINode_If { Then = new AINode_Wait() };
            ifNode.Conditions.Add(composite);

            var issues = AITreeValidator.Validate(AITreeSerializer.Load(ifNode));

            Assert.IsTrue(Mentions(issues, "PcNoComboThisRoll"));
        }

        [Test]
        public void Validate_PcReadingTriggerEffectInBehaviorEffectData_IsWarning()
        {
            var node = BehaviorWith(new EffDealDamage());
            node.Behavior.Effects[0].PreConditions.Add(new PcNoComboThisRoll());

            var issues = AITreeValidator.Validate(AITreeSerializer.Load(node));

            Assert.IsTrue(Mentions(issues, "PcNoComboThisRoll"));
        }

        [Test]
        public void Validate_ScratchEffectNestedInChain_IsWarning()
        {
            var chain = new EffChain();
            var phase = new ChainPhase();
            phase.Effects.Effects.Add(new EffAddComboBonus());
            chain.Phases.Add(phase);
            var node = BehaviorWith(chain);

            var issues = AITreeValidator.Validate(AITreeSerializer.Load(node));

            Assert.IsTrue(Mentions(issues, "EffAddComboBonus"), "EffectTree.SelfAndDescendants debe entrar en las fases del chain");
        }

        [Test]
        public void Validate_ClassSkillPushInBehavior_IsWarning()
        {
            var issues = AITreeValidator.Validate(AITreeSerializer.Load(BehaviorWith(new EffClassSkillPush())));
            Assert.IsTrue(Mentions(issues, "EffClassSkillPush"));
        }

        [Test]
        public void NeedsPlayerRollContext_DealDamage_False()
        {
            Assert.IsFalse(AITreeValidator.NeedsPlayerRollContext(typeof(EffDealDamage)));
            Assert.IsTrue(AITreeValidator.NeedsPlayerRollContext(typeof(EffMultiplyComboDamage)));
        }

        // ---- info ---------------------------------------------------------

        [Test]
        public void Validate_NoRoot_IsInfo()
        {
            var snap = new GraphSnapshot { Root = null };
            snap.Nodes.Add(new AINode_Wait());
            var issues = AITreeValidator.Validate(snap);
            Assert.IsFalse(AITreeValidator.HasErrors(issues));
            Assert.IsTrue(Of(IssueSeverity.Info, issues).Exists(i => i.Message.Contains("Sin raíz")));
        }

        [Test]
        public void Validate_DetachedCount_IsInfo()
        {
            var root = new AINode_Wait();
            var snap = AITreeSerializer.Load(root, new List<AIDecisionNode> { new AINode_Wait(), new AINode_Wait() });
            var issues = AITreeValidator.Validate(snap);
            Assert.IsFalse(AITreeValidator.HasErrors(issues));
            Assert.IsTrue(Mentions(issues, "2 subárboles sueltos"));
        }

        [Test]
        public void Validate_HealthyTree_NoIssues()
        {
            var ifNode = new AINode_If { Then = BehaviorWith(new EffDealDamage()), Else = new AINode_Move() };
            ifNode.Conditions.Add(new PcTargetInRange());
            var root = new AINode_Selector();
            root.Children.Add(ifNode);

            var issues = AITreeValidator.Validate(AITreeSerializer.Load(root));

            Assert.IsEmpty(issues, string.Join(" | ", issues.ConvertAll(i => i.Message)));
        }
    }
}
