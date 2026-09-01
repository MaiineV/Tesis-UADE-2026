using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Editor.Tools.Enemy.AITree;
using Rollgeon.Editor.Tools.Enemy.Templates;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Readers;
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

        // ---- PcGoldCompare: el falso positivo del marker -------------------

        [Test]
        public void Validate_PcGoldCompareWithConstant_NoWarning()
        {
            // Arrange — default: Value = ReadConstantInt. El oro sale de IEconomyService,
            // no del roll: es 100% usable en enemigos.
            var ifNode = new AINode_If { Then = new AINode_Wait() };
            ifNode.Conditions.Add(new PcGoldCompare());

            // Act
            var issues = AITreeValidator.Validate(AITreeSerializer.Load(ifNode));

            // Assert
            Assert.IsFalse(Mentions(issues, "PcGoldCompare"),
                string.Join(" | ", issues.ConvertAll(i => i.Message)));
        }

        [Test]
        public void Validate_PcGoldCompareWithEffectReader_IsWarning()
        {
            // Arrange — un reader que lee el EffectContext sí depende del roll inexistente.
            var ifNode = new AINode_If { Then = new AINode_Wait() };
            ifNode.Conditions.Add(new PcGoldCompare { Value = new ReadEntityStat() });

            // Act + Assert
            Assert.IsTrue(Mentions(AITreeValidator.Validate(AITreeSerializer.Load(ifNode)), "PcGoldCompare"));
        }

        [Test]
        public void PcUnusableInEnemyTree_ClassifiesTheMarkerMinusGold()
        {
            Assert.IsTrue(AITreeValidator.PcUnusableInEnemyTree(typeof(PcNoComboThisRoll)));
            Assert.IsFalse(AITreeValidator.PcUnusableInEnemyTree(typeof(PcGoldCompare)), "el oro no depende del roll");
            Assert.IsFalse(AITreeValidator.PcUnusableInEnemyTree(typeof(PcTargetInRange)));
        }

        // ---- ActionName duplicado / claves reservadas ----------------------

        static AINode_Behavior Named(string name)
        {
            var node = BehaviorWith(new EffDealDamage());
            node.Behavior.ActionName = name;
            return node;
        }

        [Test]
        public void Validate_DuplicateActionName_IsWarning()
        {
            // Arrange — dos Behaviors DISTINTOS con el mismo nombre: el gate una-acción-por-turno
            // va por string, el segundo se saltearía en silencio.
            var root = new AINode_Sequence();
            root.Children.Add(Named("Ataque"));
            root.Children.Add(Named("Ataque"));

            // Act
            var issues = AITreeValidator.Validate(AITreeSerializer.Load(root));

            // Assert
            Assert.IsFalse(AITreeValidator.HasErrors(issues));
            Assert.IsTrue(Mentions(issues, "Acción duplicada"));
        }

        [Test]
        public void Validate_DistinctActionNames_NoDuplicateWarning()
        {
            var root = new AINode_Sequence();
            root.Children.Add(Named("Ataque"));
            root.Children.Add(Named("Disparo"));

            Assert.IsFalse(Mentions(AITreeValidator.Validate(AITreeSerializer.Load(root)), "Acción duplicada"));
        }

        [Test]
        public void Validate_EnergyBookkeepingDuplicates_NoWarning()
        {
            // Arrange — Recargar/Gastar energía se repiten a propósito (el gate los exime).
            var root = new AINode_Sequence();
            root.Children.Add(EnemyTreeKit.SpendEnergy());
            root.Children.Add(EnemyTreeKit.SpendEnergy());

            // Act + Assert
            Assert.IsFalse(Mentions(AITreeValidator.Validate(AITreeSerializer.Load(root)), "Acción duplicada"));
        }

        [Test]
        public void Validate_DuplicateInDetachedSubtree_NoWarning()
        {
            // Arrange — el duplicado vive en un subárbol suelto: no ejecuta, no compite.
            var root = Named("Ataque");
            var snap = AITreeSerializer.Load(root, new List<AIDecisionNode> { Named("Ataque") });

            // Act + Assert
            Assert.IsFalse(Mentions(AITreeValidator.Validate(snap), "Acción duplicada"));
        }

        [Test]
        public void Validate_ReservedActionName_IsError()
        {
            // Arrange — "__move" es la clave del AINode_Move: un Behavior con ese nombre
            // desactiva el movimiento del turno.
            var issues = AITreeValidator.Validate(AITreeSerializer.Load(Named(AINode_Move.ActionKey)));

            // Assert
            Assert.IsTrue(AITreeValidator.HasErrors(issues));
            Assert.IsTrue(Mentions(issues, "reservada"));
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
