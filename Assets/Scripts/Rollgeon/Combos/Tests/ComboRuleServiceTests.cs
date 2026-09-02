using NUnit.Framework;
using Patterns;
using Rollgeon.Combos.Rules;

namespace Rollgeon.Combos.Tests
{
    /// <summary>
    /// Reglas de combo alterables por items (Compás Salteado): la regla vive mientras al
    /// menos una fuente la sostenga y se limpia al empezar una run.
    /// </summary>
    [TestFixture]
    public class ComboRuleServiceTests
    {
        private ComboRuleService _service;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            _service = new ComboRuleService();
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
            Assert.IsTrue(ServiceLocator.TryGetService<IComboRuleService>(out var svc));
            Assert.AreSame(_service, svc);
        }

        [Test]
        public void Default_LadderRuleIsStandard()
        {
            Assert.IsFalse(_service.LadderAllowsSkippedStep);
        }

        [Test]
        public void AddSource_EnablesRule_RemoveSource_DisablesIt()
        {
            _service.AddLadderSkippedStep("compas.salteado");
            Assert.IsTrue(_service.LadderAllowsSkippedStep);

            _service.RemoveLadderSkippedStep("compas.salteado");
            Assert.IsFalse(_service.LadderAllowsSkippedStep);
        }

        [Test]
        public void SameSourceTwice_DoesNotNeedTwoRemoves()
        {
            _service.AddLadderSkippedStep("compas.salteado");
            _service.AddLadderSkippedStep("compas.salteado");

            _service.RemoveLadderSkippedStep("compas.salteado");

            Assert.IsFalse(_service.LadderAllowsSkippedStep);
        }

        [Test]
        public void TwoSources_RuleSurvivesUntilBothLeave()
        {
            _service.AddLadderSkippedStep("a");
            _service.AddLadderSkippedStep("b");

            _service.RemoveLadderSkippedStep("a");
            Assert.IsTrue(_service.LadderAllowsSkippedStep);

            _service.RemoveLadderSkippedStep("b");
            Assert.IsFalse(_service.LadderAllowsSkippedStep);
        }

        [Test]
        public void RemoveUnknownSource_IsHarmless()
        {
            _service.RemoveLadderSkippedStep("nope");
            Assert.IsFalse(_service.LadderAllowsSkippedStep);
        }

        [Test]
        public void RunStart_ClearsRules()
        {
            _service.AddLadderSkippedStep("compas.salteado");

            EventManager.Trigger(EventName.OnRunStart, System.Guid.NewGuid());

            Assert.IsFalse(_service.LadderAllowsSkippedStep);
        }
    }
}
