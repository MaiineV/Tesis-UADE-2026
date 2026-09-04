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
            _service.AddPotionHealMultiplier("ayuno", 0.5f);

            EventManager.Trigger(EventName.OnRunStart, Guid.NewGuid());

            Assert.IsFalse(_service.PassiveItemHealingBlocked);
            Assert.AreEqual(1f, _service.PotionHealMultiplier, 0.0001f);
            Assert.AreEqual(0, _service.PotionHealMultiplierSources.Count);
        }

        // ---- Multiplicador de la poción (Ayuno ×0.5) ----------------------------------

        [Test]
        public void Default_PotionHealMultiplierIsOne()
        {
            Assert.AreEqual(1f, _service.PotionHealMultiplier, 0.0001f);
            Assert.AreEqual(0, _service.PotionHealMultiplierSources.Count);
        }

        [Test]
        public void AddPotionHealMultiplier_MultipliesAcrossSources_RemoveRestores()
        {
            _service.AddPotionHealMultiplier("ayuno", 0.5f);
            _service.AddPotionHealMultiplier("otro", 2f);
            Assert.AreEqual(1f, _service.PotionHealMultiplier, 0.0001f);
            Assert.AreEqual(0.5f, _service.PotionHealMultiplierSources["ayuno"], 0.0001f);

            _service.RemovePotionHealMultiplier("otro");
            Assert.AreEqual(0.5f, _service.PotionHealMultiplier, 0.0001f);

            _service.RemovePotionHealMultiplier("ayuno");
            Assert.AreEqual(1f, _service.PotionHealMultiplier, 0.0001f);
        }

        [Test]
        public void AddPotionHealMultiplier_SameSourceTwice_ReplacesInsteadOfStacking()
        {
            _service.AddPotionHealMultiplier("ayuno", 0.5f);
            _service.AddPotionHealMultiplier("ayuno", 0.5f);

            Assert.AreEqual(0.5f, _service.PotionHealMultiplier, 0.0001f);
        }

        [Test]
        public void AddPotionHealMultiplier_NonPositiveOrEmptySource_IsIgnored()
        {
            _service.AddPotionHealMultiplier("ayuno", 0f);
            _service.AddPotionHealMultiplier("ayuno", -1f);
            _service.AddPotionHealMultiplier("", 0.5f);

            Assert.AreEqual(1f, _service.PotionHealMultiplier, 0.0001f);
            Assert.AreEqual(0, _service.PotionHealMultiplierSources.Count);
        }
    }
}
