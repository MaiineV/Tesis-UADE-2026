using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.Rolls;
using Rollgeon.Combat.Handoff;
using Rollgeon.Combat.Initiative;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Dungeon;
using Rollgeon.Economy;
using Rollgeon.Entities;
using Rollgeon.Entities.Portraits;
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

            // 2a-bis. Portraits — lookup guid→sprite para el turn order HUD (y futuras
            //         UIs tipo bestiario). El resolver de spawn lo puebla por enemigo;
            //         el player se resuelve lazy vía IPlayerService.
            var portraits = EntityPortraitResolver.CreateAndRegister();

            // RunContext (registrado por RunBootstrapper antes de OnRunStart) alimenta
            // el tier determinístico por piso del resolver y el layout inicial (abajo).
            ServiceLocator.TryGetService<IRunContextService>(out var runContext);

            var resolver = new DefaultEnemySpawnResolver(
                registry, attributes, aiRegistry, grid, visuals, goldDrops, portraits, runContext);
            ServiceLocator.AddService<IEnemySpawnResolver>(resolver, ServiceScope.Run);

            // 2b. Register the player hero in both registries. Without this, combat
            //     pipelines discard damage on the player ("Entity not registered") and
            //     the turn order falls back to the bottom-of-queue sentinel. EnemyDataSO
            //     spawns are handled by the resolver above; the hero has no spawner yet,
            //     so RunController does it via the selected ClassHeroSO's base stats.
            var playerService = ServiceLocator.GetService<IPlayerService>();
            RegisterPlayer(playerService, registry, attributes);

            // 3. Dungeon — el tutorial usa el piso fijo autorado (plan explícito);
            //    el flujo normal, la topología random del layout default. Si el flag
            //    viene seteado pero la config no está lanzable, degrada a run normal.
            bool isTutorial = PendingRunRequest.IsTutorial;
            Rollgeon.Tutorial.TutorialConfigSO tutorialConfig = null;
            if (isTutorial
                && (!ServiceLocator.TryGetService(out tutorialConfig)
                    || tutorialConfig == null || !tutorialConfig.IsLaunchable))
            {
                Debug.LogWarning(
                    "[RunController] PendingRunRequest.IsTutorial pero TutorialConfigSO no está " +
                    "registrado/completo — degradando a run normal.");
                isTutorial = false;
            }

            // En run nueva FloorIndex es 0 → layout = _defaultLayout y seed = base
            // (idéntico a antes). En resume, el RunContext ya restauró FloorIndex
            // (StartRun lo registra antes de disparar OnRunStart) y el fast-forward
            // de la cadena NextFloor + el seed derivado regeneran el piso guardado
            // idéntico.
            int startFloorIndex = runContext != null ? runContext.FloorIndex : 0;
            var startLayout = FloorProgressionService.ResolveLayoutForFloor(_defaultLayout, startFloorIndex);
            int startFloorSeed = startFloorIndex == 0
                ? seed
                : FloorProgressionService.DeriveSeed(seed, startFloorIndex);

            DungeonManager dungeon;
            if (isTutorial)
            {
                dungeon = DungeonManager.CreateAndRegisterFromPlan(tutorialConfig.FloorPlan.ToPlan());
            }
            else
            {
                dungeon = DungeonManager.CreateAndRegister(startLayout, startFloorSeed);
            }

            // 3a-bis. Persistencia de dungeon (#0028): el DungeonManager es ISaveable.
            //   En resume, Register auto-stagea el snapshot cacheado (LoadFromDisk lo
            //   pobló en el menú) y ResumeFromSave lo aplica sobre la topología ya
            //   generada (match por GridCell) + reubica al player. Tutorial no resume.
            global::Patterns.Save.SaveSystem.Register(dungeon);
            if (RunBootstrapper.IsResuming && !isTutorial)
            {
                dungeon.ResumeFromSave();
                // El próximo spawn de la sala actual (cuando arranque el combate) usa las
                // posiciones + GUIDs guardados en vez de reposicionar random (#0028 Fase 2).
                resolver.ResumeFromSaveNextSpawn = true;
            }

            // 3a-ter. Estado de combate en curso (#0028 Fase 3): el CombatResumeService es el
            //   ISaveable (run.combat_state) y el ICombatResumeCoordinator que CombatEnterState
            //   consulta al arrancar el combate. En resume, Register auto-stagea el snapshot;
            //   TryBeginResume lo aplica cuando la FSM levanta la pelea de la sala guardada.
            var combatResume = new CombatResumeService();
            ServiceLocator.AddService<Rollgeon.Combat.Resume.ICombatResumeCoordinator>(
                combatResume, ServiceScope.Run);
            global::Patterns.Save.SaveSystem.Register(combatResume);

            // 3b. Floor shells visibility — toggles prefab vs shells según camera floor view.
            FloorShellVisibilityController.CreateAndRegister();

            // 3c. Floor progression — orquesta la transición multi-piso (#158). Recibe el
            //     layout actual + el seed base de la run; deriva el seed de cada piso
            //     siguiente con el FloorIndex absoluto.
            //     En tutorial NO se registra: el fin de piso lo maneja TutorialFlowController
            //     (teardown → fresh run) en vez de avanzar a otro piso.
            if (!isTutorial)
            {
                FloorProgressionService.CreateAndRegister(startLayout, seed);
            }

            // 4. Damage pipeline (parameterless ctor resolves from ServiceLocator)
            var damagePipeline = new DamagePipeline();
            ServiceLocator.AddService<IDamagePipeline>(damagePipeline, ServiceScope.Run);

            // 5. Heal pipeline — resolver de max HP: player vía hero.BaseMaxHp,
            //    enemigos vía EnemyDataRegistry (cuando exista) o BaseHP del SO. Para FP
            //    el único heal en uso es la poción del player, así que sólo cubrimos ese
            //    caso explícitamente; el fallback int.MaxValue queda para enemigos heal
            //    upstream.
            var healPipeline = new HealPipeline(attributes, BuildMaxHpResolver(playerService));
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

            // 11. Tutorial flow — se crea ÚLTIMO a propósito: sus handlers de eventos
            //     (OnCombatEnd, OnRoomEntered) deben correr después de los del
            //     DungeonManager (suscripto antes) para leer el estado ya actualizado.
            if (isTutorial)
            {
                Rollgeon.Tutorial.TutorialFlowController.CreateAndRegister(tutorialConfig, runId);
            }

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
            // BUG-022: máximo separado del actual — los rewards del jefe (canal Character)
            // suben el max vía modifiers sobre MaxHealth; Health.Value queda como HP actual.
            playerAttrs.SetAttribute<MaxHealth>(new MaxHealth(hero.BaseMaxHp));
            playerAttrs.SetAttribute<Speed>(new Speed(hero.BaseSpeed));
            playerAttrs.SetAttribute<Shield>(new Shield(0));
            // Attack = dmg_base_PJ (Spec Daño v2): piso garantizado del turno, aplica incluso sin combo.
            if (hero.BaseAttack <= 0)
            {
                Debug.LogWarning(
                    $"[RunController] '{hero.name}' tiene BaseAttack={hero.BaseAttack}. " +
                    "Spec Daño v2 — dmg_base_PJ nunca debería ser 0.");
            }
            playerAttrs.SetAttribute<Attack>(new Attack(hero.BaseAttack));

            registry.Register(playerService.PlayerGuid, playerAttrs);
            attributes.Register(playerService.PlayerGuid, playerAttrs);
            _registeredPlayerId = playerService.PlayerGuid;

            // Passive — §4.4.1: bind hero passive to the player entity.
            _playerEntity = new Entity { InstanceId = playerService.PlayerGuid };
            if (hero.Passive != null)
                _playerEntity.BindPassive(hero.Passive);

            // Cachea el player en el pool de rolls: RollPoolService.OnRunStartExternal
            // solo resetea _playerId, y el caller (esta funcion) tiene que llamar
            // InitializeForEntity con el Guid real. El pool queda en 0 hasta el primer
            // OnCombatStart.
            if (ServiceLocator.TryGetService<IRollPoolService>(out var rollPool) && rollPool != null)
            {
                rollPool.InitializeForEntity(playerService.PlayerGuid);
            }

            // Después del pool: el auto-restore de Register (resume) debe ver todos
            // los stats para pisar los valores base con los guardados.
            var attrsSaveable = new PlayerAttributesSaveable(playerAttrs);
            ServiceLocator.AddService<PlayerAttributesSaveable>(attrsSaveable, ServiceScope.Run);
            global::Patterns.Save.SaveSystem.Register(attrsSaveable);

            // Snapshot del pool de rolls (bonus +N por turno de los rewards). Después
            // de InitializeForEntity, por el mismo motivo que attrsSaveable.
            var rollPoolSaveable = new RollPoolSaveable();
            ServiceLocator.AddService<RollPoolSaveable>(rollPoolSaveable, ServiceScope.Run);
            global::Patterns.Save.SaveSystem.Register(rollPoolSaveable);

            // En resume el inventario viene del save — regalar de nuevo duplicaría.
            if (!RunBootstrapper.IsResuming)
            {
                GrantStartingItems(hero);
            }
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

        /// <summary>
        /// Construye el resolver de max HP que el <see cref="HealPipeline"/> usa para
        /// clampear el heal contra el HP máximo. Para el player, resuelve vía
        /// <see cref="Rollgeon.Player.PlayerMaxHp"/> (base + grants in-run, BUG-022).
        /// Para otros guids, devuelve un cap permisivo (los enemigos hoy no se curan
        /// en gameplay del FP).
        /// </summary>
        private static Func<Guid, int> BuildMaxHpResolver(IPlayerService playerService)
        {
            return guid =>
            {
                if (playerService != null && playerService.PlayerGuid == guid)
                {
                    int resolved = Rollgeon.Player.PlayerMaxHp.Resolve(guid);
                    if (resolved > 0) return resolved;
                }
                return int.MaxValue;
            };
        }
    }
}
