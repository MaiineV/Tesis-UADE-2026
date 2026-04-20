using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.Handoff;
using Rollgeon.Combat.Initiative;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Dungeon;
using Rollgeon.Exploration;
using Rollgeon.Heroes;
using Rollgeon.Phase;
using Rollgeon.Player;
using Rollgeon.UI;
using UnityEngine;

namespace Rollgeon.Run.Tests
{
    [TestFixture]
    public class RunControllerTests
    {
        private FloorLayoutSO _layout;
        private PlayerService _playerService;
        private AttributesManager _attributesManager;
        private RunController _controller;
        private ClassHeroSO _hero;
        private readonly List<UnityEngine.Object> _createdObjects = new();

        // -------------------------------------------------------------------
        // Stubs
        // -------------------------------------------------------------------

        private class StubPhaseService : IPhaseService
        {
            public GamePhase CurrentBase { get; private set; }
            public PhaseOverlay CurrentOverlay { get; private set; }
            public void ReplacePhase(GamePhase next) { CurrentBase = next; }
            public void PushOverlay(PhaseOverlay overlay) { CurrentOverlay = overlay; }
            public void PopOverlay() { }
        }

        private class StubScreenManager : IScreenManager
        {
            public IBaseScreen Current { get; private set; }
            public void Push<TScreen>(IScreenPayload payload = null) where TScreen : class, IBaseScreen { }
            public void PushByStringId(string screenId, IScreenPayload payload = null) { }
            public void PopCurrent() { }
            public void PushOverlay<TScreen>(IScreenPayload payload = null) where TScreen : class, IBaseScreen { }
            public void PopOverlay() { }
            public void RegisterScreen(IBaseScreen screen) { }
            public void UnregisterScreen(IBaseScreen screen) { }
        }

        private class StubCombatStarter : ICombatStarter
        {
            public void StartCombat(
                Guid playerId,
                IReadOnlyList<Guid> participants,
                Guid roomInstanceId,
                Action<Guid> enemyActionHandler) { }
        }

        private class StubCombatSignaller : ICombatSignaller
        {
            public int SignalCount { get; private set; }
            public void SignalEnemyDone() { SignalCount++; }
        }

        // -------------------------------------------------------------------
        // Setup / Teardown
        // -------------------------------------------------------------------

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();

            _layout = ScriptableObject.CreateInstance<FloorLayoutSO>();
            _createdObjects.Add(_layout);

            _hero = ScriptableObject.CreateInstance<ClassHeroSO>();
            _createdObjects.Add(_hero);

            _playerService = new PlayerService();
            _attributesManager = new AttributesManager();

            // Register global services that RunController and its children resolve
            ServiceLocator.AddService<IPlayerService>(_playerService, ServiceScope.Global);
            ServiceLocator.AddService<AttributesManager>(_attributesManager, ServiceScope.Global);
            ServiceLocator.AddService<IPhaseService>(new StubPhaseService(), ServiceScope.Global);
            ServiceLocator.AddService<IScreenManager>(new StubScreenManager(), ServiceScope.Global);
            ServiceLocator.AddService<ICombatStarter>(new StubCombatStarter(), ServiceScope.Global);
            ServiceLocator.AddService<ICombatSignaller>(new StubCombatSignaller(), ServiceScope.Global);
        }

        [TearDown]
        public void TearDown()
        {
            _controller?.Dispose();
            _playerService?.Dispose();
            _attributesManager?.Dispose();

            foreach (var obj in _createdObjects)
            {
                if (obj != null)
                    UnityEngine.Object.DestroyImmediate(obj);
            }
            _createdObjects.Clear();

            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        private RunController CreateController(int? seed = 42)
        {
            _controller = new RunController(_layout, seed);
            return _controller;
        }

        private void StartRun()
        {
            RunBootstrapper.StartRun(_hero, null, Guid.NewGuid());
        }

        // -------------------------------------------------------------------
        // Tests
        // -------------------------------------------------------------------

        [Test]
        public void OnRunStart_RegistersAllRunServices()
        {
            CreateController();

            StartRun();

            Assert.IsTrue(ServiceLocator.HasService<IDungeonService>(),
                "IDungeonService should be registered");
            Assert.IsTrue(ServiceLocator.HasService<IExplorationController>(),
                "IExplorationController should be registered");
            Assert.IsTrue(ServiceLocator.HasService<IDamagePipeline>(),
                "IDamagePipeline should be registered");
            Assert.IsTrue(ServiceLocator.HasService<IHealPipeline>(),
                "IHealPipeline should be registered");
            Assert.IsTrue(ServiceLocator.HasService<IEnemyAIHandler>(),
                "IEnemyAIHandler should be registered");
            Assert.IsTrue(ServiceLocator.HasService<IEnemySpawnResolver>(),
                "IEnemySpawnResolver should be registered");
            Assert.IsTrue(ServiceLocator.HasService<InMemoryEntityRegistry>(),
                "InMemoryEntityRegistry should be registered");
            Assert.IsTrue(ServiceLocator.HasService<ICombatHandoffService>(),
                "ICombatHandoffService should be registered");
            Assert.IsTrue(ServiceLocator.HasService<ICombatReturnService>(),
                "ICombatReturnService should be registered");
        }

        [Test]
        public void OnRunStart_SetsIsRunActiveTrue()
        {
            CreateController();

            StartRun();

            Assert.IsTrue(_controller.IsRunActive);
        }

        [Test]
        public void OnRunStart_BeginsExploration()
        {
            CreateController();

            StartRun();

            var exploration = ServiceLocator.GetService<IExplorationController>();
            Assert.IsTrue(exploration.IsExploring,
                "Exploration should be active after run start");
        }

        [Test]
        public void OnRunEnd_SetsIsRunActiveFalse()
        {
            CreateController();
            var runId = Guid.NewGuid();
            RunBootstrapper.StartRun(_hero, null, runId);
            Assert.IsTrue(_controller.IsRunActive);

            RunBootstrapper.EndRun(runId);

            Assert.IsFalse(_controller.IsRunActive);
        }

        [Test]
        public void Dispose_UnsubscribesFromEvents()
        {
            CreateController();
            _controller.Dispose();

            // After dispose, starting a run should NOT register run-scoped services
            StartRun();

            Assert.IsFalse(ServiceLocator.HasService<IDungeonService>(),
                "After Dispose, RunController should not react to OnRunStart");
        }

        [Test]
        public void Constructor_NullLayout_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new RunController(null));
        }
    }
}
