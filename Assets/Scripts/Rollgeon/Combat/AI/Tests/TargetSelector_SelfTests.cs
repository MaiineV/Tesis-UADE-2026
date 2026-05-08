using System;
using NUnit.Framework;
using Rollgeon.Combat.AI.Targeting;

namespace Rollgeon.Combat.AI.Tests
{
    [TestFixture]
    public class TargetSelector_SelfTests
    {
        [Test]
        public void PickTarget_ReturnsOwnerGuid()
        {
            // Arrange
            var owner = Guid.NewGuid();
            var ctx = new AIContext { SelfGuid = owner, PlayerGuid = Guid.NewGuid() };
            var selector = new TargetSelector_Self();

            // Act
            var result = selector.PickTarget(ctx, owner);

            // Assert — debe ignorar PlayerGuid; el owner es el target
            Assert.AreEqual(owner, result);
        }

        [Test]
        public void PickTarget_NullContext_StillReturnsOwnerGuid()
        {
            // Arrange — Self no depende del contexto, solo del ownerGuid arg
            var owner = Guid.NewGuid();
            var selector = new TargetSelector_Self();

            // Act
            var result = selector.PickTarget(null, owner);

            // Assert
            Assert.AreEqual(owner, result);
        }

        [Test]
        public void PickTarget_OwnerGuidEmpty_ReturnsEmpty()
        {
            // Arrange
            var ctx = new AIContext { SelfGuid = Guid.NewGuid(), PlayerGuid = Guid.NewGuid() };
            var selector = new TargetSelector_Self();

            // Act
            var result = selector.PickTarget(ctx, Guid.Empty);

            // Assert
            Assert.AreEqual(Guid.Empty, result);
        }

        [Test]
        public void Resolver_SelfSelector_RoutesToOwnerGuid()
        {
            // Arrange — verifica integración con EnemyTargetResolver
            var owner = Guid.NewGuid();
            var ctx = new AIContext { SelfGuid = owner, PlayerGuid = Guid.NewGuid() };
            var selector = new TargetSelector_Self();

            // Act
            var result = EnemyTargetResolver.Resolve(selector, ctx, owner);

            // Assert
            Assert.AreEqual(owner, result);
        }
    }
}
