using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Editor.Tools.Enemy.AITree;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    [TestFixture]
    public class AITreeAutoLayoutTests
    {
        [Test]
        public void Compute_IncludesDetachedNodes()
        {
            var root = new AINode_Sequence();
            root.Children.Add(new AINode_Wait());
            var detached = new AINode_Wait();
            var snap = AITreeSerializer.Load(root, new List<AIDecisionNode> { detached });

            var positions = AITreeAutoLayout.Compute(snap);

            Assert.AreEqual(3, positions.Count);
            Assert.IsTrue(positions.ContainsKey(detached));
        }

        [Test]
        public void Compute_DetachedSubtreeBelowMainTree_AtDepthZero()
        {
            var root = new AINode_Sequence();
            var leaf = new AINode_Wait();
            root.Children.Add(leaf);
            var detached = new AINode_Wait();
            var snap = AITreeSerializer.Load(root, new List<AIDecisionNode> { detached });

            var positions = AITreeAutoLayout.Compute(snap);

            Assert.AreEqual(0f, positions[detached].x, "un suelto arranca en profundidad 0");
            Assert.Greater(positions[detached].y, positions[leaf].y, "los sueltos se apilan debajo del árbol");
        }

        [Test]
        public void Compute_NoRootButDetached_StillLaysOut()
        {
            var snap = AITreeSerializer.Load(null, new List<AIDecisionNode> { new AINode_Wait() });
            Assert.AreEqual(1, AITreeAutoLayout.Compute(snap).Count);
        }

        [Test]
        public void PathForId_UsesGivenDirAndForwardSlashes()
        {
            Assert.AreEqual("Some/Dir/x.json", AITreeLayoutSidecar.PathForId("x", "Some/Dir"));
            StringAssert.StartsWith(AITreeLayoutSidecar.LayoutsDir, AITreeLayoutSidecar.PathForId("x"));
        }
    }
}
