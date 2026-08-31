using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Editor.Tools.Enemy.AITree;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    [TestFixture]
    public class AITreeSerializerTests
    {
        // ---- Round-trip ---------------------------------------------------

        [Test]
        public void Load_FlattensTreeIntoNodesAndEdges()
        {
            var leaf1 = new AINode_Wait();
            var leaf2 = new AINode_Move();
            var seq = new AINode_Sequence();
            seq.Children.Add(leaf1);
            seq.Children.Add(leaf2);

            var snap = AITreeSerializer.Load(seq);

            Assert.AreSame(seq, snap.Root);
            Assert.AreEqual(3, snap.Nodes.Count);
            Assert.AreEqual(2, snap.Edges.Count);
        }

        [Test]
        public void Save_RoundTripPreservesSequenceChildOrder()
        {
            var leaf1 = new AINode_Wait();
            var leaf2 = new AINode_Wait();
            var leaf3 = new AINode_Wait();
            var seq = new AINode_Sequence();
            seq.Children.Add(leaf1);
            seq.Children.Add(leaf2);
            seq.Children.Add(leaf3);

            var snap = AITreeSerializer.Load(seq);

            // mutate: clear underlying and rebuild via Save
            seq.Children.Clear();
            var rebuilt = AITreeSerializer.Save(snap, out var errors);

            Assert.IsFalse(AITreeValidator.HasErrors(errors));
            Assert.AreSame(seq, rebuilt);
            Assert.AreEqual(3, seq.Children.Count);
            Assert.AreSame(leaf1, seq.Children[0]);
            Assert.AreSame(leaf2, seq.Children[1]);
            Assert.AreSame(leaf3, seq.Children[2]);
        }

        [Test]
        public void Save_PreservesPolymorphismAcrossSubtypes()
        {
            var sel = new AINode_Selector();
            var ifNode = new AINode_If();
            var thenLeaf = new AINode_Wait();
            var elseLeaf = new AINode_Move();
            ifNode.Then = thenLeaf;
            ifNode.Else = elseLeaf;
            sel.Children.Add(ifNode);

            var snap = AITreeSerializer.Load(sel);

            // wipe topology, then rebuild from snapshot
            sel.Children.Clear();
            ifNode.Then = null;
            ifNode.Else = null;

            var rebuilt = AITreeSerializer.Save(snap, out var errors);

            Assert.IsFalse(AITreeValidator.HasErrors(errors));
            Assert.AreEqual(1, sel.Children.Count);
            Assert.AreSame(ifNode, sel.Children[0]);
            Assert.AreSame(thenLeaf, ifNode.Then);
            Assert.AreSame(elseLeaf, ifNode.Else);
        }

        // ---- Random weights -----------------------------------------------
        // Save reconstruye Options desde los edges; el peso se captura del nodo antes de
        // limpiar. Ningún test limpia Options a mano: eso vaciaría la fuente del peso.

        static AINode_Random RandomWithTwoWeightedLeaves(out AINode_Wait leaf1, out AINode_Move leaf2)
        {
            var rnd = new AINode_Random();
            leaf1 = new AINode_Wait();
            leaf2 = new AINode_Move();
            rnd.Options.Add(new AINode_Random.Option { Node = leaf1, Weight = 2f });
            rnd.Options.Add(new AINode_Random.Option { Node = leaf2, Weight = 5f });
            return rnd;
        }

        [Test]
        public void Save_Random_PreservesWeightsPerChild()
        {
            var rnd = RandomWithTwoWeightedLeaves(out var leaf1, out var leaf2);

            var snap = AITreeSerializer.Load(rnd);
            AITreeSerializer.Save(snap, out var errors);

            Assert.IsFalse(AITreeValidator.HasErrors(errors));
            Assert.AreEqual(2, rnd.Options.Count);
            Assert.AreSame(leaf1, rnd.Options[0].Node);
            Assert.AreEqual(2f, rnd.Options[0].Weight);
            Assert.AreSame(leaf2, rnd.Options[1].Node);
            Assert.AreEqual(5f, rnd.Options[1].Weight);
        }

        [Test]
        public void Save_Random_WeightsFollowChildWhenEdgeOrderChanges()
        {
            var rnd = RandomWithTwoWeightedLeaves(out var leaf1, out var leaf2);

            var snap = AITreeSerializer.Load(rnd);
            var first = snap.Edges[0];
            snap.Edges[0] = snap.Edges[1];
            snap.Edges[1] = first;
            AITreeSerializer.Save(snap, out var errors);

            Assert.IsFalse(AITreeValidator.HasErrors(errors));
            Assert.AreSame(leaf2, rnd.Options[0].Node);
            Assert.AreEqual(5f, rnd.Options[0].Weight);
            Assert.AreSame(leaf1, rnd.Options[1].Node);
            Assert.AreEqual(2f, rnd.Options[1].Weight);
        }

        [Test]
        public void Save_Random_NewEdgeDefaultsWeightToOne()
        {
            var rnd = RandomWithTwoWeightedLeaves(out _, out _);
            var leaf3 = new AINode_Wait();

            var snap = AITreeSerializer.Load(rnd);
            snap.Nodes.Add(leaf3);
            snap.Edges.Add(new GraphSnapshot.Edge(rnd, 0, leaf3));
            AITreeSerializer.Save(snap, out var errors);

            Assert.IsFalse(AITreeValidator.HasErrors(errors));
            Assert.AreEqual(3, rnd.Options.Count);
            Assert.AreEqual(2f, rnd.Options[0].Weight);
            Assert.AreEqual(5f, rnd.Options[1].Weight);
            Assert.AreSame(leaf3, rnd.Options[2].Node);
            Assert.AreEqual(1f, rnd.Options[2].Weight);
        }

        [Test]
        public void Save_Random_RemovedEdgeDropsItsOption()
        {
            var rnd = RandomWithTwoWeightedLeaves(out var leaf1, out var leaf2);

            var snap = AITreeSerializer.Load(rnd);
            snap.Nodes.Remove(leaf1);
            snap.Edges.RemoveAll(e => e.Child == leaf1);
            AITreeSerializer.Save(snap, out var errors);

            Assert.IsFalse(AITreeValidator.HasErrors(errors));
            Assert.AreEqual(1, rnd.Options.Count);
            Assert.AreSame(leaf2, rnd.Options[0].Node);
            Assert.AreEqual(5f, rnd.Options[0].Weight);
        }

        // ---- Alternate ----------------------------------------------------

        [Test]
        public void SlotsOf_Alternate_ExposesOneDynamicSlot()
        {
            var slots = AITreeTopology.SlotsOf(new AINode_Alternate());

            Assert.AreEqual(1, slots.Count);
            Assert.IsTrue(slots[0].IsDynamic);
            Assert.AreEqual("Children", slots[0].Name);
        }

        [Test]
        public void Load_Alternate_ProducesOneEdgePerChildInOrder()
        {
            var alt = new AINode_Alternate();
            var leaf1 = new AINode_Wait();
            var leaf2 = new AINode_Move();
            var leaf3 = new AINode_Wait();
            alt.Children.Add(leaf1);
            alt.Children.Add(leaf2);
            alt.Children.Add(leaf3);

            var snap = AITreeSerializer.Load(alt);

            Assert.AreEqual(4, snap.Nodes.Count);
            Assert.AreEqual(3, snap.Edges.Count);
            Assert.AreSame(leaf1, snap.Edges[0].Child);
            Assert.AreSame(leaf2, snap.Edges[1].Child);
            Assert.AreSame(leaf3, snap.Edges[2].Child);
            foreach (var e in snap.Edges) Assert.AreEqual(0, e.SlotIndex);
        }

        [Test]
        public void Save_Alternate_RoundTripPreservesChildOrder()
        {
            var alt = new AINode_Alternate();
            var leaf1 = new AINode_Wait();
            var leaf2 = new AINode_Wait();
            var leaf3 = new AINode_Wait();
            alt.Children.Add(leaf1);
            alt.Children.Add(leaf2);
            alt.Children.Add(leaf3);

            var snap = AITreeSerializer.Load(alt);
            alt.Children.Clear();
            var rebuilt = AITreeSerializer.Save(snap, out var errors);

            Assert.IsFalse(AITreeValidator.HasErrors(errors));
            Assert.AreSame(alt, rebuilt);
            Assert.AreEqual(3, alt.Children.Count);
            Assert.AreSame(leaf1, alt.Children[0]);
            Assert.AreSame(leaf2, alt.Children[1]);
            Assert.AreSame(leaf3, alt.Children[2]);
        }

        // ---- Validation ---------------------------------------------------

        [Test]
        public void Save_RejectsCycles()
        {
            var a = new AINode_Sequence();
            var b = new AINode_Sequence();
            var snap = new GraphSnapshot { Root = a };
            snap.Nodes.Add(a);
            snap.Nodes.Add(b);
            snap.Edges.Add(new GraphSnapshot.Edge(a, 0, b));
            snap.Edges.Add(new GraphSnapshot.Edge(b, 0, a));

            var rebuilt = AITreeSerializer.Save(snap, out var errors);

            Assert.IsNull(rebuilt);
            CollectionAssert.IsNotEmpty(errors);
        }

        [Test]
        public void Save_DetachedNode_IsReturnedAsDetachedRoot_NotError()
        {
            var root = new AINode_Sequence();
            var loose = new AINode_Wait();
            var snap = new GraphSnapshot { Root = root };
            snap.Nodes.Add(root);
            snap.Nodes.Add(loose);

            var rebuilt = AITreeSerializer.Save(snap, out var detached, out var errors);

            Assert.AreSame(root, rebuilt);
            Assert.IsFalse(AITreeValidator.HasErrors(errors));
            CollectionAssert.AreEqual(new[] { loose }, detached);
        }

        [Test]
        public void Save_IfWithoutThen_IsWarningButStillSaves()
        {
            var root = new AINode_If();
            var snap = new GraphSnapshot { Root = root };
            snap.Nodes.Add(root);

            var rebuilt = AITreeSerializer.Save(snap, out var errors);

            Assert.AreSame(root, rebuilt);
            Assert.IsFalse(AITreeValidator.HasErrors(errors));
            Assert.IsTrue(errors.Exists(e => e.Severity == IssueSeverity.Warning && e.Node == root));
        }

        // ---- Sueltos + reorden ----------------------------------------------

        [Test]
        public void Load_WithDetached_AppendsSubtreesAfterRootWalk()
        {
            var root = new AINode_Sequence();
            var leaf = new AINode_Wait();
            root.Children.Add(leaf);
            var loose = new AINode_Sequence();
            var looseLeaf = new AINode_Wait();
            loose.Children.Add(looseLeaf);

            var snap = AITreeSerializer.Load(root, new List<AIDecisionNode> { loose });

            CollectionAssert.AreEqual(new AIDecisionNode[] { root, leaf, loose, looseLeaf }, snap.Nodes);
            Assert.AreEqual(2, snap.Edges.Count);
            CollectionAssert.AreEqual(new[] { loose }, snap.DetachedRoots());
        }

        [Test]
        public void Load_DetachedAlreadyReachable_IsSkipped()
        {
            var root = new AINode_Sequence();
            var leaf = new AINode_Wait();
            root.Children.Add(leaf);

            var snap = AITreeSerializer.Load(root, new List<AIDecisionNode> { leaf, null });

            Assert.AreEqual(2, snap.Nodes.Count);
            Assert.IsEmpty(snap.DetachedRoots());
        }

        [Test]
        public void Save_DetachedSubtree_KeepsInternalEdges()
        {
            var root = new AINode_Wait();
            var loose = new AINode_Sequence();
            var a = new AINode_Wait();
            var b = new AINode_Wait();
            loose.Children.Add(a);
            loose.Children.Add(b);
            var snap = AITreeSerializer.Load(root, new List<AIDecisionNode> { loose });

            AITreeSerializer.Save(snap, out var detached, out var errors);

            Assert.IsFalse(AITreeValidator.HasErrors(errors));
            CollectionAssert.AreEqual(new[] { loose }, detached);
            CollectionAssert.AreEqual(new[] { a, b }, loose.Children);
        }

        [Test]
        public void Save_MoveChild_ReordersSequenceChildren()
        {
            var leaf1 = new AINode_Wait();
            var leaf2 = new AINode_Wait();
            var leaf3 = new AINode_Wait();
            var seq = new AINode_Sequence();
            seq.Children.Add(leaf1);
            seq.Children.Add(leaf2);
            seq.Children.Add(leaf3);
            var snap = AITreeSerializer.Load(seq);

            Assert.IsTrue(snap.MoveChild(seq, 0, 0, 2));
            AITreeSerializer.Save(snap, out var errors);

            Assert.IsFalse(AITreeValidator.HasErrors(errors));
            CollectionAssert.AreEqual(new[] { leaf2, leaf3, leaf1 }, seq.Children);
        }

        [Test]
        public void Save_MoveChild_RandomWeightsFollowChild()
        {
            var rnd = RandomWithTwoWeightedLeaves(out var leaf1, out var leaf2);
            var snap = AITreeSerializer.Load(rnd);

            snap.MoveChild(rnd, 0, 1, 0);
            AITreeSerializer.Save(snap, out _);

            Assert.AreSame(leaf2, rnd.Options[0].Node);
            Assert.AreEqual(5f, rnd.Options[0].Weight);
            Assert.AreSame(leaf1, rnd.Options[1].Node);
            Assert.AreEqual(2f, rnd.Options[1].Weight);
        }
    }
}
