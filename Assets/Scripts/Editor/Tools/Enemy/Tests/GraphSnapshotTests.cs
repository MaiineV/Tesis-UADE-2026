using NUnit.Framework;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Editor.Tools.Enemy.AITree;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    [TestFixture]
    public class GraphSnapshotTests
    {
        // root → a → b ; root → c ; detached d → e
        static GraphSnapshot Sample(out AINode_Sequence root, out AINode_Sequence a, out AINode_Wait b,
                                    out AINode_Wait c, out AINode_Sequence d, out AINode_Wait e)
        {
            root = new AINode_Sequence(); a = new AINode_Sequence(); b = new AINode_Wait();
            c = new AINode_Wait(); d = new AINode_Sequence(); e = new AINode_Wait();
            var snap = new GraphSnapshot { Root = root };
            snap.Nodes.AddRange(new AIDecisionNode[] { root, a, b, c, d, e });
            snap.Edges.Add(new GraphSnapshot.Edge(root, 0, a));
            snap.Edges.Add(new GraphSnapshot.Edge(a, 0, b));
            snap.Edges.Add(new GraphSnapshot.Edge(root, 0, c));
            snap.Edges.Add(new GraphSnapshot.Edge(d, 0, e));
            return snap;
        }

        [Test]
        public void IsAncestor_TransitiveDescendant_True()
        {
            var snap = Sample(out var root, out var a, out var b, out _, out _, out _);
            Assert.IsTrue(snap.IsAncestor(root, b));
            Assert.IsTrue(snap.IsAncestor(a, b));
        }

        [Test]
        public void IsAncestor_SelfOrUnrelated_False()
        {
            var snap = Sample(out var root, out _, out var b, out var c, out var d, out _);
            Assert.IsFalse(snap.IsAncestor(root, root));
            Assert.IsFalse(snap.IsAncestor(b, c));
            Assert.IsFalse(snap.IsAncestor(root, d));
            Assert.IsFalse(snap.IsAncestor(b, root), "un hijo no es ancestro de su raíz");
        }

        [Test]
        public void DetachedRoots_ExcludesRootAndNodesWithInbound_InNodesOrder()
        {
            var snap = Sample(out _, out _, out _, out _, out var d, out _);
            var detached = snap.DetachedRoots();
            Assert.AreEqual(1, detached.Count);
            Assert.AreSame(d, detached[0]);
        }

        [Test]
        public void PreOrder_WalksRootThenDetachedSubtrees()
        {
            var snap = Sample(out var root, out var a, out var b, out var c, out var d, out var e);
            var order = snap.PreOrder();
            CollectionAssert.AreEqual(new AIDecisionNode[] { root, a, b, c, d, e }, order);
        }

        [Test]
        public void ChildrenOf_ReturnsEdgeOrderForThatSlotOnly()
        {
            var ifNode = new AINode_If();
            var then = new AINode_Wait();
            var els = new AINode_Wait();
            var snap = new GraphSnapshot { Root = ifNode };
            snap.Nodes.AddRange(new AIDecisionNode[] { ifNode, then, els });
            snap.Edges.Add(new GraphSnapshot.Edge(ifNode, 1, els));
            snap.Edges.Add(new GraphSnapshot.Edge(ifNode, 0, then));

            CollectionAssert.AreEqual(new[] { then }, snap.ChildrenOf(ifNode, 0));
            CollectionAssert.AreEqual(new[] { els }, snap.ChildrenOf(ifNode, 1));
        }

        [Test]
        public void MoveChild_ReordersOnlySiblingsOfThatSlot()
        {
            var snap = Sample(out var root, out var a, out _, out var c, out _, out _);

            Assert.IsTrue(snap.MoveChild(root, 0, 0, 1));

            CollectionAssert.AreEqual(new AIDecisionNode[] { c, a }, snap.ChildrenOf(root, 0));
        }

        [Test]
        public void MoveChild_KeepsOtherParentsEdgesInPlace()
        {
            var snap = Sample(out var root, out var a, out var b, out _, out var d, out var e);

            snap.MoveChild(root, 0, 0, 1);

            // Los edges a→b y d→e siguen en sus posiciones absolutas de la lista.
            Assert.AreSame(b, snap.Edges[1].Child);
            Assert.AreSame(a, snap.Edges[1].Parent);
            Assert.AreSame(e, snap.Edges[3].Child);
            Assert.AreSame(d, snap.Edges[3].Parent);
        }

        [Test]
        public void MoveChild_OutOfRange_ReturnsFalse()
        {
            var snap = Sample(out var root, out _, out _, out _, out _, out _);
            Assert.IsFalse(snap.MoveChild(root, 0, 0, 2));
            Assert.IsFalse(snap.MoveChild(root, 0, -1, 0));
            Assert.IsFalse(snap.MoveChild(root, 0, 1, 1));
            Assert.IsFalse(snap.MoveChild(root, 5, 0, 1));
        }

        [Test]
        public void TryGetParent_FindsParentAndSlot()
        {
            var snap = Sample(out var root, out var a, out var b, out _, out var d, out _);
            Assert.IsTrue(snap.TryGetParent(b, out var p, out var slot));
            Assert.AreSame(a, p);
            Assert.AreEqual(0, slot);
            Assert.IsFalse(snap.TryGetParent(root, out _, out _));
            Assert.IsFalse(snap.TryGetParent(d, out _, out _));
        }
    }
}
