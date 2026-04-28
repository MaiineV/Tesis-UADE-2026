using System;
using NUnit.Framework;
using Rollgeon.Combat.AI.Targeting;

namespace Rollgeon.Combat.AI.Tests
{
    [TestFixture]
    public class TargetSelector_AlwaysPlayerTests
    {
        [Test]
        public void PickTarget_ReturnsPlayerGuid()
        {
            var player = Guid.NewGuid();
            var ctx = new AIContext { SelfGuid = Guid.NewGuid(), PlayerGuid = player };
            var selector = new TargetSelector_AlwaysPlayer();
            Assert.AreEqual(player, selector.PickTarget(ctx, ctx.SelfGuid));
        }

        [Test]
        public void PickTarget_NullContext_ReturnsEmpty()
        {
            var selector = new TargetSelector_AlwaysPlayer();
            Assert.AreEqual(Guid.Empty, selector.PickTarget(null, Guid.NewGuid()));
        }

        [Test]
        public void PickTarget_PlayerGuidEmpty_ReturnsEmpty()
        {
            var ctx = new AIContext { SelfGuid = Guid.NewGuid() };
            var selector = new TargetSelector_AlwaysPlayer();
            Assert.AreEqual(Guid.Empty, selector.PickTarget(ctx, ctx.SelfGuid));
        }

        // ---- Resolver --------------------------------------------------

        [Test]
        public void Resolver_NullSelector_FallsBackToAlwaysPlayer()
        {
            var player = Guid.NewGuid();
            var ctx = new AIContext { SelfGuid = Guid.NewGuid(), PlayerGuid = player };
            Assert.AreEqual(player, EnemyTargetResolver.Resolve(null, ctx, ctx.SelfGuid));
        }

        [Test]
        public void Resolver_CustomSelector_BypassesFallback()
        {
            var custom = Guid.NewGuid();
            var ctx = new AIContext { SelfGuid = Guid.NewGuid(), PlayerGuid = Guid.NewGuid() };
            var selector = new ConstSelector { Pick = custom };
            Assert.AreEqual(custom, EnemyTargetResolver.Resolve(selector, ctx, ctx.SelfGuid));
        }

        private sealed class ConstSelector : BaseEnemyTargetSelector
        {
            public Guid Pick;
            public override Guid PickTarget(AIContext ctx, Guid ownerGuid) => Pick;
        }
    }
}
