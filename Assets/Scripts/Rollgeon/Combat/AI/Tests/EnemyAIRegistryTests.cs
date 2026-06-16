using System;
using NUnit.Framework;
using Rollgeon.Combat.AI.Decisions;

namespace Rollgeon.Combat.AI.Tests
{
    [TestFixture]
    public class EnemyAIRegistryTests
    {
        [Test]
        public void Register_Lookup_Succeeds()
        {
            var reg = new EnemyAIRegistry();
            var id = Guid.NewGuid();
            var node = new AINode_Wait();
            reg.Register(id, node, 42);

            Assert.IsTrue(reg.Has(id));
            Assert.IsTrue(reg.TryGet(id, out var root, out var maxHp));
            Assert.AreSame(node, root);
            Assert.AreEqual(42, maxHp);
        }

        [Test]
        public void Unregister_RemovesEntry()
        {
            var reg = new EnemyAIRegistry();
            var id = Guid.NewGuid();
            reg.Register(id, new AINode_Wait(), 10);
            reg.Unregister(id);
            Assert.IsFalse(reg.Has(id));
            Assert.IsFalse(reg.TryGet(id, out _, out _));
        }

        [Test]
        public void Register_Twice_Overwrites()
        {
            var reg = new EnemyAIRegistry();
            var id = Guid.NewGuid();
            var first = new AINode_Wait();
            var second = new AINode_Wait();
            reg.Register(id, first, 10);
            reg.Register(id, second, 20);

            reg.TryGet(id, out var root, out var maxHp);
            Assert.AreSame(second, root);
            Assert.AreEqual(20, maxHp);
        }

        [Test]
        public void Register_EmptyGuid_Throws()
        {
            var reg = new EnemyAIRegistry();
            Assert.Throws<ArgumentException>(() => reg.Register(Guid.Empty, new AINode_Wait(), 10));
        }

        [Test]
        public void TryGet_AllowsNullRoot()
        {
            // Un enemigo sin AIRoot autorado se registra con root=null y mapHp>0,
            // sirviendo como señal "fallback al BasicEnemyAI".
            var reg = new EnemyAIRegistry();
            var id = Guid.NewGuid();
            reg.Register(id, null, 30);
            Assert.IsTrue(reg.TryGet(id, out var root, out var maxHp));
            Assert.IsNull(root);
            Assert.AreEqual(30, maxHp);
        }
    }
}
