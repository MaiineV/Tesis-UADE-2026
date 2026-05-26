using NUnit.Framework;
using Patterns;
using Rollgeon.Phase;
using Rollgeon.PreConditions.Concretes;

namespace Rollgeon.PreConditions.Tests
{
    [TestFixture]
    public class PCCurrentPhaseTests
    {
        private FakePhaseService _phase;

        [SetUp]
        public void Setup()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _phase = new FakePhaseService { Base = GamePhase.Combat, Overlay = PhaseOverlay.None };
            ServiceLocator.AddService<IPhaseService>(_phase);
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private static PreConditionContext Ctx() => new PreConditionContext();

        [Test]
        public void Evaluate_PhaseMatches_ReturnsTrue()
        {
            var pc = new PCCurrentPhase { ExpectedPhase = GamePhase.Combat };
            Assert.IsTrue(pc.Evaluate(Ctx()));
        }

        [Test]
        public void Evaluate_PhaseMismatch_ReturnsFalse()
        {
            _phase.Base = GamePhase.Exploration;
            var pc = new PCCurrentPhase { ExpectedPhase = GamePhase.Combat };
            Assert.IsFalse(pc.Evaluate(Ctx()));
        }

        [Test]
        public void Evaluate_NoPhaseService_ReturnsFalse()
        {
            ServiceLocator.Clear();
            var pc = new PCCurrentPhase { ExpectedPhase = GamePhase.Combat };
            Assert.IsFalse(pc.Evaluate(Ctx()));
        }

        [Test]
        public void Evaluate_MatchOverlay_PassesWhenBoth()
        {
            _phase.Base = GamePhase.Combat;
            _phase.Overlay = PhaseOverlay.Pause;
            var pc = new PCCurrentPhase
            {
                ExpectedPhase = GamePhase.Combat,
                MatchOverlay = true,
                ExpectedOverlay = PhaseOverlay.Pause,
            };
            Assert.IsTrue(pc.Evaluate(Ctx()));
        }

        [Test]
        public void Evaluate_MatchOverlay_FailsWhenOverlayDiffers()
        {
            _phase.Base = GamePhase.Combat;
            _phase.Overlay = PhaseOverlay.None;
            var pc = new PCCurrentPhase
            {
                ExpectedPhase = GamePhase.Combat,
                MatchOverlay = true,
                ExpectedOverlay = PhaseOverlay.Pause,
            };
            Assert.IsFalse(pc.Evaluate(Ctx()));
        }

        [Test]
        public void Evaluate_OverlayIgnoredWhenMatchOverlayFalse()
        {
            _phase.Base = GamePhase.Combat;
            _phase.Overlay = PhaseOverlay.Pause; // overlay activo, pero el PC no lo exige.
            var pc = new PCCurrentPhase
            {
                ExpectedPhase = GamePhase.Combat,
                MatchOverlay = false,
            };
            Assert.IsTrue(pc.Evaluate(Ctx()));
        }

        // ----------------------------------------------------------------
        // Fake phase service — bypasea la matrix y los eventos del real.
        // ----------------------------------------------------------------
        private sealed class FakePhaseService : IPhaseService
        {
            public GamePhase Base = GamePhase.None;
            public PhaseOverlay Overlay = PhaseOverlay.None;

            public GamePhase CurrentBase => Base;
            public PhaseOverlay CurrentOverlay => Overlay;

            public void ReplacePhase(GamePhase next) => Base = next;
            public void PushOverlay(PhaseOverlay overlay) => Overlay = overlay;
            public void PopOverlay() => Overlay = PhaseOverlay.None;
        }
    }
}
