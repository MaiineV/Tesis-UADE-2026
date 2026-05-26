using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.FSM;
using Rollgeon.Dungeon;
using Rollgeon.Exploration;
using Rollgeon.Heroes;
using Rollgeon.Player;
using Rollgeon.UI;

namespace Rollgeon.Combat.Handoff.Tests
{
    [TestFixture]
    public class CombatReturnServiceTests
    {
        private StubExplorationController _stubExploration;
        private SpyScreenManager _spyScreen;
        private StubPlayerService _stubPlayer;
        private CombatReturnService _service;

        // -------------------------------------------------------------------
        // Stubs / Spies
        // -------------------------------------------------------------------

        private class StubExplorationController : IExplorationController
        {
            public bool IsExploring { get; set; }
            public int BeginExplorationCallCount { get; private set; }
            public int ResumeAfterCombatCallCount { get; private set; }

            public void BeginExploration() => BeginExplorationCallCount++;
            public void ResumeAfterCombat() => ResumeAfterCombatCallCount++;
        }

        private class SpyScreenManager : IScreenManager
        {
            public IBaseScreen Current { get; private set; }
            public int PopCurrentCallCount { get; private set; }
            public int PushByStringIdCallCount { get; private set; }
            public string LastScreenId { get; private set; }
            public IScreenPayload LastPayload { get; private set; }

            public void Push<TScreen>(IScreenPayload payload = null) where TScreen : class, IBaseScreen { }
            public void PushByStringId(string screenId, IScreenPayload payload = null)
            {
                PushByStringIdCallCount++;
                LastScreenId = screenId;
                LastPayload = payload;
            }
            public void PopCurrent() => PopCurrentCallCount++;
            public void PushOverlay<TScreen>(IScreenPayload payload = null) where TScreen : class, IBaseScreen { }
            public void PopOverlay() { }
            public void RegisterScreen(IBaseScreen screen) { }
            public void UnregisterScreen(IBaseScreen screen) { }
        }

        private class StubPlayerService : IPlayerService
        {
            public Guid PlayerGuid { get; set; } = Guid.NewGuid();
            public Guid RunId { get; set; } = Guid.NewGuid();
            public ClassHeroSO CurrentHero { get; set; }
            public Rollgeon.Dice.DiceBagSO DiceBag { get; set; }
            public void SetPlayer(ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(Rollgeon.Dice.DiceBagSO bag) { DiceBag = bag; }
            public void ClearPlayer() { }
            public event Action<ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;
        }

        // -------------------------------------------------------------------
        // Setup / Teardown
        // -------------------------------------------------------------------

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _stubExploration = new StubExplorationController();
            _spyScreen = new SpyScreenManager();
            _stubPlayer = new StubPlayerService();

            _service = new CombatReturnService(_stubExploration, _spyScreen, _stubPlayer);
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        private void TriggerCombatEnd(Guid roomInstanceId, CombatOutcome outcome)
        {
            EventManager.Trigger(EventName.OnCombatEnd, roomInstanceId, outcome);
        }

        // -------------------------------------------------------------------
        // Tests
        // -------------------------------------------------------------------

        [Test]
        public void OnCombatEnd_Victory_PopsScreen()
        {
            var roomId = Guid.NewGuid();
            TriggerCombatEnd(roomId, CombatOutcome.Victory);

            Assert.AreEqual(1, _spyScreen.PopCurrentCallCount);
        }

        [Test]
        public void OnCombatEnd_Victory_ResumesExploration()
        {
            TriggerCombatEnd(Guid.NewGuid(), CombatOutcome.Victory);

            Assert.AreEqual(1, _stubExploration.ResumeAfterCombatCallCount);
        }

        [Test]
        public void OnCombatEnd_Defeat_PopsScreen()
        {
            TriggerCombatEnd(Guid.NewGuid(), CombatOutcome.Defeat);

            Assert.AreEqual(1, _spyScreen.PopCurrentCallCount);
        }

        [Test]
        public void OnCombatEnd_Defeat_FiresOnPlayerDefeated()
        {
            Guid received = Guid.Empty;
            EventManager.Subscribe(EventName.OnPlayerDefeated, (object[] args) =>
            {
                received = (Guid)args[0];
            });

            TriggerCombatEnd(Guid.NewGuid(), CombatOutcome.Defeat);

            Assert.AreEqual(_stubPlayer.RunId, received);
        }

        [Test]
        public void OnCombatEnd_Defeat_DoesNotResumeExploration()
        {
            TriggerCombatEnd(Guid.NewGuid(), CombatOutcome.Defeat);

            Assert.AreEqual(0, _stubExploration.ResumeAfterCombatCallCount);
        }

        [Test]
        public void OnCombatEnd_Aborted_ResumesExploration()
        {
            var roomId = Guid.NewGuid();
            TriggerCombatEnd(roomId, CombatOutcome.Aborted);

            Assert.AreEqual(1, _stubExploration.ResumeAfterCombatCallCount);
            Assert.AreEqual(1, _spyScreen.PopCurrentCallCount);
        }

        [Test]
        public void Dispose_UnsubscribesFromEvent()
        {
            _service.Dispose();

            TriggerCombatEnd(Guid.NewGuid(), CombatOutcome.Victory);

            Assert.AreEqual(0, _spyScreen.PopCurrentCallCount,
                "After Dispose, handler should not be called");
            Assert.AreEqual(0, _stubExploration.ResumeAfterCombatCallCount,
                "After Dispose, exploration should not resume");
        }
    }
}
