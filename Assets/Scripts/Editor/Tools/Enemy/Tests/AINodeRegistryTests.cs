using NUnit.Framework;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Editor.Tools.Enemy.AITree;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    [TestFixture]
    public class AINodeRegistryTests
    {
        [Test]
        public void All_DiscoversBuiltinNodes()
        {
            var types = new System.Collections.Generic.HashSet<System.Type>();
            foreach (var meta in AINodeRegistry.All) types.Add(meta.Type);

            Assert.IsTrue(types.Contains(typeof(AINode_Selector)));
            Assert.IsTrue(types.Contains(typeof(AINode_Sequence)));
            Assert.IsTrue(types.Contains(typeof(AINode_If)));
            Assert.IsTrue(types.Contains(typeof(AINode_Random)));
            Assert.IsTrue(types.Contains(typeof(AINode_Behavior)));
            Assert.IsTrue(types.Contains(typeof(AINode_Move)));
            Assert.IsTrue(types.Contains(typeof(AINode_KeepDistance)));
            Assert.IsTrue(types.Contains(typeof(AINode_Wait)));
        }

        [Test]
        public void All_StripsAINodePrefixFromDisplayName()
        {
            var meta = AINodeRegistry.Find(typeof(AINode_Selector));
            Assert.IsTrue(meta.HasValue);
            Assert.AreEqual("Selector", meta.Value.DisplayName);
        }

        [Test]
        public void All_AssignsCategoriesByBaseClass()
        {
            Assert.AreEqual(AINodeCategory.Composite, AINodeRegistry.Find(typeof(AINode_Selector)).Value.Category);
            Assert.AreEqual(AINodeCategory.Composite, AINodeRegistry.Find(typeof(AINode_Sequence)).Value.Category);
            Assert.AreEqual(AINodeCategory.Branching, AINodeRegistry.Find(typeof(AINode_If)).Value.Category);
            Assert.AreEqual(AINodeCategory.Branching, AINodeRegistry.Find(typeof(AINode_Random)).Value.Category);
            Assert.AreEqual(AINodeCategory.Leaf,      AINodeRegistry.Find(typeof(AINode_Wait)).Value.Category);
            Assert.AreEqual(AINodeCategory.Leaf,      AINodeRegistry.Find(typeof(AINode_Move)).Value.Category);
        }
    }
}
