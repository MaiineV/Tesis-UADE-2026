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

            Assert.IsEmpty(errors);
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

            Assert.IsEmpty(errors);
            Assert.AreEqual(1, sel.Children.Count);
            Assert.AreSame(ifNode, sel.Children[0]);
            Assert.AreSame(thenLeaf, ifNode.Then);
            Assert.AreSame(elseLeaf, ifNode.Else);
        }

        [Test]
        public void Save_PreservesRandomOptionsAndDefaultsWeight()
        {
            var rnd = new AINode_Random();
            var leaf1 = new AINode_Wait();
            var leaf2 = new AINode_Move();
            rnd.Options.Add(new AINode_Random.Option { Node = leaf1, Weight = 2f });
            rnd.Options.Add(new AINode_Random.Option { Node = leaf2, Weight = 5f });

            var snap = AITreeSerializer.Load(rnd);
            rnd.Options.Clear();
            AITreeSerializer.Save(snap, out var errors);

            Assert.IsEmpty(errors);
            Assert.AreEqual(2, rnd.Options.Count);
            // Note: weights collapse to default 1 because Save doesn't carry per-edge weight
            // — this test pins that behavior so changes are intentional.
            Assert.AreEqual(1f, rnd.Options[0].Weight);
            Assert.AreEqual(1f, rnd.Options[1].Weight);
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
        public void Save_RejectsOrphans()
        {
            var root = new AINode_Sequence();
            var orphan = new AINode_Wait();
            var snap = new GraphSnapshot { Root = root };
            snap.Nodes.Add(root);
            snap.Nodes.Add(orphan);

            var rebuilt = AITreeSerializer.Save(snap, out var errors);

            Assert.IsNull(rebuilt);
            CollectionAssert.IsNotEmpty(errors);
        }

        [Test]
        public void Save_RejectsIfWithoutThenBranch()
        {
            var root = new AINode_If();
            var snap = new GraphSnapshot { Root = root };
            snap.Nodes.Add(root);

            var rebuilt = AITreeSerializer.Save(snap, out var errors);

            Assert.IsNull(rebuilt);
            CollectionAssert.IsNotEmpty(errors);
        }
    }
}
