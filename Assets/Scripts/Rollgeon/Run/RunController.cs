using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.Energy;
using Rollgeon.Combat.Handoff;
using Rollgeon.Combat.Initiative;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Dungeon;
using Rollgeon.Economy;
using Rollgeon.Entities;
using Rollgeon.Exploration;
using Rollgeon.Items;
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
        private Guid _registeredPlayerId;
        private Entity _playerEntity;

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

            // 2. Enemy spawn resolver — registra spawns en InMemoryEntityRegistry
            //    (initiative / turn order), AttributesManager (stat reads del AI y
            //    pipelines de daño), IEnemyAIRegistry (árbol clonado por enemigo),
            //    IGridManager (placement) y IEntityVisualService (GameObject pawn).
            var attributes = ServiceLocator.GetService<AttributesManager>();
            ServiceLocator.TryGetService<IEnemyAIRegistry>(out var aiRegistry);
            ServiceLocator.TryGetService<Rollgeon.Grid.IGridManager>(out var grid);
            ServiceLocator.TryGetService<Rollgeon.Entities.Visuals.IEntityVisualService>(out var visuals);

            // 2a. Gold drops — escucha OnEntityDestroyed y suma al IEconomyService.
            //     El resolver le reporta el drop rolled al spawnear cada enemigo.
            EnemyGoldDropService goldDrops = null;
            if (ServiceLocator.TryGetService<IEconomyService>(out var economy) && economy != null)
            {
                goldDrops = new EnemyGoldDropService(economy);
                ServiceLocator.AddService<EnemyGoldDropService>(goldDrops, ServiceScope.Run);
            }
            else
            {
                Debug.LogWarning(
                    "[RunController] IEconomyService no registrado — los enemigos no van a dropear oro este run.");
            }

            var resolver = new DefaultEnemySpawnResolver(registry, attributes, aiRegistry, grid, visuals, goldDrops);
            ServiceLocator.AddService<IEnemySpawnResolver>(resolver, ServiceScope.Run);

            // 2b. Register the player hero in both registries. Without this, combat
            //     pipelines discard damage on the player ("Entity not registered") and
            //     the turn order falls back to the bottom-of-queue sentinel. EnemyDataSO
            //     spawns are handled by the resolver above; the hero has no spawner yet,
            //     so RunController does it via the selected ClassHeroSO's base stats.
            var playerService = ServiceLocator.GetService<IPlayerService>();
            RegisterPlayer(playerService, registry, attributes);

            // 3. Dungeon
            DungeonManager.CreateAndRegister(_defaultLayout, seed);

            // 3b. Floor shells visibility — toggles prefab vs shells según camera floor view.
            FloorShellVisibilityController.CreateAndRegister();

            // 4. Damage pipeline (parameterless ctor resolves from ServiceLocator)
            var damagePipeline = new DamagePipeline();
            ServiceLocator.AddService<IDamagePipeline>(damagePipeline, ServiceScope.Run);

            // 5. Heal pipeline
            var healPipeline = new HealPipeline();
            ServiceLocator.AddService<IHealPipeline>(healPipeline, ServiceScope.Run);

            // 5b. Shield reset handler
            var shieldReset = new ShieldResetHandler(attributes);
            ServiceLocator.AddService<ShieldResetHandler>(shieldReset, ServiceScope.Run);

            // 6. Enemy AI — reutiliza attributes + playerService resueltos arriba.
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

            // BasicEnemyAI sigue siendo el fallback cuando un enemigo no tiene AIRoot autorado.
            var basicAI = new BasicEnemyAI(attributes, playerService, damagePipeline, onTurnComplete);

            IEnemyAIHandler aiHandler;
            if (aiRegistry != null)
            {
                aiHandler = new TreeDrivenEnemyAI(aiRegistry, attributes, playerService,
                    damagePipeline, basicAI, onTurnComplete);
            }
            else
            {
                Debug.LogWarning(
                    "[RunController] IEnemyAIRegistry not registered — enemies use BasicEnemyAI fallback only.");
                aiHandler = basicAI;
            }
            ServiceLocator.AddService<IEnemyAIHandler>(aiHandler, ServiceScope.Run);

            // 7. Exploration
            ExplorationController.CreateAndRegister();

            // 8. Combat handoff
            CombatHandoffService.CreateAndRegister();

            // 8b. Exploration behavior dispatch
            ExplorationBehaviorService.CreateAndRegister();

            // 9. Combat return
            CombatReturnService.CreateAndRegister();

            // 9b. Death watcher
            CombatDeathWatcher.CreateAndRegister();

            // 10. Begin exploration
            var exploration = ServiceLocator.GetService<IExplorationController>();
            exploration.BeginExploration();

            IsRunActive = true;
        }

        private void OnRunEnd(params object[] args)
        {
            // RunBootstrapper.EndRun already calls ServiceLocator.ClearScope(ServiceScope.Run).
            // AttributesManager is Global scope, so the player entry we added in OnRunStart
            // must be unregistered explicitly to avoid stale GUIDs leaking across runs.
            if (_registeredPlayerId != Guid.Empty
                && ServiceLocator.TryGetService<AttributesManager>(out var attributes)
                && attributes != null)
            {
                attributes.Unregister(_registeredPlayerId);
            }
            _registeredPlayerId = Guid.Empty;

            _playerEntity?.Dispose();
            _playerEntity = null;

            IsRunActive = false;
        }

        private void RegisterPlayer(
            IPlayerService playerService,
            InMemoryEntityRegistry registry,
            AttributesManager attributes)
        {
            if (playerService == null || attributes == null || registry == null) return;
            if (playerService.CurrentHero == null) return;
            if (playerService.PlayerGuid == Guid.Empty) return;

            var hero = playerService.CurrentHero;
            var playerAttrs = new ModifiableAttributes();
            playerAttrs.EnsureInitialized();
            playerAttrs.SetAttribute<Health>(new Health(hero.BaseMaxHp));
            playerAttrs.SetAttribute<Speed>(new Speed(hero.BaseSpeed));
            playerAttrs.SetAttribute<Shield>(new Shield(0));

            registry.Register(playerService.PlayerGuid, playerAttrs);
            attributes.Register(playerService.PlayerGuid, playerAttrs);
            _registeredPlayerId = playerService.PlayerGuid;

            // Passive — §4.4.1: bind hero passive to the player entity.
            _playerEntity = new Entity { InstanceId = playerService.PlayerGuid };
            if (hero.Passive != null)
                _playerEntity.BindPassive(hero.Passive);

            // Hidrata Energy: EnergyService.OnRunStartExternal solo resetea _playerId,
            // y el caller (esta funcion) tiene que llamar InitializeForEntity con el
            // Guid real. Ver EnergyService.InitializeForEntity doc-comment.
            if (ServiceLocator.TryGetService<IEnergyService>(out var energy) && energy != null)
            {
                energy.InitializeForEntity(playerService.PlayerGuid);
            }

            GrantStartingItems(hero);
        }

        private static void GrantStartingItems(Rollgeon.Heroes.ClassHeroSO hero)
        {
            if (hero?.StartingItems == null || hero.StartingItems.Count == 0) return;

            if (!ServiceLocator.TryGetService<IInventoryService>(out var inventory) || inventory == null)
            {
                Debug.LogWarning(
                    "[RunController] IInventoryService no registrado — los StartingItems del hero no se entregan.");
                return;
            }

            foreach (var item in hero.StartingItems)
            {
                if (item == null) continue;
                if (!inventory.AddItem(item))
                {
                    Debug.LogWarning(
                        $"[RunController] No se pudo agregar StartingItem '{item.ItemId}' (¿inventario lleno?).");
                }
            }
        }
    }
}
