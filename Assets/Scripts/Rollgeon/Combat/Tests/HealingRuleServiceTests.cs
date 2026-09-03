using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Healing;

namespace Rollgeon.Combat.Tests
{
    /// <summary>
    /// Reglas de curación alterables por items (Ayuno): la regla vive mientras al menos una
    /// fuente la sostenga y se limpia al empezar una run.
    /// </summary>
    [TestFixture]
    public class HealingRuleServiceTests
    {
        private HealingRuleService _service;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            _service = new HealingRuleService();
            _service.Register();
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        [Test]
        public void Register_ExposesServiceThroughLocator()
        {
            Assert.IsTrue(ServiceLocator.TryGetService<IHealingRuleService>(out var svc));
            Assert.AreSame(_service, svc);
        }

        [Test]
        public void Default_PassiveHealingIsAllowed()
        {
            Assert.IsFalse(_service.PassiveItemHealingBlocked);
        }

        [Test]
        public void AddSource_BlocksHealing_RemoveSource_Unblocks()
        {
            _service.AddPassiveHealingBlock("ayuno");
            Assert.IsTrue(_service.PassiveItemHealingBlocked);

            _service.RemovePassiveHealingBlock("ayuno");
            Assert.IsFalse(_service.PassiveItemHealingBlocked);
        }

        [Test]
        public void TwoSources_RuleStaysWhileOneRemains()
        {
            _service.AddPassiveHealingBlock("a");
            _service.AddPassiveHealingBlock("b");

            _service.RemovePassiveHealingBlock("a");

            Assert.IsTrue(_service.PassiveItemHealingBlocked);
        }

        [Test]
        public void RemoveUnknownSource_IsNoOp()
        {
            _service.RemovePassiveHealingBlock("nope");
            Assert.IsFalse(_service.PassiveItemHealingBlocked);
        }

        [Test]
        public void RunStart_ClearsRules()
        {
            _service.AddPassiveHealingBlock("ayuno");

            EventManager.Trigger(EventName.OnRunStart, Guid.NewGuid());

            Assert.IsFalse(_service.PassiveItemHealingBlocked);
        }
    }
}
