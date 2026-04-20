using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.Handoff;
using Rollgeon.Combat.Initiative;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Dungeon;
using Rollgeon.Exploration;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Run
{
    /// <summary>
    /// Orchestrator that wires all run-scoped services when a run starts.
    /// Subscribes to <see cref="EventName.OnRunStart"/> and
    /// <see cref="EventName.OnRunEnd"/> to manage the lifecycle.
    /// </summary>
    public sealed class RunController : IRunController
    {
        private readonly FloorLayoutSO _defaultLayout;
        private readonly int? _seedOverride;

        private EventManager.EventReceiver _onRunStartHandler;
        private EventManager.EventReceiver _onRunEndHandler;
        private bool _disposed;

        public bool IsRunActive { get; private set; }

        public RunController(FloorLayoutSO defaultLayout, int? seedOverride = null)
        {
            _defaultLayout = defaultLayout
                ? defaultLayout
                : throw new ArgumentNullException(nameof(defaultLayout));
            _seedOverride = seedOverride;

            _onRunStartHandler = OnRunStart;
            _onRunEndHandler = OnRunEnd;

            EventManager.Subscribe(EventName.OnRunStart, _onRunStartHandler);
            EventManager.Subscribe(EventName.OnRunEnd, _onRunEndHandler);
        }

        /// <summary>
        /// Factory: creates a <see cref="RunController"/> and registers it as
        /// <see cref="IRunController"/> in <see cref="ServiceScope.Global"/>.
        /// </summary>
        public static RunController CreateAndRegister(FloorLayoutSO layout, int? seed = null)
        {
            var controller = new RunController(layout, seed);
            ServiceLocator.AddService<IRunController>(controller, ServiceScope.Global);
            return controller;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_onRunStartHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnRunStart, _onRunStartHandler);
                _onRunStartHandler = null;
            }

            if (_onRunEndHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnRunEnd, _onRunEndHandler);
                _onRunEndHandler = null;
            }

            IsRunActive = false;
        }

        private void OnRunStart(params object[] args)
        {
            // args: [Guid runId, string rulesetId]
            if (args == null || args.Length < 1) return;
            var runId = (Guid)args[0];

            int seed = _seedOverride ?? runId.GetHashCode();

            // 1. Entity registry
            var registry = new InMemoryEntityRegistry();
            ServiceLocator.AddService<InMemoryEntityRegistry>(registry, ServiceScope.Run);

            // 2. Enemy spawn resolver (takes concrete InMemoryEntityRegistry)
            var resolver = new DefaultEnemySpawnResolver(registry);
            ServiceLocator.AddService<IEnemySpawnResolver>(resolver, ServiceScope.Run);

            // 3. Dungeon
            DungeonManager.CreateAndRegister(_defaultLayout, seed);

            // 4. Damage pipeline (parameterless ctor resolves from ServiceLocator)
            var damagePipeline = new DamagePipeline();
            ServiceLocator.AddService<IDamagePipeline>(damagePipeline, ServiceScope.Run);

            // 5. Heal pipeline
            var healPipeline = new HealPipeline();
            ServiceLocator.AddService<IHealPipeline>(healPipeline, ServiceScope.Run);

            // 6. Enemy AI
            var attributes = ServiceLocator.GetService<AttributesManager>();
            var playerService = ServiceLocator.GetService<IPlayerService>();

            Action onTurnComplete;
            if (ServiceLocator.TryGetService<ICombatSignaller>(out var signaller))
            {
                onTurnComplete = signaller.SignalEnemyDone;
            }
            else
            {
                Debug.LogWarning(
                    "[RunController] ICombatSignaller not available — using no-op for enemy turn complete.");
                onTurnComplete = () => { };
            }

            var enemyAI = new BasicEnemyAI(attributes, playerService, damagePipeline, onTurnComplete);
            ServiceLocator.AddService<IEnemyAIHandler>(enemyAI, ServiceScope.Run);

            // 7. Exploration
            ExplorationController.CreateAndRegister();

            // 8. Combat handoff
            CombatHandoffService.CreateAndRegister();

            // 9. Combat return
            CombatReturnService.CreateAndRegister();

            // 10. Begin exploration
            var exploration = ServiceLocator.GetService<IExplorationController>();
            exploration.BeginExploration();

            IsRunActive = true;
        }

        private void OnRunEnd(params object[] args)
        {
            // RunBootstrapper.EndRun already calls ServiceLocator.ClearScope(ServiceScope.Run)
            IsRunActive = false;
        }
    }
}
