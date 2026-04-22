# Rollgeon — Documentación Técnica

> **Propósito.** Documento técnico vivo de Rollgeon (roguelike de dados + clases con Contrato de Generala, ver `Game Design.pdf`). Describe los sistemas de programación, patrones y estructuras de datos que componen el proyecto. Cada sección es independiente, con cross‑references por número.

---

## Índice

0.  [Convenciones y stack](#0-convenciones-y-stack)
1.  [Patrones base](#1-patrones-base) — ServiceLocator (con preload), EventManager, FSM
2.  [Sistema de Atributos](#2-sistema-de-atributos) — `IAttribute`, `IModifiable`, `ModifiableAttributes`
3.  [Sistema de Modificadores](#3-sistema-de-modificadores) — `Modifier<T>` con GUID, dirección y lifetime
4.  [Clases y Hero Sheets](#4-clases-y-hero-sheets) — `ClassHeroSO`, stats tipadas, pasiva
5.  [Contrato de Generala + Combos](#5-contrato-de-generala--combos) — combos, counters, strike
6.  [Dados y Bolsa](#6-dados-y-bolsa) — dados, encantamientos, reroll budget
7.  [Entidades y Behaviors](#7-entidades-y-behaviors) — `BaseEntitySO` parent común, `EnemyDataSO`, `PropEntitySO`, `NpcDataSO`, `BaseBehavior` con `AllowedPhases` + `BehaviorSlot` + `BehaviorLibrarySO`, `BehaviorContext` polimórfico por trigger, AI decision trees, `DialogueGraphSO`, interacción con prompts y per‑phase rules
8.  [Effects + PreConditions](#8-effects--preconditions)
9.  [Behavior Values (runtime bag)](#9-behavior-values-runtime-value-bag)
10. [Sistema de Feedback](#10-sistema-de-feedback)
11. [Selection / Targeting](#11-selection--targeting)
12. [Combate: pipeline de daño](#12-combate-pipeline-de-daño) — pipeline, action economy, turn order
13. [Dungeon, Salas y Generación Procedural](#13-dungeon-salas-y-generación-procedural)
14. [Meta‑progresión y Unlocks](#14-meta-progresión-y-unlocks) — unlocks + `RulesetSO`
15. [Sistema de Save / Persistencia](#15-sistema-de-save--persistencia)
16. [Packages del proyecto](#16-packages-del-proyecto)
17. [Sistemas transversales](#17-sistemas-transversales)
    - A. [Audio](#a-audio) — `IAudioService`, pool, mixer, integración con feedback
    - B. [Movimiento y Pathfinding](#b-movimiento-y-pathfinding) — `IMovementService`, BFS, movement como acción
    - C. [Craps — Mini‑juego de apuesta (overlay de combate)](#c-craps--mini-juego-de-apuesta-overlay-de-combate) — `PhaseOverlay.Craps`, exclusivo de combate, reusa combo system
    - D. [UI architecture + ScreenManager](#d-ui-architecture--screenmanager) — stack de screens, eventos, sin builders, `ItemInspectView`
    - E. [Cámara](#e-cámara) — `ICameraService`, rotación snapped, pan, zoom, wall occlusion, floor view
    - F. [Shop — ítems en el piso](#f-shop--ítems-en-el-piso-estilo-isaac) — `IShopManagerService`, `ShopPoolSO`, `ShopConfigSO`, sin modal
    - G. [`IPlayerService`](#g-iplayerservice--acceso-centralizado-al-jugador) — acceso centralizado al jugador activo
    - H. [`PawnRegistryService`](#h-pawnregistryservice--lookup-de-transforms-por-guid) — lookup de transforms por GUID
    - I. [`GridManager`](#i-gridmanager--grilla-de-la-sala-activa) — grilla lógica de la sala
    - J. [`IInputService`](#j-iinputservice--gestión-de-action-maps) — gestión de action maps por fase + tabla de bindings
    - K. [Scene Management](#k-scene-management--carga-aditiva-async) — carga aditiva async de pisos, UI persistente
    - L. [Enums comunes](#l-enums-comunes-no-declarados-en-otras-secciones) — `RunOutcome`, `CombatOutcome`, `FloatingNumberType`, `ComparisonOp`
    - M. [Heal Pipeline](#m-heal-pipeline) — pipeline centralizada de curación simétrica al DamagePipeline
    - N. [Cutscene overlay](#n-cutscene-overlay--flujo-básico) — `ICutsceneService`, `CutsceneDataSO`, flujo de cutscenes
    - O. [`IRngService`](#o-irngservice--aleatoriedad-determinista-por-sub-stream) — aleatoriedad determinista por sub-stream
18. [Sistema de Items e Inventario](#18-sistema-de-items-e-inventario)
19. [Sistema de Rewards / Loot](#19-sistema-de-rewards--loot)
20. [Sistema de Status Effects](#20-sistema-de-status-effects)
21. [Sistema de Quests](#21-sistema-de-quests)
22. [Tutorial / Onboarding](#22-tutorial--onboarding)
23. [Settings y Accessibility](#23-settings-y-accessibility)
24. [Object Pooling](#24-object-pooling)
25. [Analytics y Telemetría](#25-analytics-y-telemetría)
26. [Content Tooling](#26-content-tooling--pipeline-de-autoría-para-diseñadores)
27. [Convenciones de cross‑ref y changelog](#27-convenciones-de-cross-ref-y-changelog)

---

## 0. Convenciones y stack

- **Engine**: Unity + URP.
- **Inspector/Serialización**: Odin Inspector. La mayoría de los assets de dominio son `SerializedScriptableObject` para soportar `Dictionary<Type, …>`, listas polimórficas arrastrables y filtros de tipo en editor. Ver §16 para listado completo de packages.
- **Single‑player**. No hay networking, no hay RPCs, no hay sincronización cliente/servidor. Todo el pipeline (input → resolve → feedback) corre local.
- **Persistencia unificada**. Un único sistema de save (§15) con cache en memoria y flush a JSON por triggers configurables. Cualquier componente que necesite persistir implementa `ISaveable` y se auto‑registra.
- **IDs como dropdowns**. Regla transversal: **todo string ID expuesto al editor** (EntityId, RoomId, UnlockId, FeedbackId, ComboId, DiceEnchantmentId, …) se edita con `[ValueDropdown(...)]` alimentado desde un catálogo SO central. Nunca se escribe a mano. Evita typos y hace refactor `id → id` rastreable.
- **Namespaces** (propuestos):
  - `Patterns` → infraestructura (ServiceLocator, EventManager, FSM, Save).
  - `Rollgeon.Attributes` → `IAttribute`, `IModifiable`, `Modifier<T>`, contenedores.
  - `Rollgeon.Heroes` → `ClassHeroSO`, `ContractSheet`, pasivas.
  - `Rollgeon.Combos` → `BaseComboSO` + concretos.
  - `Rollgeon.Dice` → tipos de dado, bolsa, encantamientos.
  - `Rollgeon.Entities` → `BaseEntitySO`, `EnemyDataSO`, `PropEntitySO`, `NpcDataSO`, `BaseBehavior`, `BehaviorLibrarySO`, `DialogueGraphSO`.
  - `Rollgeon.Effects`, `Rollgeon.PreConditions` → pipeline de efectos.
  - `Rollgeon.Feedback` → FX/anim pipeline local.
  - `Rollgeon.Dungeon` → salas, layout, generación procedural.
  - `Rollgeon.Meta` → unlocks, save, run records.
  - `Rollgeon.Items` → `ItemSO`, `IInventoryService`, catálogo.
  - `Rollgeon.Rewards` → `RewardEntrySO`, `IRewardService`, pools.
  - `Rollgeon.StatusEffects` → `StatusEffectSO`, `IStatusEffectService`.
  - `Rollgeon.Quests` → `QuestSO`, `IQuestService`.
  - `Rollgeon.Tutorial` → `TutorialStepSO`, `ITutorialService`.
  - `Rollgeon.Analytics` → `IAnalyticsService`, telemetría.
  - `Rollgeon.Audio` → `IAudioService`, `AudioManager`, pool de sources, mixer.
  - `Rollgeon.Camera` → `ICameraService`, `CameraConfigSO`, wall occlusion, floor view.
  - `Rollgeon.Movement` → `IMovementService`, BFS pathfinding, `GridManager`.
  - `Rollgeon.Shop` → `IShopManagerService`, `ShopPoolSO`, `ShopConfigSO`.
  - `Rollgeon.Craps` → `ICrapsSessionService`, `CrapsConfigSO`.
  - `Rollgeon.Pool` → `IPoolService`, `ComponentPool<T>`.
  - `Rollgeon.Settings` → `ISettingsService`, `GameSettingsSO`.
  - `Rollgeon.Input` → `IInputService`, action maps, bindings.
  - `Rollgeon.Scene` → `ISceneService`, carga aditiva async de pisos y UI.
  - `Rollgeon.Player` → `IPlayerService`, acceso centralizado al jugador activo.
- **Directorios**:
  ```
  Assets/Rollgeon/
    Heroes/           — ClassHeroSO assets
    Combos/           — BaseComboSO assets
    Dice/             — DiceType, DiceBagSO, DiceEnchantmentSO
    Entities/         — EnemyDataSO, PropEntitySO, NpcDataSO, BehaviorLibrarySO, DialogueGraphSO
    Effects/          — BaseEffect concretes + PreConditions
      Queries/        — BaseTargetQuery + TQ_* concretes (§11.2b)
    Feedback/         — FeedbackDBSO + runtime
    Dungeon/          — RoomSO, RoomPrefab, FloorLayoutSO, pools
    Meta/             — BaseUnlockSO + ClassUnlock/DiceUnlock/ItemUnlock/PassiveUnlock
    Items/            — ItemSO, ItemCatalogSO
    Rewards/          — RewardEntrySO, RewardCatalogSO
    StatusEffects/    — StatusEffectSO, StatusCatalogSO
    Quests/           — QuestSO, QuestCatalogSO
    Tutorial/         — TutorialStepSO assets
    Audio/            — AudioSettingsSO, BiomeMusicEntry assets
    Camera/           — CameraConfigSO
    Shop/             — ShopPoolSO, ShopConfigSO
    Craps/            — CrapsConfigSO
    Settings/         — GameSettingsSO
    Actions/          — ActionDefinitionSO, ActionCatalogSO
    Rulesets/         — RulesetSO, RulesetCatalogSO
    Save/             — SaveSettingsSO, runtime
    Catalogs/         — CatalogSOs centrales para dropdowns (IDs)
  Assets/Scripts/     — código runtime (managers, FSM, pipelines)
  ```

---

## 1. Patrones base

### 1.1 `ServiceLocator` con precarga

Registro estático `Type → instance`. Para Rollgeon se extiende con un **bootstrap** que permite pre‑cargar todos los SOs necesarios antes de que empiece la partida, sin depender de `Awake`/`OnEnable` de MonoBehaviours.

```csharp
public enum ServiceScope
{
    /// <summary>Persiste toda la sesión del juego (catalogs, settings, managers globales).</summary>
    Global,
    /// <summary>Persiste solo durante una run. Se limpia al terminar la run sin afectar los Global.</summary>
    Run,
}

public static class ServiceLocator
{
    private static readonly Dictionary<Type, (object instance, ServiceScope scope)> Services = new();

    public static void AddService<T>(object instance, ServiceScope scope = ServiceScope.Global)
        => Services[typeof(T)] = (instance, scope);

    public static T    GetService<T>() => (T)Services[typeof(T)].instance;
    public static bool TryGetService<T>(out T service)
    {
        if (Services.TryGetValue(typeof(T), out var entry)) { service = (T)entry.instance; return true; }
        service = default; return false;
    }
    public static void RemoveService<T>() => Services.Remove(typeof(T));
    public static bool HasService<T>() => Services.ContainsKey(typeof(T));

    /// <summary>
    /// Limpia solo los servicios registrados con el scope dado.
    /// Llamar ClearScope(Run) al terminar una run limpia TurnManager,
    /// DiceRoller, etc. sin tocar los catalogs y settings (Global).
    /// </summary>
    public static void ClearScope(ServiceScope scope)
    {
        var toRemove = Services
            .Where(kvp => kvp.Value.scope == scope)
            .Select(kvp => kvp.Key)
            .ToList();
        foreach (var key in toRemove) Services.Remove(key);
    }

    /// <summary>Limpia todo — solo usar en shutdown o tests.</summary>
    public static void Clear() => Services.Clear();
}
```

**Regla de scope.**
- **`Global`** (default) — catalogs (`EntityCatalogSO`, `ComboCatalogSO`, …), settings (`SaveSettingsSO`, `CameraConfigSO`), managers de infraestructura (`IAudioService`, `IScreenManager`).
- **`Run`** — managers de gameplay que nacen al iniciar una run y mueren al terminarla (`TurnManager`, `DiceRoller`, `IInteractionService`, `IDungeonManager`, `IPhaseService`, `ICrapsSessionService`, `RunComboCounterState`, `RunStrikeState`).

Al terminar la run: `ServiceLocator.ClearScope(ServiceScope.Run)` limpia todo lo de la run sin afectar la infraestructura global.

#### 1.1.1 `ServiceBootstrapSO`

SO único que lista todos los servicios y assets críticos que hay que registrar antes de la partida. Se referencia desde el splash scene / bootstrap loader y dispara el registro en orden.

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Service Bootstrap")]
public class ServiceBootstrapSO : SerializedScriptableObject
{
    [Title("Catalog SOs (dropdown populators)")]
    [OdinSerialize] public EntityCatalogSO     EntityCatalog;      // §7.0 — héroes + enemigos + props + npcs
    [OdinSerialize] public BehaviorLibrarySO   BehaviorLibrary;    // §7.2b — templates de behaviors compartibles
    [OdinSerialize] public ComboCatalogSO      ComboCatalog;
    [OdinSerialize] public DiceCatalogSO       DiceCatalog;
    [OdinSerialize] public FeedbackDBSO        FeedbackDB;
    [OdinSerialize] public RoomCatalogSO       RoomCatalog;
    [OdinSerialize] public UnlockCatalogSO     UnlockCatalog;

    [Title("Extended Catalogs")]
    [OdinSerialize] public ItemCatalogSO       ItemCatalog;        // §18
    [OdinSerialize] public RewardCatalogSO     RewardCatalog;      // §19
    [OdinSerialize] public StatusCatalogSO     StatusCatalog;      // §20
    [OdinSerialize] public QuestCatalogSO      QuestCatalog;       // §21

    [Title("Settings")]
    [OdinSerialize] public SaveSettingsSO      SaveSettings;       // §15
    [OdinSerialize] public MinimapIconsSO      MinimapIcons;       // §13.7
    [OdinSerialize] public CameraConfigSO      CameraConfig;       // §17.E

    [Title("Runtime services (pre‑instantiated)")]
    [Title("Extended Catalogs — Actions & Rulesets")]
    [OdinSerialize] public ActionCatalogSO    ActionCatalog;      // §12.6
    [OdinSerialize] public RulesetCatalogSO   RulesetCatalog;     // §14.7

    [Title("Extended Settings")]
    [OdinSerialize] public AudioSettingsSO     AudioSettings;      // §17.A
    [OdinSerialize] public ShopConfigSO        ShopConfig;         // §17.F
    [OdinSerialize] public PhaseTransitionMatrixSO PhaseMatrix;    // §12.0.5
    [OdinSerialize] public GameSettingsSO      GameSettings;       // §23

    [Title("Runtime services (pre‑instantiated)")]
    [OdinSerialize] public List<IPreloadableService> ExtraServices = new();

    /// <summary>
    /// Llamado desde el bootstrap scene antes de cargar la partida.
    /// Registra todos los SOs de catálogo y settings en el ServiceLocator
    /// para que cualquier sistema pueda resolverlos sin Awake.
    /// </summary>
    public void RegisterAll()
    {
        ServiceLocator.AddService<EntityCatalogSO>(EntityCatalog);
        ServiceLocator.AddService<BehaviorLibrarySO>(BehaviorLibrary);
        ServiceLocator.AddService<ComboCatalogSO>(ComboCatalog);
        ServiceLocator.AddService<DiceCatalogSO>(DiceCatalog);
        ServiceLocator.AddService<FeedbackDBSO>(FeedbackDB);
        ServiceLocator.AddService<RoomCatalogSO>(RoomCatalog);
        ServiceLocator.AddService<UnlockCatalogSO>(UnlockCatalog);
        ServiceLocator.AddService<SaveSettingsSO>(SaveSettings);
        ServiceLocator.AddService<MinimapIconsSO>(MinimapIcons);
        ServiceLocator.AddService<CameraConfigSO>(CameraConfig);
        ServiceLocator.AddService<ItemCatalogSO>(ItemCatalog);
        ServiceLocator.AddService<RewardCatalogSO>(RewardCatalog);
        ServiceLocator.AddService<StatusCatalogSO>(StatusCatalog);
        ServiceLocator.AddService<QuestCatalogSO>(QuestCatalog);
        ServiceLocator.AddService<ActionCatalogSO>(ActionCatalog);
        ServiceLocator.AddService<RulesetCatalogSO>(RulesetCatalog);
        ServiceLocator.AddService<AudioSettingsSO>(AudioSettings);
        ServiceLocator.AddService<ShopConfigSO>(ShopConfig);
        ServiceLocator.AddService<PhaseTransitionMatrixSO>(PhaseMatrix);
        ServiceLocator.AddService<GameSettingsSO>(GameSettings);

        foreach (var svc in ExtraServices) svc.Register();
    }
}

public interface IPreloadableService
{
    void Register();
}
```

**`EntityCatalogSO` — catálogo unificado.** Antes existían `HeroCatalogSO` y `EntityCatalogSO` como catálogos separados. A partir de la introducción de `BaseEntitySO` (§7.0), el héroe y el resto de entidades comparten parent, y se fusionaron en un único catálogo tipado sobre `BaseEntitySO` con filtros por subtipo:

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Entity Catalog")]
public class EntityCatalogSO : SerializedScriptableObject
{
    [OdinSerialize] private List<BaseEntitySO> _entries = new();

    // Por id (genérico) — usado por los [ValueDropdown] de §0
    public IEnumerable<string> AllIds => _entries.Select(e => e.EntityId);

    // Filtros por subtipo — la UI de selección de clase usa GetHeroes();
    // los spawners de enemigos (§13.4) usan GetEnemies(); los spawners de
    // props (cofres, puertas, pociones) usan GetProps(); los NPCs (§7.6) se
    // enumeran con GetNpcs() cuando un sistema necesita sólo vendors/
    // dialogue/quest givers.
    public IEnumerable<ClassHeroSO>  GetHeroes()  => _entries.OfType<ClassHeroSO>();
    public IEnumerable<EnemyDataSO>  GetEnemies() => _entries.OfType<EnemyDataSO>();
    public IEnumerable<PropEntitySO> GetProps()   => _entries.OfType<PropEntitySO>();
    public IEnumerable<NpcDataSO>    GetNpcs()    => _entries.OfType<NpcDataSO>();

    public BaseEntitySO GetById(string id) =>
        _entries.FirstOrDefault(e => e.EntityId == id);

    public T GetById<T>(string id) where T : BaseEntitySO =>
        GetById(id) as T;

    // --- Passives (usado por ClassPassiveSO §4.4) ---------------------------
    public IEnumerable<string> AllPassiveIds =>
        GetHeroes().Where(h => h.Passive != null).Select(h => h.Passive.PassiveId);

    // --- Dialogue graphs (§7.6b) --------------------------------------------
    [OdinSerialize] private List<DialogueGraphSO> _dialogues = new();
    public IEnumerable<string> AllDialogueIds =>
        _dialogues.Select(d => d.DialogueId);
    public DialogueGraphSO GetDialogueById(string id) =>
        _dialogues.FirstOrDefault(d => d.DialogueId == id);
}
```

**Preload de Addressables.** Como cada `BaseEntitySO` tiene un `PrefabRef` (§7.0), `EntityCatalogSO` se encarga de **preloadear** todos los prefabs del catálogo vía `Addressables.LoadAssetAsync<GameObject>` durante el bootstrap. Después de `RegisterAll`, cualquier sistema que instancie una entidad ya tiene el prefab cacheado — cumple la regla de §1.1 (nada de `Resources.Load` / `LoadAsset` en hot paths).

#### 1.1.2 Bootstrap scene

Una scene ultra‑liviana (`00_Bootstrap.unity`) cuyo único MonoBehaviour hace:

```csharp
public class GameBootstrap : MonoBehaviour
{
    [SerializeField] private ServiceBootstrapSO _bootstrap;
    [SerializeField] private string _nextScene = "01_MainMenu";

    private void Awake()
    {
        _bootstrap.RegisterAll();
        SaveSystem.LoadFromDisk();               // §15
        SceneManager.LoadScene(_nextScene);
    }
}
```

**Regla.** Después del bootstrap, **ningún** sistema puede hacer `Resources.Load` o `Addressables.LoadAsset` dentro de hot paths — todo lo necesario para la partida ya está resuelto en el `ServiceLocator`. Assets que se cargan dinámicamente (por ejemplo prefabs de enemigos instanciados por sala) se piden al catálogo correspondiente, que los trae de Addressables una vez y los cachea.

#### 1.1.3 Managers runtime vs catalogs

- **Catalogs** (`HeroCatalog`, `ComboCatalog`, `ActionCatalog`, …): SOs con listas y lookup por id. Pre‑cargados en el bootstrap (§1.1.1). Registrados en `ServiceScope.Global`. **Read‑only en runtime.**
- **Managers** (`TurnManager`, `AttributesManager`, `GridManager`, `DungeonManager`, `DiceRoller`, `FeedbackBus`, `AttackResolver`, `IInteractionService`, `IPhaseService`, `IStatusEffectService`, `IRewardService`, `IQuestService`, `ICrapsSessionService`, `IShopManagerService`, `IMovementService`, `ICameraService`, `IAudioService`, `IInputService`, `ISceneService`, `IPlayerService`, `PawnRegistryService`, `ITutorialService`, `ISettingsService`, `IAnalyticsService`, `IPoolService`, `IRngService`, `SaveSystem`): instancias runtime, **stateful**. Registrados en `ServiceScope.Global` los de infraestructura, y `ServiceScope.Run` los que nacen/mueren con la run.

Ambos viven en el `ServiceLocator` pero su ciclo de vida es distinto. **§1.1.1 cubre `Global`**; los managers `Run` se registran en el flujo `RunStart` descrito abajo.

##### 1.1.3.a — Flujo `RunStart`

Se dispara después de la selección de clase (§4.5) y antes de instanciar la primera sala del dungeon. Su responsabilidad: instanciar y registrar todos los managers `ServiceScope.Run` **en orden de dependencia**. Vive en `RunBootstrapper`, un componente que el `ClassSelectScreen` invoca cuando el jugador confirma:

```csharp
public static class RunBootstrapper
{
    /// <summary>
    /// Llamado una vez post-selección de clase. Instancia managers Run en el
    /// orden correcto para que cada uno pueda resolver sus dependencias vía
    /// ServiceLocator en su constructor.
    /// </summary>
    public static void StartRun(ClassHeroSO selected, RulesetSO ruleset, Guid runId)
    {
        // 1. Infraestructura de la run — no dependen de gameplay.
        ServiceLocator.AddService<IRngService>(new RngService(seed: runId), ServiceScope.Run);
        ServiceLocator.AddService<IPhaseService>(new PhaseService(), ServiceScope.Run);
        ServiceLocator.AddService<IPlayerService>(new PlayerService(selected), ServiceScope.Run);

        // 2. Grilla + movimiento — GridManager antes que Movement (§B.10).
        ServiceLocator.AddService<GridManager>(new GridManager(), ServiceScope.Run);
        ServiceLocator.AddService<IMovementService>(new MovementService(), ServiceScope.Run);
        ServiceLocator.AddService<PawnRegistryService>(new PawnRegistryService(), ServiceScope.Run);

        // 3. Turn order + turn manager — turn order antes (§12.7).
        ServiceLocator.AddService<ITurnOrderService>(new TurnOrderService(), ServiceScope.Run);
        ServiceLocator.AddService<TurnManager>(new TurnManager(), ServiceScope.Run);

        // 4. Pipelines de combate — dependen de turn manager y phase service.
        ServiceLocator.AddService<AttackResolver>(new AttackResolver(), ServiceScope.Run);
        ServiceLocator.AddService<DamagePipeline>(new DamagePipeline(), ServiceScope.Run);
        ServiceLocator.AddService<HealPipeline>(new HealPipeline(), ServiceScope.Run);

        // 5. Dados + combos — DiceRoller depende de IRngService.
        ServiceLocator.AddService<DiceRoller>(new DiceRoller(), ServiceScope.Run);
        ServiceLocator.AddService<RunComboCounterState>(new RunComboCounterState(), ServiceScope.Run);
        ServiceLocator.AddService<RunStrikeState>(new RunStrikeState(), ServiceScope.Run);
        ServiceLocator.AddService<RunUpgradeState>(new RunUpgradeState(), ServiceScope.Run);   // §19.6

        // 6. Sistemas gameplay layer — interactúan con los anteriores.
        ServiceLocator.AddService<IInteractionService>(new InteractionService(), ServiceScope.Run);
        ServiceLocator.AddService<IStatusEffectService>(new StatusEffectService(), ServiceScope.Run);
        ServiceLocator.AddService<IRewardService>(new RewardService(), ServiceScope.Run);
        ServiceLocator.AddService<IQuestService>(new QuestService(ruleset), ServiceScope.Run);
        ServiceLocator.AddService<IShopManagerService>(new ShopManagerService(), ServiceScope.Run);
        ServiceLocator.AddService<ICrapsSessionService>(new CrapsSessionService(), ServiceScope.Run);

        // 7. Dungeon — último. Necesita que GridManager, Movement y Pools estén listos.
        ServiceLocator.AddService<IDungeonManager>(new DungeonManager(ruleset), ServiceScope.Run);

        // 8. Hookups cross-service — ahora que todos existen.
        var player = ServiceLocator.GetService<IPlayerService>().Active;
        player.BindPassive(selected.Passive);          // §4.4 — passive hookup
        ServiceLocator.GetService<IQuestService>().SubscribeEventTriggers(); // §21.2

        EventManager.Trigger(EventName.OnRunStart, runId, ruleset.RulesetId);
    }

    /// <summary>
    /// Flujo simétrico al final de una run (Victory, Defeat, o abandonar).
    /// Dispara OnRunEnd → flush de save → limpieza del scope.
    /// </summary>
    public static void EndRun(Guid runId, RunOutcome outcome)
    {
        EventManager.Trigger(EventName.OnRunEnd, runId, outcome);
        SaveSystem.Flush();                             // §15 — garantiza persistencia de unlocks ganados
        ServiceLocator.ClearScope(ServiceScope.Run);    // tira todos los managers Run
    }
}
```

**Orden de registro — regla de oro.** Si manager `A` resuelve manager `B` en su constructor vía `ServiceLocator.GetService<B>()`, entonces `B` se registra **antes** que `A`. El orden de arriba está armado así:
1. RNG, Phase, Player (sin dependencias gameplay).
2. Grid → Movement (Movement consulta Grid — §B.10).
3. TurnOrder → TurnManager.
4. AttackResolver, DamagePipeline, HealPipeline (dependen de TurnManager + Phase).
5. DiceRoller (depende de IRngService, sub-stream dice).
6. Servicios de gameplay layer (Interaction, Status, Reward, Quest, Shop, Craps).
7. DungeonManager al final — consume GridManager, Movement, Pools, RewardService.
8. Hookups cross-service — pasiva del player, listeners de quest, etc. Estos no se pueden hacer en constructor porque `IPlayerService` se instancia antes que `IQuestService`.

**Simetría `RunEnd`.** El cleanup no invierte el orden: `ClearScope(Run)` borra todo en un paso. Si algún manager necesita unhook explícito (ej: `IQuestService` unsubscribing del `EventManager`), implementa `IDisposable` y `ServiceLocator.ClearScope` lo invoca antes de remover la entry. Regla: los managers `Run` con event subscriptions en su ctor **deben** implementar `IDisposable` o el bus acumula handlers zombies entre runs.

**Tests / prototipado.** Para tests unitarios de un manager específico, instanciar a mano y hacer `AddService` con `ServiceScope.Run` directamente. `TearDown` llama `ClearScope(Run)`. No mockear el ServiceLocator — el scope `Run` ya provee aislamiento.

**Cross‑ref §1.1.3.** §1.1 (`ServiceScope`), §1.1.1 (bootstrap `Global`), §4.5 (flujo completo de inicio de run — selección de clase + llamada a `RunBootstrapper`), §14.6 (end‑of‑run — unlocks antes de `EndRun`), §15 (`SaveSystem.Flush` en `EndRun`), §21.2 (`QuestService.SubscribeEventTriggers`).

### 1.2 `EventManager`

Event bus global con enum `EventName`. Strings tipados para eventos de alto nivel cross‑sistema.

```csharp
public static class EventManager
{
    public delegate void EventReceiver(params object[] parameter);
    public static void Subscribe(EventName eventType, EventReceiver method);
    public static void UnSubscribe(EventName eventType, EventReceiver method);
    public static void Trigger(EventName eventType, params object[] parameters);
    public static void ResetEventDictionary();
}
```

#### 1.2.1 `TypedEvent<T>` — bus tipado complementario

El `EventManager` legacy transporta `object[]` — funcional pero frágil: los casts manuales fallan silenciosamente si un publisher agrega o reordena parámetros. Para eventos de alto tráfico y payload complejo, se usa un bus tipado que **reemplaza** al `EventManager` para los eventos que listan un `TypedPayload` a continuación.

```csharp
/// <summary>
/// Bus tipado por struct de payload. Elimina casts manuales y documenta
/// el schema del evento en el tipo mismo. Coexiste con el EventManager
/// legacy — se usa para eventos nuevos o de alto tráfico donde los
/// errores de cast serían costosos.
/// </summary>
public static class TypedEvent<T> where T : struct
{
    private static event Action<T> _listeners;

    public static void Subscribe(Action<T> listener)   => _listeners += listener;
    public static void Unsubscribe(Action<T> listener) => _listeners -= listener;
    public static void Raise(T payload)                => _listeners?.Invoke(payload);
    public static void Clear()                         => _listeners = null;
}
```

**Structs de payload canónicos:**

```csharp
public struct DamageResolvedPayload
{
    public Guid SourceGuid;
    public Guid TargetGuid;
    public int FinalDamage;
    public bool WeaknessHit;
}

public struct HealthChangedPayload
{
    public Guid EntityGuid;
    public int Current;
    public int Max;
}

public struct ComboMatchedPayload
{
    public Guid SourceGuid;
    public string ComboId;
    public int BaseDamage;
}
```

**Uso:**

```csharp
// Publisher (DamagePipeline)
TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload {
    SourceGuid = source.InstanceId,
    TargetGuid = target.InstanceId,
    FinalDamage = finalDmg,
    WeaknessHit = wasWeak
});

// Subscriber (HUDScreen)
TypedEvent<HealthChangedPayload>.Subscribe(OnHealthChanged);
private void OnHealthChanged(HealthChangedPayload p) {
    _healthBar.value = (float)p.Current / p.Max;
}
```

**Regla de canal único — no coexistencia.** Cada evento del juego publica por **exactamente una** vía: `EventManager` (legacy, `object[]`) **o** `TypedEvent<T>` (tipado). Nunca ambos. La tabla de familias abajo marca con `🟦 Typed` los eventos que viven en `TypedEvent<T>` y con `🟪 Legacy` los que siguen en `EventManager`. Un evento migrado a `TypedEvent<T>` **desaparece** del enum `EventName` y no se puede volver a publicar por la vía legacy — el compilador se encarga. Migrar un evento es dos cambios: eliminar su entry del `EventName` + reemplazar publishers/subscribers por la versión tipada.

**Criterio de qué vive en cada bus.**
- `🟦 Typed` — eventos con payload ≥ 2 campos de tipos heterogéneos, o de alto tráfico (resolución de daño, tick de HUD). Cuando un cast silencioso costaría un bug difícil de rastrear, se tipa.
- `🟪 Legacy` — eventos simples (1 campo o marker sin payload) donde un struct sería overkill. Broadcast genérico (OnRunStart, OnFloorCleared) donde todos los listeners son de bajo volumen.

**Familia mínima de `EventName`** para Rollgeon:

| Región | Eventos |
|---|---|
| Run      | `OnRunStart`, `OnRunEnd` |
| Combat (lifecycle) | `OnCombatStart`, `OnCombatEnd` |
| Turn     | `OnTurnStarted`, `OnTurnFinished`, `OnEnergyChanged`, `OnTurnQueueBuilt` |
| Phase (§12.0) | `OnPhaseExit`, `OnPhaseEnter`, `OnOverlayPushed`, `OnOverlayPopped` |
| Roll     | `OnRollStarted`, `OnDiceRolled`, `OnRerollStarted`, `OnRollResolved` |
| Combat (resolve) | `OnDamageOutgoing`, `OnDamageIncoming`, `OnDamageResolved`, `OnHealthChanged`, `OnShieldChanged`, `OnEntityDestroyed` |
| Contract | `OnComboMatched`, `OnComboCrossed`, `OnWeaknessHit` |
| Modifier | `OnModifierAdded`, `OnModifierRemoved` |
| Dungeon  | `OnRoomEntered`, `OnRoomCleared`, `OnFloorCleared` |
| HUD      | `OnPlayerHealthChanged`, `OnPlayerEnergyChanged`, `OnGoldChanged`, `OnFloatingNumberRequested` |
| Screens  | *(movidos)* — `IScreenManager.OnScreenPushed` / `OnScreenPopped` / `OnPauseChanged` (§17.D) |
| Craps    | `OnCrapsSessionStarted`, `OnCrapsBetPlaced`, `OnCrapsResolved` |
| Save     | `OnCaptureRequested`, `OnRestoreCompleted` (§15) |
| Feedback | `OnFeedbackStarted`, `OnFeedbackCompleted` |
| Interaction | `OnInteractionTargetChanged`, `OnInteractionExecuted` (§7.7) |
| Shop (§17.F) | `OnShopItemTargetChanged`, `OnShopItemPurchased`, `OnShopRestocked` |
| Status (§20) | `OnStatusApplied`, `OnStatusRemoved`, `OnStatusTicked` |
| Items (§18) | `OnItemObtained`, `OnItemRemoved`, `OnActiveItemUsed` |
| Quest (§21) | `OnQuestStateChanged` |
| Scene (§K) | `OnSceneLoaded`, `OnSceneUnloaded` |

**Regla transversal de `args[0]`.** Para todo evento legacy cuyo payload referencie a una entidad concreta (run, combat, turn, damage, modifier, interaction, status, etc.), `args[0]` es **siempre** `Guid` — el `InstanceId` de la entidad primaria a la que se refiere el evento. Nunca se pasa `Entity` ni `MonoBehaviour`. Esta regla existe porque `Modifier<T>.OnTickTriggered` (§3.1) filtra por `OwnerId == (Guid)args[0]`; un publisher que pase otra cosa hace que el tick no dispare y el mod quede suscripto para siempre.

**Payloads.** Los parámetros viajan como `object[]`. Para evitar errores de cast, cada `EventName` tiene su schema documentado al lado del enum:

```csharp
public enum EventName
{
    // --- Run lifecycle ------------------------------------------------------
    /// <summary>args: [Guid runId, string rulesetId]</summary>
    OnRunStart,
    /// <summary>args: [Guid runId, RunOutcome outcome]</summary>
    OnRunEnd,

    // --- Combat lifecycle ---------------------------------------------------
    /// <summary>args: [Guid roomInstanceId]</summary>
    OnCombatStart,
    /// <summary>args: [Guid roomInstanceId, CombatOutcome outcome]</summary>
    OnCombatEnd,

    // --- Damage pipeline ----------------------------------------------------
    /// <summary>args: [Guid sourceGuid, Guid targetGuid, int baseDamage]</summary>
    OnDamageOutgoing,
    /// <summary>args: [Guid sourceGuid, Guid targetGuid, int incomingDamage]</summary>
    OnDamageIncoming,
    /// <summary>args: [Guid sourceGuid, Guid targetGuid, int finalDamage, bool weakness]</summary>
    OnDamageResolved,

    // --- Turn / initiative --------------------------------------------------
    /// <summary>args: [Guid entityGuid]. Quien arranca su turno.</summary>
    OnTurnStarted,
    /// <summary>args: [Guid entityGuid]. Quien cierra su turno — lo consume Modifier&lt;T&gt; para decrementar Duration.</summary>
    OnTurnFinished,
    /// <summary>args: [Guid entityGuid, int current, int max]</summary>
    OnEnergyChanged,
    /// <summary>args: [IReadOnlyList&lt;Guid&gt; orderForRound, int roundIndex]</summary>
    OnTurnQueueBuilt,

    // --- Modifier lifecycle ------------------------------------------------
    /// <summary>args: [Guid ownerGuid, Guid modifierId]</summary>
    OnModifierAdded,
    /// <summary>args: [Guid ownerGuid, Guid modifierId]</summary>
    OnModifierRemoved,

    // --- HUD bindings (le hablan al §D ScreenManager) ----------------------
    /// <summary>args: [Guid entityGuid, int current, int max]</summary>
    OnPlayerHealthChanged,
    /// <summary>args: [Guid entityGuid, int current, int max]</summary>
    OnPlayerEnergyChanged,
    /// <summary>args: [int current, int delta]</summary>
    OnGoldChanged,
    /// <summary>args: [Guid targetGuid, FloatingNumberType type, float value, Vector3 offset]</summary>
    OnFloatingNumberRequested,

    // --- Screen stack: NO viven acá — el IScreenManager (§17.D) expone
    //     sus events C# directamente. Suscribirse vía ServiceLocator.
    //     OnScreenPushed / OnScreenPopped fueron removidos del bus legacy
    //     para no tener dos canales publicando lo mismo.

    // --- Craps --------------------------------------------------------------
    /// <summary>args: [Guid sessionId, Guid playerGuid]</summary>
    OnCrapsSessionStarted,
    /// <summary>args: [Guid sessionId, string comboId, int stake]</summary>
    OnCrapsBetPlaced,
    /// <summary>args: [Guid sessionId, CrapsOutcome outcome, int payout]</summary>
    OnCrapsResolved,

    // --- Phase lifecycle (§12.0) --------------------------------------------
    /// <summary>args: [GamePhase exiting]</summary>
    OnPhaseExit,
    /// <summary>args: [GamePhase entering]</summary>
    OnPhaseEnter,
    /// <summary>args: [PhaseOverlay overlay]</summary>
    OnOverlayPushed,
    /// <summary>args: [PhaseOverlay overlay]</summary>
    OnOverlayPopped,

    // --- Interaction (§7.7) ------------------------------------------------
    /// <summary>args: [Guid targetGuid, string resolvedLabel, bool isAvailable].
    /// targetGuid == Guid.Empty significa "no hay target, esconder el prompt".
    /// resolvedLabel es el LocalizedString del label ya resuelto por el
    /// LocalizationManager. isAvailable == false => prompt grayed out.</summary>
    OnInteractionTargetChanged,
    /// <summary>args: [Guid targetGuid]</summary>
    OnInteractionExecuted,

    // --- Shop (§17.F) ----------------------------------------------------
    /// <summary>args: [bool hasTarget, string itemName, string description, int price, Sprite icon].
    /// hasTarget == false → esconder el ItemInspectView.</summary>
    OnShopItemTargetChanged,
    /// <summary>args: [string spawnPointId, string rewardId, int pricePaid]</summary>
    OnShopItemPurchased,
    /// <summary>args: [Guid roomInstanceId, int slotsRestocked]</summary>
    OnShopRestocked,

    // ...
}
```

**Acoplamiento con `Modifier<T>` (§3).** Los modificadores temporales se auto‑suscriben a un `EventName` para decrementar `Duration`. Típicamente `OnTurnFinished`.

### 1.3 FSM

```csharp
public class EventFSM<T>
{
    public State<T> Current { get; }
    public void SendInput(T input);    // busca transición válida y cambia estado
    public void Update();
    public void LateUpdate();
    public void FixedUpdate();
}

public abstract class State<T>
{
    public virtual void Enter(T input) { }
    public virtual void Exit(T input)  { }
    public virtual void Update()       { }
    public virtual void LateUpdate()   { }
    public virtual void FixedUpdate()  { }
    public abstract bool CheckInput(T input, out State<T> next);
}
```

**FSM concreta — `CombatTurnFSM`**. Macro del combate. No confundir con `GamePhase` (§12.0), el enum global para gates per‑fase: el `CombatTurnFSM` **vive dentro** de `GamePhase.Combat` y todos sus estados son sub‑steps invisibles para listeners externos.

**Convención de naming.** Ningún estado lleva el sufijo `Phase` — esa palabra queda reservada al enum macro. Se usa `Step` cuando hace falta desambiguar (p.ej. `Roll`, `Reroll` en vez de `RollPhase`).

```
─── PlayerBranch ───────────────────────────────────────────────
StartPlayerTurn
   └→ CheckTurnSkip
        ├→ (skipped, CC activo)  ─────────────────┐
        └→ (ok) PlayerInput  ◄─ loop ──┐          │
                    ├─ Attack/Special → Roll → Reroll → ResolveCombo → ApplyDamage
                    ├─ Defend                                      → Defend
                    ├─ Interact (heal, prop, NPC, skill check)     → Interact
                    ├─ Move                                        → (no damage)
                    └─ EndTurn  ─── exit loop ──┐                  │
                                                │                  ▼
                                                │       every ApplyDamage ⇒
                                                │       ResolveReactions → CheckCombatEnd
                                                │                  │ (ongoing: back to loop)
                                                ▼                  │
                                        CleanupPlayerModifiers ◄───┘
                                                │
─── EnemyBranch ────────────────────────────────┼──────────────
                                                ▼
                                        EnterEnemyBranch
                                                │
                                        EnemyPickNext  ◄─ loop ─┐
                                         ├→ (queue empty) ──────┼──┐
                                         └→ EnemyAction         │  │
                                              └→ ApplyDamage    │  │
                                                  └→ ResolveReactions
                                                       └→ CheckCombatEnd
                                                             (ongoing ┘)
                                                                      ▼
                                                       CleanupEnemyModifiers
                                                                      │
                                                                      ▼
                                                            StartPlayerTurn
                                                              (next round)

─── Terminal ──────────────────────────────────────────────────
CheckCombatEnd  ─┬→ Victory  → OnCombatEnd → salir a GamePhase previa
                 └→ Defeat   → OnCombatEnd → game over flow
```

**Estados.**

| Estado | Responsabilidad | Notas |
|---|---|---|
| `StartPlayerTurn` | Limpia `_actionsUsedThisTurn`, dispara `OnTurnStarted(player)`. | Entry point del round. |
| `CheckTurnSkip` | Chequea CC (stun, freeze, sleep) sobre el player. Si aplica, transiciona directo a `CleanupPlayerModifiers` sin pasar por `PlayerInput`. | Reemplaza guardas ad‑hoc dentro de `PlayerInput`. |
| `PlayerInput` | Espera input del jugador. Cada acción válida corre su sub‑flujo y vuelve acá si queda action economy. | Loop hasta `EndTurn` o hasta quedar sin recursos para ninguna acción. |
| `Roll` → `Reroll` → `ResolveCombo` | Sub‑flujo de ataque: `DiceRoller.RollAll`, hasta 2 rerolls + 1 con energía (§6.5), `AttackResolver.ResolveAttack` (§12.1). | Sólo para acciones de ataque (basic / special / weapon). |
| `Defend` | `IDefenseResolver.ResolveDefense` sobre los rerolls sobrantes → `Shield` (§12.4). | Alternativa a `ResolveCombo`. |
| `Interact` | **Delega al `IInteractionService`** (§7.7). Resuelve heal, cofre, puerta forzada, NPC vendor, shop — todo por la misma ruta unificada vía `PhaseInteractionRule(Combat, …)`. | Reemplaza el viejo `SecondaryAction (heal, door, chest)` y borra sus branches dedicados. |
| `ApplyDamage` | Único punto que llama a `DamagePipeline.Resolve` (§12.2). Dispara `OnDamageResolved`. | No hay accesos directos a `Health.SetValue` desde otro state. |
| `ResolveReactions` | Dispara los behaviors reactivos del target (`OnDamaged`, thorns, contragolpes) filtrados por `AllowedPhases`. Si un reactivo vuelve a hacer daño, re‑entra en `ApplyDamage` — recursión con stack depth cap para atajar loops patológicos (reactivo → reactivo → reactivo …). | Evita que los contragolpes se pierdan mid‑turno. |
| `CheckCombatEnd` | **Corre post cada `ApplyDamage`.** Chequea victoria (todos los enemigos muertos) y derrota (`player.Health ≤ 0`). Si se cumple, sale a `Victory` / `Defeat`. Si no, vuelve al caller state. | Cubre player attacks, enemy attacks, reactivos, traps, DOTs — cualquier fuente de daño. |
| `CleanupPlayerModifiers` | Decrementa duration de todos los `Modifier<T>` con `OwnerId = player` y `TickEvent = OnTurnFinished`. Los que llegan a 0 se remueven. Dispara `OnTurnFinished(player)`. | Los buffs/debuffs del jugador gastan 1 tick al **terminar su turno**. |
| `EnterEnemyBranch` | Arma la cola con los enemigos vivos consultando `TurnOrderService` (§12.7). | — |
| `EnemyPickNext` | Saca el próximo enemigo de la cola. Si está vacía → `CleanupEnemyModifiers`. Si el próximo murió mid‑turno por un reactivo, lo descarta y sigue. | Iteración first‑class — reemplaza el `EnemyAction*` con asterisco del draft anterior. |
| `EnemyAction` | El enemigo activo evalúa su `AIRoot` (§7.5) y ejecuta la decisión. Dispara `OnTurnStarted(enemy)` / `OnTurnFinished(enemy)`. | Un tick por enemigo. Los reactivos del player caen en `ResolveReactions` como cualquier otro. |
| `CleanupEnemyModifiers` | Decrementa duration de todos los `Modifier<T>` con `OwnerId ∈ bando enemy` (enemigos, bosses, props hostiles) y `TickEvent = OnTurnFinished`. Los que llegan a 0 se remueven. Dispara `OnEnemyPhaseFinished`. | Los buffs/debuffs del bando enemigo gastan 1 tick al terminar la fase de **todos** los enemigos — no por cada enemigo individual. Ver regla abajo. |
| `Victory` / `Defeat` | Terminal. Disparan `OnCombatEnd` con el resultado. El `TurnManager` devuelve el control al `GamePhase` macro (vuelve a `Exploration`, `Shop`, etc.). | Sale de la FSM de combate. |

**Inputs del FSM**:

```csharp
public enum TurnInput
{
    // Player
    EndTurn, Attack, Special, Defend, Move, Interact,
    Reroll, UseEnergyReroll,
    // System
    CombatEnded,          // emitido por CheckCombatEnd al detectar win/lose
    ReactionResolved,     // emitido por ResolveReactions al estabilizar
    EnemyQueueEmpty,      // emitido por EnemyPickNext cuando la cola se vacía
    TurnSkipped,          // emitido por CheckTurnSkip al detectar CC activo
}
```

Los viejos inputs `Heal` / `ForceDoor` del draft anterior desaparecen — ambos son ahora `Interact` y los resuelve el `IInteractionService` contra el `PhaseInteractionRule(Combat)` de la poción / puerta / cofre / NPC, sin rama dedicada en el FSM.

**Regla de la split de cleanup — por qué hay dos.**

- **`CleanupPlayerModifiers`** corre una sola vez por round, entre el fin del turno del player y `EnterEnemyBranch`. Los modificadores con `TickEvent = OnTurnFinished` y `OwnerId = player` decrementan acá.
- **`CleanupEnemyModifiers`** corre una sola vez por round, después de que **todos** los enemigos hayan actuado. Los modificadores con `TickEvent = OnTurnFinished` y `OwnerId` perteneciente al bando enemigo decrementan acá.
- **Consecuencia buscada.** Un debuff de `Duration = 1` aplicado al `Enemy 2` **no** se gasta cuando actúa el `Enemy 1`. El tick ocurre al cerrar la fase completa, así que el debuff alcanza a afectar al Enemy 2 cuando le toca su turno. Análogamente, un buff `Duration = 1` que el player se aplica a sí mismo dura exactamente "su turno", no sobrevive a la fase enemy ni se gasta mid‑turno.
- **Modificadores globales simétricos** (p.ej. "durante 2 rounds el daño es doble para todos") se modelan como **dos** modificadores espejados — uno con `OwnerId = player` y otro con `OwnerId = enemyBand` — para que cada tick respete su propia fase. Evita el edge case "¿quién decrementa este mod?" ambigüo.

**Cross‑ref §1.3.** §1.2 (`OnTurnStarted` / `OnTurnFinished` / `OnPhaseExit` / `OnPhaseEnter`), §3 (lifecycle de modificadores, `TickEvent`, `ModifierLifetime`), §7.2 (`AllowedPhases` — filtro de behaviors en cada dispatcher), §7.5 (`AIRoot` ejecutado en `EnemyAction`), §7.7 (`IInteractionService` — a quién delega `Interact`), §12 (`DamagePipeline`, action economy, turn order, action definitions), §12.0 (`GamePhase` + `PhaseOverlay` + `IPhaseService`).

---

## 2. Sistema de Atributos

### 2.1 Contratos base

```csharp
public interface IAttribute
{
    T GetValue<T>();
    void SetValue<T>(T value);
    Type GetValueType();
    string GetAttributeName();
    IAttribute Duplicate();
}

public interface IModifiable : IAttribute
{
    T GetModifiedValue<T>();
    void SubscribeModifier();
    bool AddModifier<T>(IModifier<T> modifier);
    void RemoveModifier(Guid modifierId);
    void LinkAttribute(Action<Guid> callback);
}
```

**Separación deliberada:**

- `IAttribute` → valor serializable con nombre + tipo. Usado para **reglas estáticas** (config, plantillas).
- `IModifiable` → `IAttribute` + stack de modificadores + resolución del valor modificado. Usado para **stats de runtime**.

Los concretos (`Health : IModifiable<int>`, `Energy : IModifiable<int>`, `Speed : IModifiable<int>`, `Shield : IModifiable<int>`, `IncomingDamageMultiplier : IModifiable<float>`, …) guardan valor base + `List<Modifier<T>>` interna y resuelven `GetModifiedValue<T>()` aplicando cada `Operation` en orden.

### 2.2 Contenedor tipado: `ModifiableAttributes`

```csharp
[Serializable]
public class ModifiableAttributes
{
    [OdinSerialize] public Dictionary<Type, IModifiable> attributes;

    public V    GetAttributeModifiedValue<T, V>() where T : IModifiable;
    public T    GetAttribute<T>() where T : class, IModifiable;
    public void SetAttribute<T>(IModifiable attribute);
    public bool HasAttribute<T>() where T : class, IModifiable;
    public void RemoveAttribute<T>() where T : class, IModifiable;
    public V    GetAttributeValue<T, V>() where T : class, IModifiable;
    public void SetAttributeValue<T, V>(V value) where T : class, IModifiable;
    public List<IModifiable> GetAllAttributes();
    public ModifiableAttributes DuplicateAttributes();
    public void EnsureInitialized();
}
```

**Invariantes:**

- Indexado por `Type` literal del stat concreto → **una entidad no puede tener dos instancias del mismo stat**. Intencional.
- `DuplicateAttributes()` clona por atributo (llama `Duplicate()`) → se usa al iniciar run para no mutar el `ClassHeroSO` origen.

**Validación de tipo en accessors.** Los métodos genéricos `GetValue<T>()` / `GetModifiedValue<T>()` de `IAttribute` / `IModifiable` pueden fallar silenciosamente si el caller pide un tipo incorrecto (ej: `GetValue<float>()` sobre un `Health : IModifiable<int>`). Para prevenir este error:

```csharp
public T GetValue<T>()
{
    if (typeof(T) != GetValueType())
        throw new InvalidCastException(
            $"[{GetAttributeName()}] Expected {GetValueType().Name}, got {typeof(T).Name}. " +
            "Check the generic type argument.");
    return (T)_rawValue;
}
```

En debug/editor, el error es una excepción con mensaje claro. En release, el `#if` puede degradar a un log + default para no crashear la run.

### 2.3 Ownership por GUID

Cada entidad del juego (jugador, enemigos, bosses, objetos de sala) tiene un `Guid InstanceId` único generado al spawneo. Los modificadores, eventos y lookups **siempre** referencian entidades por `Guid`, nunca por `int OwnerId`:

```csharp
public class Entity
{
    public Guid InstanceId { get; } = Guid.NewGuid();
    public ModifiableAttributes Attributes { get; } = new();
    public Entity(/* ... */) { /* ... */ }
}
```

**Razón.** `int OwnerId` era suficiente cuando había 2 jugadores fijos (source = 0, target = 1). En Rollgeon hay un jugador + N enemigos simultáneos + objetos de sala; los enemigos se spawnean y mueren dinámicamente, y persisten entre salas (GD: "enemigos vivos reaparecen con la vida que tenían"). `int` indexing colisiona al respawnear; `Guid` es único por instancia y se sostiene toda la run.

**Cross‑ref.** §3 (modificadores usan `Guid OwnerId`), §7 (entidades), §12 (pipeline de daño lookup por guid), §15 (save usa guid como save key).

---

## 3. Sistema de Modificadores

### 3.1 `Modifier<T>`

```csharp
/// <summary>
/// Enum serializable que reemplaza al delegate Func&lt;T, T, T&gt;.
/// Un Func no se serializa con Unity ni con Odin — al persistir un
/// Modifier con el save system (§15) la operación se perdería.
/// Con un enum, el save captura el valor entero y al restaurar se
/// resuelve la operación desde el catálogo estático (§3.3).
/// </summary>
public enum ModifierOperation
{
    Add,
    Subtract,
    Multiply,
    Override,
    Min,
    Max,
    Percent,
    Set,        // para bool
    And,        // para bool
    Or,         // para bool
}

[Serializable]
public class Modifier<T>
{
    public T Amount;
    public ModifierOperation Operation;     // serializable — resuelta a Func via OperationResolver
    public int Duration;                    // solo relevante cuando Lifetime == Turns
    public Guid ModifierId;                 // único por instancia
    public Guid CarrierId;                  // entidad (Entity.InstanceId) que CARGA el modificador — usado para tick-gating y event payload
    public Guid SourceId;                   // entidad/efecto que ORIGINÓ el modificador — usado para RemoveBySource cuando la fuente muere/expira. Guid.Empty si no aplica.
    public ModifierDirection Direction;     // Outgoing | Incoming | Intrinsic
    public ModifierLifetime Lifetime;       // Turns | Permanent | Run | Encounter
    public EventName TickEvent;             // evento que decrementa Duration (solo Turns)

    [NonSerialized]
    private Func<T, T, T> _resolvedOp;     // cache runtime — resuelta desde Operation

    public Modifier(T amount, ModifierOperation op, int duration,
                    Guid carrierId, Guid sourceId,
                    ModifierDirection dir, ModifierLifetime lifetime, EventName tickEvent)
    {
        Amount = amount;
        Operation = op;
        Duration = duration;
        ModifierId = Guid.NewGuid();
        CarrierId = carrierId;
        SourceId = sourceId;
        Direction = dir;
        Lifetime = lifetime;
        TickEvent = tickEvent;
        _resolvedOp = OperationResolver.Resolve<T>(op);
        OnLoad();
    }

    /// <summary>
    /// Subscribe al event que corresponda según el Lifetime. Permanent no se
    /// subscribe a nada — solo se remueve por EffRemoveModifier explícito.
    /// Idempotente: si ya estaba suscripto, UnSubscribe + Subscribe garantiza
    /// una sola subscripción — necesario para el camino de Restore (§15) que
    /// vuelve a invocar OnLoad sobre instancias deserializadas que pueden
    /// tener _resolvedOp nulo y estado de subscripción indeterminado.
    /// </summary>
    public void OnLoad()
    {
        _resolvedOp = OperationResolver.Resolve<T>(Operation);
        switch (Lifetime)
        {
            case ModifierLifetime.Turns:
                EventManager.UnSubscribe(TickEvent, OnTickTriggered);
                EventManager.Subscribe(TickEvent, OnTickTriggered);
                break;
            case ModifierLifetime.Run:
                EventManager.UnSubscribe(EventName.OnRunEnd, OnScopeEnded);
                EventManager.Subscribe(EventName.OnRunEnd, OnScopeEnded);
                break;
            case ModifierLifetime.Encounter:
                EventManager.UnSubscribe(EventName.OnCombatEnd, OnScopeEnded);
                EventManager.Subscribe(EventName.OnCombatEnd, OnScopeEnded);
                break;
            case ModifierLifetime.Permanent:
                // no subscription — vive hasta que algo lo remueva a mano
                break;
        }
    }

    public T ApplyModifier(T value)
    {
        _resolvedOp ??= OperationResolver.Resolve<T>(Operation);
        return _resolvedOp(value, Amount);
    }

    public void OnRemove()
    {
        switch (Lifetime)
        {
            case ModifierLifetime.Turns:     EventManager.UnSubscribe(TickEvent, OnTickTriggered); break;
            case ModifierLifetime.Run:       EventManager.UnSubscribe(EventName.OnRunEnd, OnScopeEnded); break;
            case ModifierLifetime.Encounter: EventManager.UnSubscribe(EventName.OnCombatEnd, OnScopeEnded); break;
        }
    }

    private void OnTickTriggered(params object[] args)
    {
        // args[0] debe ser Guid de la entidad cuyo turno/evento dispara el tick.
        // Gating: sólo tickeamos si este mod pertenece al CARRIER que está activando.
        if (args.Length == 0 || !(args[0] is Guid triggerGuid)) return;
        if (CarrierId != triggerGuid) return;

        Duration--;
        if (Duration == 0)
            RemoveAndNotify();
    }

    private void OnScopeEnded(params object[] args) => RemoveAndNotify();

    private void RemoveAndNotify()
    {
        OnRemove();
        EventManager.Trigger(EventName.OnModifierRemoved, CarrierId, ModifierId);
    }
}

public enum ModifierDirection
{
    /// <summary>Aplica cuando la entidad es ORIGEN de la operación (daño saliente, heal saliente).</summary>
    Outgoing,
    /// <summary>Aplica cuando la entidad es DESTINO de la operación (recibe daño, recibe heal).</summary>
    Incoming,
    /// <summary>No depende de dirección — aplica al stat directamente (ej: +10 Health max, tick de veneno).</summary>
    Intrinsic,
}

public enum ModifierLifetime
{
    /// <summary>Duration cuenta ticks de TickEvent. Se remueve al llegar a 0. Uso: buffs de N turnos.</summary>
    Turns,
    /// <summary>No tickea. Vive hasta que un EffRemoveModifier explícito lo quite. Uso: stat boosts comprados en tienda, pasivas siempre-activas.</summary>
    Permanent,
    /// <summary>Se remueve con OnRunEnd. Uso: run buffs, boss debuffs, strike combos, cualquier cosa que dure "esta corrida".</summary>
    Run,
    /// <summary>Se remueve con OnCombatEnd. Uso: "durante este combate los combos de fuego hacen +20%".</summary>
    Encounter,
}
```

**Patrón.** Strategy (via `Operation` delegate) + self‑managed subscription por **scope** + direction‑aware application. El campo `Lifetime` absorbe la semántica que antes quedaba implícita (`Duration == -1` = permanente en el prototipo) y a la vez habilita los scopes "run" y "encounter" del GDD sin tener que inventar eventos de tick ad‑hoc.

**Nota de naming — `CarrierId` vs `SourceId`.** Los dos Guid son ortogonales y ambos importan:

- **`CarrierId`** (antes `OwnerId`) = **entidad que lleva el modificador pegado**. Es el `Entity.InstanceId` del jugador, enemigo, boss o prop sobre el cual el mod está activo. Se usa para: (a) **tick-gating** — `OnTickTriggered` compara `CarrierId == args[0]` para que los buffs del jugador sólo decrementen duration en su turno y no en los turnos de los enemigos; (b) **event payload** — `OnModifierRemoved(CarrierId, ModifierId)` le dice al HUD y al `AttributesManager` qué entidad limpiar.
- **`SourceId`** = **entidad/efecto que originó este modificador**. Es el `Entity.InstanceId` del enemigo que lanzó el debuff, del item activo que aplicó el buff, o del ability concreto que lo colocó. Se usa para: (a) **remove‑by‑source** — "al morir Boss X, sacar todos los mods que X puso sobre el jugador" = `AttributesManager.RemoveAllModifiersBySource(bossInstanceId)`; (b) **expiración por fuente** — "este buff dura mientras la poción activa esté vigente, al retirarla se limpian sus mods"; (c) **pasivas de reflect/counter** — "si la fuente del daño recibido es un enemigo de tipo X, aplicar...".

Cuando el `SourceId` no aplica (mod auto‑infligido, stat boost permanente de tienda sin origen identificable), se pasa `Guid.Empty`. `RemoveAllModifiersBySource(Guid.Empty)` es **siempre no‑op** por seguridad (evita borrar masivamente mods anónimos por accidente).

**Cambio de naming (changelog interno).** El campo que §3.1 antes llamaba `OwnerId` se renombró a `CarrierId` para evitar confusión con la noción de "owner" en otros contextos. Rollgeon es single‑player — no existen actores remotos ni hosts. Otros lugares del spec (§7.2 cleanup, §12 pipeline, §20 status) todavía usan `OwnerId` textual; cada tarea downstream los ajusta cuando los toca. La semántica es idéntica.

### 3.2 `ModifierDirection` y por qué importa

El problema: si un modificador "recibe 50% más daño" vive en el jugador, y la pipeline de cálculo sólo lee modificadores de la fuente (enemigo), ese modificador **nunca se evalúa**.

Solución: cada modificador declara su dirección. La pipeline de daño (§12) consulta:

1. Modificadores `Outgoing` en la entidad **fuente**.
2. Modificadores `Incoming` en la entidad **destino**.
3. Modificadores `Intrinsic` se aplican al leer el stat mismo (no participan de pipelines direccionales).

**Ejemplos:**

| Ejemplo | Dirección | Vive en | Pipeline que lo consume |
|---|---|---|---|
| "Haces +30% daño con Escalera" | `Outgoing` | Jugador | `DamagePipeline.ApplyOutgoing` |
| "Recibes +50% daño" | `Incoming` | Jugador | `DamagePipeline.ApplyIncoming` |
| "El enemigo pierde 10 HP por turno (veneno)" | `Intrinsic` | Enemigo (sobre `Health`) | Directo al stat via `OnTurnFinished` |
| "Escudo bloquea el próximo golpe" | `Incoming` | Jugador | `DamagePipeline.ApplyIncoming` (pre‑shield) |
| "Tus curaciones curan +20%" | `Outgoing` | Jugador | `HealPipeline.ApplyOutgoing` |

### 3.3 Catálogo de operaciones

Tabla estática de operaciones por tipo de valor (`int`, `float`, `bool`, ref), cada una identificada por un string para serializarse en el inspector.

- `int` / `float` → `Add`, `Subtract`, `Multiply`, `Override`, `Min`, `Max`, `Percent`.
- `bool` → `Set`, `And`, `Or`, `Xor`.
- ref → `Replace`, `Link`.

La base de efectos resuelve el dropdown vía `OperationsConstants.GetAll*OperationsNames()` (§8).

### 3.4 Lifecycle

1. Un efecto (`EffAddIntModifier`, §8) construye el `Modifier<T>` en el inspector: operación, amount, duration, dirección, **lifetime**, tick event.
2. `AttributesManager.AddModifier(targetGuid, mod)` lo asocia a un `IModifiable` de la entidad target.
3. El modifier corre su `OnLoad()` según `Lifetime`:
   - `Turns` → se suscribe a `TickEvent` y decrementa `Duration` en cada trigger cuyo `args[0]` matche `CarrierId`.
   - `Run` → se suscribe a `OnRunEnd`. Al dispararse, se remueve entero.
   - `Encounter` → se suscribe a `OnCombatEnd`. Al dispararse, se remueve entero.
   - `Permanent` → **no** se suscribe a nada. La única forma de sacarlo es `EffRemoveModifier` (§8.7) buscando por `ModifierId` o por tipo.
4. Cuando el modifier se remueve (por cualquiera de los caminos anteriores) dispara `OnModifierRemoved(OwnerId, ModifierId)` para que listeners externos reaccionen.

### 3.5 Rehidratación desde save (§15)

El `Modifier<T>` se serializa como data puro — sus suscripciones al `EventManager` no se persisten (son handlers runtime) y su `_resolvedOp` está marcado `[NonSerialized]`. Al cargar un save que contiene modificadores, el constructor **no se ejecuta**; Unity/Odin instancia el objeto y puebla los campos sin invocar `OnLoad()`. Esto deja al mod huérfano: `Duration` nunca decrementa porque el `TickEvent` no tiene suscriptor, y `_resolvedOp` es null → primer `ApplyModifier` lo re‑resuelve on‑demand pero la suscripción sigue rota.

**Regla.** Todo `ISaveable` que contenga `Modifier<T>` (típicamente `ModifiableAttributes` dentro de `Entity`) **debe** invocar `mod.OnLoad()` sobre cada modifier restaurado dentro de su `RestoreState(...)`. Por eso `OnLoad()` es idempotente (§3.1): llamarlo varias veces no duplica suscripciones. La responsabilidad vive en el saveable que contiene el mod, no en `SaveSystem` — éste no conoce el tipo concreto.

**Cross‑ref.** §1.2 (events `OnRunEnd` / `OnCombatEnd`), §2.3 (Guid ownership), §8 (factories via efectos), §12 (pipeline de daño), §15.2 (`ISaveable.RestoreState` — punto de re‑hook).

---

## 4. Clases y Hero Sheets

### 4.1 `ClassHeroSO`

> Renombrado desde `ClassSO` para ser explícito: es la plantilla del héroe jugable. Subtipo concreto de `BaseEntitySO` (§7.0) — hereda `EntityId`, `DisplayName`, `Description`, `PrefabRef`, `Portrait`, `_baseStats`, `Behaviors`, `CreateRuntimeStats` y la API de accessors. Aquí sólo se declaran los campos **hero‑specific**.

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Class Hero")]
public class ClassHeroSO : BaseEntitySO
{
    [Title("Passive")]
    [InfoBox("La pasiva NO va en el diccionario de stats. Es un campo aparte — no es un valor numérico.")]
    public ClassPassiveSO Passive;

    [Title("Contract")]
    [HideLabel, InlineProperty, OdinSerialize]
    public ContractSheet Sheet = new();                          // §5

    [Title("Dice")]
    public DiceBagSO StartingDiceBag;                            // §6
}
```

**Cross‑ref al parent §7.0.** El `EntityId` del héroe vive en el dropdown común alimentado por `EntityCatalogSO` (§1.1.1) — no hay `ClassId` separado. El visual del héroe (prefab 3D + portrait UI) también viene del parent: `PrefabRef` es `AssetReferenceGameObject` cargado por Addressables (§16.2) y `Portrait` es un `Sprite` para la UI de selección de clase.

**Filtrado por subtipo.** La UI de selección de clase pide `EntityCatalogSO.GetHeroes()` (§1.1.1) — un filtro sobre el catálogo unificado que devuelve sólo los `ClassHeroSO`. Los sistemas que quieren enumerar enemigos piden `GetEnemies()`, los spawners de props usan `GetProps()`, los de NPCs usan `GetNpcs()`. Nadie construye catálogos paralelos.

### 4.2 Stats estándar del héroe

| Stat (tipo) | Valor | Descripción GD | Notas |
|---|---|---|---|
| `Health` (`IModifiable<int>`) | int | Puntos de vida totales. Muere al llegar a 0. | Varía por clase. |
| `Energy` (`IModifiable<int>`) | int | Recurso de acciones por turno. | Máx 4. No acumulable. |
| `Speed` (`IModifiable<int>`) | int | Orden de turno dentro del round. | **Oculta** en UI. |
| `Shield` (`IModifiable<int>`) | int | Escudo temporal del sistema de defensa. | Se resetea a 0 en `OnTurnStarted` (§12.4). |
| `Gold` (`IModifiable<int>`) | int | Moneda de la run. Se gasta en shops (§17.F), craps (§17.C) y se gana vía rewards (§19). | Persistido en `RunProgress` (§15). |
| `MovementRange` (`IModifiable<int>`) | int | Casillas que puede moverse por acción de movimiento. | Afectado por Slow/Haste/Root (§B.3). |
| `OutgoingDamageMultiplier` (`IModifiable<float>`) | float | Multiplicador al daño saliente. | Base 1.0. |
| `IncomingDamageMultiplier` (`IModifiable<float>`) | float | Multiplicador al daño entrante. | Base 1.0. |
| `ShopPriceMultiplier` (`IModifiable<float>`) | float | Multiplicador aplicado a precios de shop. | Base 1.0. Pasivas tipo "Comerciante" lo reducen (§17.F.8). |

**Nota importante.** `Passive` **NO es un stat**. No vive en el diccionario `_baseStats` (heredado de §7.0). Es un campo aparte porque:

1. No tiene un valor numérico que se pueda modificar.
2. Los modificadores no se aplican sobre ella.
3. Su lifecycle (hook → unhook al EventManager) es distinto al de los stats.
4. Mezclarla en el diccionario rompería la homogeneidad del contenedor (`ModifiableAttributes` asume `IModifiable` en runtime).

### 4.3 Filtrado de tipos en el inspector

El `[TypeFilter]` resuelve en el editor los tipos concretos (no abstractos, no interfaces) que derivan de `IAttribute`. Con eso, al agregar una entrada al diccionario en el inspector, Odin muestra un dropdown con **sólo** los stats válidos — no hay que escribir nombres a mano ni arriesgar typos.

Para restringir aún más (ej. excluir stats "internos" que no se deben editar desde el SO), usar un atributo custom `[AttributeStatsFilter]` que devuelva una lista curada.

### 4.4 `ClassPassiveSO`

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Class Passive")]
public class ClassPassiveSO : SerializedScriptableObject
{
    [ValueDropdown(nameof(GetPassiveIds))]
    public string PassiveId;
    public string DisplayName;
    [TextArea] public string Description;

    [Title("Hooks")]
    [InfoBox("Cada hook se evalúa cuando se dispara su TriggerEvent.")]
    [OdinSerialize]
    public List<PassiveHook> Hooks = new();

#if UNITY_EDITOR
    private static IEnumerable<string> GetPassiveIds() =>
        ServiceLocator.TryGetService<EntityCatalogSO>(out var cat) ? cat.AllPassiveIds : Array.Empty<string>();
#endif
}

[Serializable]
public class PassiveHook
{
    public EventName TriggerEvent;
    [OdinSerialize] public EffectData Effect = new();           // §8
}
```

**Ejemplos del GD (Progress System):**

- **Berserker — "Primer golpe ×3"**: hook en `OnRollResolved` con `EffMultiplyDamage` condicionado a "es la primera tirada del combate" (`PCFirstRollOfCombat`).
- **Gambler — "Escalera ×2 + Craps anticipado"**: hook en `OnComboMatched` que duplica daño si `MatchedCombo is ComboStraight`; hook en `OnTurnStarted` que habilita Craps un turno antes.
- **Necromancer — "Triple repetición ×2"**: hook en `OnComboMatched` con check "3+ dados mismo valor".

**Regla de diseño (GD).** La pasiva **no** modifica el `ContractSheet`. Los combos de la clase son fijos; la pasiva modifica el combate (multiplicadores, efectos extra, triggers en eventos).

#### 4.4.1 Lifecycle — `ClassPassiveSO` es ref compartida, el bind vive en `Entity`

`ClassPassiveSO` es un asset **inmutable y compartido** entre todas las instancias de la misma clase. Dos `Entity` del mismo héroe (p. ej. una run en curso + una preview en el menú) comparten la misma ref al SO. Esto significa que **los hooks al `EventManager` no pueden vivir en el SO** — si lo hicieran, habría handlers duplicados o fantasmas según qué instancia los suscribió.

**Regla.** El hookup al bus vive en `Entity.BindPassive(passive)`, que crea handlers **cerrados sobre `InstanceId`** y los guarda localmente para poder des‑suscribirlos en `Entity.Dispose` / `RunEnd`. El SO solo describe **qué** hacer; `Entity` decide **cuándo** (mientras está vivo) y **para quién** (su `InstanceId`).

```csharp
public partial class Entity
{
    private readonly List<(EventName evt, EventManager.EventReceiver handler)> _passiveHandlers = new();

    /// <summary>
    /// Bindea la pasiva al EventManager. Llamado una vez al crear la Entity
    /// (RunBootstrapper §1.1.3.a para el player; no se llama para enemigos
    /// porque ClassPassiveSO es exclusivo de clases). Los handlers se cierran
    /// sobre this.InstanceId y filtran por OwnerId == (Guid)args[0].
    /// </summary>
    public void BindPassive(ClassPassiveSO passive)
    {
        if (passive == null) return;
        if (Passive != null) UnbindPassive();            // idempotente
        Passive = passive;

        foreach (var hook in passive.Hooks)
        {
            var evt = hook.TriggerEvent;
            EventManager.EventReceiver handler = args =>
            {
                // Regla transversal de args[0] (§1.2): el InstanceId primario
                // del evento viene en args[0]. Solo reaccionamos si somos el owner.
                if (args.Length == 0 || !(args[0] is Guid ownerId)) return;
                if (ownerId != InstanceId) return;

                var ctx = new EffectContext(source: this, target: this, eventArgs: args);
                hook.Effect.Execute(ctx);                // §8
            };
            EventManager.Subscribe(evt, handler);
            _passiveHandlers.Add((evt, handler));
        }
    }

    /// <summary>
    /// Des-suscribe todos los hooks. Llamado desde Entity.Dispose()
    /// (implementa IDisposable para ClearScope(Run) en §1.1.3).
    /// </summary>
    public void UnbindPassive()
    {
        foreach (var (evt, handler) in _passiveHandlers)
            EventManager.UnSubscribe(evt, handler);
        _passiveHandlers.Clear();
        Passive = null;
    }
}
```

**Consecuencias.**
- El SO nunca se modifica. No lleva estado de "quién me suscribió".
- Dos entidades distintas apuntando al mismo `ClassPassiveSO` suscriben handlers **independientes**, cada uno filtrando por su propio `InstanceId`. No hay cross‑talk.
- Load de save reinstancia el `Entity` (§15 / §2.4) y vuelve a llamar `BindPassive` — los handlers del snapshot previo se perdieron con la `Entity` anterior, no hay leak.
- Tests: crear `Entity` + `BindPassive` + disparar event + assert + `UnbindPassive` + assert no‑op.

**Anti‑patrón.** `passive.Subscribe(entity)` en el SO — queda desactualizado acá y se debe reemplazar por `entity.BindPassive(passive)`.

### 4.5 Flujo de inicio de run

```
ElegirClase(ClassHeroSO)
  ↓
ArmarBolsa(DiceBagSO editable en UI)                              // §6
  ↓
RunStart
  ↓
Player.InstanceId       = Guid.NewGuid()
Player.RuntimeStats     = hero.CreateRuntimeStats()
Player.RuntimeBehaviors = hero.CreateRuntimeBehaviors()                 // §7.2b — deep‑clone de slots; library via ServiceLocator
Player.Sheet            = hero.Sheet.Instantiate()                     // clone para tachar combos
Player.DiceBag          = hero.StartingDiceBag.Clone()
Player.BindPassive(hero.Passive)                                       // §4.4.1 — crea handlers cerrados sobre InstanceId
  ↓
SaveSystem.Register(Player)                                        // §15
SaveSystem.CaptureAll(); SaveSystem.Flush(SaveTrigger.RunStart)
```

**Cross‑ref.** §2 (ModifiableAttributes + Guid), §2.4 (runtime `Entity`), §5 (ContractSheet), §6 (DiceBag), §8 (EffectData en pasivas), §14 (unlocks), §15 (save).

### 2.4 Runtime `Entity` — instancia de gameplay

La clase runtime que representa cualquier entidad viva en el juego. Se crea al spawnear al héroe (§4.5), un enemigo (§13.4), un prop (§7.1b) o un NPC (§7.6). **No es un SO** — es una instancia en memoria que vive durante la run (o durante el combate, según la entidad).

```csharp
public partial class Entity : ISaveable, IDisposable
{
    // --- Identity -----------------------------------------------------------
    public Guid              InstanceId       { get; } = Guid.NewGuid();
    public BaseEntitySO      Template         { get; }       // SO origen (readonly en runtime)

    // --- Stats --------------------------------------------------------------
    public ModifiableAttributes Attributes    { get; }       // deep‑clone de Template._baseStats (§2.2)

    // --- Behaviors ----------------------------------------------------------
    public List<BaseBehavior> RuntimeBehaviors { get; }      // deep‑clone de Template.Behaviors (§7.2b)

    // --- Hero‑specific (null si no es héroe) --------------------------------
    public ContractSheet     Sheet            { get; set; }  // clone de ClassHeroSO.Sheet (§5.3)
    public DiceBagSO         DiceBag          { get; set; }  // clone de StartingDiceBag (§6.2)
    public ClassPassiveSO    Passive          { get; private set; }  // ref a ClassHeroSO.Passive (§4.4) — set via BindPassive

    // --- Enemy‑specific (null si no es enemigo) -----------------------------
    public AIDecisionNode    AIRoot           { get; set; }  // deep‑clone del árbol (§7.5)

    // --- Scene --------------------------------------------------------------
    public Transform         Transform        { get; set; }  // transform del GameObject instanciado
    public GridCoord         GridPosition     { get; set; }  // posición en la grilla (§B.1)

    // --- Constructor --------------------------------------------------------
    public Entity(BaseEntitySO template)
    {
        Template         = template;
        Attributes       = template.CreateRuntimeStats();
        RuntimeBehaviors = template.CreateRuntimeBehaviors();   // library via ServiceLocator

        if (template is EnemyDataSO enemy && enemy.AIRoot != null)
            AIRoot = SerializationUtility.CreateCopy(enemy.AIRoot);
    }

    // --- ISaveable (§15) ----------------------------------------------------
    public string SaveKey => $"Entity:{InstanceId}";
    public object CaptureState() => new EntitySnapshot
    {
        InstanceId    = InstanceId,
        EntityId      = Template.EntityId,
        Stats         = Attributes.CaptureStats(),
        GridPosition  = GridPosition,
    };
    public void RestoreState(object state) { /* idem inverso */ }

    // --- IDisposable --------------------------------------------------------
    // Llamado por ClearScope(Run) vía SaveSystem al terminar la run, o
    // manualmente cuando un enemigo muere y su Entity se retira. Des-suscribe
    // los handlers de la pasiva (§4.4.1) y cualquier otro lifecycle binding.
    public void Dispose()
    {
        UnbindPassive();                                     // §4.4.1
    }
}
```

**Reglas.**

- `Entity` es **la** unidad de identidad en runtime. Todo lookup (modificadores, pipelines, events) va por `InstanceId` (§2.3).
- Los campos hero‑specific y enemy‑specific son `null` para entities que no los usan. No se crean subclases de `Entity` por subtipo de SO — el polimorfismo vive en los SOs y en los behaviors, no en la instancia runtime.
- `Template` es read‑only — nunca se muta el SO en runtime. Los stats mutables viven en `Attributes`, los behaviors en `RuntimeBehaviors`, el sheet en `Sheet`.
- Los props (`PropEntitySO`) y NPCs (`NpcDataSO`) también son `Entity` — tienen `InstanceId`, `Attributes` (pueden tener Health si son destructibles), `RuntimeBehaviors`. No tienen `AIRoot`, `Sheet` ni `DiceBag`.

**Cross‑ref.** §1.1 (ServiceLocator para lookups), §2.3 (GUID ownership), §4.5 (flujo de inicio de run crea Entity del héroe), §7 (behaviors runtime), §12 (DamagePipeline opera sobre Entity), §13.4 (spawn de enemies crea Entity), §15 (save via ISaveable).

---

## 5. Contrato de Generala + Combos

### 5.1 `BaseComboSO`

Cada combo del Contrato de Generala es un SO reusable, arrastrable a cualquier `ContractSheet`. Un combo implementa su propia regla de matching sobre el resultado final de los 5 dados.

```csharp
public abstract class BaseComboSO : SerializedScriptableObject
{
    [Title("Identity")]
    [ValueDropdown(nameof(GetComboIds))]
    public string ComboId;
    public string DisplayName;
    [TextArea] public string Description;
    public Sprite Icon;

    [Title("Damage")]
    [Tooltip("Daño plano sumado encima de la cuenta del combo. Usado para combos que ignoran el valor de los dados.")]
    [MinValue(0)] public int BaseDamage;

    [Title("Cuenta del combo — fórmula")]
    [Tooltip("Multiplicadores por cara (index 0 = pip 1, … index 5 = pip 6). Default = todos 0 → el combo es flat (sólo BaseDamage).")]
    public float[] ValueMultipliers = new float[6];           // index = pip - 1

    [Tooltip("Multiplicador general aplicado al resultado de la suma ponderada.")]
    [MinValue(0)] public float GeneralMultiplier = 1f;

    [Title("Extra effects (opcional)")]
    [Tooltip("BaseEffects extra que se disparan junto al daño del combo. Upgrades (§19) pueden agregar acá.")]
    [OdinSerialize] public List<EffectData> ExtraEffects = new();    // §8

    [Title("Matching")]
    /// <summary>
    /// Evalúa el combo sobre los 5 dados finales. Devuelve true si aplica.
    /// Los dados vienen como int[] ya resueltos (cara final post‑encantamientos).
    /// </summary>
    public abstract bool Matches(int[] finalDice);

    /// <summary>
    /// Prioridad de matching cuando varios combos son válidos simultáneamente.
    /// Mayor = se prefiere. Generala debe tener la prioridad más alta del sheet.
    /// </summary>
    public virtual int Priority => BaseDamage;

    /// <summary>
    /// Cuenta del combo (§5.1.1). Fórmula:
    ///   cuenta = (Σ dado × ValueMultipliers[dado-1]) × GeneralMultiplier
    /// No incluye BaseDamage — ese se suma aparte en AttackResolver (§12.1).
    /// </summary>
    public float ComputeCount(int[] finalDice)
    {
        float sum = 0f;
        for (int i = 0; i < finalDice.Length; i++)
        {
            int pip = finalDice[i];
            if (pip >= 1 && pip <= 6) sum += pip * ValueMultipliers[pip - 1];
        }
        return sum * GeneralMultiplier;
    }

#if UNITY_EDITOR
    private static IEnumerable<string> GetComboIds() =>
        ServiceLocator.TryGetService<ComboCatalogSO>(out var cat) ? cat.AllIds : Array.Empty<string>();
#endif
}
```

**Invariantes:**

- Los `BaseComboSO` son assets compartidos — **no** se instancian por clase.
- `BaseDamage` es balanceo plano: el Game Designer lo edita desde el inspector.
- Un combo "existe una vez" y puede aparecer en N `ContractSheet`. Tachar un combo en runtime (§5.3) tacha *la entrada en el sheet*, no el asset.

#### 5.1.1 Cuenta del combo — fórmula y ejemplos

Daño final producido por un combo en un ataque:

```
damage = BaseDamage + ComputeCount(finalDice)
       = BaseDamage + (Σ dado × ValueMultipliers[dado-1]) × GeneralMultiplier
```

La multiplicación general **se aplica sobre la suma**, no dado por dado — como en la fórmula del GD. `BaseDamage` queda afuera porque es un piso plano de balanceo; sumarlo dentro del paréntesis haría que `GeneralMultiplier` lo escale con los upgrades, rompiendo la intención.

**Casos canónicos:**

| Combo | `BaseDamage` | `ValueMultipliers` (index 0..5) | `GeneralMultiplier` | Resultado |
|---|---|---|---|---|
| `ComboPair` | 10 | `[0,0,0,0,0,0]` | 1 | Flat 10. |
| `ComboThreeSixes` | 0 | `[0,0,0,0,0,2]` | 1 | Sólo 6s cuentan doble. Si la tirada tiene 3 seis → damage = 3×6×2×1 = 36. |
| `ComboOdds` | 5 | `[1,0,1,0,1,0]` | 1 | Suma los impares tal cual, +5 plano. |
| `ComboGenerala` | 100 | `[1,1,1,1,1,1]` | 1 | Suma de los 5 dados + 100. |

**Upgrades (§19).** Un `ComboUpgradeSO` puede modificar cualquiera de los tres campos (`BaseDamage`, `ValueMultipliers`, `GeneralMultiplier`) y/o agregar a `ExtraEffects`. No persiste entre runs — se aplica sobre el estado run‑scoped del combo (§19).

**Feedback.** `FeedbackRequest` de un combo pone la cuenta en el `BehaviorValues` bag (§9) para que la UI de floating numbers muestre el desglose: `BaseDamage + Count × GeneralMultiplier` aparece como tres segmentos encadenados.

### 5.2 Concretos

Mapeados 1:1 con los combos del GD:

| Asset | Condición | Daño base (GD) |
|---|---|---|
| `ComboPair` | 2 dados iguales | 10 |
| `ComboDoublePair` | 2 pares distintos | 18 |
| `ComboSum4` | Suma de los 4 más altos | 25 |
| `ComboThreeOfKind` | 3 iguales | 28 |
| `ComboFullHouse` | Trío + par | 40 |
| `ComboStraight` | 5 consecutivos | 35 |
| `ComboPoker` | 4 iguales | 60 |
| `ComboGenerala` | 5 iguales | 100 |
| `ComboThreeSixes` | ≥3 dados en 6 | 42 |
| `ComboThreeFives` | ≥3 dados en 5 | 30 |
| `ComboOdds` | Los 5 son impares | 38 |
| `ComboPrimes` | Los 5 son primos (2, 3, 5, 7, …) | 50 |

Ejemplo de concreto:

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Combos/Full House")]
public class ComboFullHouse : BaseComboSO
{
    public override bool Matches(int[] dice)
    {
        var groups = dice.GroupBy(d => d).Select(g => g.Count()).OrderByDescending(c => c).ToArray();
        return groups.Length >= 2 && groups[0] == 3 && groups[1] == 2;
    }
}
```

### 5.3 `ContractSheet`

`[Serializable]` (no SO) embebido en cada `ClassHeroSO` vía `[OdinSerialize]`. Su único campo de contenido es la lista de combos arrastrables.

```csharp
[Serializable]
[HideReferenceObjectPicker]
public class ContractSheet
{
    [ListDrawerSettings(ShowFoldout = false, DraggableItems = true)]
    [OdinSerialize]
    public List<BaseComboSO> Combos = new();

    [NonSerialized] private HashSet<string> _crossedComboIds;

    /// <summary>
    /// Reglas del GD: exactamente 8 entradas, sin duplicados,
    /// última entrada ComboGenerala y con Priority máxima.
    /// </summary>
    public bool Validate(out string error);

    /// <summary>
    /// Evalúa los 5 dados contra los combos disponibles (no tachados).
    /// Devuelve el combo de mayor Priority que matchee, o null si ninguno matchea
    /// → el caller aplica daño mínimo (dado más alto).
    /// </summary>
    public BaseComboSO EvaluateRoll(int[] finalDice);

    /// <summary>
    /// Tacha un combo para el resto de la run a cambio de una mejora
    /// (vida/daño/oro — resuelto por el caller). Idempotente.
    /// </summary>
    public void CrossCombo(BaseComboSO combo);
    public bool IsCrossed(BaseComboSO combo);

    /// <summary>Clona el sheet para instanciarlo en una run.</summary>
    public ContractSheet Instantiate();
}
```

### 5.4 Ejemplo — sheets

**Guerrero** (clase de entrada):

```
Combos = [ Pair, DoublePair, Sum4, ThreeOfKind, FullHouse, Straight, Poker, Generala ]
```

**Necromancer**:

```
Combos = [ ThreeSixes, ThreeFives, Odds, ThreeOfKind, FullHouse, Poker, Primes, Generala ]
```

### 5.5 Combo counters (Balatro‑style)

El GDD menciona un sistema de **contador por combo**: cada ejecución exitosa de un combo incrementa un contador, y al cruzar ciertos thresholds se disparan efectos (stat boost, unlock de pasiva transitoria, modificador scoped a la run, etc.). El doc define la infra abstracta — los thresholds concretos y los rewards son **data**, no código.

```csharp
/// <summary>
/// Hook que el AttackResolver dispara al aplicar un combo. Cualquier sistema
/// interesado (counters, pasivas, achievements) se engancha por acá para no
/// contaminar la pipeline de combate con lógica específica.
/// </summary>
public interface IComboCounterHook
{
    void OnComboExecuted(BaseComboSO combo, AttackResult result, Guid sourceGuid);
}

/// <summary>
/// Estado run‑scoped de contadores por combo. Es ISaveable (§15) así el save
/// system lo captura sin tratamiento especial. Viaja adentro del RunState.
/// </summary>
public class RunComboCounterState : ISaveable
{
    public string SaveKey => "run.combo_counters";
    public Dictionary<string, int> Counts = new();   // comboId → count

    public int Get(string comboId) => Counts.TryGetValue(comboId, out var n) ? n : 0;
    public void Increment(string comboId) => Counts[comboId] = Get(comboId) + 1;

    public object CaptureState() => new Dictionary<string, int>(Counts);
    public void RestoreState(object state) => Counts = new Dictionary<string, int>((Dictionary<string, int>)state);
}
```

**Thresholds** — se declaran como `UnlockConditionSO` (§14.1) que evalúan contra el `RunComboCounterState`. Reutilizamos la abstracción de unlocks en vez de inventar una nueva:

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Combos/Counter Threshold")]
public class ComboCounterThresholdSO : SerializedScriptableObject
{
    [ValueDropdown(nameof(GetComboIds))] public string ComboId;
    [MinValue(1)] public int RequiredCount;

    [Title("Rewards")]
    [InfoBox("Cualquier BaseEffect sirve — el threshold dispara un EffectData normal.")]
    [OdinSerialize] public EffectData OnThresholdReached = new();
}
```

**Flujo**:
1. `AttackResolver.ResolveAttack` aplica el combo y construye el `AttackResult`.
2. Dispara `OnComboMatched` (§1.2) con el `BaseComboSO`.
3. Un `ComboCounterService` registrado en el `ServiceLocator` escucha `OnComboMatched` y hace `state.Increment(combo.ComboId)`.
4. Tras incrementar, recorre los `ComboCounterThresholdSO` del ruleset activo (§14.7). Si algún threshold matchea `Count == RequiredCount`, ejecuta `OnThresholdReached` como un `EffectData` normal (§8).
5. Los rewards son `BaseEffect`s libres — un threshold puede dar `EffAddIntModifier` con `Lifetime = Run`, `EffUnlockPassive`, `EffHeal`, lo que sea.

Todo lo numérico (qué thresholds existen, qué rewards dan) es data de SOs editables desde el inspector. El código no fija ninguna curva.

### 5.6 Strike combos (tachar) — ⚠️ TBD, NO implementar todavía

> **Estado (2026-04-18):** esta mecánica queda **diferida** por decisión de orquestación. El GDD aún no cerró qué stat boost otorga el strike, qué combos son `IStrikableCombo` (cuáles pueden tacharse y cuáles no), ni qué UI usa para que el jugador elija el combo a quemar. Sprint 03 FP **no** implementa `RunStrikeState`, `EffStrikeCombo`, ni el gate de `EvaluateRoll` sobre strike.
>
> **Impacto en `ContractSheet.EvaluateRoll`:** la implementación actual (T97b) no debe consultar `RunStrikeState`. Si y cuando Strike se habilite, se agrega el filtro por `TryGetService<RunStrikeState>` sin romper la API.
>
> **La sección de abajo queda como referencia de diseño** para cuando se retome post-FP. No crear archivos `RunStrikeState.cs` / `EffStrikeCombo.cs` hasta que esta nota se remueva.

Regla del GDD (diseño futuro): el jugador puede **quemar** un combo de su sheet durante la run a cambio de un stat boost temporal. Ese combo deja de ser ejecutable hasta el fin de la run.

```csharp
/// <summary>
/// Marker interface. Los combos que NO pueden ser strikados (p.e. ComboGenerala,
/// si el balance así lo decide) simplemente no la implementan.
/// </summary>
public interface IStrikableCombo { }

/// <summary>
/// Estado run‑scoped de combos tachados. ISaveable → entra al RunState.
/// </summary>
public class RunStrikeState : ISaveable
{
    public string SaveKey => "run.strike_state";
    public HashSet<string> StrikenComboIds = new();

    public bool IsStricken(string comboId) => StrikenComboIds.Contains(comboId);
    public void Strike(string comboId) => StrikenComboIds.Add(comboId);

    public object CaptureState() => new List<string>(StrikenComboIds);
    public void RestoreState(object state) => StrikenComboIds = new HashSet<string>((List<string>)state);
}
```

**Integración con el resolver.** `ContractSheet.EvaluateRoll` filtra combos strikados antes de matchear. `RunStrikeState` es `ServiceScope.Run` (§1.1) — fuera de una run activa (p.e. UI de preview de clase en el main menu) no está registrado, así que el lookup usa `TryGetService` y el filtro degrada a "ningún combo está strikado":

```csharp
public BaseComboSO EvaluateRoll(int[] finalDice)
{
    ServiceLocator.TryGetService<RunStrikeState>(out var strike);   // puede ser null fuera de run
    BaseComboSO best = null;
    foreach (var combo in Combos)
    {
        if (combo == null) continue;
        if (strike != null && strike.IsStricken(combo.ComboId)) continue;   // ← gate solo si hay run
        if (!combo.Matches(finalDice)) continue;
        if (best == null || combo.Priority > best.Priority) best = combo;
    }
    return best;
}
```

**El acto de strike** se modela como un efecto:

```csharp
public class EffStrikeCombo : BaseEffect, IUsesSelection
{
    // El target es un BaseComboSO elegido por el jugador. La Selection usa
    // un TQ_AvailableCombos (§11.2b) que lista los combos disponibles
    // no strikados de la sheet actual.
    public override bool ApplyEffect(EffectContext ctx)
    {
        var combo = ctx.SelectionResult.FirstSelectedCombo;
        if (combo == null || combo is not IStrikableCombo) return false;
        ServiceLocator.GetService<RunStrikeState>().Strike(combo.ComboId);
        EventManager.Trigger(EventName.OnComboCrossed, ctx.SourceGuid, combo.ComboId);
        return true;
    }
}
```

El **reward** del strike (vida / daño / oro / lo que sea) se autorea en el mismo `EffectData` que contiene el `EffStrikeCombo`: un efecto siguiente aplica un `Modifier<T>` con `Lifetime = Run` al stat correspondiente. No hay código específico — el encadenamiento vive en el `EffectData` del SO que ofrece el strike (p.e. `SkullRoomRewardSO`).

**Gate de acción — `!CanAct`.** Strike es una acción del jugador que cuesta 1 Energy y consume un slot de action economy (§12.6). No es invocable si el FSM ya rechazó el turno: concretamente, si `CheckTurnSkip` (§1.3) detecta stun/freeze/sleep, el FSM transiciona directo a `CleanupPlayerModifiers` sin pasar por `PlayerInput`, y el botón de Strike no se expone. Regla para la UI: el `StrikeButton` de la sheet observa `IPlayerService.Active.CanAct` (derivado de los tags de CC activos sobre el player) y queda grayed out cuando es `false`. Misma regla aplica a `Attack`, `Defend` e `Interact` — no hay camino para gastar energía durante un turno skipped.

**Cross‑ref.** §1.3 (FSM `CheckTurnSkip`), §4 (ClassHeroSO), §8 (effects + EffectData + EffRemoveModifier), §12 (AttackResolver usa `EvaluateRoll`), §12.6 (action economy — Strike consume slot), §13.4 (debilidad enemiga referencia `BaseComboSO`), §14 (UnlockConditionSO reutilizado para thresholds), §15 (save de `RunComboCounterState` y `RunStrikeState`), §19 (ComboUpgradeSO).

---

## 6. Dados y Bolsa

### 6.1 Tipos de dado

```csharp
public enum DiceType { D4, D6, D8, D10, D12, D20 }

public static class DiceTypeExt
{
    public static int MaxFace(this DiceType t) => t switch {
        DiceType.D4 => 4, DiceType.D6 => 6, DiceType.D8 => 8,
        DiceType.D10 => 10, DiceType.D12 => 12, DiceType.D20 => 20, _ => 6 };

    public static int MaxPerBag(this DiceType t) => t switch {
        DiceType.D4 => 5, DiceType.D6 => 5, DiceType.D8 => 4,
        DiceType.D10 => 3, DiceType.D12 => 2, DiceType.D20 => 1, _ => 5 };
}
```

**Reglas del GD (Dice Builder).** 5 dados exactos por bolsa, máximos por tipo duros (sin pesos).

### 6.2 `DiceBagSO`

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Dice Bag")]
public class DiceBagSO : ScriptableObject
{
    [ListDrawerSettings(ShowFoldout = false)]
    public List<DiceType> Dice = new();

    public bool Validate(out string error);
    public DiceBagSO Clone();

    private void OnValidate()
    {
        if (Dice.Count != 5)
            Debug.LogWarning($"{name}: DiceBag debe tener 5 dados (tiene {Dice.Count})");

        foreach (var group in Dice.GroupBy(d => d))
            if (group.Count() > group.Key.MaxPerBag())
                Debug.LogWarning($"{name}: {group.Key} excede máximo ({group.Count()}/{group.Key.MaxPerBag()})");
    }
}
```

### 6.3 `DiceRoller`

```csharp
public interface IDiceRoller
{
    int[] RollAll(DiceBagSO bag);
    int[] Reroll(DiceBagSO bag, int[] previousResult, bool[] keep);
}
```

### 6.4 Encantamientos

```csharp
public abstract class DiceEnchantmentSO : SerializedScriptableObject
{
    [ValueDropdown(nameof(GetEnchantmentIds))]
    public string EnchantmentId;
    public string DisplayName;
    public DiceType CompatibleWith;
    [TextArea] public string Description;

    /// <summary>Modifica la cara obtenida al tirar. Ej: D20 → sólo impares.</summary>
    public abstract int TransformRoll(int rawFace);

    /// <summary>Efectos extra al resolver la tirada (quemadura, slow, …).</summary>
    [OdinSerialize] public List<EffectData> OnRollEffects = new();

#if UNITY_EDITOR
    private static IEnumerable<string> GetEnchantmentIds() =>
        ServiceLocator.TryGetService<DiceCatalogSO>(out var cat) ? cat.AllEnchantmentIds : Array.Empty<string>();
#endif
}
```

Ejemplos del GD:

- `EnchantmentIceD6` — si sale 6, dispara `EffApplySlow` al enemigo (1 turno).
- `EnchantmentFireD8` — si sale 8, aplica quemadura (`Modifier<int>` sobre `Health`, duration 2, `Direction = Intrinsic`).
- `EnchantmentOddD20` — mapea caras pares a la impar inmediata inferior.

### 6.5 Reroll budget (energy re‑roll)

Regla del GDD: además de las 3 tiradas base del sistema Generala, el jugador puede gastar 1 de energía para habilitar una **tirada extra**. Aplica tanto a ataque como a skill checks (§12.5).

Modelo — el `DiceRoller` (§6.3) recibe un **hook** opcional que se consulta antes de cerrar la tirada:

```csharp
public interface IRerollBudget
{
    int BaseRollsRemaining { get; }              // cuántas tiradas "gratis" quedan
    bool CanSpendEnergyReroll(Guid playerGuid);  // ¿tiene energía? ¿le queda margen por ruleset?
    bool TrySpendEnergyReroll(Guid playerGuid);  // consume 1 Energy vía AttributesManager
}

public interface IDiceRoller
{
    // ... (RollAll / Reroll del §6.3 siguen igual)

    /// <summary>
    /// Pregunta al budget si hay una tirada más disponible. Devuelve la razón por la
    /// que no se puede si falla (sin energía, ruleset la bloquea, etc.).
    /// </summary>
    RerollAvailability QueryReroll(Guid playerGuid);
}

public readonly struct RerollAvailability
{
    public readonly bool IsFreeRoll;       // true si usa BaseRollsRemaining (no cuesta energía)
    public readonly bool CostsEnergy;      // true si consume un Energy point
    public readonly string BlockedReason;  // null si está disponible
}
```

**Flujo canónico** en un turno de jugador:

```
1. DiceRoller.RollAll(bag)                                   → dice
2. loop: el jugador puede pedir un reroll
     a. DiceRoller.QueryReroll(playerGuid)
     b. Si IsFreeRoll   → gastar BaseRollsRemaining
        Si CostsEnergy → TrySpendEnergyReroll(playerGuid) → gastar Energy
        Si BlockedReason ≠ null → UI muestra el motivo, el jugador pasa
     c. DiceRoller.Reroll(bag, dice, keep)                    → dice'
3. sheet.EvaluateRoll(dice) → combo (§5.3)
```

**Valores límite** — los números (tiradas base, máximo de rerolls por energía, coste en energía) viven en el `RulesetSO` (§14.7), **no** en el código:

```csharp
// RulesetSO
public int BaseRollsPerAttack;            // default 3 (GDD)
public int MaxExtraRerollsByEnergy;       // default N (tunneable por modo de juego)
public int EnergyCostPerExtraReroll;      // default 1
```

Aplica a todas las acciones basadas en dados — ataque Generala, defensa (§12.4), acciones secundarias (§12.5). Un `ActionDefinitionSO` (§12.6) puede declarar que **no** permite energy reroll setteando un flag (algunas skill checks pueden querer ser tirada única y nada más).

**Cross‑ref.** §8 (`OnRollEffects` es pipeline de efectos), §3 (modificadores con dirección), §12.5 (skill checks consumen el mismo budget), §12.6 (action definitions), §14.7 (RulesetSO define los límites numéricos).

---

## 7. Entidades y Behaviors

### 7.0 `BaseEntitySO` — contrato común

Todo lo que existe en el juego con identidad, visual, stats y behaviors — **jugador, enemigos, bosses, objetos interactuables** — hereda de `BaseEntitySO`. Concentra lo genérico en un único lugar para evitar duplicar contrato entre el héroe y el resto de entidades.

```csharp
[HideMonoScript]
public abstract class BaseEntitySO : SerializedScriptableObject
{
    [Title("Identity")]
    [ValueDropdown(nameof(GetEntityIds))]          // §0 — dropdown desde el catálogo
    public string EntityId;
    public string DisplayName;
    [TextArea] public string Description;

    [Title("Visual")]
    public AssetReferenceGameObject PrefabRef;     // Addressables — §16.2
    public Sprite Portrait;                        // UI (HUD, tooltips, selección)

    [Title("Base Stats")]
    [InfoBox("Sólo tipos concretos de IAttribute (filtrado por [TypeFilter]).")]
    [OdinSerialize]
    [TypeFilter(nameof(GetStatTypes))]
    protected Dictionary<Type, IAttribute> _baseStats = new();

    [Title("Behaviors")]
    [InfoBox("Cada slot es Inline (behavior autoreado acá mismo) o FromLibrary " +
             "(referencia por id a un template del BehaviorLibrarySO §7.2b). " +
             "En ambos casos al spawn se hace deep‑clone para que el runtime " +
             "no contamine el template.")]
    [ListDrawerSettings(ShowFoldout = false, DraggableItems = true)]
    [OdinSerialize]
    public List<BehaviorSlot> Behaviors = new();

    // --- Accessors -----------------------------------------------------------

    public T GetStat<T>() where T : class, IAttribute =>
        _baseStats.TryGetValue(typeof(T), out var a) ? a as T : null;

    public V GetStatValue<T, V>() where T : class, IAttribute =>
        GetStat<T>() is T attr ? attr.GetValue<V>() : default;

    /// <summary>
    /// Instancia un bag de stats runtime (IModifiable) para una instancia nueva
    /// de esta entidad (run del héroe, spawn de enemigo, etc.). Duplica los
    /// atributos para no mutar el SO origen.
    /// </summary>
    public ModifiableAttributes CreateRuntimeStats()
    {
        var runtime = new ModifiableAttributes();
        foreach (var (type, staticAttr) in _baseStats)
        {
            if (staticAttr.Duplicate() is IModifiable modifiable)
                runtime.SetAttribute(modifiable);
        }
        return runtime;
    }

    /// <summary>
    /// Resuelve todos los BehaviorSlots a instancias concretas listas para runtime.
    /// Para slots Inline: deep‑clone con SerializationUtility.CreateCopy.
    /// Para slots FromLibrary: pide al library un clon del template por id.
    /// El caller recibe una List<BaseBehavior> independiente del SO y del library.
    /// La library se resuelve internamente vía ServiceLocator (registrada en el
    /// bootstrap §1.1.1); ningún caller necesita pasarla — un único punto de
    /// verdad evita que distintos callsites resuelvan distintas libraries.
    /// </summary>
    public List<BaseBehavior> CreateRuntimeBehaviors()
    {
        var lib = ServiceLocator.GetService<BehaviorLibrarySO>();
        var result = new List<BaseBehavior>(Behaviors.Count);
        foreach (var slot in Behaviors)
        {
            var bh = slot.Resolve(lib);
            if (bh != null) result.Add(bh);
        }
        return result;
    }

#if UNITY_EDITOR
    protected virtual IEnumerable<string> GetEntityIds() =>
        ServiceLocator.TryGetService<EntityCatalogSO>(out var cat)
            ? cat.AllIds
            : Array.Empty<string>();

    protected static IEnumerable<Type> GetStatTypes() =>
        TypeCache.GetTypesDerivedFrom<IAttribute>()
            .Where(t => !t.IsAbstract && !t.IsInterface);
#endif
}
```

**Regla de visuales.** `PrefabRef` es `AssetReferenceGameObject` siempre — nunca `GameObject` directo, nunca lookup por string contra un catálogo intermedio. Esto alinea con §1.1 (prohibido `Resources.Load` en hot paths) y §16.2 (todo asset dinámico va por Addressables). El `EntityCatalogSO` (§1.1.1) preloadea los `PrefabRef` de su lista en el bootstrap; en runtime la entidad ya tiene el prefab cacheado cuando se instancia.

**Regla de ids.** `EntityId` es string pero **siempre** se edita vía `[ValueDropdown(GetEntityIds)]` alimentado desde `EntityCatalogSO.AllIds`. Si un id se referencia desde otro SO (unlocks, rewards, quests), ese SO usa el mismo dropdown apuntando al mismo catálogo — nunca hay strings libres de entidades en el proyecto.

**Subtipos.** `BaseEntitySO` es `abstract` — no se instancia. Las cuatro concreciones oficiales son:

- `ClassHeroSO` (§4.1) — héroe jugable (pasiva, sheet, starting dice bag).
- `EnemyDataSO` (§7.1) — enemigos y bosses (archetype de combate + árbol de IA polimórfico §7.5).
- `PropEntitySO` (§7.1b) — objetos interactivos inertes de sala (chests, doors, potions, trampas, decoración). Sin IA, sin archetype de combate.
- `NpcDataSO` (§7.6) — vendors, dialogue NPCs, quest givers, trainers, flavor chars. Sin IA, sin archetype.

Agregar nuevos subtipos es válido mientras hereden el contrato base. La separación `EnemyDataSO` / `PropEntitySO` evita que un cofre arrastre un `AIRoot` que nunca evalúa, o que un enemigo cargue un `PropCategory` irrelevante.

### 7.1 `EnemyDataSO`

Subtipo concreto de `BaseEntitySO` para **enemigos y bosses** — criaturas hostiles que toman turnos y resuelven combate. Los objetos interactivos inertes (cofres, puertas, pociones) viven en `PropEntitySO` (§7.1b); los NPCs amigables en `NpcDataSO` (§7.6). **El jugador no usa esta plantilla** — usa `ClassHeroSO` (§4.1), que también hereda de `BaseEntitySO`.

> Renombrado desde `EntityDataSO` en v10. El enum `EntityArchetype` pierde el valor `Interactable` y queda como `EnemyArchetype` (sólo combate táctico).

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Enemy Data")]
public class EnemyDataSO : BaseEntitySO
{
    [Title("Combat")]
    public EnemyArchetype Archetype;               // Melee | Ranged | Support | Boss

    [Title("Weakness")]
    [InfoBox("Combo al que este enemigo es débil. Si el jugador matchea este combo, " +
             "el DamagePipeline aplica WeaknessMultiplier al daño (§12.2).")]
    [OdinSerialize]
    public BaseComboSO WeakAgainst;                // null = sin debilidad

    [InfoBox("Multiplicador aplicado cuando se golpea la debilidad. Default 1.5 = 50% más daño.")]
    [MinValue(1f)] public float WeaknessMultiplier = 1.5f;

    [Title("AI Decision Tree")]
    [InfoBox("Árbol polimórfico inline — cada EnemyDataSO tiene su propia copia. Ver §7.5.")]
    [OdinSerialize]
    [SerializeReference]
    public AIDecisionNode AIRoot;                  // §7.5
}

public enum EnemyArchetype { Melee, Ranged, Support, Boss }
```

Todo lo genérico (`EntityId`, `DisplayName`, `PrefabRef`, `Portrait`, `_baseStats`, `Behaviors`, `CreateRuntimeStats`, `CreateRuntimeBehaviors`) viene del parent §7.0. Aquí sólo viven los campos que **el héroe no comparte**: el `Archetype` (que el héroe no tiene — es una clase, no un arquetipo táctico) y el árbol de IA (el héroe lo controla el jugador, no hay decisión automática).

### 7.1b `PropEntitySO`

Subtipo concreto de `BaseEntitySO` para **objetos interactivos inertes de sala**: cofres, puertas, pociones del suelo, trampas, torches, decoración interactuable. No tienen IA ni archetype de combate — reaccionan a triggers (`OnInteract`, `OnEntered`, `OnPlayerInRange`) a través de sus `Behaviors` (§7.2).

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Prop Entity")]
public class PropEntitySO : BaseEntitySO
{
    [Title("Prop‑specific")]
    public PropCategory Category;                  // Chest | Door | Potion | Trap | Torch | Decoration | Generic

    [InfoBox("Si el prop debe desaparecer al consumirse (cofre abierto, " +
             "poción levantada), el behavior que lo consume debe disparar " +
             "EffRemoveRoomEntity — que setea RoomObjectState.Consumed " +
             "(§13.6), y el DungeonManager/InteractionService lo ignoran " +
             "en los siguientes pases por la sala.")]
    public bool SingleUse;
}

public enum PropCategory { Chest, Door, Potion, Trap, Torch, Decoration, Generic }
```

**Interactabilidad.** Igual que los NPCs (§7.6), la interactabilidad de un prop es decisión del prefab: si el `PrefabRef` lleva un `InteractableComponent` (§7.7) en su jerarquía, el `IInteractionService` lo toma; si no, es puramente decorativo (p.ej. una antorcha ambient). No hay flag en el SO.

**Persistencia entre salas.** `PropEntitySO` instanciado + `RoomObjectState` (§13.6) cubre el caso "entro a la sala con el cofre ya abierto": `Consumed = true` gana sobre cualquier `PhaseInteractionRule` y el service lo ignora.

#### Patrón documentado — trampas activas

Las trampas (spikes, lava tiles, poison vents) son `PropEntitySO` con `Category = Trap`. Se modelan exclusivamente con `BaseBehavior` y triggers automáticos — no requieren código nuevo ni sistema dedicado.

**Trampa de daño al pisar (spike trap):**

```
SpikeTrapPrefab (spawned desde PropEntitySO "SpikeTrap")
├── NO InteractableComponent (no tiene prompt, no es accionable)
└── PropEntitySO.Behaviors:
    └── BaseBehavior(OnEntered, AllowedPhases: All)
        └── EffectData:
            ├── Effect: EffDealDamage(amount: 5, target: TriggeringEntity)
            └── Effect: EffPlayFeedback("spike_activate")
```

**Trampa de proximidad (poison vent):**

```
PoisonVentPrefab (spawned desde PropEntitySO "PoisonVent")
├── NO InteractableComponent
└── PropEntitySO.Behaviors:
    └── BaseBehavior(OnPlayerInRange, AllowedPhases: All)
        └── EffectData:
            ├── Effect: EffAddStatusEffect(Poison, Duration: 3)
            └── Effect: EffPlayFeedback("poison_cloud")
```

**Trampa single-use (bear trap):**

```
BearTrapPrefab (spawned desde PropEntitySO "BearTrap", SingleUse = true)
├── NO InteractableComponent
└── PropEntitySO.Behaviors:
    └── BaseBehavior(OnEntered, AllowedPhases: All)
        └── EffectData:
            ├── Effect: EffDealDamage(amount: 10, target: TriggeringEntity)
            ├── Effect: EffAddStatusEffect(Root, Duration: 1)
            └── Effect: EffConsumeProp    // desaparece después de activarse
```

**Regla.** Las trampas no necesitan `InteractableComponent` porque no son "accionables" — actúan automáticamente vía `OnEntered` o `OnPlayerInRange`. El `IMovementService` (§B) dispara estos triggers al mover al jugador. Pueden afectar también a enemigos si el diseñador configura el `EffDealDamage` con un `TargetQuery` que incluya al trigger entity sin filtrar por bando.

**Cross‑ref §7.1b:** §7.0 (BaseEntitySO parent), §7.2 (triggers + behaviors), §7.7 (interacción via `InteractableComponent`), §8 (`EffRemoveRoomEntity`), §13.4 (pools de props en salas), §13.6 (`RoomObjectState.Consumed`).

### 7.2 `BaseBehavior`

Clase abstracta serializable (no SO; vive embebida en cualquier subtipo de `BaseEntitySO` vía Odin, o en un `BehaviorLibrarySO` §7.2b cuando se comparte entre entities).

```csharp
[Serializable]
[HideReferenceObjectPicker]
public abstract class BaseBehavior
{
    [Title("@BehaviorName")]
    [EnumToggleButtons] public BehaviorTrigger Trigger;

    [ShowIf(nameof(IsEventTrigger))]
    public EventName TriggerEvent;

    [Title("Phase filter")]
    [InfoBox("En qué GamePhase este behavior es elegible. `All` por default. " +
             "El trigger sigue siendo el gate principal; AllowedPhases es un " +
             "filtro adicional que los services consultan antes de ejecutar.")]
    [EnumToggleButtons]
    public GamePhaseMask AllowedPhases = GamePhaseMask.All;

    [Title("Effects")]
    [ListDrawerSettings(ShowFoldout = false)]
    [OdinSerialize]
    public List<EffectData> Effects = new();                      // §8

    public abstract string BehaviorName { get; }

    public virtual bool CanExecute(BehaviorContext ctx);
    public abstract void Execute(BehaviorContext ctx);

    // Runtime value bag — §9
    [NonSerialized]
    private Dictionary<BehaviorValueKey, List<BaseBehaviorStoredValue>> _storedValues;

    public IReadOnlyDictionary<BehaviorValueKey, List<BaseBehaviorStoredValue>> StoredValues => _storedValues;

    public void SetBehaviorValue(BehaviorValueKey key, BaseBehaviorStoredValue value);
    public bool TryGetBehaviorValues<T>(BehaviorValueKey key, out IReadOnlyList<T> values)
        where T : BaseBehaviorStoredValue;
    public void ClearBehaviorValues();

    private bool IsEventTrigger() => Trigger == BehaviorTrigger.OnEvent;
}

public enum BehaviorTrigger
{
    OnTurnStart, OnTurnEnd, OnEvent,
    OnDamaged, OnEntered, OnPlayerInRange, OnInteract,
}

[Flags]
public enum GamePhaseMask
{
    None        = 0,
    Exploration = 1 << 1,  // bit = GamePhase.Exploration (§12.0)
    Combat      = 1 << 2,  // bit = GamePhase.Combat
    All         = ~0,
}
```

**Semántica de cada trigger.** Distintos managers disparan distintos triggers; cada dispatcher construye el **subtipo de `BehaviorContext`** (§7.3) que corresponde, garantizando que los datos trigger‑específicos (damage info, tile del movimiento, payload del evento…) lleguen hasta los effects sin casteos manuales ni lookups.

| Trigger | Quién lo dispara | Cuándo | Context subtype (§7.3) | Uso típico |
|---|---|---|---|---|
| `OnEntered` | `IMovementService` (§B) | El jugador pisa el tile del owner | `MovementBehaviorContext` | Pad de presión, puerta en exploration (auto‑pass), item que se levanta al caminar encima |
| `OnInteract` | `IInteractionService` (§7.7) | El jugador confirma un prompt con input (o click en UI) estando en rango | `InteractionBehaviorContext` | Cofre, puerta forzada en combate, NPC vendor, shop |
| `OnPlayerInRange` | `IInteractionService` (§7.7) | El jugador entra al rango del owner, **sin** requerir input | `MovementBehaviorContext` | Trampa armable, sensor, buff area que se aplica al acercarse |
| `OnDamaged` | `DamagePipeline` (§12.2) | El owner recibe daño resuelto | `DamageBehaviorContext` | Contragolpe, reactivo |
| `OnTurnStart / OnTurnEnd` | `TurnManager` (§12) | El owner empieza / termina turno | `TurnBehaviorContext` | Regen, tick de buff |
| `OnEvent` | `EventManager` (§1.2) | El `TriggerEvent` configurado se dispara | `EventBehaviorContext` | Hook genérico cross‑sistema |

**Regla de `AllowedPhases`.** Todos los dispatchers (el `IInteractionService`, el `IMovementService`, el `TurnManager`, el `DamagePipeline`, etc.) filtran los behaviors del owner por `AllowedPhases.HasFlag(currentPhase)` antes de ejecutar. Un behavior que no pasa el filtro se ignora silenciosamente en esa fase — sin errores, sin warning. Esto permite que una misma entidad exponga **comportamientos distintos según la fase** (ver el ejemplo canónico de la puerta en §7.7.3): un behavior `OnEntered` con `AllowedPhases = Exploration` auto‑cambia de sala al pisar el tile, y en paralelo un behavior `OnInteract` con `AllowedPhases = Combat` expone un prompt "Forzar puerta" que resuelve un skill check.

**Dónde vive `GamePhase`.** El enum `GamePhase` (`None`, `Exploration`, `Combat`, `Loading`, `GameOver`) y el enum `PhaseOverlay` (`Pause`, `Cutscene`, `Craps`) están formalizados en **§12.0** junto con el `IPhaseService` (stack con Push/Pop/Replace), la `PhaseTransitionMatrixSO` editable desde engine, y el `DeathWatchService`. `GamePhaseMask` sólo tiene bits para las fases base (`Exploration`, `Combat`) — los overlays no participan en el filtro de behaviors (§12.0.7). `Shop` y `Craps` ya no son fases: la shop son props en el piso (§17.F) y Craps es overlay exclusivo de combate (§17.C). El `BehaviorContext` base (§7.3) expone `CurrentPhase : GamePhase` para que los behaviors no tengan que hacer lookup contra `IPhaseService` en cada `Execute`.

### 7.2b `BehaviorSlot` y `BehaviorLibrarySO`

**Problema.** Si `BaseEntitySO.Behaviors` es una `List<BaseBehavior>` inline, dos enemigos con "ataque melee básico" duplican el mismo bloque palabra por palabra. Mantener el balance entre N enemigos pasa a ser editar N SOs cada vez que cambia un número. Pero un fix naive — compartir una referencia directa a un behavior reusable — rompe el mismo principio del §7.5: editar el template centralmente afecta a todos los enemigos que lo usan, incluso los que deberían tener variantes.

**Diseño.** Un behavior reusable vive como **template** en un `BehaviorLibrarySO`; las entities lo referencian vía un `BehaviorSlot` que puede ser **inline** (autoreado en el SO) o **from library** (referencia por id). En ambos casos, al spawn se hace **deep‑clone** — el runtime nunca mantiene una referencia viva al template. Si el diseñador necesita desviarse de un template en un enemy puntual, clickea un botón del inspector ("Break out to inline") que copia el template en el slot y lo deja editable, rompiendo el vínculo con el library.

La regla replica exactamente el patrón de `SerializationUtility.CreateCopy` que §7.5 usa para los AI trees.

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Behavior Library")]
public class BehaviorLibrarySO : SerializedScriptableObject
{
    [Title("Templates")]
    [InfoBox("Los templates se DEEP‑CLONEAN al spawn. Editar un template " +
             "acá afecta a todas las entities que lo referencien la PRÓXIMA " +
             "vez que se spawneen — pero NO al runtime actual.")]
    [ListDrawerSettings(ShowFoldout = false, DraggableItems = true)]
    [OdinSerialize]
    private Dictionary<string, BaseBehavior> _templates = new();

    public IEnumerable<string> AllTemplateIds => _templates.Keys;

    public BaseBehavior GetClone(string templateId)
    {
        if (string.IsNullOrEmpty(templateId)) return null;
        if (!_templates.TryGetValue(templateId, out var template)) return null;
        return SerializationUtility.CreateCopy(template);
    }
}

[Serializable]
[HideReferenceObjectPicker]
public class BehaviorSlot
{
    public enum SlotMode { Inline, FromLibrary }

    [EnumToggleButtons, HideLabel]
    public SlotMode Mode = SlotMode.Inline;

    [ShowIf(nameof(IsInline))]
    [OdinSerialize, SerializeReference]
    [HideReferenceObjectPicker]
    public BaseBehavior Inline;

    [ShowIf(nameof(IsFromLibrary))]
    [ValueDropdown(nameof(GetTemplateIds))]
    public string TemplateId;

    [ShowIf(nameof(IsFromLibrary))]
    [Button("Break out to inline"), GUIColor(1f, 0.8f, 0.4f)]
    private void BreakOut()
    {
        if (!ServiceLocator.TryGetService<BehaviorLibrarySO>(out var lib)) return;
        var clone = lib.GetClone(TemplateId);
        if (clone == null) return;
        Inline = clone;
        Mode = SlotMode.Inline;
        TemplateId = null;
    }

    public BaseBehavior Resolve(BehaviorLibrarySO lib)
    {
        return Mode == SlotMode.Inline
            ? SerializationUtility.CreateCopy(Inline)
            : lib?.GetClone(TemplateId);
    }

    private bool IsInline()      => Mode == SlotMode.Inline;
    private bool IsFromLibrary() => Mode == SlotMode.FromLibrary;

#if UNITY_EDITOR
    private static IEnumerable<string> GetTemplateIds() =>
        ServiceLocator.TryGetService<BehaviorLibrarySO>(out var lib)
            ? lib.AllTemplateIds
            : Array.Empty<string>();
#endif
}
```

**Sin overrides campo‑a‑campo.** A diferencia de Unity prefab variants, un `BehaviorSlot` `FromLibrary` **no** expone overrides granulares sobre el template. Si el diseñador quiere un behavior distinto en 1 enemy de 20, usa "Break out to inline" — el slot se rellena con una copia fresca del template y queda totalmente editable, sin afectar al resto. Esto mantiene el modelo simple y evita una capa de reflection‑based overrides que complicaría el inspector sin beneficio real (los ajustes per‑enemy son rarísimos vs. el patrón de "100 enemies reusan el mismo melee basic").

**Registro y lifecycle.**

- `BehaviorLibrarySO` se agrega al `ServiceBootstrapSO` (§1.1) y se registra en `ServiceLocator` en `RegisterAll`.
- `BaseEntitySO.CreateRuntimeBehaviors()` (§7.0) resuelve la library desde el `ServiceLocator`, deep‑clonea cada slot y devuelve la lista lista para attacharse al runtime `Entity`. La library no se pasa por parámetro — un único punto de verdad evita drift entre callsites.
- El spawn de héroe (§4.5), enemigos y props (§13.4) llama `CreateRuntimeBehaviors` además de `CreateRuntimeStats`.

**Regla importante.** Los `StoredValues` (§9) viven en la instancia runtime del behavior, no en el template. Deep‑clonear al spawn garantiza que dos enemies con el mismo template melee no compartan su bag de valores almacenados — cada uno lleva el suyo.

**Cross‑ref §7.2b:** §1.1 (`ServiceBootstrapSO`), §4.5 (run init llama `CreateRuntimeBehaviors`), §7.0 (`BaseEntitySO.Behaviors : List<BehaviorSlot>` + resolver), §7.5 (mismo patrón `SerializationUtility.CreateCopy`), §9 (StoredValues por instancia, no por template), §13.4 (spawn de enemies/props).

### 7.3 `BehaviorContext` (polimórfico por trigger)

`BehaviorContext` es una **jerarquía polimórfica**, no un struct plano. La base concentra los campos comunes a todo trigger (owner, dueño, fase actual, behavior source); los subtipos sellados cargan los datos específicos que cada dispatcher puede producir — `DamageInfo` en `OnDamaged`, tile previo/actual en `OnEntered`, `InteractableComponent` en `OnInteract`, etc.

Esto cierra dos gaps del diseño anterior: los behaviors `OnDamaged` no tenían cómo leer el daño resuelto, y los behaviors `OnEvent` no tenían payload. También elimina la inconsistencia con `EffectContext.TriggeringEntity` (§8.4), que ya existía pero no tenía un camino formal de ida.

```csharp
public abstract class BehaviorContext
{
    public Guid         OwnerGuid;           // entity dueña del behavior
    public Entity       SourceEntity;        // resuelta vía AttributesManager
    public Entity       TriggeringEntity;    // quien disparó el trigger — player, atacante, evento
    public string       CurrentRoomId;
    public GamePhase    CurrentPhase;        // snapshot del TurnManager al momento del dispatch
    public BaseBehavior SourceBehavior;      // para StoredValues §9
}

/// <summary>Dispatchers: TurnManager.OnTurnStart / OnTurnEnd.</summary>
public sealed class TurnBehaviorContext : BehaviorContext
{
    public int RoundIndex;
    public int TurnIndex;                    // posición del owner dentro del round
    public bool IsTurnStart;                 // false → TurnEnd
}

/// <summary>Dispatcher: DamagePipeline.OnDamaged (§12.2).</summary>
public sealed class DamageBehaviorContext : BehaviorContext
{
    public DamageInfo Damage;                // valor final post‑pipeline
    public Entity     Attacker;              // puede coincidir con TriggeringEntity
    public bool       BlockedByShield;
    public bool       WasLethal;             // true si el owner murió por este hit
}

/// <summary>Dispatcher: IInteractionService.ExecuteCurrent (§7.7.4).</summary>
public sealed class InteractionBehaviorContext : BehaviorContext
{
    public InteractableComponent Interactable;
    public PhaseInteractionRule  Rule;       // la rule activa en la fase actual
    public int                    DistanceToTriggerer;
}

/// <summary>Dispatcher: IMovementService (§B) — OnEntered / OnPlayerInRange.</summary>
public sealed class MovementBehaviorContext : BehaviorContext
{
    public Vector2Int PreviousTile;          // puede ser (−1,−1) si es el primer tile del round
    public Vector2Int CurrentTile;
    public int        DistanceToOwner;       // en tiles, relevante para OnPlayerInRange
    public bool       EnteredThisStep;       // false → sólo cambio de distancia dentro del rango
}

/// <summary>Dispatcher: EventManager (§1.2) — trigger = OnEvent.</summary>
public sealed class EventBehaviorContext : BehaviorContext
{
    public EventName EventName;
    public object    Payload;                // lo define el publisher en §1.2
}
```

**Contrato dispatcher → subtipo.** Cada dispatcher es responsable de construir el subtipo correcto y llamar `behavior.Execute(ctx)` pasando la referencia base. Los effects que necesitan datos específicos hacen `ctx.TryGetTriggerContext<T>(out var typed)` sobre el `EffectContext` (§8.4), que expone el `BehaviorContext` tipado.

**Regla de serialización.** Los subtipos son `sealed` — si aparece un trigger nuevo se agrega un subtipo más, no se muta uno existente. Esto preserva la estabilidad de los effects que hacen casts tipados.

**Cross‑ref §7.3:** §1.2 (`EventName` + payloads), §7.2 (tabla trigger → dispatcher → subtype), §7.7.4 (construcción del `InteractionBehaviorContext`), §8.4 (`EffectContext.TriggerContext`), §8.5 (capability `IRequiresTriggerContext<TCtx>`), §12.2 (`DamagePipeline` construye `DamageBehaviorContext`), §B (`IMovementService` construye `MovementBehaviorContext`).

### 7.4 Usos

- **Enemigos** (`EnemyDataSO` §7.1): cada enemy tiene un `BaseBehavior` por acción (atacar, moverse hacia el jugador, kitear, curar aliados). **Qué** behavior ejecutar en cada turno lo decide el árbol de IA (§7.5), no un switch sobre `Archetype`.
- **Bosses** (`EnemyDataSO` con `Archetype = Boss`): mismos `BaseBehavior` + pasivas/debuffs adicionales (hooks en `EventName`).
- **Objetos inertes interactuables** (`PropEntitySO` §7.1b — cofres, puertas, pociones, trampas): uno o más `BaseBehavior` con `Trigger = OnInteract` y/o `OnEntered`, usualmente filtrados con `AllowedPhases` para tener comportamientos distintos por fase. Ver §7.7 para el flujo completo (proximidad → prompt → dispatch).
- **NPCs** (`NpcDataSO` §7.6 — vendors, dialogue, quest givers): mismo patrón que props pero con semántica social; los effects típicos son `EffOpenScreen(ScreenId.Dialogue, payload: dialogueGraph)`. Los NPC vendors **no abren una pantalla de shop** — los ítems ya están en el piso como props del `IShopManagerService` (§17.F); el vendor puede dar diálogo, descuentos o lore vía `DialogueGraphSO`.

### 7.5 AI Decision Trees

Los enemigos toman decisiones con un **árbol de decisión polimórfico** autorable desde el editor. **No** se usa un enum `EnemyBehavior { Aggressive, Ranged, … }` ni una FSM fija — cada `EnemyDataSO` tiene su propio árbol que el diseñador construye arrastrando nodos. Los otros subtipos (`PropEntitySO`, `NpcDataSO`, `ClassHeroSO`) **no** tienen árbol — los props y NPCs reaccionan a triggers via `Behaviors` (§7.2) y el héroe lo controla el jugador.

**Requisito crítico**: el diseñador tiene que poder agarrar un árbol base (ej: "patrón de ataque cuerpo a cuerpo") y overridear un valor puntual (ej: `BaseDamageOverride = 15` en este enemigo, `25` en aquel). Si los nodos fueran `ScriptableObject` compartidos, editar el nodo en un enemigo afectaría a todos los que compartan el asset. Por eso los nodos son **clases serializables polimórficas inline** con `[SerializeReference]` (nativo de Unity) o `[OdinSerialize]` — cada `EnemyDataSO` tiene **su propia copia** del árbol completo, independiente del resto.

**Forma del árbol**:

```csharp
[Serializable]
public abstract class AIDecisionNode
{
    public string Label;                        // identificador legible para el graph editor
    public abstract AIResult Evaluate(AIContext ctx);
}

/// <summary>Nodo que hace algo (atacar, mover, usar habilidad, emitir log).</summary>
public abstract class AIActionNode : AIDecisionNode { }

/// <summary>Nodo que ramifica en base a una condición del estado del tablero.</summary>
public abstract class AIQuestionNode : AIDecisionNode { }

// --- Nodos concretos — cada uno expone los campos que el diseñador puede
//     overridear per‑enemigo. BaseDamageOverride, TileBudget, etc. viven
//     EN ESTA INSTANCIA del nodo, por eso no pueden ser SOs. ---

public class AINode_Attack : AIActionNode
{
    // ActionId referencia ActionCatalogSO (§12.6) — catálogo maestro unificado
    // de acciones (ataques, abilities, combos, items activos). Un AINode_Attack
    // típico apunta a una ActionDefinitionSO con ActionType = Attack.
    [ValueDropdown(nameof(GetActionIds))]
    public string ActionId;
    public int BaseDamageOverride;                      // override per‑enemigo
    [OdinSerialize] public List<EffectData> ExtraEffects = new();
    public override AIResult Evaluate(AIContext ctx) { /* resuelve attack vía DamagePipeline §12 */ }

#if UNITY_EDITOR
    private static IEnumerable<string> GetActionIds() =>
        ServiceLocator.TryGetService<ActionCatalogSO>(out var cat)
            ? cat.GetIdsByType(ActionType.Attack)
            : Array.Empty<string>();
#endif
}

public class AINode_Move : AIActionNode
{
    public MovePattern Pattern;                         // ApproachClosest | KeepDistance | Flee | Patrol
    public int TileBudget;                              // override per‑enemigo
    public override AIResult Evaluate(AIContext ctx) { /* usa IMovementService §B */ }
}

public class AINode_UseAbility : AIActionNode
{
    // Mismo catálogo unificado que AINode_Attack — filtrado por ActionType.Ability.
    [ValueDropdown(nameof(GetActionIds))]
    public string ActionId;
    [OdinSerialize] public EffectData Effect = new();
    public override AIResult Evaluate(AIContext ctx) { /* ejecuta Effect del §8 */ }

#if UNITY_EDITOR
    private static IEnumerable<string> GetActionIds() =>
        ServiceLocator.TryGetService<ActionCatalogSO>(out var cat)
            ? cat.GetIdsByType(ActionType.Ability)
            : Array.Empty<string>();
#endif
}

/// <summary>
/// Nodo de acción "no hacer nada". El enemigo pasa su turno deliberadamente.
/// Útil para enemigos que esperan a que el jugador se acerque, o para
/// modelar patrones rítmicos (atacar-esperar-atacar).
/// </summary>
public class AINode_Wait : AIActionNode
{
    [InfoBox("El enemigo no realiza ninguna acción este turno. " +
             "Puede usarse como rama de un AINode_If para esperar " +
             "hasta que se cumpla una condición (ej: jugador en rango).")]
    public override AIResult Evaluate(AIContext ctx) => AIResult.Succeeded;
}

public class AINode_Sequence : AIDecisionNode
{
    [SerializeReference] public List<AIDecisionNode> Children = new();
    // Ejecuta todos los hijos en orden; falla si alguno falla.
}

public class AINode_Selector : AIDecisionNode
{
    [SerializeReference] public List<AIDecisionNode> Children = new();
    // Ejecuta hasta el primer éxito.
}

public class AINode_If : AIQuestionNode
{
    [SerializeReference] public AICondition Condition;
    [SerializeReference] public AIDecisionNode IfTrue;
    [SerializeReference] public AIDecisionNode IfFalse;
    public override AIResult Evaluate(AIContext ctx)
        => (Condition?.Eval(ctx) ?? false) ? IfTrue.Evaluate(ctx) : IfFalse?.Evaluate(ctx) ?? AIResult.Failed;
}

public class AINode_Random : AIDecisionNode
{
    [SerializeReference] public List<WeightedBranch> Branches = new();
    // pickeo ponderado entre hijos — permite patrones no determinísticos
}

[Serializable]
public class WeightedBranch
{
    [MinValue(0)] public int Weight = 1;
    [SerializeReference] public AIDecisionNode Node;
}
```

**Condiciones** — también polimórficas, también inline:

```csharp
[Serializable]
public abstract class AICondition
{
    public abstract bool Eval(AIContext ctx);
}

public class AICond_HPBelow         : AICondition { [Range(0,1)] public float Percent; }
public class AICond_InRange         : AICondition { public int Range; }
public class AICond_AllyAliveWithTag: AICondition { public string AllyTag; }
public class AICond_PlayerHasModifier: AICondition { public string ModifierId; }
public class AICond_RoundNumber     : AICondition { public ComparisonOp Op; public int Value; }
public class AICond_And / AICond_Or / AICond_Not : AICondition { [SerializeReference] public List<AICondition> Children; }
```

**Contexto de ejecución**:

```csharp
public class AIContext
{
    public Entity Self;                          // la entidad que corre la IA
    public Entity CurrentPlayer;                 // target principal
    public RoomInstance Room;                    // §13 — grilla y resto de entidades
    public int RoundIndex;
    public Random Rng;                           // determinístico por run
}

public enum AIResult { Succeeded, Failed, Running }
```

**Raíz del árbol** — ya declarada en §7.1 como `AIRoot : AIDecisionNode` sobre `EnemyDataSO` (vía `[SerializeReference]`). No existe en los otros subtipos.

**Runtime**:

1. Al spawnear el enemigo en una sala, el `DungeonManager` (§13) hace un **deep clone** del árbol: `SerializationUtility.CreateCopy(template.AIRoot)` (Odin) o equivalente con `JsonUtility`. Esto aísla al runtime del asset, así los nodos pueden mantener estado de ejecución (p.e. `Running` en un `AINode_Patrol`) sin contaminar el template.
2. En cada turno del enemigo, el `TurnManager` llama `entity.AIRoot.Evaluate(new AIContext { … })`.
3. El árbol decide qué acciones ejecutar. Las acciones disparan `EffectData` normales — no hay lógica de combate duplicada, se reusa §8 y §12.

**Editor** — el diseñador ve el árbol en el inspector del `EnemyDataSO`. El patrón de dropdown de tipos sale del `[TypeFilter]` de Odin sobre `AIDecisionNode` / `AICondition`. Para proyectos más grandes se puede wrappear con un graph editor custom (xNode / NodeCanvas / GraphView) pero **el modelo de datos no cambia** — los nodos siguen siendo clases serializables.

**Cómo agregar un nodo nuevo**:
1. Crear `class AINode_MiPatron : AIActionNode` (o `AIQuestionNode`).
2. Override `Evaluate(ctx)` con la lógica.
3. Agregar campos serializables con los valores que el diseñador pueda customizar per‑enemigo.
4. El inspector lo recoge automáticamente via `[TypeFilter]` sin cambios adicionales.

**Qué NO hace este sistema**:
- **No reemplaza la FSM de §1.3.** La FSM sigue viva para el flow macro de combate (`Roll → Reroll → ResolveCombo → ApplyDamage → ResolveReactions → CheckCombatEnd → …`). El árbol de IA solo decide **qué hace un enemigo en su turno** — se ejecuta dentro del state `EnemyAction`.
- **No tiene SOs.** Está dicho pero conviene repetirlo: compartir un nodo entre dos enemigos es un anti‑feature — el objetivo es que cada uno tenga su propia versión editable.
- **No implementa behavior trees "de verdad"** (tick‑by‑tick con estado running largo). Los turnos de enemigo son discretos; cada evaluación empieza de cero desde la raíz.

**Cross‑ref.** §1.3 (FSM macro), §2 (atributos — los nodos leen stats), §8 (effects — los nodos de ataque despachan `EffectData`), §12 (DamagePipeline), §B (movement), §13 (RoomInstance que los nodos consultan).

### 7.6 `NpcDataSO`

Subtipo concreto de `BaseEntitySO` (§7.0) para NPCs: vendors, dialogue NPCs, quest givers, trainers, flavor chars. **No** es la plantilla de enemigos (`EnemyDataSO` §7.1) ni de objetos inertes (`PropEntitySO` §7.1b).

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Npc Data")]
public class NpcDataSO : BaseEntitySO
{
    [Title("NPC‑specific")]
    public NpcRole Role;                       // Vendor | Dialogue | QuestGiver | Trainer | Generic

    [Title("Default interaction label")]
    [InfoBox("LocalizedString — usado por el InteractionPromptView (§7.7, §D) " +
             "cuando el prefab de este NPC tenga un InteractableComponent sin " +
             "LabelOverride por fase. Si el NPC es puramente decorativo (no " +
             "tiene InteractableComponent en su prefab), este campo se ignora.")]
    public LocalizedString InteractionLabel;

    [Title("Dialogue (opcional)")]
    [InfoBox("Grafo de diálogo branchable con preconditions y effects. " +
             "Ver §7.6b para la forma del grafo. Si Role es Vendor/Trainer " +
             "sin líneas propias, dejar null — el behavior OnInteract abre " +
             "directamente la shop/training screen.")]
    [ShowIf("@Role == NpcRole.Dialogue || Role == NpcRole.QuestGiver")]
    public DialogueGraphSO Dialogue;
}

public enum NpcRole { Vendor, Dialogue, QuestGiver, Trainer, Generic }
```

**Reglas.**

- Hereda de `BaseEntitySO` — `EntityId`, `PrefabRef`, `Portrait`, `_baseStats`, `Behaviors` vienen del parent.
- **No tiene AI tree ni `Archetype`.** Esos son exclusivos de `EnemyDataSO` (§7.1).
- **La interactabilidad de un NPC es decisión del prefab, no del SO.** Un NPC es interactuable sí y sólo sí su `PrefabRef` tiene un `InteractableComponent` (§7.7) en algún nivel de la jerarquía. NPCs decorativos (ambient crowd, estatuas de fondo, NPCs de cutscene) simplemente no llevan el component — el service los ignora por completo. No existe ningún flag `IsInteractable` en el SO.
- Los NPCs interactuables se autoran con uno o más `BaseBehavior`s cuyos effects abren la screen relevante: `EffOpenScreen(ScreenId.Shop, payload: npcId)` para un vendor, `EffOpenScreen(ScreenId.Dialogue, payload: dialogueGraph)` para un dialogue NPC o quest giver. El `DialogueGraphSO` (§7.6b) lleva el branching, los skill checks y los rewards — la screen es pura UI, no tiene lógica propia. Los behaviors pueden usar `AllowedPhases` (§7.2) para decidir en qué fases son accionables — p.ej. un vendor podría estar disponible sólo en `Exploration`, o un quest giver podría aceptar la entrega de un item también durante `Combat`.
- Los `NpcDataSO` se catalogan en `EntityCatalogSO` (§1.1.1) junto con héroes, enemigos y props, accedidos vía `GetNpcs()`.

**Cross‑ref §7.6:** §4.1 (ClassHeroSO), §7.0 (BaseEntitySO parent), §7.2 (triggers + `AllowedPhases`), §7.6b (`DialogueGraphSO`), §7.7 (sistema de interacción y prompts), §1.1.1 (registro en `EntityCatalogSO`), §17.D (`InteractionPromptView`), §17.F (shop ítems en el piso — el vendor da diálogo/descuentos, no abre modal).

### 7.6b `DialogueGraphSO`

Grafo de diálogo para NPCs con branching, preconditions y effects. Reemplaza al primer draft de "`List<LocalizedString> DialogueLines`" — una lista plana no soportaba choice menus, checks de quest state, skill checks, ni effects al final de una rama (darte un item, abrir una shop, marcar un flag).

**Por qué no es un SO con lista de líneas:** el diseñador necesita que un NPC sea rico — un vendor tiene "Comprar / Vender / Charlar / Salir", un quest giver tiene ramas condicionadas a si ya completaste la quest, un trainer pide un skill check antes de enseñarte. La forma natural es un grafo polimórfico. **Por qué no son SOs por nodo:** como los AI trees (§7.5), queremos que cada `DialogueGraphSO` tenga su propia copia de los nodos — editar un choice en un NPC no puede afectar a otros. Por eso los nodos son `[SerializeReference]` inline.

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Dialogue Graph")]
public class DialogueGraphSO : SerializedScriptableObject
{
    [ValueDropdown(nameof(GetDialogueIds))]
    public string DialogueId;
    public string DisplayName;

    [Title("Graph")]
    [InfoBox("Grafo polimórfico inline. Cada DialogueGraphSO tiene su propia " +
             "copia de los nodos — editar acá NO afecta a otros graphs.")]
    [OdinSerialize, SerializeReference]
    public DialogueNode Root;

#if UNITY_EDITOR
    private static IEnumerable<string> GetDialogueIds() =>
        ServiceLocator.TryGetService<EntityCatalogSO>(out var cat)
            ? cat.AllDialogueIds : Array.Empty<string>();
#endif
}

[Serializable]
[HideReferenceObjectPicker]
public abstract class DialogueNode
{
    [HorizontalGroup("id"), LabelWidth(60)]
    public string NodeId;                    // para saltos con DialogueJumpNode

    public abstract DialogueNodeKind Kind { get; }
}

public enum DialogueNodeKind { Line, Choice, Effect, Jump, End }

public class DialogueLineNode : DialogueNode
{
    public DialogueSpeaker Speaker;          // Npc | Player | Narrator
    public LocalizedString Text;

    [OdinSerialize, SerializeReference]
    public DialogueNode Next;

    public override DialogueNodeKind Kind => DialogueNodeKind.Line;
}

public class DialogueChoiceNode : DialogueNode
{
    [ListDrawerSettings(ShowFoldout = false, DraggableItems = true)]
    [OdinSerialize]
    public List<DialogueChoice> Choices = new();

    public override DialogueNodeKind Kind => DialogueNodeKind.Choice;
}

[Serializable]
public class DialogueChoice
{
    public LocalizedString Text;

    [Title("Visible if (all must pass)")]
    [InfoBox("Si alguna falla, la choice NO aparece en el menu. Útil para " +
             "ocultar opciones de quest que aún no empezaron.")]
    [OdinSerialize]
    public List<BasePreCondition> VisibleIf = new();                         // §8.2

    [Title("Enabled if (all must pass)")]
    [InfoBox("Si alguna falla, la choice aparece grayed out. Útil para " +
             "'Pagar 100 oro' cuando el player no tiene suficiente.")]
    [OdinSerialize]
    public List<BasePreCondition> EnabledIf = new();

    [OdinSerialize, SerializeReference]
    public DialogueNode Next;
}

public class DialogueEffectNode : DialogueNode
{
    [InfoBox("Dispara un EffectData y continúa por Next. Uso típico: " +
             "abrir una shop, entregar un quest reward, tomar/dar un item, " +
             "marcar un flag, restar oro, hacer un skill check.")]
    [OdinSerialize]
    public EffectData Effect = new();                                        // §8

    [OdinSerialize, SerializeReference]
    public DialogueNode Next;

    public override DialogueNodeKind Kind => DialogueNodeKind.Effect;
}

public class DialogueJumpNode : DialogueNode
{
    [InfoBox("Salta a otro nodo por NodeId. Permite hubs de diálogo con " +
             "'Volver atrás' sin duplicar subgrafos.")]
    public string TargetNodeId;

    public override DialogueNodeKind Kind => DialogueNodeKind.Jump;
}

public class DialogueEndNode : DialogueNode
{
    public override DialogueNodeKind Kind => DialogueNodeKind.End;
}

public enum DialogueSpeaker { Npc, Player, Narrator }
```

**Ejemplo canónico — vendor con 4 ramas:**

```
Root → LineNode(Npc, "¿Qué se te ofrece, viajero?")
       ↓ Next
       ChoiceNode
       ├── Choice "Descuento"  EnabledIf: PCGold >= 50
       │   → EffectNode { Effect: EffApplyShopDiscount(npcId, 0.2f) }
       │     → LineNode(Npc, "Tenés 20% de descuento. Mirá los pedestales.")
       │       → JumpNode(Root)
       ├── Choice "Charlar"  VisibleIf: PCQuestState("intro", Completed)
       │   → LineNode(Npc, "Dicen que hay un dragón en el piso 5...")
       │     → JumpNode(Root)
       └── Choice "Salir"
           → EndNode
```

**Ejemplo — quest giver con skill check:**

```
Root → LineNode(Npc, "¿Podrías traerme la llave del calabozo?")
       ↓
       ChoiceNode
       ├── Choice "Claro"  VisibleIf: PCQuestState("dungeon_key", NotStarted)
       │   → EffectNode { Effect: EffStartQuest("dungeon_key") }
       │     → EndNode
       ├── Choice "Ya la tengo"  VisibleIf: PCHasItem("dungeon_key")
       │   → EffectNode { Effect: EffRollSkillCheck(Charisma, DC: 12) }
       │     → ChoiceNode (sin texto — ramifica por lastResult)
       │        ├── Choice "(éxito)"  EnabledIf: PCLastRollSucceeded
       │        │   → EffectNode { Effect: EffTakeItem("dungeon_key") + EffGiveReward(100g) }
       │        │     → EndNode
       │        └── Choice "(fallo)"  EnabledIf: PCLastRollFailed
       │            → LineNode(Npc, "Mmm... no me parece genuina.")
       │              → EndNode
       └── Choice "Quizás luego"
           → EndNode
```

**Runtime.** El `DialogueScreen` (§17.D) recibe el `DialogueGraphSO` en el payload del `EffOpenScreen(ScreenId.Dialogue, graph)`, monta un iterador que camina el grafo:

1. **Line** → renderiza la línea, espera input de continuar, salta a `Next`.
2. **Choice** → por cada choice, evalúa `VisibleIf` (filtro) y `EnabledIf` (gray out), pinta el menu. Input del player → salta al `Next` del choice elegido.
3. **Effect** → arma un `EffectContext` con `TriggerContext = null` (dialogue no es un trigger de behavior — es una ejecución driven por UI) y corre el `EffectData`. Sigue por `Next`.
4. **Jump** → resuelve `TargetNodeId` contra el grafo (lookup por `NodeId`, implementación del screen). Continua desde ahí.
5. **End** → cierra la screen, pop del stack del `IScreenManager`.

**Cancelación.** En cualquier nodo que esté esperando input (Line, Choice), el botón **Cancel** del `IInputService` (§17.J — mismo binding que cierra screens modales) aborta el walker. El comportamiento es:

- Si el nodo actual es `Line` o `Choice` → el walker se descarta sin evaluar `Next`. Ningún `DialogueEffectNode` pendiente se ejecuta.
- Si estamos dentro de un `DialogueEffectNode` (el effect ya corriendo) → el cancel se **ignora**; el effect debe terminar para mantener atomicidad (sino, un `EffTakeItem("dungeon_key") + EffGiveReward(100g)` podría quedar a mitad y dejar inconsistencia).
- Al abortar, `DialogueScreen` hace pop del stack y dispara `OnDialogueEnded(graphId, cancelled: true)` vía `TypedEvent<DialogueEndedPayload>` (§1.2.1). Listeners como quests que esperaban un effect de cierre deben chequear el flag `cancelled` antes de avanzar estado.
- `DialogueEffectNode` con effects reversibles (mayoría de quest starts, shop opens) son seguros porque los inicia sólo al llegar al nodo; si el player cancela antes, simplemente no se disparan.

El walker **no** expone "volver atrás" automático — eso es responsabilidad del autor del grafo vía `DialogueJumpNode` a un nodo previo (patrón "hub" del vendor del ejemplo anterior).

**Precondiciones y effects reutilizados de §8.** `DialogueChoice.VisibleIf/EnabledIf` usan el mismo `BasePreCondition` que el resto del juego, con `PreConditionContext { Entity = player, OpponentGuid = npc.Guid }`. Los `DialogueEffectNode.Effect` son `EffectData` estándar — no hay lógica de diálogo duplicada.

**Cross‑ref §7.6b:** §1.1.1 (`EntityCatalogSO.AllDialogueIds`), §7.6 (`NpcDataSO.Dialogue`), §7.7 (el behavior `OnInteract` del NPC dispara `EffOpenScreen(Dialogue, graph)`), §8.1 (`EffectData`), §8.2 (`BasePreCondition`), §17.D (`DialogueScreen` implementa el walker).

### 7.7 Interacción, prompts y per‑phase rules

Sistema que unifica todos los "objetos accionables" del juego — chests, doors, potions, **shop items en el piso** (§17.F), NPCs vendors, quest givers, sensores, trampas — detrás de un único contrato. Soporta dos modos de disparo (pisar el tile o confirmar un prompt con input), y permite que un mismo interactable tenga **comportamientos distintos según la `GamePhase` actual** (§12.0). Los ítems de shop son props normales del `IInteractionService` — no hay modal de shop, no hay `GamePhase.Shop`.

#### 7.7.1 Principio — `InteractableComponent` decide la interactabilidad

Cualquier prefab de `PropEntitySO` (§7.1b — chest, door, potion) o `NpcDataSO` (§7.6 — vendor) que quiera ser accionable lleva un `InteractableComponent` en su jerarquía. Si el prefab no lo tiene, la entity es puramente decorativa y el service la ignora. Esto permite tener NPCs o props no interactuables sin agregar flags al SO. **`EnemyDataSO` nunca lleva `InteractableComponent`** — los enemigos no son "accionables" por prompt; su loop es el combate (§12), no la interacción.

Una entity puede tener exactamente un `InteractableComponent` (por convención — el service no maneja múltiples components en el mismo prefab).

#### 7.7.2 `InteractionMode` y `PhaseInteractionRule`

```csharp
public enum InteractionMode
{
    Direct,     // se dispara al pisar el tile (OnEntered)
    Prompt,     // se dispara al confirmar el prompt estando en rango (OnInteract)
    Both,       // ambos — pisar o prompt ambos válidos en esta fase
    Disabled,   // el interactable es inerte en esta fase
}

[Serializable]
public class PhaseInteractionRule
{
    public GamePhase Phase;
    public InteractionMode Mode = InteractionMode.Prompt;

    [ShowIf("@Mode == InteractionMode.Prompt || Mode == InteractionMode.Both")]
    [InfoBox("Radio en tiles de la grilla isométrica (§11.3).")]
    public int PromptRange = 1;

    [InfoBox("Si vacío, se usa el label default del SO del owner " +
             "(NpcDataSO.InteractionLabel o equivalente). Override por fase y " +
             "por instance — la misma entity puede decir texto distinto en " +
             "fases distintas, o en instances distintas de la misma sala.")]
    public LocalizedString LabelOverride;

    [InfoBox("Tie‑break si hay varios interactables en rango en esta fase. " +
             "Mayor gana. Segundo criterio: distancia ascendente.")]
    public int Priority = 0;
}
```

#### 7.7.3 `InteractableComponent`

```csharp
public class InteractableComponent : MonoBehaviour
{
    [Title("Phase rules")]
    [InfoBox("Una regla por GamePhase en la que el interactable está activo. " +
             "Fases que no aparecen en la lista son equivalentes a Mode = Disabled " +
             "— el service simplemente ignora el interactable en esa fase.")]
    [ListDrawerSettings(ShowFoldout = false, DraggableItems = true)]
    public List<PhaseInteractionRule> PhaseRules = new();

    // --- Resuelto en Awake contra el DungeonManager + el owner prefab ------

    public BaseEntitySO OwnerData   { get; private set; }
    public Entity       OwnerEntity { get; private set; }
    public string       SpawnPointId { get; private set; }   // §13.6 → RoomObjectState

    public PhaseInteractionRule GetRule(GamePhase phase) =>
        PhaseRules.FirstOrDefault(r => r.Phase == phase);
}
```

**Ejemplo canónico — la puerta** (requisito del GD):

```
DoorPrefab  (spawned desde PropEntitySO "Door")
├── InteractableComponent
│   └── PhaseRules:
│       ├── { Phase: Exploration, Mode: Direct,  Priority: 0 }
│       └── { Phase: Combat,      Mode: Prompt,  Range: 1,
│                                  LabelOverride: "Forzar puerta" }
└── PropEntitySO.Behaviors (heredado via PrefabRef):
    ├── BaseBehavior(OnEntered, AllowedPhases: Exploration)
    │   └── EffectData: EffLeaveRoom
    └── BaseBehavior(OnInteract, AllowedPhases: Combat)
        └── EffectData:
            ├── PreCondition: PCHasIntAttribute(Energy, >= 1)
            ├── Effect: EffSpendEnergy(1)
            ├── Effect: EffRollSkillCheck(DC: 8, Stat: Strength)
            └── Effect: EffConditional(lastResult, then: EffLeaveRoom)
```

En `Exploration`, el `IMovementService` ve el `PhaseInteractionRule(Direct)` para esa fase y al pisar el tile dispara el behavior `OnEntered` filtrado por `AllowedPhases = Exploration`. El jugador pasa a la sala adyacente automáticamente, sin prompt.

En `Combat`, el `IMovementService` **ignora** el tile (la rule de combate es `Prompt`, no `Direct`), y el `IInteractionService` ofrece el prompt "Forzar puerta" en el HUD al estar en rango. Al presionar `F`, se dispara el behavior `OnInteract` filtrado por `AllowedPhases = Combat`, que gasta energía, rolea un skill check y si tiene éxito llama `EffLeaveRoom`.

La misma entity, dos behaviors con `AllowedPhases` disjuntas, dos rules con `Mode` distinto — cero conflicto.

#### 7.7.4 `IInteractionService`

```csharp
public interface IInteractionService
{
    InteractableComponent CurrentTarget { get; }    // null si no hay ninguno en rango
    bool                  IsAvailable  { get; }     // el current target pasa sus preconditions

    void Register(InteractableComponent interactable);
    void Unregister(InteractableComponent interactable);

    /// <summary>Invocado por el input bind "Interact" o por el botón del HUD.</summary>
    void ExecuteCurrent();

    event Action<InteractableComponent, LocalizedString, bool> TargetChanged;
    event Action<InteractableComponent> Executed;
}
```

**Responsabilidades frame a frame (o al cambio de tile del player — el service decide):**

1. **Lee la fase actual** del `TurnManager` (`ServiceLocator.Get<TurnManager>().CurrentPhase`).
2. **Filtra candidatos** — para cada `InteractableComponent` registrado:
   - `rule = c.GetRule(currentPhase)` — si `null` o `Mode == Disabled` → descartado.
   - `Mode == Direct` → descartado del pool de **prompts** (pero sigue activo para el movement service cuando el player pise el tile).
   - `Mode == Prompt` o `Both` → candidato a prompt.
3. **Consulta `RoomObjectState` (§13.6)** — si el state del `SpawnPointId` del target tiene `Consumed == true`, el candidato se descarta. Un consumed gana sobre cualquier rule porque el objeto físicamente ya no está.
4. **Best target selection** — entre los candidatos en rango (`distance ≤ rule.PromptRange`), elige por `Priority` desc, luego por distancia asc. Resultado → `CurrentTarget`.
5. **Precondition check al mostrar** — evalúa las preconditions del `BaseBehavior(OnInteract)` del owner (filtrado por `AllowedPhases.HasFlag(currentPhase)`), construyendo un `PreConditionContext` con el player como `OpponentGuid`. El resultado boolean es `IsAvailable`. Si falla, el prompt se muestra **grayed out** con el label — el jugador ve qué podría hacer y por qué no puede todavía.
6. **Label resolution** — toma `rule.LabelOverride` si no está vacío; si no, toma el label default del SO del owner (`NpcDataSO.InteractionLabel`, o el que el subtipo dicte). Resuelve el `LocalizedString` vía `LocalizationManager`.
7. **Emite `TargetChanged`** con `(target, labelResolved, isAvailable)`. Publica además en `EventManager` como `OnInteractionTargetChanged` (§1.2) para que el HUD consuma por bus global.
8. **Dispatch del input** — `ExecuteCurrent()` busca el primer `BaseBehavior(OnInteract)` del owner cuyo `AllowedPhases` contiene la fase actual, construye un `InteractionBehaviorContext` (§7.3) con `OwnerGuid = owner.Guid`, `SourceEntity = owner`, `TriggeringEntity = player`, `CurrentRoomId = dungeon.CurrentRoom.Id`, `CurrentPhase = turnManager.CurrentPhase`, `Interactable = currentTarget`, `Rule = activeRule`, `DistanceToTriggerer = …` y llama `behavior.Execute(ctx)`. Emite `Executed` + `OnInteractionExecuted`.

**Modo `Direct` — responsabilidad del movement service.** Los interactables con `Mode = Direct` en la fase actual **no llegan al interaction service**. El `IMovementService` (§B), al resolver el paso del player, detecta si el tile destino tiene un `InteractableComponent` con `rule.Mode ∈ { Direct, Both }` en la fase actual y dispara los `BaseBehavior(OnEntered)` del owner filtrados por `AllowedPhases.HasFlag(currentPhase)`.

**Modo `Both`.** El interactable es tanto pisable como promptable en esa fase. El movement service dispara `OnEntered` al pisar el tile; el interaction service ofrece el prompt paralelamente al estar en rango. El jugador elige cuál usar.

#### 7.7.5 Integración con las fases — sin regla global

**No hay regla global "prompts sólo fuera de combate".** El service sigue activo en toda fase listada en las `PhaseRules` de algún interactable. El diseñador decide, por fase y por entidad, qué está accionable y cómo. Casos que el modelo cubre naturalmente:

- **Cofre abrible en combate** — `PhaseRules: { Exploration: Prompt, Combat: Prompt }`, y el behavior `OnInteract` con `AllowedPhases = All` (o `Exploration | Combat` explícito). Se puede abrir en ambas fases.
- **Shop item en el piso** (§17.F) — `PhaseRules: { Exploration: Prompt }`. El behavior `OnInteract` tiene precondition `PCGold >= price`; si pasa, ejecuta `EffDeductGold` + `EffAddItem` + `EffConsumeProp`. Si no, prompt grayed out "No tenés suficiente oro". No hay modal de shop: el jugador se acerca al pedestal, lee la info en el `ItemInspectView` (§D.6b), y compra con `F`.
- **NPC vendor sólo fuera de combate** — `PhaseRules: { Exploration: Prompt }`. En combate no hay rule → inerte. Puede dar diálogo vía `DialogueGraphSO` (§7.6b) pero **no abre** una pantalla de shop — los ítems ya están en el piso de la sala.
- **Puerta con comportamientos distintos por fase** — el ejemplo canónico de §7.7.3.
- **Sensor que arma trampa al acercarse** — usa `OnPlayerInRange` (§7.2) con `AllowedPhases = All`. Ningún prompt, ningún mode — el trigger es automático por proximidad.
- **NPC decorativo** — el prefab no tiene `InteractableComponent`. Punto. El service no sabe que existe.

#### 7.7.6 Registro y scope

`IInteractionService` se registra con **`ServiceScope.Run`** (§1.1) al iniciar una run — no en el bootstrap global. Se limpia con `ClearScope(Run)` al terminar la run. No hay SO de config propio — los tunables (fade times del prompt, smoothing, refresh rate) viven en los `InteractableComponent` y en el prefab del `InteractionPromptView` (§17.D).

**Cross‑ref §7.7:** §1.1.1 (bootstrap), §1.2 (eventos `OnInteractionTargetChanged/Executed`), §7.2 (triggers + `AllowedPhases`), §7.6 (`NpcDataSO`), §8 (`EffectData` del `OnInteract`), §11.3 (grid distance), §12 (`TurnManager.CurrentPhase` + `GamePhase`), §13.6 (`RoomObjectState.Consumed` check), §B (`IMovementService` dispara `OnEntered` filtrado por `AllowedPhases`), §17.D (`InteractionPromptView` + acción `Interact` del Input System).

---

**Cross‑ref general §7.** §8 (EffectData), §9 (StoredValues), §13.3 (spawn de entidades desde salas), §13.4 (enemigos con debilidad).

---

## 8. Effects + PreConditions

### 8.1 `EffectData`

Unidad de ejecución: grupo de `PreConditions` + grupo de `IEffect`.

```csharp
[HideReferenceObjectPicker]
public class EffectData
{
    [PropertyOrder(-1)] public string Label = "Effect Group";

    [Title("Conditions", "All must pass to execute effects")]
    [ListDrawerSettings(ShowFoldout = false, DraggableItems = true)]
    [OdinSerialize] public List<BasePreCondition> PreConditions = new();

    [Title("Effects", "Executed in order")]
    [ListDrawerSettings(ShowFoldout = false, DraggableItems = true)]
    [OdinSerialize] public List<IEffect> Effects = new();

    public bool CanBeExecuted(PreConditionContext preCtx);
    public void Execute(EffectContext ctx);
    public bool TryExecute(EffectContext ctx, PreConditionContext preCtx);
}
```

### 8.2 `BasePreCondition`

```csharp
[Serializable, HideReferenceObjectPicker]
public abstract class BasePreCondition
{
    public abstract string ConditionName { get; }
    public abstract bool Evaluate(PreConditionContext context);
    [SerializeField] protected bool _isConstantValue = true;
}

public class PreConditionContext
{
    public Guid OwnerGuid;
    public Guid OpponentGuid;
    public Entity Entity;
}
```

**Concretos previstos:**

- `PCHasIntAttribute` — comparación sobre un `int` stat (`Energy >= 2`, `Health < 50%`, …).
- `PCHasModifier` — presencia de un `Modifier<T>` específico por tipo/id.
- `PCCurrentPhase` — fase del turno es X.
- `PCFirstRollOfCombat` — flag ad‑hoc para pasiva de Berserker.
- `PCComboAvailable` — sheet tiene disponible el combo X (no tachado).
- `PCEntityInRange` — otra entidad a ≤ N casillas.

### 8.3 `IEffect` + `BaseEffect<A, V>`

```csharp
public interface IEffect
{
    string GetEffectName();
    SelectionSettings GetSelection();
    bool HasSelectionRequirement();
    bool Apply(EffectContext context);
    bool RequiresSelectionAt(SelectionTiming timing);
    bool ValidateSelection(TargetSelectionResult result, Guid ownerGuid, out string error);
}
```

### 8.4 `EffectContext`

```csharp
public class EffectContext
{
    public Guid SourceGuid;
    public Guid TargetGuid;
    public Entity SourceEntity;
    public Entity TriggeringEntity;        // quien disparó el trigger (p.ej. en OnDamaged)

    public TargetSelectionResult SelectionResult;    // §11
    public int EffectIndex;
    public bool lastResult = true;                    // cortocircuito entre efectos encadenados

    public BaseBehavior    SourceBehavior;             // para StoredValues (§9)
    public BehaviorContext TriggerContext;             // §7.3 — subtipo según trigger

    /// <summary>
    /// Acceso tipado al contexto del trigger. Devuelve false si el TriggerContext
    /// no es del subtipo pedido — los effects con IRequiresTriggerContext (§8.5)
    /// deberían dar warning en el inspector cuando se atan a un behavior cuyo
    /// trigger construye otro subtipo.
    /// </summary>
    public bool TryGetTriggerContext<T>(out T ctx) where T : BehaviorContext
    {
        ctx = TriggerContext as T;
        return ctx != null;
    }
}
```

**Regla de construcción.** El `EffectData.Execute(ctx)` (§8.1) es quien copia la referencia al `TriggerContext` dentro del `EffectContext` que pasa a cada `IEffect.Apply`. El `BaseBehavior.Execute(BehaviorContext)` recibe el contexto polimórfico del dispatcher, y arma el `EffectContext` inicial con `TriggerContext = ctx`, `SourceBehavior = this`, `SourceEntity = ctx.SourceEntity`, `TriggeringEntity = ctx.TriggeringEntity`.

**Ejemplo — efecto de contragolpe (`EffReflectDamage`):**

```csharp
public class EffReflectDamage : BaseEffect, IRequiresTriggerContext<DamageBehaviorContext>
{
    [Range(0f, 1f)] public float ReflectPercent = 0.5f;

    public override bool ApplyEffect(EffectContext ctx)
    {
        if (!ctx.TryGetTriggerContext<DamageBehaviorContext>(out var dmg)) return false;
        if (dmg.Attacker == null) return false;
        var reflected = Mathf.RoundToInt(dmg.Damage.FinalAmount * ReflectPercent);
        DamagePipeline.Apply(ctx.SourceEntity, dmg.Attacker, reflected);
        return true;
    }
}
```


### 8.5 Capability interfaces (marker)

La base renderiza secciones del inspector **sólo si** el efecto concreto implementa la interfaz marcadora. Patrón: type tagging + conditional editor rendering.

| Interfaz | Sección habilitada |
|---|---|
| `IUsesAttribute` | Attribute source (Entity self / Opponent / Triggering) + reader |
| `ICanBeEntityAttribute` / `ICanBeTriggeringEntityAttribute` | Opciones del dropdown de source |
| `IUsesValue` | Value source (Constant / Entity / Generic) |
| `ICanBeConstantValue` / `ICanBeEntityValue` / `ICanBeGenericValue` | Opciones del dropdown de value |
| `IUsesSelection` | Selection settings (§11) |
| `IUsesGridSelection` | Grid variable (`bool[,]`) con `[TableMatrix]` |
| `IUsesFeedback` / `IUsesFeedbackTarget` / `IUsesFeedbackSequence` | Feedback (§10) |
| `ICanBeVFXFeedback` / `ICanBeSFXFeedback` / `ICanBeAnimFeedback` | Opciones de tipo de feedback |
| `IHasDuration` | Duration + tick event (modificadores temporales) |
| `IHasOperation` | Dropdown de operación (via `OperationsConstants`) |
| `IHasModifierDirection` | Dropdown de `ModifierDirection` para efectos que crean mods |
| `IShouldStoreValuesOnBehavior` | Declara que el efecto escribe en `StoredValues` (§9) |
| `IRequiresTriggerContext<TCtx>` | Declara que el efecto necesita un `BehaviorContext` de subtipo `TCtx` (§7.3). El inspector valida en tiempo de edición que el behavior que contiene este effect tenga un `Trigger` cuyo dispatcher construye ese subtipo (ver tabla §7.2). Si el match falla, se muestra warning naranja — soft check, no error, porque `OnEvent` puede transportar cualquier payload. |

### 8.6 Readers

Los readers (`IEntityReader<T>`, `IPlayerReader<T>`, …) son SOs que saben cómo leer un atributo concreto del contenedor. Devuelven un `IAttribute` / `IModifiable` o un valor tipado, manteniendo los efectos agnósticos del atributo concreto.

### 8.7 Catálogo inicial de efectos

| Categoría | Efecto | Uso |
|---|---|---|
| **Atributos** | `EffModifyIntAttribute` | Suma/resta sobre un int stat |
| | `EffRechargeAttribute` | Recarga parcial por evento (Energy fin de turno) |
| **Modificadores** | `EffAddIntModifier` | Aplica `Modifier<int>` con duración + dirección |
| | `EffAddFloatModifier` | Aplica `Modifier<float>` (multiplicadores) |
| | `EffAddBoolModifier` | Aplica `Modifier<bool>` (estados: envenenado, stun) |
| | `EffRemoveModifier` | Quita modificador por guid o tipo |
| **Daño / Heal** | `EffDealDamage` | Daño a entidad seleccionada (usa `DamagePipeline` §12) |
| | `EffMultiDealDamage` | Daño a múltiples targets (AoE) |
| | `EffHeal` | Curación via 1 tirada vs umbral (GD: poción) |
| **Movimiento** | `EffMoveEntity` | Mueve entidad N casillas |
| | `EffTeleport` | Teleport a slot específico |
| **Sala / Mundo** | `EffOpenDoor` | Consumir energía + tirada vs umbral (GD: forzar puerta) |
| | `EffOpenChest` | Idem para cofres |
| | `EffRemoveRoomEntity` | Desaparece cofre/enemigo |
| **Control de flujo** | `EffConditional` | Ejecuta otro `EffectData` si se cumple condición |
| **Room / World** | `EffLeaveRoom` | Dispara transición de sala vía `IDungeonManager` (§13.6). Usado por puertas en exploration y skill checks exitosos en combat. |
| | `EffConsumeProp` | Marca `RoomObjectState.Consumed = true` (§13.6) para el spawn point del prop actual y destruye el visual. Usado por trampas single-use (§7.1b), cofres abiertos y shop items comprados (§17.F). |
| **Status** | `EffAddStatusEffect` | Aplica un status effect al target (§20.4) |
| | `EffRemoveStatusEffect` | Remueve un status effect del target por id |
| **Shop** | `EffDeductGold` | Resta gold del jugador. Precondición implícita: `PCGold >= amount`. |
| | `EffAddItemToInventory` | Agrega un ítem al `IInventoryService` (§18.3) |
| | `EffRestockShop` | Llama `IShopManagerService.Restock(rng)` (§17.F) |
| | `EffNotifyShopPurchase` | Notifica al `IShopManagerService` que un ítem fue comprado |
| **Quests** | `EffStartQuest` | Inicia una quest vía `IQuestService` (§21) |
| | `EffCompleteQuest` | Completa una quest activa |
| **Craps** | `EffStartCraps` | Registra `ICrapsSessionService`, pushea `PhaseOverlay.Craps` (§17.C) |
| **Feedback** | `EffPlayFeedback` | Dispara entrada del DB (§10) |
| | `EffPlaySequence` | Secuencia encadenada (anim → VFX → SFX) |

### 8.8 Pipeline de ejecución

```csharp
public bool Apply(EffectContext context)
{
    if (!context.lastResult) return false;                    // cortocircuito
    if (HasGridSelectionSettings() && !Selection.RequiresSelection)
        AddGridSelection(context);

    context.lastResult = ApplyEffect(context);
    return context.lastResult;
}

public abstract bool ApplyEffect(EffectContext context);
```

**Cross‑ref.** §3 (modificadores), §7 (behaviors los contienen), §9 (stored values), §10 (feedback), §11 (selection), §12 (pipeline de daño unificado).

---

## 9. Behavior Values (runtime value bag)

### 9.1 Problema

Durante el resolve de un behavior, múltiples efectos producen datos que la pipeline de feedback necesita después (daño calculado, magnitud de dirección para camera shake, guid del target, …). Hace falta un bag runtime tipado por subtipo con claves enum.

### 9.2 Tipos

```csharp
[Serializable]
public abstract class BaseBehaviorStoredValue { }

public class FloatBehaviorValue : BaseBehaviorStoredValue
{
    public float Value;
}

public class FloatingNumberBehaviorValue : BaseBehaviorStoredValue
{
    public FloatingNumberType Type;       // Damage, Heal, Shield, Crit, Miss
    public float Value;
    public Vector3 Offset;
    public Guid TargetEntityGuid;
    public float Delay;
}

public enum BehaviorValueKey
{
    None,
    FloatingDamage, FloatingHeal, FloatingShield,
    DirectionMagnitude, HitImpulse,
    ComboMatched, WeaknessHit,
}
```

### 9.3 API

```csharp
behavior.SetBehaviorValue(key, value);        // append (lista por key)
behavior.TryGetBehaviorValues<T>(key, out);   // lectura tipada
behavior.ClearBehaviorValues();
```

### 9.4 Consumo por feedback

`FeedbackBus` lee `StoredValues` al ejecutar una `FeedbackRequest` (§10) y despacha sobre el subtipo concreto:

- `FloatBehaviorValue` → param de animator.
- `FloatingNumberBehaviorValue` → spawn de numerito flotante en world space del target.
- Futuros subtipos → despachos nuevos sin tocar los existentes.

**Nota.** Single‑player → el diccionario vive en memoria y se lee directamente. Sin RPC split.

### 9.5 Lifecycle — quién escribe, quién lee, cuándo se limpia

El bag vive **por instancia de `BaseBehavior`** (§7.2b garantiza que el deep‑clone al spawn da a cada entity su propio bag). El ciclo por ejecución de behavior es:

1. **Escritura (resolve).** Durante `behavior.Execute(ctx)`, cada `BaseEffect` que implementa `IShouldStoreValuesOnBehavior` (§8.5) escribe su valor al bag via `behavior.SetBehaviorValue(key, value)`. Typical writers: `EffDealDamage` guarda el daño final resuelto bajo `FloatingDamage`, `EffHeal` bajo `FloatingHeal`, `EffApplyImpulse` bajo `HitImpulse`.
2. **Captura (snapshot).** Al final del resolve, antes de que el control vuelva al dispatcher, el caller que armó el `FeedbackRequest` (§10.4) invoca `FeedbackRequest.CaptureState(behavior)` — esto copia **una instantánea** del bag al request. Desde este punto, el request es autosuficiente y puede viajar async sin depender de que el bag siga vivo.
3. **Lectura (feedback).** `FeedbackBus` consume el snapshot del request (§10.7) y despacha por subtipo como describe §9.4. Nunca lee directo del behavior.
4. **Limpieza.** El bag se limpia en **dos momentos**:
   - Al **terminar la ejecución del behavior** — `BaseBehavior.Execute` hace `ClearBehaviorValues()` en su `finally` después de que todos los feedbacks hayan capturado. Idempotente.
   - Como red de seguridad, al **fin del turno del owner** — el dispatcher de `OnTurnFinished` pasa por los behaviors del entity y llama `ClearBehaviorValues()` por si algún path no llegó al `finally` (excepción, corte async). Evita que un valor fantasma del turno anterior contamine el resolve del siguiente.

**Por qué dos limpiezas.** El primario es el `finally` post‑resolve. El secundario al fin del turno es solo defensivo — si el bag ya está vacío, el clear es no‑op. Un valor escrito en el resolve del turno N **no** debe ser legible en el turno N+1.

**Regla de composición.** Un effect que corre después de otro en el mismo `EffectData` puede leer valores que escribió un effect anterior vía `behavior.TryGetBehaviorValues<T>(key, out)`. Esto es útil para, p. ej., un `EffPlayFeedback` que lee el `FloatingDamage` escrito por un `EffDealDamage` previo sin re‑calcularlo. Pero la dependencia queda **dentro de la misma ejecución** — no sobrevive al clear.

**Anti‑patrón.** Usar `StoredValues` como storage persistente entre turnos. Para eso están los modifiers (§3) o el save system (§15). El bag es scratch space de un solo resolve.

**Cross‑ref.** §7 (vive en `BaseBehavior`), §8 (efectos que escriben con `IShouldStoreValuesOnBehavior`), §10.4 (`FeedbackRequest.CaptureState` copia el snapshot), §10.7 (despacho lee del snapshot).

---

## 10. Sistema de Feedback

> **Cómo leer esta sección.** El pipeline de feedback es uno de los sistemas más grandes
> heredados del proyecto Bot‑Game y se porta casi 1:1, con una sola amputación mayor:
> en Rollgeon (single‑player) desaparece la capa `NetworkBehaviour` + `ObserversRpc`.
> Todo lo que Bot‑Game marshalla en arrays paralelos de primitivas (`int[] FloatValueKeys`,
> `float[] FloatValues`, etc.) en Rollgeon viaja como `IReadOnlyDictionary<BehaviorValueKey, List<BaseBehaviorStoredValue>>`
> por referencia. El resto — DB, entries, secuencias, event bus, listeners, resolver
> de posición, timeout watchdog — queda igual. El efecto autor (§10.9) sigue siendo
> "pedir al `FeedbackManager` que toque un id y esperar a que termine".

### 10.1 Vista de componentes

| Componente | Rol | Archivo Bot‑Game de referencia |
|---|---|---|
| `FeedbackManager` | Orquestador. Recibe requests, dispara el pipeline, mantiene `_activeFeedbacks` y el watchdog de timeout. Registrado en `ServiceLocator`. | `Assets/Scripts/Online/Managers/FeedbackManager.cs` |
| `FeedbackDBSO` | `SerializedScriptableObject` con la lista autoral de `FeedbackEntry`. Cachea por id, expone `TryGetFeedback`, `GetAllFeedbackIds`, `GetFilteredFeedbackIds(type)`. Botones de editor: `FindDuplicates`, `RemoveEmptyEntries`, `SortById`, `ClearAll`. | `Assets/Scripts/Scriptables/FeedbackDBSO.cs` |
| `FeedbackEntry` | Una fila del DB. Id + `FeedbackType` + campos condicionales por `ShowIf`. | `Assets/Scripts/Feedback/FeedbackEntry.cs` |
| `FeedbackRequest` | DTO que el efecto arma y pasa al manager. En Rollgeon lleva `StoredValues` directo (sin arrays paralelos). | `Assets/Scripts/Feedback/FeedbackRequest.cs` |
| `FeedbackSequenceStep` | Un paso dentro de una secuencia. Source + StartMode + EndMode + BlockSequence. | `Assets/Scripts/Feedback/FeedbackSequenceStep.cs` |
| `FeedbackEventBus` | Pub/sub **latched** por secuencia (un key publicado queda "firado" hasta el fin de la secuencia). Creado nuevo por cada `ExecuteLocalSequence`. | `Assets/Scripts/Feedback/FeedbackEventBus.cs` |
| `FeedbackSequenceRuntime` | Puntero estático a la secuencia activa. Lo usan los Animation Events y los particle‑stop callbacks para publicar sin referencia dura. | `Assets/Scripts/Feedback/FeedbackSequenceRuntime.cs` |
| `FeedbackPositionResolver` | Staticu que resuelve `SpawnPosition` → `Vector3` usando `PawnRegistryService` y `GridManager`. | `Assets/Scripts/Feedback/FeedbackPositionResolver.cs` |
| `FeedbackCallbackListener` | `MonoBehaviour` que se pega al VFX/pawn y dispara `OnCompleted` cuando un particle system para (`stopAction = Callback`) o cuando un Animator state llega a `normalizedTime ≥ 1`. | `Assets/Scripts/Feedback/FeedbackCallbackListener.cs` |
| `FloatingNumber` (prefab en `Resources/FloatingNumber`) | UI worldspace que instancia una `FloatingNumberBehaviorValue` con su valor, tipo (Damage/Heal/…​) y offset. | `Assets/Resources/FloatingNumber.prefab` |
| `EffPlayFeedback` | Efecto concreto que el autoral coloca en un `EffectData` — único puente estable entre la pipeline de effects (§8) y el feedback pipeline. | `Assets/Scripts/EffectNPrecondition/Effects/EffPlayFeedback.cs` |

Todos los componentes `ScriptableObject` se pre‑cargan desde `ServiceBootstrapSO` (§1.1.1)
para que el primer efecto que toque `ServiceLocator.GetService<FeedbackManager>()` /
`GetService<FeedbackDBSO>()` no pegue nulls.

### 10.2 `FeedbackDBSO` y `FeedbackEntry`

**DB.** `FeedbackDBSO : SerializedScriptableObject` con `List<FeedbackEntry> _entries`,
`Dictionary<string, FeedbackEntry> _cache` rebuildeable en `OnEnable` / `OnValidate`,
y API pública:

```csharp
public bool TryGetFeedback(string feedbackId, out FeedbackEntry entry);
public FeedbackEntry GetFeedbackOrDefault(string feedbackId);
public bool HasFeedback(string feedbackId);
public IEnumerable<string> GetAllFeedbackIds();
public IEnumerable<string> GetFilteredFeedbackIds(FeedbackType type);
public void RebuildCache();
```

El DB alimenta los `[ValueDropdown]` del inspector — tanto los de `EffPlayFeedback`
como los de `FeedbackSequenceStep.FeedbackRefId` — vía `GetAllFeedbackIds()` /
`GetFilteredFeedbackIds(type)`. Esto cumple la regla transversal de dropdowns (§0):
ningún id de feedback debería tipearse a mano en el inspector.

**Entry.** `FeedbackEntry` tiene los campos comunes + un bloque condicional por type:

```csharp
[Serializable]
public class FeedbackEntry
{
    public string FeedbackId;
    public FeedbackType Type;           // VFX | SFX | Animation | Wait | BehaviorValue | FloatingNumber
    public SpawnPosition Position;      // ver §10.6
    [ShowIf("@Position == SpawnPosition.FromReader")]
    public IPositionReader PositionReader;
    [ShowIf("@Position == SpawnPosition.FromReader")]
    public Players PlayerTarget;
    public Vector3 PositionOffset;
    public float Duration = 1f;         // fallback timer
    public FeedbackCompletionMode CompletionMode = FeedbackCompletionMode.Timer;

    // VFX
    [ShowIf(nameof(IsVFX))] public GameObject VfxPrefab;
    [ShowIf(nameof(IsVFX))] public bool ShouldDestroyOnParticleEnd;

    // SFX
    [ShowIf(nameof(IsSFX))] public AudioClip AudioClip;
    [ShowIf(nameof(IsSFX))] [Range(0,1)] public float Volume = 1f;

    // Animation
    [ShowIf(nameof(IsAnimation))] public string AnimTrigger;
    [ShowIf(nameof(IsAnimation))] public bool TargetSourcePawn = true;

    // BehaviorValue
    [ShowIf(nameof(IsBehaviorValue))] public BehaviorValueKey BehaviorValueKey = BehaviorValueKey.None;
    [ShowIf(nameof(IsBehaviorValue))] public BehaviorValueTarget ValueTarget = BehaviorValueTarget.Target;

    // FloatingNumber
    [ShowIf(nameof(IsFloatingNumber))] public BehaviorValueKey FloatingNumberSourceKey = BehaviorValueKey.FloatingDamage;
}
```

Los campos condicionales son el contrato autoral: el tipo de entry define qué
campos son relevantes, y el `ShowIf` los oculta para que el autor no configure
basura.

### 10.3 `FeedbackType` y enums de secuencia

```csharp
public enum FeedbackType { VFX, SFX, Animation, Wait, BehaviorValue, FloatingNumber }

public enum BehaviorValueTarget { Source, Target }

public enum SpawnPosition
{
    AtSource,                 // pos del pawn source (fallback a slot si no se encuentra)
    AtTarget,                 // pos del pawn target (fallback a slot)
    AtSlot,                   // pos del targetSlotId
    BetweenSourceAndTarget,   // Vector3.Lerp(source, target, 0.5f)
    WorldPosition,            // usa request.WorldPosition
    FromReader                // delega a un IPositionReader (leer §10.6)
}

public enum FeedbackCompletionMode { Timer, AnimationEvent, ParticleEnd }

public enum StepSource { FeedbackRef, InlineWait, InlineAnimation, InlineBehaviorValue }
public enum StepStartMode { Immediate, AfterPrevious, AfterStep, OnEvent }
public enum StepEndMode { OnDuration, OnNaturalEnd, OnEvent, Immediate }
```

Invariante clave del sequencer: **"done" es una marca de orden, no un comando de stop**.
Cuando un step se marca done, jamás se detiene el particle system, ni se corta el
audio, ni se interrumpe la animación. Solo desbloquea a los steps que lo estaban
esperando. Los efectos siempre tocan hasta el final por su cuenta.

### 10.4 `FeedbackRequest` (DTO)

Versión single‑player, simplificada del original de Bot‑Game:

```csharp
public struct FeedbackRequest
{
    public string FeedbackId;               // id de entry — solo si IsSequence == false
    public string SourcePawnGuid;           // guids como string para logging consistente
    public string TargetPawnGuid;
    public int SourcePlayerId;              // ids de jugador (0 = player, 1 = enemigo en Rollgeon single-player)
    public int SourceSlotId;
    public int TargetSlotId;
    public Vector3 WorldPosition;           // usado cuando la entry tiene SpawnPosition.WorldPosition
    public bool IsSequence;
    public List<FeedbackSequenceStep> SequenceSteps;
    public Action OnComplete;

    // Snapshot del bag de behavior values (§9). Se pasa por referencia en Rollgeon
    // (antes eran arrays paralelos para cruzar FishNet ObserversRpc).
    public IReadOnlyDictionary<BehaviorValueKey, List<BaseBehaviorStoredValue>> StoredValues;
}
```

El `StoredValues` es un **snapshot** — no una referencia viva al bag del behavior.
Esto es importante porque el behavior puede limpiarse (`ClearBehaviorValues()`)
entre que el efecto arma el request y que el feedback efectivamente corre.
`BaseEffect.GetFeedbackRequest(ctx)` es responsable de copiar el bag (ver §9 y §8.5).

### 10.5 Pipeline — single feedback

`EffPlayFeedback` (§10.9) llama `feedbackManager.RequestFeedbackBlocking(request, onComplete)`.
El camino interno para un feedback simple (no secuencia):

```
FeedbackManager.RequestFeedbackBlocking(request, onComplete)
  ├─ DB.TryGetFeedback(request.FeedbackId, out entry)
  │     └─ si falla: Debug.LogWarning, onComplete(), return
  ├─ instanceId = _nextFeedbackId++
  ├─ _activeFeedbacks[instanceId] = new ActiveFeedback { CompletionCallback = onComplete }
  ├─ StartCoroutine(ExecuteLocalFeedback(instanceId, entry, …, storedValues))
  └─ StartCoroutine(FeedbackTimeoutCoroutine(instanceId, entry.Duration + 0.5f))

ExecuteLocalFeedback:
  ├─ PlayFeedbackEntry(entry, …)                     // §10.7 (dispatch por type)
  ├─ EventManager.Trigger(OnFeedbackStarted, instanceId, entry.FeedbackId)
  ├─ espera:
  │     if playback.Listener != null → "listener OR timer — lo que venga primero"
  │     else                          → WaitForSeconds(duration)
  ├─ cleanup de VFX (destroy ahora O wait‑on‑particle‑end si ShouldDestroyOnParticleEnd)
  ├─ EventManager.Trigger(OnFeedbackCompleted, instanceId, entry.FeedbackId)
  └─ CompleteFeedback(instanceId) → invoca onComplete, limpia _activeFeedbacks
```

La coexistencia listener‑OR‑timer es importante: si la entry tiene `CompletionMode = ParticleEnd`
pero el particle nunca para, el timer `Duration` corta igual. Nunca colgamos la resolución
del turno por un particle roto.

### 10.6 Resolución de posición

`FeedbackPositionResolver.Resolve(entry, mode, source/target guids, slot ids, worldPos, offset)`
es un switch puro sobre `SpawnPosition`:

| Mode | Cómo resuelve |
|---|---|
| `AtSource` / `AtTarget` | `PawnRegistryService.Get(guid)?.transform.position` con fallback a `GridManager.Grid[slotId]` |
| `AtSlot` | `GridManager.Grid[targetSlotId].transform.position` |
| `BetweenSourceAndTarget` | `Vector3.Lerp(source, target, 0.5f)` |
| `WorldPosition` | `request.WorldPosition` (el efecto lo setea antes de publicar) |
| `FromReader` | delega a un `IPositionReader` (SO) con un `ReadInfo { playerId }` resuelto vía `entry.PlayerTarget` (`Player` / `Enemy`). Permite reglas tipo "centro del HUD del jugador" o "cámara + offset". |

El resultado final es `resolvedPosition + entry.PositionOffset`. El offset es
sumado por el resolver, no por el caller — no duplicar sumas.

**Cross‑ref.** `PawnRegistryService` en §7, `GridManager` en §13.

### 10.7 Dispatch por tipo — `PlayFeedbackEntry`

Primitivo compartido entre el single path y el sequence path. Switch por `entry.Type`:

**VFX.**
```csharp
handle.SpawnedVfx = Instantiate(entry.VfxPrefab, position, Quaternion.identity);
if (entry.CompletionMode == FeedbackCompletionMode.ParticleEnd)
{
    var listener = handle.SpawnedVfx.AddComponent<FeedbackCallbackListener>();
    listener.ListenForParticleEnd();   // setea ParticleSystem.MainModule.stopAction = Callback
    handle.Listener = listener;
}
```

**SFX.**
```csharp
ServiceLocator.GetService<IAudioService>()
    .PlaySfx(entry.AudioClip, position, entry.Volume);
```
Rutea por el `IAudioService` (§A) para respetar el mixer y el master volume. Sin listener — SFX siempre termina por timer.

**Animation.**
```csharp
var animator = GetTargetAnimator(entry.TargetSourcePawn, sourceGuid, targetGuid);
ApplyAnimatorFloats(animator, storedValues);          // §10.10
animator.SetTrigger(entry.AnimTrigger);
if (entry.CompletionMode == FeedbackCompletionMode.AnimationEvent)
{
    var listener = animator.gameObject.GetComponent<FeedbackCallbackListener>()
                ?? animator.gameObject.AddComponent<FeedbackCallbackListener>();
    listener.ListenForAnimatorStateEnd(animator, entry.AnimTrigger, entry.Duration);
    handle.Listener = listener;
}
```

**BehaviorValue.** Delega en `ApplyBehaviorValue(key, target, storedValues, …)` —
busca el key en el snapshot, resuelve el pawn (Source/Target), y despacha por
subtipo runtime en `DispatchBehaviorValueFeedback`. Hoy los `FloatBehaviorValue`
no tienen handler standalone (su camino real es `ApplyAnimatorFloats`), y los
`FloatingNumberBehaviorValue` emiten warning porque tienen su propio step
(preferir `FeedbackType.FloatingNumber`).

**FloatingNumber.** Itera `storedValues[entry.FloatingNumberSourceKey]` y por
cada `FloatingNumberBehaviorValue`:

```csharp
StartCoroutine(SpawnFloatingNumberDelayed(fn, sourceGuid, targetGuid));
```

`SpawnFloatingNumberDelayed` espera `fn.Delay`, carga el prefab desde
`Resources/FloatingNumber` (cacheado en static), resuelve el pawn
(`fn.TargetPawnGuid` si está, sino `targetGuid`), instancia y llama
`instance.Initialize(fn.Value.ToString(), fn.Type, pawn.position + fn.Offset)`.
Cada entry tiene su propia coroutine, así un multi‑hit de 4 golpes puede
staggearse sin que una entry bloquee a otra.

**Wait.** No hace nada en `PlayFeedbackEntry` — el tiempo lo maneja `WaitEndTrigger`
en el sequencer.

### 10.8 Secuencias, event bus y runners paralelos

Una secuencia se autora como `List<FeedbackSequenceStep>`. Cada step declara
cuatro cosas:

1. **Source** — `FeedbackRef` (un id del DB), `InlineWait`, `InlineAnimation`,
   `InlineBehaviorValue`.
2. **Start trigger** — `Immediate` / `AfterPrevious` / `AfterStep(index)` /
   `OnEvent(key)`, con `StartDelay` opcional post‑trigger.
3. **End trigger** — `OnDuration` (con `DurationOverride` opcional) / `OnNaturalEnd` /
   `OnEvent(key)` / `Immediate`.
4. **BlockSequence** — si false, la secuencia global no espera a este step
   para reportar completa (fire‑and‑forget).

El ejecutor (`ExecuteLocalSequence`) arranca:

```csharp
var bus = new FeedbackEventBus();                       // §10.8.1
var handles = new StepHandle[steps.Length];
FeedbackSequenceRuntime.SetCurrent(bus);                // §10.8.2

for (int i = 0; i < steps.Length; i++)
    StartCoroutine(RunStep(i, steps, handles, bus, ctx)); // todos en paralelo

while (true)
{
    bool allBlockingDone = true;
    for (int i = 0; i < steps.Length; i++)
        if (steps[i].BlockSequence && !handles[i].InternalDoneSignaled)
            { allBlockingDone = false; break; }
    if (allBlockingDone) break;
    yield return null;
}

FeedbackSequenceRuntime.ClearCurrent(bus);
```

`RunStep` es `WaitStartTrigger → (StartDelay) → dispatch playback → WaitEndTrigger →
handles[i].InternalDoneSignaled = true; bus.Publish($"$step.{i}.end")`.

**10.8.1 `FeedbackEventBus`**. Hash‑set latched por secuencia:

```csharp
public class FeedbackEventBus
{
    private readonly HashSet<string> _fired = new();
    public void Publish(string key) { if (!string.IsNullOrEmpty(key)) _fired.Add(key); }
    public bool HasFired(string key) => !string.IsNullOrEmpty(key) && _fired.Contains(key);
}
```

Un key publicado queda "firado" para todo el resto de la secuencia — si un step
se suscribe con `OnEvent("hit")` **después** de que "hit" ya fue publicado,
resume inmediatamente. Esto previene la race condition clásica de pub/sub
donde un subscriber late se pierde el evento.

Scope: cada `ExecuteLocalSequence` crea un bus nuevo. Event keys nunca leakean
entre secuencias concurrentes (aunque en el modelo turn‑blocking de Rollgeon
solo corre una secuencia top‑level a la vez).

**10.8.2 `FeedbackSequenceRuntime`**. Puntero estático al bus activo:

```csharp
public static class FeedbackSequenceRuntime
{
    public static FeedbackEventBus Current { get; private set; }
    public static void SetCurrent(FeedbackEventBus bus);
    public static void ClearCurrent(FeedbackEventBus expected);
    public static void Publish(string key);   // conveniencia — no-op si no hay activa
}
```

Lo usan los componentes que no tienen referencia directa al bus:
- **Animation Events** — el autoral pega un evento en el clip que llama a un
  helper `AnimationFeedbackEvent` que internamente hace `FeedbackSequenceRuntime.Publish(key)`.
  Así un step `OnEvent("slash-impact")` se sincroniza con el frame exacto del
  golpe en vez de depender de un timer.
- **Particle stop callbacks** que quieran publicar un key al terminar.

`ClearCurrent(expected)` solo limpia si `Current == expected`, para proteger
contra teardowns fuera de orden.

**10.8.3 Patrones típicos de autoría**.

| Objetivo | Autoría |
|---|---|
| "Tocar VFX + SFX a la vez" | Dos steps `Immediate` / `OnDuration` — corren en paralelo por default |
| "Animación de ataque, cuando el Animation Event 'hit' dispara, spawneo damage VFX" | Step 0: `InlineAnimation`, `Immediate`, `OnEvent("hit")` como end. Step 1: `FeedbackRef` VFX, `OnEvent("hit")` como start |
| "Multi‑hit con stagger" | Un solo step `FeedbackRef` FloatingNumber — cada `FloatingNumberBehaviorValue` en la lista tiene su `Delay` propio |
| "Fire‑and‑forget particle de ambiente" | `BlockSequence = false` — la secuencia reporta complete sin esperarlo |
| "Wait de 0.3s entre dos VFX" | Step intermedio `InlineWait` con `WaitDuration = 0.3`, `AfterPrevious` |

### 10.9 Efecto autor — `EffPlayFeedback`

El único puente oficial entre la pipeline de efectos (§8) y la de feedback es:

```csharp
public class EffPlayFeedback : BaseEffect, IUsesFeedback, ICanBeAnimFeedback, ICanBeSFXFeedback, ICanBeVFXFeedback
{
    public override string EffectName => "Play Generic Feedback";

    public override bool ApplyEffect(EffectContext context)
    {
        var feedbackManager = ServiceLocator.GetService<FeedbackManager>();
        var turnManager = ServiceLocator.GetService<TurnManager>();

        turnManager.BeginFeedbackWait();
        feedbackManager.RequestFeedbackBlocking(
            GetFeedbackRequest(context),
            () => turnManager.OnFeedbackComplete());
        return true;
    }
}
```

**Importante**:
1. `BeginFeedbackWait()` / `OnFeedbackComplete()` son los hooks del `TurnManager`
   que hacen al pipeline de resolución (§12) esperar el feedback antes de seguir
   al próximo efecto de la cadena. Sin esto, los efectos se encadenarían sin darle
   tiempo al jugador a ver el hit.
2. `GetFeedbackRequest(context)` está en `BaseEffect` — arma el request leyendo
   los ids del autor, el source/target resuelto de la selección y el snapshot
   de `context.SourceBehavior.StoredValues` (§9).
3. Las capability interfaces (`IUsesFeedback`, `ICanBeAnimFeedback`, …) le dicen
   al inspector qué campos mostrar — siguen el mismo patrón que el resto del
   sistema de effects (§8.4).

Cualquier otro efecto puede igualmente stagear valores (`SetBehaviorValue`) y
un `EffPlayFeedback` downstream los va a consumir. Ver `EffMakeDamage`
(`Assets/Scripts/EffectNPrecondition/Effects/EffMakeDamage.cs`) como ejemplo —
stagea `FloatingDamage` y `DirectionMagnitude`, y depende de que un step
FloatingNumber y un step Animation las recojan.

### 10.10 `ApplyAnimatorFloats` — blend trees driveados por effects

Antes de cada `SetTrigger`, el manager hace:

```csharp
private static void ApplyAnimatorFloats(
    Animator animator,
    IReadOnlyDictionary<BehaviorValueKey, List<BaseBehaviorStoredValue>> storedValues)
{
    foreach (var kv in storedValues)
    {
        for (int i = kv.Value.Count - 1; i >= 0; i--)
        {
            if (kv.Value[i] is FloatBehaviorValue f)
            {
                animator.SetFloat(kv.Key.ToString(), f.Value);  // enum name = animator param name
                break;    // last-write-wins
            }
        }
    }
}
```

**Contrato**: el **nombre del valor en el enum `BehaviorValueKey`** tiene que
matchear **letra por letra** el nombre del parámetro Animator. Si el enum tiene
`DirectionMagnitude` y el Animator tiene un float param `DirectionMagnitude`,
el blend tree va a ver el valor en el primer frame del state triggereado. Si
no matchea, `SetFloat` es un no‑op silencioso.

Cuando una misma key tiene múltiples `FloatBehaviorValue` staged, gana el último
(last‑write‑wins). La iteración va en reversa para tomar el último en O(1) por key.

**Cross‑ref.** `BehaviorValueKey` y las subclases de `BaseBehaviorStoredValue`
viven en §9.

### 10.11 Listener de finalización — `FeedbackCallbackListener`

`MonoBehaviour` que se adjunta al GameObject a observar. Dos caminos:

**Particle system** (`CompletionMode = ParticleEnd`):
```csharp
public void ListenForParticleEnd()
{
    var ps = GetComponentInChildren<ParticleSystem>();
    var main = ps.main;
    main.stopAction = ParticleSystemStopAction.Callback;
}
private void OnParticleSystemStopped() => Complete();
```

**Animator state** (`CompletionMode = AnimationEvent`):
- Opción A: agregar un Animation Event en el clip que llame al método
  `OnFeedbackAnimationComplete()`.
- Opción B: `ListenForAnimatorStateEnd(animator, triggerName, timeout)` —
  coroutine que monitorea `GetCurrentAnimatorStateInfo(0).normalizedTime >= 1 &&
  !IsInTransition(0)`, con timeout de seguridad.

Ambos caminos disparan `OnCompleted` una sola vez (`_completed` guard), y
`IsCompleted` queda expuesto para que el caller haga poll. El listener nunca
se destruye a sí mismo — eso es responsabilidad del dueño (el manager cleanup
en single feedback path, o nadie en sequence path porque los effects "play
out naturally").

### 10.12 Watchdog de timeout

`FeedbackManager.FeedbackTimeoutCoroutine(instanceId, duration + safety)` es
una red de seguridad contra feedbacks que se cuelgan — particles rotos, listeners
que nunca disparan, bugs de secuencia. Si el `instanceId` sigue en
`_activeFeedbacks` pasado el tiempo:

```csharp
Debug.LogWarning($"[FeedbackManager] Feedback {instanceId} timed out. Force completing.");
CompleteFeedback(instanceId);
```

Presupuestos:
- Single feedback: `entry.Duration + 0.5f`
- Secuencia: `max(sum de EstimateStepDuration de steps BlockSequence, 2f) + 2f`
  donde `EstimateStepDuration` usa 5s conservador para steps `OnNaturalEnd` /
  `OnEvent` sin override (por las dudas de que el event nunca fire).

El watchdog **siempre** llama al `onComplete` original, así `TurnManager` nunca
queda colgado esperando un `OnFeedbackComplete()`. El turno avanza aunque el
arte del feedback esté roto — degrada en vez de deadlockear.

### 10.13 Cómo extender el sistema

**Agregar un `FeedbackType` nuevo** (ej: "CameraShake"):
1. Agregar el valor al enum `FeedbackType`.
2. Agregar los campos autorales en `FeedbackEntry` con `ShowIf` sobre el nuevo type.
3. Agregar un case al switch en `PlayFeedbackEntry` que despacha el nuevo type.
4. Si el dispatch necesita completion natural, rellenar `handle.Listener`
   apuntando a un `FeedbackCallbackListener` que sepa cuándo el efecto terminó.
5. Si el dispatch necesita un prefab SO externo, cargarlo vía `ServiceLocator`
   (pre‑cargado desde §1.1.1) — nunca `Resources.Load` en caliente, salvo
   excepciones muy puntuales como el `FloatingNumber` que ya es static‑cached.

**Agregar un `BaseBehaviorStoredValue` nuevo**:
1. Crear la subclase en `EffectNPrecondition/BehaviorValues/`.
2. Agregar la rama al switch de `DispatchBehaviorValueFeedback` si tiene
   un handler standalone (o al `PlayFeedbackEntry` si merece un `FeedbackType`
   dedicado).
3. Agregar el `BehaviorValueKey` correspondiente al enum.
4. Los efectos que quieran stagearlo usan `context.SourceBehavior.SetBehaviorValue(key, new MyValue { … })`.
5. El autor lo consume con un step de type `BehaviorValue` o un `FeedbackType`
   dedicado, y con `BehaviorValueTarget.Source | Target` según el caso.

**Agregar un `StepSource` nuevo**: agregar al enum, agregar los campos con
`ShowIf`, agregar las ramas a `RunStep` (dispatch) y `EstimateStepDuration`
(budget para el watchdog).

**Agregar un `SpawnPosition` nuevo**: agregar al enum, agregar el case al
`FeedbackPositionResolver.Resolve` switch. El offset final se suma fuera
del switch — no sumarlo dentro del case para no duplicar.

**Cross‑ref.** §1.1.1 (bootstrap), §7 (PawnRegistryService), §8 (effects pipeline),
§9 (behavior values), §12 (TurnManager y la integración con la resolución
de daño), §13 (GridManager para resolver slots).

---

## 11. Selection / Targeting

### 11.1 Modelo

```csharp
public class TargetRef
{
    public static TargetRef Slot(int slotId);
    public static TargetRef Entity(Guid guid);
}

public class TargetSelectionResult
{
    public bool WasCompleted;
    public bool WasCancelled;
    public bool WasSkipped;
    public List<TargetRef> SelectedTargets;

    public int FirstSelectedId => SelectedTargets?[0]?.SlotId ?? -1;
    public Guid FirstSelectedGuid => SelectedTargets?[0]?.Guid ?? Guid.Empty;
}
```

### 11.2 `SelectionSettings`

```csharp
[Serializable]
public class SelectionSettings
{
    public bool RequiresSelection;
    public SelectionTiming Timing;
    public bool IsConstantSelectionCount;
    public int SelectionCount;
    public bool IsSkippable;
    public bool RequireEmptySlot;
    public bool RequireOccupiedSlot;        // excluyentes — validado

    [OdinSerialize, SerializeReference]
    [HideReferenceObjectPicker]
    public BaseTargetQuery TargetQuery;     // inline polimórfico — ver §11.2b

    public int GetSelectionCount(ReadInfo info);
    public bool NeedsSelectionAt(SelectionTiming t);
}
```

`BaseTargetQuery` define la query lógica inline (qué casillas/entidades son válidas). Cada efecto configura su propia instancia — no es un SO compartido. Ver §11.2b.

### 11.2b `BaseTargetQuery`

Clase abstracta polimórfica serializada inline dentro de `SelectionSettings`. Sigue el mismo patrón que `BasePreCondition` (§8.2) y `AIDecisionNode` (§7.5): `[Serializable, HideReferenceObjectPicker]` + `[OdinSerialize, SerializeReference]` en el campo contenedor.

```csharp
[Serializable, HideReferenceObjectPicker]
public abstract class BaseTargetQuery
{
    public abstract string QueryName { get; }
    public abstract List<TargetRef> Evaluate(TargetQueryContext context);
}

public class TargetQueryContext
{
    public Guid OwnerGuid;
    public Entity OwnerEntity;
    public GridCoord OwnerPosition;
    public RoomInstance CurrentRoom;
    public GamePhase CurrentPhase;
}
```

**Catálogo inicial de queries concretas:**

| Query | Parámetros | Uso |
|---|---|---|
| `TQ_AdjacentEnemies` | `int Range = 1` | Ataques melee |
| `TQ_EnemiesInLineOfSight` | `int MaxRange` | Ataques ranged |
| `TQ_AllEnemies` | — | AoE |
| `TQ_EmptySlots` | `bool LineOfSightRequired` | Movimiento, teleport |
| `TQ_ReachableTiles` | — (delega a `IMovementService` via `ServiceLocator`) | `EffMove` |
| `TQ_OccupiedSlots` | `EntityFilterMask Filter` | Targeting tiles ocupados |
| `TQ_AvailableCombos` | `bool ExcludeStricken = true` | `EffStrikeCombo` |
| `TQ_Self` | — | Efectos self-targeting |
| `TQ_Composite` | `List<BaseTargetQuery> Queries`, `CompositeMode {Union, Intersect}` | Queries multi-criterio |

Prefijo `TQ_` alineado con las convenciones del proyecto (`PC` preconditions, `Eff` effects, `AI` nodos de IA).

**Acceso a servicios.** Las queries que necesitan servicios runtime (e.g. `TQ_ReachableTiles` → `IMovementService`) los obtienen via `ServiceLocator` dentro de `Evaluate`, igual que los efectos acceden a servicios desde `ApplyEffect`.

### 11.3 Grid selection

`IUsesGridSelection` habilita un `[TableMatrix] bool[,] GridSelection` cuyo tamaño se parametriza por la sala actual — Rollgeon tiene grillas isométricas de tamaño variable según `RoomSO` (§13.2).

### 11.4 Runtime

- `ActiveSelectionSession` — estado de una selección en curso (highlight, click capture, cancel).
- `SelectionController` — orquestador con callbacks. Resuelve targets válidos llamando `settings.TargetQuery.Evaluate(ctx)` sobre la instancia inline de `BaseTargetQuery` (§11.2b).
- `MultiActionSelectionData` — selecciones encadenadas en un mismo behavior.

### 11.5 Validación

```csharp
public virtual bool ValidateSelection(TargetSelectionResult result, Guid ownerGuid, out string error)
{
    error = null;
    if (!HasSelectionRequirement()) return true;
    if (result == null) { error = "Selection result is null"; return false; }
    if (result.WasCancelled && !Selection.IsSkippable) { error = "Selection cancelled"; return false; }

    if (result.WasCompleted)
    {
        var required = Selection.GetSelectionCount(new ReadInfo { ownerGuid = ownerGuid });
        if (result.SelectedTargets.Count < required)
        {
            error = $"Expected {required}, got {result.SelectedTargets.Count}";
            return false;
        }
    }
    return true;
}
```

---

## 12. Combate: pipeline de daño

> Esta sección une §3 (modificadores direccionales), §4 (clase + sheet), §5 (combos), §6 (dados) y §8 (efectos). Es el flujo completo de resolución de un ataque.

### 12.0 `GamePhase`, overlays y `IPhaseService`

Formalización del gate macro — cierra la deuda de v9/v10. Se referencia desde §1.2 (events de fase), §1.3 (`CombatTurnFSM` vive dentro de `Combat`), §7.2 (`AllowedPhases` filter), §7.7 (`PhaseInteractionRule`), §16.1 (input maps), §17.C (Craps como overlay de combate), §17.E (camera) y §17.F (shop service).

#### 12.0.1 Enum `GamePhase`

```csharp
public enum GamePhase
{
    None        = 0,  // pre‑bootstrap o entre runs (MainMenu screen activo, sin gameplay)
    Exploration = 1,  // free movement en sala limpia; NPCs, props, shop items en el piso
    Combat      = 2,  // hay enemigos vivos; CombatTurnFSM (§1.3) corriendo
    Loading     = 3,  // transición entre floors/salas pesadas; input 100% bloqueado
    GameOver    = 4,  // player muerto, mostrando resultados; espera input para volver al meta
}
```

**Cambios vs. draft anterior.** `Shop` se elimina — los ítems de shop son props en el piso que el jugador compra vía `IInteractionService` estando en `Exploration` (§17.F). `Craps` se mueve a `PhaseOverlay` — es una mecánica exclusiva de combate (§17.C). `Cutscene` se mueve a `PhaseOverlay` — suspende la fase actual y vuelve al hacer pop. Se agrega `None` (pre‑gameplay), `Loading` (bloqueo total de input entre transiciones) y `GameOver` (terminal de muerte fuera de combate — dentro de combate, `Defeat` dispara el `ReplacePhase(GameOver)` internamente).

**Regla de estabilidad.** Los valores enteros del enum son persistidos en saves. **Nunca renumerar. Nuevos valores se appendean con el próximo entero libre. Valores deprecados se marcan `[Obsolete]` y se reservan — no se reutiliza el entero.** Lo mismo aplica a `PhaseOverlay`.

#### 12.0.2 Enum `PhaseOverlay`

Overlays suspenden la fase base sin destruirla. Se apilan con Push/Pop.

```csharp
public enum PhaseOverlay
{
    None     = 0,
    Pause    = 1,  // menú de pausa; congela CombatTurnFSM si base == Combat
    Cutscene = 2,  // secuencia narrativa; input bloqueado salvo skip
    Craps    = 3,  // mini‑juego de dados §17.C; exclusivo de Combat
}
```

#### 12.0.3 `IPhaseService`

Reemplaza al viejo `TurnManager.SetPhase`. Registrado en `ServiceLocator`.

```csharp
public interface IPhaseService
{
    GamePhase    CurrentBase    { get; }
    PhaseOverlay CurrentOverlay { get; }
    IReadOnlyList<PhaseStackEntry> Stack { get; }

    void ReplacePhase(GamePhase next);
    void PushOverlay(PhaseOverlay overlay);
    void PopOverlay();

#if UNITY_EDITOR || DEBUG
    void DebugForcePhase(GamePhase phase, PhaseOverlay overlay = PhaseOverlay.None);
#endif
}

public readonly struct PhaseStackEntry
{
    public GamePhase    Base    { get; init; }
    public PhaseOverlay Overlay { get; init; }
}
```

**`ReplacePhase(next)`** — transición destructiva. Limpia overlays activos (si hay un Pause pusheado, se popea primero). Dispara `OnPhaseExit(old)` + `OnPhaseEnter(next)`.

**`PushOverlay(overlay)`** — apila un overlay sobre la fase base actual. La fase base queda intacta pero **congelada**: el `CombatTurnFSM` no tickea mientras hay un overlay activo, el input de gameplay se desactiva, la cámara se congela. Dispara `OnOverlayPushed(overlay)`.

**`PopOverlay()`** — restaura el estado previo. Dispara `OnOverlayPopped(overlay)`.

**Regla: nadie lee `CurrentBase`/`CurrentOverlay` en `Update()`.** Los sub‑sistemas toman snapshot vía los events y actúan sobre event. Para enforcement, un architecture test en editor grepea por `CurrentBase`/`CurrentOverlay` dentro de métodos `Update`/`LateUpdate`/`FixedUpdate`.

**Regla: `ReplacePhase` prohibido desde overlay context.** Cuando `CurrentOverlay != PhaseOverlay.None`, la única transición legal es `PopOverlay()`. Si un listener de overlay (p. ej. un `BaseBehavior` dentro de `PhaseOverlay.Cutscene`) llama `ReplacePhase(next)`, el servicio tira `InvalidPhaseTransitionException` en builds Debug, y es **no‑op** en builds Release (fail‑soft).

```csharp
public void ReplacePhase(GamePhase next)
{
    if (CurrentOverlay != PhaseOverlay.None)
    {
#if UNITY_EDITOR || DEBUG
        throw new InvalidPhaseTransitionException(
            $"ReplacePhase({next}) is illegal while overlay {CurrentOverlay} is active. " +
            $"Call PopOverlay() first, then ReplacePhase.");
#else
        return;   // no-op en release — no queremos tirar el juego del jugador
#endif
    }

    if (_isDispatching)
    {
        _pendingTransitions.Enqueue(() => ReplacePhase(next));
        return;
    }
    ExecuteReplace(next);
    DrainQueue();
}
```

**Razón.** Un overlay tiene semántica de "suspensión": la fase base queda congelada y el overlay debe devolverle el control vía pop. Dejar que un overlay haga `ReplacePhase` rompe el contrato — la fase base nunca reanuda, el stack queda sucio, y un listener que esperaba el `OnOverlayPopped` nunca lo recibe. Casos de uso que parecen necesitarlo (p. ej. "una cutscene que al terminar lleva al Game Over"): se resuelven con el patrón **pop → replace** dentro del handler de `OnOverlayPopped`, no dentro del overlay:

```csharp
// En el BaseBehavior de la cutscene de muerte del boss:
EventManager.Subscribe(EventName.OnOverlayPopped, args => {
    if ((PhaseOverlay)args[0] == PhaseOverlay.Cutscene)
        ServiceLocator.GetService<IPhaseService>().ReplacePhase(GamePhase.Exploration);
});
ServiceLocator.GetService<IPhaseService>().PopOverlay();
```

El `DeathWatchService` (§12.0 más abajo) cumple este patrón: al detectar muerte fuera de combate, espera a que cualquier overlay se popee antes de hacer `ReplacePhase(GameOver)`.

#### 12.0.4 Queue + drain (reentrancy)

Si un listener llama a `ReplacePhase` o `PushOverlay` dentro de un handler de `OnPhaseEnter`, la transición se encola y se despacha al terminar el dispatch actual. Determinístico.

```csharp
public class PhaseService : IPhaseService
{
    private readonly Queue<Action> _pendingTransitions = new();
    private bool _isDispatching;

    // ReplacePhase (con guard de overlay) se muestra completo en §12.0.3 arriba.
    // Acá solo reproducimos la variante de PushOverlay para ilustrar el patrón
    // de reentrancy — la forma de ReplacePhase con Queue+drain es idéntica.

    public void PushOverlay(PhaseOverlay overlay)
    {
        if (_isDispatching)
        {
            _pendingTransitions.Enqueue(() => PushOverlay(overlay));
            return;
        }
        ExecutePush(overlay);
        DrainQueue();
    }

    private void ExecuteReplace(GamePhase next)
    {
        if (next == CurrentBase && CurrentOverlay == PhaseOverlay.None) return;

        _isDispatching = true;

        // Popear overlays primero si hay alguno activo
        while (CurrentOverlay != PhaseOverlay.None)
            ExecutePop();

        var prev = CurrentBase;
        EventManager.Trigger(EventName.OnPhaseExit, prev);
        CurrentBase = next;
        EventManager.Trigger(EventName.OnPhaseEnter, next);

        _isDispatching = false;
    }

    private void ExecutePush(PhaseOverlay overlay)
    {
        ValidateOverlay(CurrentBase, overlay);
        _isDispatching = true;

        _stack.Push(new PhaseStackEntry { Base = CurrentBase, Overlay = CurrentOverlay });
        CurrentOverlay = overlay;
        EventManager.Trigger(EventName.OnOverlayPushed, overlay);

        _isDispatching = false;
    }

    private void ExecutePop()
    {
        var exiting = CurrentOverlay;
        _isDispatching = true;

        if (_stack.Count > 0)
        {
            var restored = _stack.Pop();
            CurrentOverlay = restored.Overlay;
        }
        else
        {
            CurrentOverlay = PhaseOverlay.None;
        }

        EventManager.Trigger(EventName.OnOverlayPopped, exiting);
        _isDispatching = false;
    }

    private bool _isDraining;

    private void DrainQueue()
    {
        if (_isDraining) return;       // evita re-entrar en el drain
        _isDraining = true;
        try
        {
            while (_pendingTransitions.Count > 0)
                _pendingTransitions.Dequeue().Invoke();
        }
        finally
        {
            _isDraining = false;
        }
    }
}
```

#### 12.0.5 `PhaseTransitionMatrixSO` — validación editable desde engine

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Phase/Transition Matrix")]
public class PhaseTransitionMatrixSO : SerializedScriptableObject
{
    [Title("Transiciones de fase base")]
    [InfoBox("Marcar las transiciones permitidas. En debug, ReplacePhase " +
             "hace assert contra esta matriz. En release, se ignora (perf).")]
    [TableMatrix(HorizontalTitle = "To", VerticalTitle = "From")]
    [OdinSerialize]
    public bool[,] AllowedTransitions = new bool[5, 5];

    [Title("Overlays válidos por fase base")]
    [InfoBox("Qué overlays se pueden pushear sobre cada fase base.")]
    [OdinSerialize]
    public Dictionary<GamePhase, List<PhaseOverlay>> AllowedOverlays = new();

    public bool CanTransition(GamePhase from, GamePhase to) =>
        AllowedTransitions[(int)from, (int)to];

    public bool CanPushOverlay(GamePhase currentBase, PhaseOverlay overlay) =>
        AllowedOverlays.TryGetValue(currentBase, out var list) && list.Contains(overlay);
}
```

**Matriz de transiciones default.**

| From \ To | None | Exploration | Combat | Loading | GameOver |
|---|---|---|---|---|---|
| **None** | - | ok | - | ok | - |
| **Exploration** | - | - | ok | ok | ok |
| **Combat** | - | ok | - | - | ok |
| **Loading** | - | ok | - | - | - |
| **GameOver** | ok | - | - | - | - |

**Overlays válidos por fase base.**

| Fase base | Overlays permitidos |
|---|---|
| None | (ninguno) |
| Exploration | Pause, Cutscene |
| Combat | Pause, Cutscene, Craps |
| Loading | (ninguno) |
| GameOver | (ninguno) |

Craps es **exclusivamente** overlay de `Combat` — no se puede pushear en `Exploration` ni en ninguna otra fase. El `CombatTurnFSM` se congela mientras el overlay está activo y se reanuda al pop.

**Validación runtime.**

```csharp
[Conditional("UNITY_EDITOR"), Conditional("DEBUG")]
private void ValidateTransition(GamePhase from, GamePhase to)
{
    if (!_matrix.CanTransition(from, to))
        Debug.LogError($"[PhaseService] Invalid transition: {from} → {to}");
}

[Conditional("UNITY_EDITOR"), Conditional("DEBUG")]
private void ValidateOverlay(GamePhase currentBase, PhaseOverlay overlay)
{
    if (!_matrix.CanPushOverlay(currentBase, overlay))
        Debug.LogError($"[PhaseService] Invalid overlay: {overlay} over {currentBase}");
}
```

#### 12.0.6 Eventos de fase (schema §1.2)

Reemplazan al viejo `OnPhaseChange` (single event) con cuatro eventos especializados:

| Evento | Args | Cuándo |
|---|---|---|
| `OnPhaseExit` | `[GamePhase exiting]` | Justo antes de cambiar `CurrentBase`. |
| `OnPhaseEnter` | `[GamePhase entering]` | Justo después de cambiar `CurrentBase`. |
| `OnOverlayPushed` | `[PhaseOverlay overlay]` | Después de pushear un overlay. |
| `OnOverlayPopped` | `[PhaseOverlay overlay]` | Después de popear un overlay. |

Un listener que sólo le importa "entramos a combate" se suscribe a `OnPhaseEnter` y filtra `entering == Combat`. No ve exits ni overlays. Elimina el boilerplate del viejo `if (next == X)` / `if (prev == X)`.

#### 12.0.7 Relación con `GamePhaseMask` (§7.2)

El flags mask **sólo tiene bits para las fases base**:

```csharp
[Flags]
public enum GamePhaseMask
{
    None        = 0,
    Exploration = 1 << 1,  // bit = GamePhase.Exploration
    Combat      = 1 << 2,  // bit = GamePhase.Combat
    All         = ~0,
}
```

Los behaviors se autorean con `AllowedPhases` que filtra por la fase base activa. **Los overlays no participan en el filtro de behaviors** — un behavior activo en `Combat` sigue siendo activo si hay un overlay de `Pause` o `Craps` encima (aunque no se va a ejecutar porque el FSM está congelado). Esto mantiene el filtro simple y no fuerza al diseñador a pensar en overlays cuando autorea behaviors.

`Loading`, `GameOver` y `None` no tienen bit en el mask — ningún behavior debería ser elegible en esos estados.

#### 12.0.8 `DeathWatchService` — muerte fuera de combate

```csharp
public class DeathWatchService
{
    public DeathWatchService()
    {
        EventManager.Subscribe(EventName.OnHealthChanged, OnHealthChanged);
    }

    private void OnHealthChanged(params object[] args)
    {
        var entityGuid = (Guid)args[0];
        var currentHp = (int)args[1];

        if (entityGuid != ServiceLocator.GetService<IPlayerService>().PlayerGuid) return;
        if (currentHp > 0) return;

        var phaseService = ServiceLocator.GetService<IPhaseService>();

        if (phaseService.CurrentBase == GamePhase.Combat)
            return; // CheckCombatEnd del CombatTurnFSM se encarga (§1.3)

        var playerService = ServiceLocator.GetService<IPlayerService>();
        phaseService.ReplacePhase(GamePhase.GameOver);
        EventManager.Trigger(EventName.OnRunEnd, playerService.RunId, RunOutcome.Death);
    }
}
```

Registrado en el bootstrap (§1.1.1). Se suscribe a `OnHealthChanged` y detecta si el player llega a 0 HP **fuera** de combate — trampas en `Exploration`, penalidades de Craps (overlay sobre `Combat` pero si el damage ocurre post‑pop), ítems malditos. Si estamos en `Combat`, no hace nada: el `CheckCombatEnd` del `CombatTurnFSM` ya cubre ese caso. Cierra el bug latente de "morir fuera de combate no tiene handler".

#### 12.0.9 Save policy

**El save point siempre ocurre al terminar un combate o finalizar una sala/piso — nunca mid‑combat.** Implicaciones:

- **`CurrentBase` snapshoteado siempre es `Exploration`** (o `GameOver` para registrar el run). Nunca `Combat`, `Loading`, ni con un overlay activo.
- **El `CombatTurnFSM` no necesita ser `ISaveable`** — no hay estado de FSM que capturar si el save nunca ocurre mid‑combat.
- **`CrapsSession` tampoco** — Craps es overlay de combate; si el save es post‑combate, la sesión ya terminó.
- **La `RoomInstance` (§13.6) ya refleja el resultado** — enemigos `IsDead`, cofres `Opened`, ítems `Purchased`. El save no captura estado transitorio.
- **Regla doc:** "El `SaveSystem` (§15) nunca ejecuta `Flush` durante `GamePhase.Combat`, `GamePhase.Loading`, ni durante un `PhaseOverlay` activo. Los triggers válidos son `SaveTrigger.RoomEnd` y `SaveTrigger.FloorEnd`, ambos implícitamente dentro de `Exploration`."

#### 12.0.10 Dónde vive el `CombatTurnFSM`

Cuando el `PhaseService` transiciona a `GamePhase.Combat`, el `TurnManager` inicializa un `CombatTurnFSM` (§1.3) y lo tickea **sólo cuando `CurrentOverlay == None`**. Si el player pushea `Pause`, `Cutscene` o `Craps`, el FSM se congela automáticamente. Los estados internos del FSM (`Roll`, `Reroll`, `PlayerInput`, `EnemyPickNext`, …) siguen siendo **sub‑steps invisibles** para los listeners de `OnPhaseEnter`/`OnPhaseExit`. Naming: ningún micro‑state lleva `Phase` — la palabra queda reservada al enum macro (convención §1.3).

Los terminales del `CombatTurnFSM` llaman a `IPhaseService.ReplacePhase`:
- `Victory` → `ReplacePhase(Exploration)`.
- `Defeat` → `ReplacePhase(GameOver)`.

**Cross‑ref §12.0.** §1.2 (`OnPhaseExit/Enter/OverlayPushed/Popped`), §1.3 (`CombatTurnFSM` + convención naming), §7.2 (`AllowedPhases` / `GamePhaseMask`), §7.7 (`PhaseInteractionRule`), §16.1 (input maps por fase), §17.C (Craps como overlay de combate), §17.E (camera), §17.F (shop service — ítems en el piso, no modal).

### 12.1 `AttackResolver`

Servicio registrado en `ServiceLocator` que resuelve ataques básicos, especiales y de arma.

```csharp
public interface IAttackResolver
{
    AttackResult ResolveAttack(AttackRequest req);
}

public class AttackRequest
{
    public Entity Source;                // jugador o enemigo
    public Entity Target;
    public AttackKind Kind;              // Basic, Special, Weapon
    public int[] FinalDice;              // ya resueltos tras re‑rolls + encantamientos
    public ContractSheet SourceSheet;    // sheet runtime del atacante (si aplica)
}

public class AttackResult
{
    public BaseComboSO MatchedCombo;
    public int RawDamage;
    public int FinalDamage;
    public bool HitWeakness;
    public int AbsorbedByShield;
    public List<Guid> TriggeredModifiers;
}
```

### 12.2 `DamagePipeline`

Pipeline central para resolver daño entre dos entidades. **Todo** el daño pasa por acá — no hay accesos directos a `Health.SetValue`.

```csharp
public interface IDamagePipeline
{
    int Resolve(Entity source, Entity target, int rawDamage, DamageContext ctx);
}

public class DamageContext
{
    public BaseComboSO Combo;             // combo que generó el daño (si aplica)
    public bool IsWeaknessHit;
    public AttackKind Kind;
}

public class DamagePipeline : IDamagePipeline
{
    public int Resolve(Entity source, Entity target, int rawDamage, DamageContext ctx)
    {
        // ── 1. Outgoing: modificadores "hago X%" del source ────────────────
        float outgoingMult = source.Attributes
            .GetAttributeModifiedValue<OutgoingDamageMultiplier, float>();
        int afterOutgoing = Mathf.RoundToInt(rawDamage * outgoingMult);

        EventManager.Trigger(EventName.OnDamageOutgoing,
            source.InstanceId, target.InstanceId, afterOutgoing);

        // ── 2. Weakness: multiplicador por debilidad del target ────────────
        if (ctx.IsWeaknessHit && target is EnemyEntity enemy)
        {
            afterOutgoing = Mathf.RoundToInt(afterOutgoing * enemy.WeaknessMultiplier);
            EventManager.Trigger(EventName.OnWeaknessHit,
                source.InstanceId, target.InstanceId);
        }

        // ── 3. Incoming: modificadores "recibo X%" del target ──────────────
        float incomingMult = target.Attributes
            .GetAttributeModifiedValue<IncomingDamageMultiplier, float>();
        int afterIncoming = Mathf.RoundToInt(afterOutgoing * incomingMult);

        EventManager.Trigger(EventName.OnDamageIncoming,
            source.InstanceId, target.InstanceId, afterIncoming);

        // ── 4. Shield: absorción del escudo del target ─────────────────────
        int shield = target.Attributes.GetAttributeModifiedValue<Shield, int>();
        int absorbed = Mathf.Min(shield, afterIncoming);
        int finalDamage = afterIncoming - absorbed;

        if (absorbed > 0)
        {
            target.Attributes.SetAttributeValue<Shield, int>(shield - absorbed);
            EventManager.Trigger(EventName.OnShieldChanged, target.InstanceId, shield - absorbed);
        }

        // ── 5. Apply: commit al stat Health ────────────────────────────────
        if (finalDamage > 0)
        {
            var currentHp = target.Attributes.GetAttributeModifiedValue<Health, int>();
            target.Attributes.SetAttributeValue<Health, int>(currentHp - finalDamage);
            EventManager.Trigger(EventName.OnHealthChanged, target.InstanceId, currentHp - finalDamage);
        }

        EventManager.Trigger(EventName.OnDamageResolved,
            source.InstanceId, target.InstanceId, finalDamage, ctx.IsWeaknessHit);

        return finalDamage;
    }
}
```

**Cómo se resuelve "el jugador recibe +50% de daño":**

1. Al inicio del efecto del enemigo, un `EffAddFloatModifier` crea un `Modifier<float>` con:
   - `Amount = 1.5`
   - `Operation = Multiply`
   - `Direction = ModifierDirection.Incoming`
   - `OwnerId = player.InstanceId`
   - `Lifetime = ModifierLifetime.Turns`
   - `Duration = 1`
   - `TickEvent = OnTurnFinished`
2. El modificador se agrega al `IncomingDamageMultiplier` del jugador: `player.Attributes.GetAttribute<IncomingDamageMultiplier>().AddModifier(mod)`.
3. Cuando el enemigo ataca (pasos 1–2 de la pipeline), el modificador **no** participa en el cálculo del source → `outgoingMult = 1.0`.
4. Paso 3 (Incoming): `incomingMult = 1.0 × 1.5 = 1.5`, `afterIncoming = rawDamage × 1.5`.
5. El modificador se auto‑decrementa en `OnTurnFinished` gracias al lifecycle de §3.

#### 12.2.1 Fuentes de daño no‑combo (DOTs, pasivas, trampas, ambientales)

Toda fuente de daño pasa por `DamagePipeline.Resolve` — incluyendo las que no vienen de un combo del jugador. La regla de negocio del diseño: **weakness solo se dispara cuando hay un combo explícito** que la entidad declara como debilidad (§13.4). DOTs de status effects, hooks de pasivas, trampas y efectos ambientales **nunca** disparan `ctx.IsWeaknessHit = true` por default — si un efecto quiere debilidad, lo declara via combo.

**DOTs (§20 Status Effects).** `IStatusEffectService.Tick` dispara, al fin del turno del owner del status, un `EffDealDamage` que llama `DamagePipeline.Resolve` con `DamageContext { Combo = null, IsWeaknessHit = false, Kind = AttackKind.DamageOverTime }`. El pipeline corre outgoing/incoming/shield/health igual que siempre — la diferencia es solo el `Kind` (para feedback/logging) y la ausencia de weakness/combo. La pasiva del Berserker "Primer golpe ×3" (§4.4) es idéntica: si sus hooks ejecutan `EffDealDamage`, va por el pipeline con `Combo = null`.

**Trampas y daño ambiental (§7.1b PropEntitySO hostil).** Mismo patrón: cuando un `PropEntitySO` con behavior `OnEntered` dispara un `EffDealDamage` contra el jugador, la entity de la trampa pasa como `source` y se llama `DamagePipeline.Resolve` con `Combo = null`, `IsWeaknessHit = false`, `Kind = AttackKind.Environmental`. Los modificadores de incoming del player se respetan (un resist a `Environmental` filtra solo ese Kind).

**Excepción — efecto con combo explícito.** Si un diseñador quiere que una trampa o DOT gatille weakness (p. ej. una trampa de fuego que cuenta como `ComboThreeSixes` contra un enemigo débil al fuego), el `EffectData` lo autorea de forma explícita: el `EffDealDamage` lleva `Combo = comboReference` en su config y el pipeline detecta la weakness normal. El default es nunca — "gratis" no hay.

```csharp
public enum AttackKind
{
    ComboAttack,        // jugador o enemigo con combo resuelto
    BasicAttack,        // ataque sin combo (dado más alto — GD §5)
    DamageOverTime,     // status tick — no weakness por default
    Environmental,      // trampa, lava, lluvia de flechas
    Reaction,           // thorns, contragolpe — ver 12.2.2
    ScriptedAbility     // habilidad de pasiva que hace daño sin combo
}
```

#### 12.2.2 Reactions y post‑mortem — orden vs `CheckCombatEnd`

El state `ResolveReactions` del FSM (§1.3) corre **antes** de `CheckCombatEnd`. Consecuencia buscada: si un ataque es letal contra el target, sus behaviors reactivos (`OnDamaged` con `WasLethal = true`) disparan **antes** de que el FSM evalúe victoria/derrota. Esto permite que un thorns on‑death alcance a golpear al attacker aunque el target muera en ese mismo hit.

**Secuencia exacta para un ataque letal con thorns:**

```
ApplyDamage(player → enemy)
  └→ DamagePipeline.Resolve → enemy.HP llega a 0
     EventManager.Trigger(OnDamageResolved, player, enemy, dmg, weakness)
ResolveReactions
  └→ Dispatch OnDamaged sobre enemy → thorns behavior dispara
     └→ EffDealDamage(enemy, player, thornsDmg) con Kind = Reaction
        └→ DamagePipeline.Resolve(enemy, player, thornsDmg, ctx)
           ├→ Si player muere acá: push a stack de reacciones
           ├→ Si no: commit daño
           └→ EventManager.Trigger(OnDamageResolved, enemy, player, thornsDmg)
     └→ (recursivo) ResolveReactions sobre el player si corresponde
CheckCombatEnd
  └→ Detecta enemy con HP = 0 → sigue
  └→ Si queda algún enemigo vivo: ongoing, vuelve al caller state
  └→ Si no: Victory
```

**Cap de recursión.** `ResolveReactions` tiene un `StackDepthLimit` (default: 8) para evitar loops patológicos de thorns → contra‑thorns → contra‑contra‑thorns. Cuando se alcanza el cap, los reactivos restantes se descartan con un `Debug.LogWarning` en editor y el FSM continúa. Ningún combate real debería acercarse a 8 niveles, el cap es solo defensivo.

**Un enemigo muerto no actúa más.** `EnemyPickNext` (§1.3) verifica `enemy.Attributes.Get<Health>() > 0` antes de darle el turno. Un enemigo que murió mid‑turno por un reactivo se descarta y el FSM avanza al siguiente. Similar para reactivos: un thorns sobre un entity ya muerto no re‑entra (el dispatcher de `OnDamaged` chequea vivo).

**Daño post‑mortem explícito.** Si un diseñador quiere que una habilidad siga ejecutándose aunque su caster muera en el mismo frame (p. ej. "explota al morir"), el behavior debe tener `Trigger = OnDamaged` con condición `WasLethal = true` — el thorns pattern es el molde. No hay camino "ignorar muerte" fuera de este patrón.

### 12.3 Flujo completo de un ataque de jugador

```
1. El `CombatTurnFSM` está en el state `Roll` para el jugador (§1.3).
2. DiceRoller.RollAll(player.DiceBag) → int[] raw
3. (opcional) hasta 2 Reroll(...) + energía extra para 1 reroll bonus
4. Aplicar DiceEnchantment.TransformRoll por dado                          (§6.4)
5. Aplicar OnRollEffects de encantamientos (veneno, fuego, slow)            (§8)
6. AttackResolver.ResolveAttack:
     a. sheet.EvaluateRoll(finalDice) → MatchedCombo | null                (§5.3)
     b. rawDamage = MatchedCombo?.BaseDamage ?? finalDice.Max()            (GD: mínimo = dado más alto)
     c. ctx.IsWeaknessHit = (target.WeakAgainst == MatchedCombo)            (§13.4)
     d. finalDamage = DamagePipeline.Resolve(player, target, rawDamage, ctx)   (§12.2)
7. Stage FloatingNumberBehaviorValue en el behavior de la acción           (§9)
8. FeedbackBus dispara secuencia de anim/VFX                               (§10)
9. EventManager.Trigger(OnComboMatched) si matchea → dispara pasivas
10. Al fin del turno: EventManager.Trigger(OnTurnFinished, player.InstanceId)
    → modificadores con duration decrementan
11. SaveSystem.CaptureAll(); SaveSystem.Flush(SaveTrigger.RoomEnd) si es fin de sala   (§15)
```

### 12.4 Defensa (sistema de tiradas restantes del GD)

Si el jugador tiene re‑rolls sobrantes tras su ataque, puede reservarlos para anotar un combo de **defensa**. El combo se traduce en `Shield` temporal sobre el jugador.

```csharp
public interface IDefenseResolver
{
    int ResolveDefense(int[] finalDice, ContractSheet sheet);
    // Mismo matching que ataque. El valor se suma al Shield actual del jugador.
}
```

`Shield` es un `IModifiable<int>` con `EventName = OnTurnStarted` como tick de limpieza (se resetea a 0 al comenzar el siguiente turno del jugador).

### 12.5 Acciones secundarias (1 tirada vs umbral)

Del GD: curarse, forzar puerta, abrir cofre resuelven con **1 sola tirada vs umbral** (no 3 tiradas Generala).

```csharp
public interface ISecondaryRollResolver
{
    bool ResolveThreshold(int[] singleRoll, int threshold);
    // Suma los 5 dados, compara contra umbral, devuelve éxito/fallo.
}
```

Los umbrales se definen en el `RoomSO` (cofre/puerta) o en el SO del ítem (poción).

### 12.6 Action economy y repetition constraint

Regla del GDD: "**no se puede repetir la misma acción en el mismo turno**". Es un constraint del action economy, independiente de qué acciones existan o cuánto cuesten.

#### 12.6.0 `ActionCatalogSO` — catálogo maestro unificado

**Regla transversal.** El juego tiene **una única fuente de verdad** para "cosa que un actor puede ejecutar en combate" — el `ActionCatalogSO`. Todo lo que antes vivía como catálogo independiente (combos, ataques, abilities, items activos, movement, etc.) referencia un `ActionId` en este catálogo. Elimina la proliferación de IDs (`ComboId`, `AttackId`, `AbilityId`) que apuntaban cada uno a su catálogo — cuatro catálogos paralelos era un ID hell.

```csharp
public enum ActionType
{
    Combo,         // combo del Contrato de Generala (§5) — wrapped por ActionDefinitionSO
    Attack,        // ataque directo sin combo (dado más alto del GD, AI nodes)
    Ability,       // habilidad única de clase/enemigo con efecto autoreo (§8)
    Defend,        // §12.4
    Move,          // §B
    Interact,      // §7.7
    UseItem,       // item activo (§18)
    SkillCheck     // §12.5
}
```

**`ActionDefinitionSO` ahora lleva `ActionType`:**

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Actions/Action Definition")]
public class ActionDefinitionSO : SerializedScriptableObject
{
    [Title("Identity")]
    [ValueDropdown(nameof(GetActionIds))]
    public string ActionId;                      // "combo.full_house", "attack.basic", "ability.berserker.frenzy", "item.potion.heal_small", …
    public ActionType Type;
    public string DisplayName;

    [Title("Compat — referencia del SO específico (opcional)")]
    [InfoBox("Para Type = Combo → referenciar el BaseComboSO. Para Type = UseItem → el ItemSO. " +
             "Para Attack/Ability puro, vive el effect abajo y este campo queda null.")]
    [OdinSerialize] public ScriptableObject BackingAsset;   // BaseComboSO | ItemSO | null

    [Title("Cost")]
    [MinValue(0)] public int EnergyCost;

    [Title("Repetition")]
    [ToggleLeft]
    [InfoBox("Si true, esta ActionId no puede ejecutarse dos veces en el mismo turno. Default del GDD.")]
    public bool BlockOnRepeat = true;

    [Title("Reroll")]
    [ToggleLeft] public bool AllowsEnergyReroll = true;

    [Title("Effect (si no hay BackingAsset)")]
    [OdinSerialize] public EffectData Effect = new();   // §8

#if UNITY_EDITOR
    private static IEnumerable<string> GetActionIds() =>
        ServiceLocator.TryGetService<ActionCatalogSO>(out var cat) ? cat.AllIds : Array.Empty<string>();
#endif
}
```

**Convención de naming de `ActionId`.** `<tipo>.<subtipo>.<nombre>`. Ejemplos:
- `combo.full_house`, `combo.generala`
- `attack.basic`, `attack.ranged`
- `ability.berserker.frenzy`, `ability.necromancer.raise`
- `item.potion.heal_small`, `item.bomb.poison`
- `move`, `defend`, `interact`, `skill.heal`, `skill.force_door`

Los catálogos específicos (`ComboCatalogSO`, `ItemCatalogSO`) siguen existiendo para el lookup tipado en su dominio — un `ContractSheet` sigue teniendo `List<BaseComboSO>`, no `List<string>`. Pero en el punto del combate donde se decide "¿ejecuto esto?", todo es `ActionId` vs `ActionCatalogSO`.

**Catálogo:**

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Actions/Action Catalog")]
public class ActionCatalogSO : SerializedScriptableObject
{
    [OdinSerialize] private List<ActionDefinitionSO> _entries = new();

    public IEnumerable<string> AllIds => _entries.Select(e => e.ActionId);
    public IEnumerable<string> GetIdsByType(ActionType type) =>
        _entries.Where(e => e.Type == type).Select(e => e.ActionId);

    public ActionDefinitionSO GetById(string id) =>
        _entries.FirstOrDefault(e => e.ActionId == id);

    public T GetBackingAsset<T>(string id) where T : ScriptableObject =>
        GetById(id)?.BackingAsset as T;        // ej: catalog.GetBackingAsset<BaseComboSO>("combo.full_house")
}
```

**Enforcement** — el `TurnManager` mantiene el set de acciones usadas en el turno actual, tagueadas por `ActionId`:

```csharp
public class TurnManager
{
    private readonly HashSet<string> _actionsUsedThisTurn = new();

    public bool CanExecute(ActionDefinitionSO action, Guid playerGuid, out string reason)
    {
        reason = null;

        if (action.BlockOnRepeat && _actionsUsedThisTurn.Contains(action.ActionId))
        {
            reason = $"Action '{action.ActionId}' already used this turn.";
            return false;
        }

        var energy = AttributesManager.GetAttributeModifiedValue<Energy, int>(playerGuid);
        if (energy < action.EnergyCost)
        {
            reason = $"Not enough energy ({energy}/{action.EnergyCost}).";
            return false;
        }

        return true;
    }

    public bool TryExecute(ActionDefinitionSO action, Guid playerGuid, EffectContext ctx)
    {
        if (!CanExecute(action, playerGuid, out _)) return false;

        // Cobrar energía antes de ejecutar.
        AttributesManager.Modify<Energy, int>(playerGuid, e => e - action.EnergyCost);

        if (!action.Effect.TryExecute(ctx, ctx.BuildPreConditionContext())) return false;

        _actionsUsedThisTurn.Add(action.ActionId);
        return true;
    }

    private void OnTurnStarted(params object[] args) => _actionsUsedThisTurn.Clear();
}
```

**Consecuencias del diseño**:

- **Movement** y **defense** son acciones normales con su propio `ActionDefinitionSO`. Movement suele setear `BlockOnRepeat = false` para que el jugador pueda moverse varias veces si tiene energía (única manera de escapar según el GDD).
- **Skill checks** (§12.5) también son `ActionDefinitionSO` — la diferencia con un ataque es el efecto que resuelven, no la forma.
- El constraint es **opt‑in por acción** via el flag, no una regla global. Si algún modo de juego quiere permitir repeticiones, un `RulesetSO` (§14.7) puede overridear el flag por `ActionId` via `ForbiddenActionIds`.
- El `EnergyCost` y el `AllowsEnergyReroll` viven en el SO, no en el código — cualquier ajuste de balance es data, no rebuild.

### 12.7 Turn order con velocidad oculta

Regla del GDD: el orden del turno dentro de un round se decide por una **velocidad oculta** de cada entidad — el jugador no la ve como número, solo ve **el orden** (via HUD de turn queue). El jugador arranca con su propia `Speed`, los enemigos tienen la suya, y en cada combate se compone un orden.

**Atributo** — `Speed` ya está declarado en §4.2 como stat del héroe. Importante: se marca como **hidden** en el UI contract, así el layer de HUD (§D) lo skipea en cualquier render de stats.

**Servicio**:

```csharp
public interface IInitiativeProvider
{
    /// <summary>Devuelve un valor de initiative para ordenar el round. Mayor = antes.</summary>
    int RollInitiative(Guid entityGuid);
}

/// <summary>
/// Implementación default: toma el Speed del target, le suma una tirada
/// interna (speed die) cuyos min/max salen del RulesetSO, y devuelve la suma.
/// Es reemplazable — cualquier estrategia que quiera una regla distinta
/// implementa IInitiativeProvider y se registra en el bootstrap.
/// </summary>
public class DefaultInitiativeProvider : IInitiativeProvider { … }

public class TurnOrderService
{
    private readonly List<Guid> _orderForRound = new();
    private int _cursor;

    public IReadOnlyList<Guid> OrderForRound => _orderForRound;
    public Guid Current => _orderForRound[_cursor];

    public void BuildForCombat(IEnumerable<Guid> participants)
    {
        var provider = ServiceLocator.GetService<IInitiativeProvider>();
        var rolls = participants.Select(g => (guid: g, init: provider.RollInitiative(g)));
        _orderForRound.Clear();
        _orderForRound.AddRange(rolls.OrderByDescending(x => x.init).Select(x => x.guid));
        _cursor = 0;

        EventManager.Trigger(EventName.OnTurnQueueBuilt, _orderForRound.ToList(), /*roundIndex*/ 0);
    }

    public void Advance()
    {
        _cursor = (_cursor + 1) % _orderForRound.Count;
        if (_cursor == 0)
            EventManager.Trigger(EventName.OnTurnQueueBuilt, _orderForRound.ToList(), /*nextRound*/ -1);
    }
}
```

**Regla de UI** — el HUD (§D) se suscribe a `OnTurnQueueBuilt` y renderiza el orden como una cola de íconos/portraits, **nunca** como números. Ningún listener debe leer directamente el `Speed` para exponerlo al jugador. Esto deja el stat libre de "contaminación de UI" y permite que las pasivas que lo modifican sigan siendo numéricas sin romper la regla de diseño.

**Interacción con modificadores**:
- Un `Modifier<int>` sobre `Speed` con `Direction = Intrinsic` cambia el valor base — el próximo `BuildForCombat` usa el nuevo valor.
- Un `Modifier<int>` con `Lifetime = Encounter` sobre `Speed` solo afecta el combate actual.
- El encantamiento "Hielo d6" del GDD (ralentiza al enemigo que sacó un 6) aplica un `Modifier<int>` negativo con `Duration = 1` y `TickEvent = OnTurnFinished`.

**Cross‑ref.** §2 (atributos — `Speed` declarado acá), §3 (modificadores de Speed), §12.2 (DamagePipeline), §D (UI de turn queue), §14.7 (RulesetSO define los rangos del speed die).

**Cross‑ref §12.** §3 (modifiers direccionales), §4 (stats del héroe incluyen Incoming/Outgoing multipliers), §5 (EvaluateRoll), §6 (DiceRoller + Encantamientos), §6.5 (reroll budget), §10 (feedback), §13.4 (weakness).

---

## 13. Dungeon, Salas y Generación Procedural

> Inspirado en Isaac. Las salas son **preseteadas por el diseñador como prefabs**; la generación del piso es procedural sobre un pool de salas disponibles.

### 13.1 Principios

1. **Diseñador arma salas como prefabs** — layout de obstáculos, spawn points, puertas, puntos de recompensa.
2. **Pools por tipo de sala** — Combat, Boss, Shop, Potion. Cada pool es una lista en `FloorLayoutSO`.
3. **Generación procedural del piso** — elige N salas aleatorias del pool (`N` ∈ `[min, max]`), las conecta tipo grilla Isaac, asegura 1 boss + salas especiales obligatorias.
4. **Spawns por pool** — enemigos, objetos, cofres, puertas se resuelven en runtime tomando entidades aleatorias de pools asociados a la sala.
5. **Setups predefinidos o aleatorios** — una sala puede tener setups fijos ("2 orcs arriba, 2 arqueros abajo") o dejar que el manager asigne enemigos aleatorios del pool a los spawn points del prefab.
6. **Floor con rango de salas** — el número de salas del piso es un aleatorio entre `RoomCountMin` y `RoomCountMax`, no un valor fijo.
7. **Boss aleatorio por piso** — la lista de bosses posibles es una colección; el `DungeonManager` elige uno al generar el piso.

### 13.2 `RoomSO`

Plantilla de sala. Referencia al prefab + metadata + pools asociados.

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Dungeon/Room")]
public class RoomSO : SerializedScriptableObject
{
    [Title("Identity")]
    [ValueDropdown(nameof(GetRoomIds))]
    public string RoomId;
    public string DisplayName;
    public RoomType Type;

    [Title("Prefab")]
    [InfoBox("El prefab debe contener un componente RoomLayout con spawn points marcados.")]
    public GameObject RoomPrefab;
    public Vector2Int GridSize;

    [Title("Enemies")]
    [InfoBox("Si hay Setups fijos, se elige uno al azar. Si no hay, se puebla con EnemyPool sobre los spawn points del prefab.")]
    public List<EnemySetupSO> PossibleSetups = new();           // setups curados — opcional
    public EnemyPoolSO EnemyPool;                                // fallback procedural

    [Title("Rewards / Objects")]
    public RewardPoolSO RewardPool;                              // cofres, items, shops
    public ObstaclePoolSO ObstaclePool;                          // obstáculos decorativos/bloqueantes
    public DoorConfigSO Doors;                                    // puertas posibles (N/S/E/W, configurable)

#if UNITY_EDITOR
    private static IEnumerable<string> GetRoomIds() =>
        ServiceLocator.TryGetService<RoomCatalogSO>(out var cat) ? cat.AllIds : Array.Empty<string>();
#endif
}

public enum RoomType { Start, Combat, Boss, Shop, Potion }
```

### 13.3 Prefab de sala — componentes esperados

El prefab referenciado en `RoomSO.RoomPrefab` debe tener:

```csharp
public class RoomLayout : MonoBehaviour
{
    [Title("Spawn Points")]
    public List<Transform> EnemySpawnPoints;
    public List<Transform> RewardSpawnPoints;
    public List<Transform> ObstacleSpawnPoints;

    [Title("Doors")]
    public List<DoorSlot> DoorSlots;             // N/S/E/W con transform + estado

    [Title("Nav")]
    public GridSnapshot GridOverride;             // tamaño grilla + celdas bloqueadas

    [Title("Bounds")]
    [InfoBox("Bounding box local del layout. Se usa para la vista de piso de la cámara (§E.9). " +
             "Se recomputa en OnValidate a partir de los renderers children; el diseñador puede " +
             "override manualmente si la sala tiene elementos no‑visuales que deberían contar.")]
    public Bounds Bounds;

#if UNITY_EDITOR
    private void OnValidate()
    {
        var renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
        if (renderers.Length == 0) return;

        var b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

        // Local space — restamos la posición del transform del layout
        b.center -= transform.position;
        Bounds = b;
    }
#endif
}
```

**Responsabilidades:**

- Los spawn points son `Transform` vacíos ubicados por el diseñador en el editor. El `DungeonManager` los consume cuando instancia la sala.
- `DoorSlot` describe dónde están las puertas posibles y cuáles abrir según las conexiones del layout.
- `GridSnapshot` parametriza la grilla isométrica para `GridManager` (§11.3).
- `Bounds` alimenta dos consumidores del camera service (§17.E): (1) las shells procedurales del floor view (§E.9) — se lee desde el prefab asset sin instanciarlo, (2) el clamp del pan al área total del piso, que `DungeonManager` agrega sumando los bounds de cada sala en su `WorldPosition`.

### 13.4 Pools

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Dungeon/Enemy Pool")]
public class EnemyPoolSO : ScriptableObject
{
    public List<WeightedEnemy> Entries = new();

    /// <summary>Selecciona N enemigos pesados por weight para los spawn points.</summary>
    public List<EnemyDataSO> RollForSpawns(int count, Random rng);
}

[Serializable]
public struct WeightedEnemy
{
    public EnemyDataSO Enemy;
    [MinValue(0)] public float Weight;
}

[CreateAssetMenu(menuName = "Rollgeon/Dungeon/Enemy Setup")]
public class EnemySetupSO : ScriptableObject
{
    public string SetupName;
    [InfoBox("Asignación fija enemigo → índice del spawn point del prefab.")]
    public List<SetupSlot> Slots = new();
}

[Serializable]
public struct SetupSlot
{
    public int SpawnPointIndex;
    public EnemyDataSO Enemy;
}

[CreateAssetMenu(menuName = "Rollgeon/Dungeon/Prop Pool")]
public class PropPoolSO : ScriptableObject
{
    public List<WeightedProp> Entries = new();

    /// <summary>Resuelve un prop por spawn point (cofre/puerta/poción/trap).</summary>
    public PropEntitySO RollForSpawn(Random rng);
}

[Serializable]
public struct WeightedProp
{
    public PropEntitySO Prop;
    [MinValue(0)] public float Weight;
}
```

Análogos para recompensas y obstáculos:

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Dungeon/Reward Pool")]
public class RewardPoolSO : ScriptableObject
{
    public List<WeightedReward> Entries = new();
    public RewardEntrySO RollForChest(Random rng);
    // RollForShop eliminado — los ítems de shop se rollan desde ShopPoolSO (§17.F)
}

[CreateAssetMenu(menuName = "Rollgeon/Dungeon/Obstacle Pool")]
public class ObstaclePoolSO : ScriptableObject
{
    public List<GameObject> ObstaclePrefabs = new();
    public GameObject RollObstacle(Random rng);
}
```

**Regla Isaac‑style.** Cuando una sala tiene N `RewardSpawnPoints` y una `RewardPoolSO`, el manager hace `pool.RollForChest(rng)` por cada spawn point → cada cofre / ítem de tienda se resuelve con un roll independiente del pool. Mismo criterio para enemigos en salas sin setups fijos.

### 13.5 `FloorLayoutSO`

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Dungeon/Floor Layout")]
public class FloorLayoutSO : SerializedScriptableObject
{
    [Title("Identity")]
    public string FloorName;
    public string Theme;                         // visual theme — consumido por arte

    [Title("Room Count")]
    [InfoBox("El número de salas del piso se elige aleatorio en [Min, Max].")]
    [MinValue(1)] public int RoomCountMin = 8;
    [MinValue(1)] public int RoomCountMax = 14;

    [Title("Room Pool")]
    [InfoBox("Pool de salas de combate disponibles para este piso.")]
    public List<RoomSO> CombatRooms = new();

    [Title("Special Rooms (mandatory)")]
    [InfoBox("Salas especiales que deben aparecer en el piso. Mínimo 1 de cada según el GD.")]
    public List<RoomSO> ShopRooms   = new();
    public List<RoomSO> PotionRooms = new();
    [MinValue(0)] public int ShopCount   = 1;
    [MinValue(0)] public int PotionCount = 1;

    [Title("Boss")]
    [InfoBox("Lista de bosses posibles. Se elige uno aleatorio por piso.")]
    public List<RoomSO> BossRooms = new();

    [Title("Notes")]
    [InfoBox("Craps ya no tiene sala dedicada — las mesas de craps se spawnean como props en " +
             "salas de combate vía PropPoolSO (§17.C). Sacrifice removido — no definido en GDD.")]
}
```

### 13.6 `DungeonManager`

Servicio registrado en `ServiceLocator`. Responsable de armar el piso y gestionar las transiciones entre salas.

```csharp
public interface IDungeonManager
{
    GeneratedFloor GenerateFloor(FloorLayoutSO layout, int seed);
    RoomInstance EnterRoom(string roomId);
    void ExitCurrentRoom();
    MinimapState GetMinimapState();

    /// <summary>
    /// Bounds agregados del piso en world space — union de los Bounds de cada
    /// RoomLayout trasladados a su WorldPosition. Consumido por el camera
    /// service (§17.E.6) para clampear el pan al área del piso.
    /// </summary>
    Bounds GetFloorBounds();
}

public class GeneratedFloor
{
    public List<RoomInstance> Rooms;       // N salas conectadas tipo Isaac
    public RoomInstance StartRoom;
    public RoomInstance BossRoom;

    /// <summary>
    /// Shell procedural por sala — generada en GenerateFloor a partir del
    /// Bounds del RoomLayout (§13.3) del prefab. Usada por el camera service
    /// para la vista de piso al zoomear afuera (§17.E.9). Cada shell es un
    /// GameObject con un cube mesh escalado al bounds; default hidden.
    /// </summary>
    public Dictionary<Guid, GameObject> FloorShells = new();
}

public class RoomInstance : ISaveable
{
    public Guid InstanceId;
    public RoomSO Template;
    public GameObject SpawnedPrefab;        // instanciado al EnterRoom
    public Vector3 WorldPosition;           // resuelto al GenerateFloor por la topología
    public List<Entity> Enemies;           // spawneadas al entrar
    public List<Entity> Rewards;
    public RoomState State;                 // Uncleared | Cleared | Locked

    // Runtime state granular indexado por spawn point. Cada spawn point del
    // RoomPrefab declara un SpawnPointId (string via ValueDropdown), y el
    // DungeonManager inicializa una entry tipada al entrar por primera vez.
    //
    // [SerializeReference] — CRÍTICO: sin este atributo, Odin/Unity serializan
    // cada value del diccionario al tipo base RoomObjectState y pierden los
    // campos de los subtipos (ChestState.Opened, ShopItemState.Purchased, …)
    // al persistir/restaurar. Con [SerializeReference], cada entry mantiene su
    // tipo concreto por polimorfismo. Ver §13.6.1 abajo.
    [SerializeReference]
    public Dictionary<string, RoomObjectState> ObjectStates = new();

    public string SaveKey => $"room.{InstanceId}";
    public object CaptureState() { /* serializa State + ObjectStates + Enemies vivos */ }
    public void RestoreState(object state) { /* idem inverso */ }
}
```

**State granular por objeto de sala** — cada spawn point del prefab puede albergar un objeto consumible (cofre, poción, ítem de shop, puerta forzable). El estado de cada uno vive en el `ObjectStates` de la `RoomInstance` para que al salir y volver a entrar el jugador vea la sala como la dejó (cofre ya abierto → no reaparece, ítem comprado → no reaparece, etc.).

```csharp
[Serializable]
public abstract class RoomObjectState
{
    public string SpawnPointId;
    public bool Consumed;     // flag común: una vez true, el objeto no re-materializa al re-entrar
}

[Serializable] public class ChestState : RoomObjectState
{
    public bool Opened;
    public List<string> LootRolled;   // ids de loot ya ruleados — evita re-roll al reentrar
}

[Serializable] public class PotionState : RoomObjectState
{
    public bool Collected;            // la sala Potion recarga la poción; idempotente una vez tomada
}

[Serializable] public class ShopItemState : RoomObjectState
{
    public bool Purchased;
    public string ReservedItemId;     // qué ítem específico ocupa este slot (rolled al generar la sala)
    public int ReservedPrice;         // precio ya calculado — no se re-rolea al reentrar
}

[Serializable] public class DoorState : RoomObjectState
{
    public bool Forced;               // resultado de un skill check exitoso
    public bool Unlocked;
}

[Serializable] public class EnemySpawnState : RoomObjectState
{
    public string EntityId;
    public int CurrentHP;
    public bool IsDead;               // muertos no re-aparecen (GD)
}
```

#### 13.6.1 Regla de serialización polimórfica — `[SerializeReference]` + `[Serializable]`

**Invariante.** `RoomInstance.ObjectStates : Dictionary<string, RoomObjectState>` es polimórfico por diseño — cada value puede ser cualquier subtipo (`ChestState`, `ShopItemState`, …). Para que Unity/Odin preserven el **tipo concreto** al persistir/restaurar, se cumplen **tres** condiciones simultáneas:

1. **Clase base `RoomObjectState` abstract.** Si fuera concreta, Unity degrada a la base y pierde campos en serialización default.
2. **Cada subtipo marcado `[Serializable]`.** Requerido por el serializer tanto de Unity nativo como de Odin. Las clases sin `[Serializable]` no son instanciables por el deserializer polimórfico.
3. **El campo contenedor lleva `[SerializeReference]`.** Este atributo activa el serializer polimórfico de Unity, que graba el nombre del tipo concreto junto al payload. Al deserializar, el reverse lookup restaura la instancia correcta. **Sin `[SerializeReference]`**, Unity serializa al tipo declarado del campo (`RoomObjectState`) y descarta los campos de los subtipos — un bug **silencioso** (ni warning, ni exception, solo valores perdidos al reload).

**Por qué importa.** Sin esta regla, el flujo "entrar a sala → abrir cofre → salir → volver a entrar" aparenta funcionar dentro de la misma sesión (la instancia runtime tiene el `ChestState.Opened = true`), pero al **cerrar y reabrir el juego**, el save system (§15) re-instancia `RoomObjectState` y todos los campos específicos del subtipo vuelven a `default`. Cofre "se reabre", ítem "reaparece", enemigo muerto "revive". Es el peor tipo de bug porque el testeo en‑engine pasa y solo se revela en save/load cross‑session.

**Testing recomendado.** En la suite de save (§15 / §24 si aplica):
```csharp
[Test]
public void RoomObjectState_Polymorphism_SurvivesRoundTrip()
{
    var instance = new RoomInstance();
    instance.ObjectStates["sp_chest_1"] = new ChestState {
        SpawnPointId = "sp_chest_1", Consumed = true, Opened = true,
        LootRolled = new List<string> { "reward.potion_small" }
    };

    var snapshot = instance.CaptureState();
    var restored = new RoomInstance();
    restored.RestoreState(snapshot);

    Assert.IsInstanceOf<ChestState>(restored.ObjectStates["sp_chest_1"]);
    Assert.IsTrue(((ChestState)restored.ObjectStates["sp_chest_1"]).Opened);
}
```

**Migración de campos agregados.** Si un subtipo agrega un campo en un patch (p. ej. `ChestState.LootTier : int` v1.2), los saves antiguos tendrán `default(int) = 0`. Regla: los campos nuevos deben tolerar `default` como valor legítimo, o la clase implementa una override simple en `RestoreState` que completa el campo a partir de otros (p. ej. derivar `LootTier` del `RoomSO` correspondiente). No usar Unity `ISerializationCallbackReceiver` para esto — es un pattern frágil con Odin. Ver §15 para versioning general de saves.

**Anti‑patrón.** Usar `[SerializeField]` en lugar de `[SerializeReference]`: `[SerializeField]` es value‑type serialization y **no** soporta polimorfismo. El doc lo aclara porque es el error más común cuando alguien "tipea rápido" el atributo.

**Lifecycle**:

1. `DungeonManager.EnterRoom(roomId)` — si es la primera vez, recorre los spawn points del prefab (`RoomLayout` component del §13.3) y por cada uno crea el `RoomObjectState` del subtipo correcto:
   ```csharp
   foreach (var sp in roomLayout.SpawnPoints)
       instance.ObjectStates[sp.Id] = sp.CreateInitialState();
   ```
2. Al ejecutar un efecto sobre el objeto (ej: `EffOpenChest`), el efecto resuelve el spawn point, busca la entry en `ObjectStates` y la muta (`ChestState.Opened = true`, agrega loot a `LootRolled`).
3. `DungeonManager.ExitCurrentRoom()` — el estado se queda donde está. La `RoomInstance` completa es `ISaveable`, así el save system (§15) captura `ObjectStates` en el próximo `Flush` sin intervención adicional.
4. `DungeonManager.EnterRoom(roomId)` cuando ya existe la instance — el manager no re-inicializa. Los spawn points consultan su `RoomObjectState` y deciden si materializar el objeto:
   - `ChestState.Opened == true` → no spawnear el prop del cofre.
   - `ShopItemState.Purchased == true` → no spawnear el item en el piso.
   - `EnemySpawnState.IsDead == true` → no spawnear el enemigo.
   - `EnemySpawnState.CurrentHP < max` → spawnear el enemigo con ese HP en posición aleatoria de los spawn points libres (GD: "enemigos vivos reaparecen con su HP").

**Por qué un bag tipado y no un flag global `Cleared`**:
- `RoomState.Cleared` solo dice "todos los enemigos murieron". No sabe si el jugador abrió el cofre, si compró el ítem, si forzó la puerta.
- El bag tipado cubre cada consumible por separado, se extiende agregando una nueva subclase de `RoomObjectState`, y no rompe código existente.
- El save system lo captura gratis porque `RoomInstance` es `ISaveable` — cada nuevo subtipo se serializa polimorphicamente vía Odin `SerializationUtility`.

**Responsabilidades del `DungeonManager`:**

- Elige `RoomCount = rng.Range(layout.RoomCountMin, layout.RoomCountMax + 1)`.
- Construye la topología de la grilla tipo Isaac (algoritmo greedy: start en centro, camina aleatorio, asegura conectividad, ubica boss al final del path más largo).
- Por cada slot de sala: elige un `RoomSO` del pool correspondiente (`CombatRooms`, `ShopRooms`, `BossRooms`, …) y resuelve su `WorldPosition` en la topología (offset por grid cell).
- **Boss**: `layout.BossRooms[rng.Range(0, layout.BossRooms.Count)]` → una lista permite múltiples variantes de boss por piso.
- **Generación de shells** para el camera floor view (§17.E.9). Por cada `RoomInstance` resuelta:
  ```csharp
  var layoutComponent = roomInstance.Template.RoomPrefab.GetComponent<RoomLayout>();
  var bounds = layoutComponent.Bounds;          // §13.3 — local, recomputado en OnValidate
  var shell  = CreateShellBox(bounds, cameraConfig.ShellColor);
  shell.transform.position = roomInstance.WorldPosition + bounds.center;
  shell.transform.localScale = bounds.size;
  shell.SetActive(false);                        // default hidden — el camera service los muestra al cruzar el zoom threshold
  floor.FloorShells[roomInstance.InstanceId] = shell;
  ```
  `CreateShellBox` arma un `GameObject` runtime con un cube `MeshFilter` + `MeshRenderer` con material transparente. Cero asset manual por sala. `cameraConfig` se resuelve vía `ServiceLocator.GetService<CameraConfigSO>()`.
- **`GetFloorBounds()`** — devuelve la unión de `RoomLayout.Bounds` trasladados a `RoomInstance.WorldPosition`. Consumido por el camera service para clampear el pan (§17.E.6).
- Al entrar a una sala por primera vez: instancia el `RoomPrefab`, elige setup de enemigos (fijo o procedural), rolea rewards por cada spawn point, spawnea obstáculos del pool, inicializa `ObjectStates` por spawn point. Notifica al `ICameraService.SetFollowTarget(player)` (§17.E).
- **Interactables de la sala.** Cada `InteractableComponent` (§7.7) hijo del `RoomPrefab` resuelve su `OwnerData` / `OwnerEntity` / `SpawnPointId` en `Awake` contra el `DungeonManager` del `ServiceLocator` y se auto‑registra en `IInteractionService` vía `OnEnable`. Al `ExitCurrentRoom()` el manager desactiva (o destruye) el prefab de la sala, lo que dispara `OnDisable` en los components y los desregistra. El service siempre refleja sólo los interactables de la sala actualmente activa.
- **Consumed bloquea prompts.** Los subtipos de `RoomObjectState` con semántica de consumible (`ChestState.Opened`, `ShopItemState.Purchased`, `PotionState.Collected`, …) son respetados por `IInteractionService`: antes de ofrecer el `CurrentTarget`, el service consulta el `RoomObjectState` del `SpawnPointId` del candidate y, si `Consumed == true`, lo descarta. Esta regla es **ortogonal** a las `PhaseRules` — un objeto consumido no aparece como target en ninguna fase, porque físicamente ya no está.
- Al re‑entrar: consulta `ObjectStates` y materializa solo lo que queda pendiente. Enemigos, loot rolled, precios de shop y puertas forzadas se persisten automáticamente via §15.

### 13.7 `MinimapIconsSO`

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Dungeon/Minimap Icons")]
public class MinimapIconsSO : ScriptableObject
{
    [InfoBox("Lista de salas especiales con ícono o texto fallback.")]
    public List<MinimapEntry> Entries = new();

    public bool TryGetEntry(RoomType type, out MinimapEntry entry);
}

[Serializable]
public class MinimapEntry
{
    public RoomType Type;
    [InfoBox("Prioriza el ícono. El fallback text sólo se usa si Icon es null.")]
    public Sprite Icon;
    public string FallbackText;              // "T", "B", "P" — legacy / placeholder
}
```

Pre‑cargado desde el bootstrap (§1.1.1). El minimap HUD consulta `minimapIcons.TryGetEntry(room.Type, out entry)`:

- Si `entry.Icon != null` → renderiza el sprite.
- Si no → renderiza `entry.FallbackText` como `TMP_Text`.

**Reglas del GD:**

- Minimap siempre visible en HUD.
- Sólo salas especiales muestran etiqueta/ícono. Las de combate aparecen como tiles en blanco.
- Adyacentes no descubiertas aparecen como contornos.
- Puertas se muestran como aberturas entre tiles.

### 13.8 Persistencia de enemigos entre salas

Del GD: enemigos vivos reaparecen con la HP que tenían al salir, posición aleatoria; muertos no reaparecen.

Implementación: cada `Enemy` implementa `ISaveable` (§15). Al `ExitCurrentRoom`, `SaveSystem.CaptureAll()` captura el estado (HP, guid, template). Al `EnterRoom` otra vez, los enemigos vivos se re‑instancian desde el estado cacheado, con posición aleatoria sobre los spawn points libres.

**Cross‑ref.** §7.1 (`EnemyDataSO`), §7.1b (`PropEntitySO`), §7.6 (`NpcDataSO`), §12 (combate en sala), §14 (rewards desbloquean items), §15 (save system).

---

## 14. Meta‑progresión y Unlocks

> Sistema de unlocks basado en abstracción `BaseUnlockSO` con subtipos concretos por categoría. Cada unlock tiene una lista de condiciones (AND por defecto) configurable desde el editor.

### 14.1 `BaseUnlockSO`

```csharp
public abstract class BaseUnlockSO : SerializedScriptableObject
{
    [Title("Identity")]
    [ValueDropdown(nameof(GetUnlockIds))]
    public string UnlockId;
    public string DisplayName;
    [TextArea] public string Description;
    public Sprite Icon;

    [Title("Conditions")]
    [InfoBox("Todas las condiciones deben cumplirse (AND). Para disyunciones usar UC_Or.")]
    [ListDrawerSettings(ShowFoldout = false, DraggableItems = true)]
    [OdinSerialize]
    public List<UnlockConditionSO> Conditions = new();

    [Title("Display")]
    [InfoBox("Si está locked y se spoilea → muestra Icon + Description. Si no, silhouette.")]
    public bool SpoilerVisible = true;

    /// <summary>
    /// Evalúa todas las condiciones contra un RunRecord. AND por defecto.
    /// Lista vacía = condición trivial (siempre true).
    /// </summary>
    public virtual bool Evaluate(RunRecord record) =>
        Conditions.Count == 0 || Conditions.All(c => c != null && c.Evaluate(record));

    /// <summary>Llamado cuando se desbloquea. Override en el subtipo para side‑effects.</summary>
    public abstract void Apply(UnlockStateSO state);

#if UNITY_EDITOR
    private static IEnumerable<string> GetUnlockIds() =>
        ServiceLocator.TryGetService<UnlockCatalogSO>(out var cat) ? cat.AllIds : Array.Empty<string>();
#endif
}
```

### 14.2 Subtipos concretos

Un SO individual por tipo de unlock. Cada uno referencia el asset específico a desbloquear y sabe cómo aplicarlo al `UnlockStateSO`.

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Meta/Unlocks/Hero")]
public class HeroUnlockSO : BaseUnlockSO
{
    public ClassHeroSO TargetHero;

    public override void Apply(UnlockStateSO state) =>
        state.UnlockHero(TargetHero.EntityId);             // heredado de BaseEntitySO §7.0
}

[CreateAssetMenu(menuName = "Rollgeon/Meta/Unlocks/Dice")]
public class DiceUnlockSO : BaseUnlockSO
{
    public DiceType TargetDice;

    public override void Apply(UnlockStateSO state) =>
        state.UnlockDice(TargetDice);
}

[CreateAssetMenu(menuName = "Rollgeon/Meta/Unlocks/Item")]
public class ItemUnlockSO : BaseUnlockSO
{
    public ItemSO TargetItem;

    public override void Apply(UnlockStateSO state) =>
        state.UnlockItem(TargetItem.ItemId);
}

[CreateAssetMenu(menuName = "Rollgeon/Meta/Unlocks/Passive")]
public class PassiveUnlockSO : BaseUnlockSO
{
    public ClassPassiveSO TargetPassive;

    public override void Apply(UnlockStateSO state) =>
        state.UnlockPassive(TargetPassive.PassiveId);
}

[CreateAssetMenu(menuName = "Rollgeon/Meta/Unlocks/Upgrade")]
public class UpgradeUnlockSO : BaseUnlockSO
{
    public UpgradeSO TargetUpgrade;

    public override void Apply(UnlockStateSO state) =>
        state.UnlockUpgrade(TargetUpgrade.UpgradeId);
}
```

**Agregar una nueva categoría de unlock** = crear un nuevo subtipo de `BaseUnlockSO` + método correspondiente en `UnlockStateSO`. No requiere tocar la infraestructura base.

### 14.3 `UnlockConditionSO`

Abstracción separada de `BaseUnlockSO` porque una condición es **reusable** entre varios unlocks. El mismo "Ganar con 5×d6" puede ser condición para desbloquear el d8 y para desbloquear una pasiva.

```csharp
public abstract class UnlockConditionSO : ScriptableObject
{
    public string DisplayText;               // "Win con 5×d6 (Guerrero)"
    public abstract bool Evaluate(RunRecord record);
}
```

**Concretos** (mapean a las condiciones del árbol del GD):

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Meta/Conditions/Win With Dice")]
public class UC_WinWithDice : UnlockConditionSO
{
    [InfoBox("Ej: [d6, d6, d6, d6, d6] para 5×d6. Orden no importa — se compara como multiset.")]
    public List<DiceType> RequiredBag;

    public override bool Evaluate(RunRecord record) =>
        record.Won &&
        record.Bag.OrderBy(d => d).SequenceEqual(RequiredBag.OrderBy(d => d));
}

[CreateAssetMenu(menuName = "Rollgeon/Meta/Conditions/Win With Class")]
public class UC_WinWithClass : UnlockConditionSO
{
    public ClassHeroSO Hero;

    public override bool Evaluate(RunRecord record) =>
        record.Won && record.HeroId == Hero.EntityId;     // HeroId del record, EntityId heredado §7.0
}

[CreateAssetMenu(menuName = "Rollgeon/Meta/Conditions/No Crossed Combos")]
public class UC_NoCrossedCombos : UnlockConditionSO
{
    public override bool Evaluate(RunRecord record) =>
        record.Won && record.CombosCrossed.Count == 0;
}

[CreateAssetMenu(menuName = "Rollgeon/Meta/Conditions/Win With N Classes")]
public class UC_WinWithNClasses : UnlockConditionSO
{
    [MinValue(1)] public int Count = 3;

    public override bool Evaluate(RunRecord record) =>
        ServiceLocator.GetService<UnlockStateSO>().CompletedClasses.Count >= Count;
}

/// <summary>Disyunción: al menos una de las sub‑condiciones debe cumplirse.</summary>
[CreateAssetMenu(menuName = "Rollgeon/Meta/Conditions/OR")]
public class UC_Or : UnlockConditionSO
{
    [OdinSerialize] public List<UnlockConditionSO> Any = new();

    public override bool Evaluate(RunRecord record) =>
        Any.Any(c => c != null && c.Evaluate(record));
}
```

**Ejemplo — unlock del d20 (GD: "Completar una run con 3 clases distintas"):**

```
d20Unlock : DiceUnlockSO
  TargetDice = D20
  Conditions = [ UC_WinWithNClasses(Count = 3) ]
```

**Ejemplo — unlock compuesto** (varias condiciones AND):

```
alchemistUnlock : HeroUnlockSO
  TargetHero = Alchemist
  Conditions = [
    UC_WinWithClass(Necromancer),
    UC_NoCrossedCombos()
  ]
```

### 14.4 `UnlockStateSO`

Runtime state de los unlocks. **Persistido via §15** (Save System) — el SO **no** escribe directo a disco.

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Meta/Unlock State")]
public class UnlockStateSO : ScriptableObject, ISaveable
{
    [SerializeField] private HashSet<string> _unlockedHeroes = new();
    [SerializeField] private HashSet<DiceType> _unlockedDice = new();
    [SerializeField] private HashSet<string> _unlockedItems = new();
    [SerializeField] private HashSet<string> _unlockedPassives = new();
    [SerializeField] private HashSet<string> _unlockedUpgrades = new();
    [SerializeField] private HashSet<string> _completedClasses = new();

    public IReadOnlyCollection<string> CompletedClasses => _completedClasses;

    public bool IsHeroUnlocked(string heroId) => _unlockedHeroes.Contains(heroId);
    public bool IsDiceUnlocked(DiceType dice) => _unlockedDice.Contains(dice);
    // + IsItemUnlocked, IsPassiveUnlocked, IsUpgradeUnlocked

    public void UnlockHero(string heroId)    { _unlockedHeroes.Add(heroId); Dirty(); }
    public void UnlockDice(DiceType dice)    { _unlockedDice.Add(dice); Dirty(); }
    public void UnlockItem(string itemId)    { _unlockedItems.Add(itemId); Dirty(); }
    public void UnlockPassive(string passiveId) { _unlockedPassives.Add(passiveId); Dirty(); }
    public void UnlockUpgrade(string upgradeId) { _unlockedUpgrades.Add(upgradeId); Dirty(); }

    public void RegisterClassCompletion(string classId)
    {
        _completedClasses.Add(classId);
        Dirty();
    }

    // ── ISaveable (§15) ─────────────────────────────────────────────────────
    public string SaveKey => "UnlockState";
    public object CaptureState() => new UnlockStateSnapshot { /* ... */ };
    public void RestoreState(object state) { /* ... */ }

    /// <summary>
    /// Los unlocks son high-value: no queremos perderlos por un crash entre el
    /// momento del unlock y el próximo trigger de save configurado (§15.4).
    /// Por eso hacemos Flush inmediato con SaveTrigger.Manual — el cache se
    /// captura y se vuelca a disco sincrónicamente. El costo es despreciable
    /// (un HashSet chico) y evita que el progreso meta dependa de RunEnd.
    /// </summary>
    private void Dirty()
    {
        SaveSystem.MarkDirty(this);
        SaveSystem.Flush(SaveTrigger.Manual);
    }
}
```

### 14.5 `RunRecord`

Resumen inmutable de una run terminada. Lo consumen las `UnlockConditionSO.Evaluate`.

```csharp
public class RunRecord
{
    public string HeroId;                            // EntityId del ClassHeroSO que jugó la run
    public List<DiceType> Bag;
    public bool Won;
    public int FloorReached;
    public HashSet<string> CombosCrossed;            // por ComboId
    public int TotalDamageDealt;
    public HashSet<string> ComboTypesMatched;        // ComboId → aparece al menos 1 vez
    public DateTime Finished;
    public int Seed;                                 // seed del IRngService (§17.O) — reproducible para QA
    public string RulesetId;                         // ruleset activo durante la run (§14.7)
}
```

### 14.6 Flujo de end‑of‑run

```
1. Run termina (victoria o muerte).
2. RunSummary = Player.BuildRunRecord()
3. foreach (unlock in unlockCatalog.AllUnlocks):
      if (!unlockState.IsUnlocked(unlock) && unlock.Evaluate(RunSummary)):
          unlock.Apply(unlockState)
          EventManager.Trigger(EventName.OnUnlockGranted, unlock.UnlockId)
          // UI muestra notificación
4. SaveSystem.CaptureAll()
5. SaveSystem.Flush(SaveTrigger.RunEnd)
```

**Cross‑ref.** §4 (ClassHeroSO), §6 (DiceType), §15 (save), §17 (changelog).

### 14.7 `RulesetSO` — reglas de run y curvas de scaling

Todo número del juego que pueda variar entre modos — **max energy**, **tiradas base por ataque**, **re‑rolls por energía**, **HP / daño / gold scaling por piso**, **cantidad de enemigos por sala** — vive en un `RulesetSO`. Un ruleset encarna un "modo de juego" (ej: arcade, hardcore, relajado, daily run). Cambiar de modo = swappear el SO activo; cero código.

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Meta/Ruleset")]
public class RulesetSO : SerializedScriptableObject
{
    [Title("Identity")]
    [ValueDropdown(nameof(GetRulesetIds))]
    public string RulesetId;
    public string DisplayName;
    [TextArea] public string Description;

    [Title("Combat — Action economy")]
    public int MaxEnergy;                             // cap del stat Energy del jugador
    public int StartingEnergy;                        // energía con la que empieza el turno 1
    public int BaseEnergyRegenPerTurn;                // cuánto recarga OnTurnFinished
    public int MaxCarriedEnergy;                      // cap de acumulación turno a turno

    [Title("Combat — Rolls")]
    public int BaseRollsPerAttack;                    // GDD: 3
    public int MaxExtraRerollsByEnergy;               // cuántos extras se pueden comprar por turno
    public int EnergyCostPerExtraReroll;              // default 1

    [Title("Combat — Defense")]
    public bool DefenseFromUnusedRolls;               // §12.4 activado
    public int SingleRollThresholdBase;               // umbral default para skill checks §12.5

    [Title("Combat — Reactions")]
    [InfoBox("Profundidad máxima de la cadena de reacciones (reactivo → reactivo → …). " +
             "Previene loops patológicos. Default 3 = un contragolpe puede generar " +
             "otro contragolpe que puede generar uno más, pero ahí se corta.")]
    [MinValue(1)] public int MaxReactionDepth = 3;

    [Title("Initiative")]
    public int SpeedDieMin;                           // speed die del §12.7
    public int SpeedDieMax;

    [Title("Scaling curves (x = floor index)")]
    [InfoBox("Multiplicadores aplicados a las stats de los enemigos spawneados según el piso actual.")]
    [OdinSerialize] public AnimationCurve EnemyHPMultiplier       = AnimationCurve.Linear(0, 1, 10, 2);
    [OdinSerialize] public AnimationCurve EnemyDamageMultiplier   = AnimationCurve.Linear(0, 1, 10, 2);
    [OdinSerialize] public AnimationCurve GoldRewardMultiplier    = AnimationCurve.Linear(0, 1, 10, 1.5f);
    [OdinSerialize] public AnimationCurve EnemyCountCurve         = AnimationCurve.Linear(0, 2, 10, 5);
    [OdinSerialize] public AnimationCurve ObstacleCountCurve      = AnimationCurve.Linear(0, 1, 10, 4);

    [Title("Progression")]
    public int StartingFloor;
    public int MaxFloors;

    [Title("Overrides")]
    [InfoBox("ActionIds que quedan forbidden en este ruleset (p.e. 'combo.strike' apagado en easy mode). " +
             "Se valida contra ActionCatalogSO (§12.6).")]
    [ValueDropdown(nameof(GetActionIds))]
    public List<string> ForbiddenActionIds = new();

#if UNITY_EDITOR
    private static IEnumerable<string> GetActionIds() =>
        ServiceLocator.TryGetService<ActionCatalogSO>(out var cat) ? cat.AllIds : Array.Empty<string>();
#endif

    [Title("Inventory")]
    [InfoBox("Cantidad máxima de ítems activos que el jugador puede tener equipados.")]
    [MinValue(1)] public int MaxActiveItemSlots = 1;

    [Title("Combo counters")]
    [OdinSerialize] public List<ComboCounterThresholdSO> ComboCounterThresholds = new();   // §5.5

    [Title("Shop")]
    [InfoBox("Globals de shop: pricing multiplier, variance, first-purchase discount, " +
             "restock rules. La COMPOSICIÓN del pool (qué ítems, con qué pesos) vive en " +
             "el ShopPoolSO por piso (§17.F). Regla de autoridad: RulesetSO dicta el " +
             "cómo se calcula el precio; ShopPoolSO dicta qué se vende.")]
    [OdinSerialize] public ShopConfigSO ShopConfig;

#if UNITY_EDITOR
    private static IEnumerable<string> GetRulesetIds() =>
        ServiceLocator.TryGetService<RulesetCatalogSO>(out var cat) ? cat.AllIds : Array.Empty<string>();
#endif
}
```

**`RulesetCatalogSO`** — catálogo de rulesets disponibles. Pre-cargado en `ServiceBootstrapSO` (§1.1.1).

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Meta/Ruleset Catalog")]
public class RulesetCatalogSO : SerializedScriptableObject
{
    [OdinSerialize] private List<RulesetSO> _entries = new();
    public IEnumerable<string> AllIds => _entries.Select(e => e.RulesetId);
    public RulesetSO GetById(string id) => _entries.FirstOrDefault(e => e.RulesetId == id);
}
```

**Ciclo de vida**:

1. El jugador elige el ruleset al arrancar una run (o implícitamente por el modo de juego).
2. `ServiceLocator.AddService<RulesetSO>(selectedRuleset)` — el ruleset queda registrado como servicio singleton para toda la run.
3. Cualquier sistema que necesite un número lo lee con `ServiceLocator.GetService<RulesetSO>().BaseRollsPerAttack` (etc.).
4. Al terminar la run, el servicio se des-registra. El próximo arranque puede usar otro ruleset sin conflictos.

**Consumidores canónicos**:
- `TurnManager` — `MaxEnergy`, `BaseEnergyRegenPerTurn`, `MaxCarriedEnergy`, `ForbiddenActionTags`.
- `DiceRoller` / `IRerollBudget` (§6.5) — `BaseRollsPerAttack`, `MaxExtraRerollsByEnergy`, `EnergyCostPerExtraReroll`.
- `IDefenseResolver` (§12.4) — `DefenseFromUnusedRolls`.
- `ISecondaryRollResolver` (§12.5) — `SingleRollThresholdBase` como fallback si el `RoomSO` no define uno propio.
- `DefaultInitiativeProvider` (§12.7) — `SpeedDieMin`, `SpeedDieMax`.
- `DungeonManager` (§13.6) — curvas de scaling, al spawnear enemigos aplica `enemyBase.HP * ruleset.EnemyHPMultiplier.Evaluate(floorIndex)`, idem damage y gold.
- `FloorLayoutSO.RoomCountMin/Max` — sigue existiendo, pero el ruleset puede overridear vía `EnemyCountCurve` si se quiere.
- `ComboCounterService` (§5.5) — lee `ComboCounterThresholds` para evaluar cada `OnComboMatched`.

**Por qué `AnimationCurve` y no `List<floatPerFloor>`**:
- Permite curvas no‑lineales (exponenciales, escalones, valles).
- Se edita gráficamente en el inspector — el diseñador ve la forma.
- Es resolución independiente (`Evaluate(floorIndex)` en cualquier punto, no solo ints).
- Trivial de tunear para balance sin rebuild.

**Unlocks que dependen del ruleset**:
Un `UnlockConditionSO` (§14.3) puede chequear contra el ruleset activo — por ejemplo, "ganar una run en el ruleset `hardcore`" se escribe como un `UC_RulesetBeaten` que lee `runRecord.RulesetId`.

**Cross‑ref.** §1.1.1 (bootstrap registra el ruleset activo), §6.5 (reroll budget lee del ruleset), §12.6 (action economy), §12.7 (turn order), §13 (dungeon scaling), §14.1 (UnlockConditionSO puede filtrar por ruleset), §15 (el ruleset activo se persiste como parte del `RunRecord`).

---

## 15. Sistema de Save / Persistencia

> Un único sistema de save para todo el juego. Cache runtime en memoria + flush a JSON por triggers configurables desde editor.

### 15.1 Diseño

- **Cache en memoria**: `SaveSystem` mantiene un diccionario `saveKey → serializedState` que se actualiza cada vez que se captura el estado.
- **Registro por interfaz**: cualquier componente que necesite persistir implementa `ISaveable` y se auto‑registra.
- **Captura bajo demanda**: `CaptureAll()` recorre todos los `ISaveable` y actualiza el cache.
- **Flush configurable**: `Flush(trigger)` escribe el cache a JSON sólo si el `SaveSettingsSO` dice que ese trigger debe escribir.
- **Triggers**:
  - `RunStart` — al iniciar una run.
  - `RoomEnd` — al salir de una sala.
  - `FloorEnd` — al terminar un piso.
  - `Manual` — botón de guardar en el menú.
  - `RunEnd` — al terminar la run (victoria o muerte).
  - `Exit` — al cerrar el juego.

### 15.2 `ISaveable`

```csharp
public interface ISaveable
{
    /// <summary>
    /// Clave única del ISaveable. Puede ser estática (singletons como UnlockState)
    /// o dinámica basada en guid (entidades spawneadas en runtime).
    /// </summary>
    string SaveKey { get; }

    /// <summary>Serializa el estado actual a un objeto opaco.</summary>
    object CaptureState();

    /// <summary>Restaura el estado desde el objeto capturado.</summary>
    void RestoreState(object state);
}
```

**Ejemplos:**

```csharp
// Singleton: UnlockState (§14.4)
public class UnlockStateSO : ScriptableObject, ISaveable
{
    public string SaveKey => "UnlockState";       // estático
}

// Dinámico: entidad en runtime
public class Entity : ISaveable
{
    public Guid InstanceId { get; }
    public string SaveKey => $"Entity:{InstanceId}";
}

// Progreso de la run actual
public class RunProgress : ISaveable
{
    public string SaveKey => "RunProgress";
    public object CaptureState() => new RunProgressSnapshot { /* ... */ };
    public void RestoreState(object state) { /* ... */ }
}
```

### 15.3 `SaveSystem`

**`WeakReference<T>` para registrations.** El sistema usa `WeakReference<ISaveable>` en vez de referencias fuertes para prevenir memory leaks. Un `WeakReference` es un wrapper del runtime de .NET que mantiene una referencia a un objeto **sin impedir que el garbage collector lo recolecte**. Si el objeto referenciado se destruye (por ejemplo, un `MonoBehaviour` cuyo `GameObject` fue destruido sin llamar `Unregister`), el `WeakReference` devuelve `null` en vez de mantener el objeto zombie en memoria. Esto previene el caso donde un `ISaveable` que no llamó `Unregister` (porque `OnDestroy` no se ejecutó, o un null ref lo impidió) queda en la lista para siempre, causando memory leaks y errores silenciosos en `CaptureAll`.

```csharp
public static class SaveSystem
{
    private static readonly Dictionary<string, object> _cache = new();
    private static readonly List<WeakReference<ISaveable>> _registered = new();
    private static readonly HashSet<string> _dirty = new();

    // --- Registration --------------------------------------------------------

    public static void Register(ISaveable s)
    {
        // Chequea si ya está registrado (comparando el target del WeakReference)
        foreach (var wr in _registered)
            if (wr.TryGetTarget(out var existing) && ReferenceEquals(existing, s))
                return;

        _registered.Add(new WeakReference<ISaveable>(s));

        // Si tenemos estado previo en cache, restaurarlo automáticamente.
        if (_cache.TryGetValue(s.SaveKey, out var state))
            s.RestoreState(state);
    }

    public static void Unregister(ISaveable s)
    {
        // Capturamos antes de soltar para que el cache mantenga el estado final.
        for (int i = _registered.Count - 1; i >= 0; i--)
        {
            if (_registered[i].TryGetTarget(out var existing) && ReferenceEquals(existing, s))
            {
                _cache[s.SaveKey] = s.CaptureState();
                _registered.RemoveAt(i);
                break;
            }
        }
    }

    public static void MarkDirty(ISaveable s) => _dirty.Add(s.SaveKey);

    // --- Capture / Restore ---------------------------------------------------

    /// <summary>
    /// Recorre todos los ISaveable y actualiza el cache en memoria.
    /// Purga automáticamente las WeakReferences cuyo target fue recolectado
    /// por el GC (ISaveables que no llamaron Unregister antes de destruirse).
    /// </summary>
    public static void CaptureAll()
    {
        for (int i = _registered.Count - 1; i >= 0; i--)
        {
            if (_registered[i].TryGetTarget(out var s))
                _cache[s.SaveKey] = s.CaptureState();
            else
                _registered.RemoveAt(i);   // purga referencia huérfana
        }
        _dirty.Clear();
        EventManager.Trigger(EventName.OnCaptureRequested);
    }

    /// <summary>Sólo captura los marcados como dirty. Más eficiente si el grafo es grande.</summary>
    public static void CaptureDirty()
    {
        for (int i = _registered.Count - 1; i >= 0; i--)
        {
            if (_registered[i].TryGetTarget(out var s))
            {
                if (_dirty.Contains(s.SaveKey))
                    _cache[s.SaveKey] = s.CaptureState();
            }
            else
                _registered.RemoveAt(i);   // purga referencia huérfana
        }
        _dirty.Clear();
    }

    /// <summary>Restaura todos los ISaveable registrados desde el cache.</summary>
    public static void RestoreAll()
    {
        for (int i = _registered.Count - 1; i >= 0; i--)
        {
            if (_registered[i].TryGetTarget(out var s))
            {
                if (_cache.TryGetValue(s.SaveKey, out var state))
                    s.RestoreState(state);
            }
            else
                _registered.RemoveAt(i);   // purga referencia huérfana
        }
        EventManager.Trigger(EventName.OnRestoreCompleted);
    }

    // --- Flush / Load --------------------------------------------------------

    /// <summary>
    /// Versión del schema del payload. Se bumpea a mano cuando un ISaveable
    /// cambia la forma de su CaptureState/RestoreState de manera incompatible
    /// (campo renombrado, tipo distinto, etc.). Al cargar, un mismatch
    /// descarta el save viejo con mensaje claro en vez de crashear al
    /// deserializar. No hay migration paths — en fase de diseño el save es
    /// desechable y el progreso meta se re-evalúa en el próximo run.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// Escribe el cache a disco si el trigger está habilitado en SaveSettings.
    /// El payload va wrappeado en un SavePayload con SchemaVersion para
    /// detectar incompatibilidades al cargar.
    ///
    /// Flush strategy (§15.3.1): bajo threshold → sincrono en el frame.
    /// Sobre threshold → async vía Task.Run (o UniTask si está adoptado).
    /// </summary>
    public static void Flush(SaveTrigger trigger)
    {
        var settings = ServiceLocator.GetService<SaveSettingsSO>();
        if (!settings.ShouldFlushOn(trigger)) return;

        var path = settings.GetSavePath();
        var payload = new SavePayload {
            SchemaVersion = CurrentSchemaVersion,
            Data = _cache,
        };
        var json = SerializationUtility.SerializeValue(payload, DataFormat.JSON);

        if (json.Length < settings.AsyncFlushThresholdBytes)
        {
            // Sincrono: el jugador no va a ver el frame drop.
            File.WriteAllBytes(path, json);
            if (settings.LogFlushes) Debug.Log($"[SaveSystem] Sync flush on {trigger} → {path} ({json.Length} B)");
        }
        else
        {
            // Async: escribe off-thread. Un flush concurrente del mismo path
            // es idempotente (última escritura gana), pero el lock serializa
            // writes para no producir archivos corruptos con interleaved bytes.
            FlushAsync(path, json, trigger);
        }
    }

    private static readonly object _flushLock = new();

    private static void FlushAsync(string path, byte[] json, SaveTrigger trigger)
    {
        Task.Run(() =>
        {
            lock (_flushLock)
            {
                // Write-to-temp + atomic rename evita corruption si el proceso
                // muere mid-write. Funciona en Windows/Mac/Linux igual.
                var tmp = path + ".tmp";
                File.WriteAllBytes(tmp, json);
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
        });
    }

    public static void LoadFromDisk()
    {
        var settings = ServiceLocator.GetService<SaveSettingsSO>();
        var path = settings.GetSavePath();
        if (!File.Exists(path)) return;

        var json = File.ReadAllBytes(path);
        SavePayload payload;
        try
        {
            payload = SerializationUtility.DeserializeValue<SavePayload>(json, DataFormat.JSON);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SaveSystem] Save corrupt, starting fresh: {ex.Message}");
            return;
        }

        if (payload.SchemaVersion != CurrentSchemaVersion)
        {
            Debug.LogWarning(
                $"[SaveSystem] Save schema v{payload.SchemaVersion} incompatible with " +
                $"current v{CurrentSchemaVersion} — discarding save.");
            return;
        }

        _cache.Clear();
        foreach (var (k, v) in payload.Data) _cache[k] = v;

        RestoreAll();
    }

    public static void Clear()
    {
        _cache.Clear();
        _dirty.Clear();
    }
}

[Serializable]
public class SavePayload
{
    public int SchemaVersion;
    public Dictionary<string, object> Data;
}

public enum SaveTrigger { RunStart, RoomEnd, FloorEnd, Manual, RunEnd, Exit }
```

**Serialización.** Se usa `Sirenix.Serialization.SerializationUtility` (Odin) porque maneja polimorfismo, colecciones anidadas e interfaces transparentemente. `JsonUtility` no soporta `Dictionary<,>` ni interfaces.

**Rehidratación de tipos no‑triviales.** Algunos `ISaveable` contienen objetos que necesitan re‑wire de eventos tras restore — canónicamente `Modifier<T>` (§3.5), que al deserializar pierde su suscripción al `EventManager`. Cada `ISaveable` que contenga objetos así debe re‑hookear dentro de su `RestoreState(...)`: para modifiers, invocar `mod.OnLoad()` sobre cada uno tras poblar la lista. `SaveSystem` **no** hace esto automáticamente — desconoce los tipos concretos.

**Cleanup de referencias huérfanas.** `CaptureAll` / `CaptureDirty` / `RestoreAll` iteran la lista de WeakReferences **en reverso** para poder remover in‑place las entries cuyo `TryGetTarget` devuelve `false` (target recolectado por el GC). Esto evita el leak clásico de "`ISaveable` se destruyó sin `Unregister`" — en vez de mantener la entry para siempre, el primer `CaptureAll` después de la destrucción la purga. No se necesita un sweep explícito.

#### 15.3.1 Flush strategy — sync vs async

**Regla.** Bajo el threshold `AsyncFlushThresholdBytes` (§15.4, default 500 KB), `Flush` escribe sincrono en el frame. Sobre el threshold, `Task.Run` despacha el write off‑thread y devuelve control inmediatamente.

**Por qué dos modos.**
- **Sync bajo threshold.** Un write < 500 KB completa en milisegundos incluso en HDD lento — el frame drop es invisible. Sincrono evita toda la complejidad de async (disposal de tasks, ordering, double-flush races).
- **Async sobre threshold.** Un save mid-dungeon con historial de rooms, inventario grande y muchos modifiers puede escalar a 1–5 MB. `File.WriteAllBytes` sincrono a ese tamaño produce frame drops visibles (especialmente en HDD / SD cards móviles). Task.Run + write-to-temp + atomic rename es suficiente — no hace falta UniTask para esto.

**Atomicity — write-to-temp + rename.** El async path siempre escribe a `path.tmp` y renombra al final. Si el proceso muere mid-write (crash, power loss), el save anterior queda intacto. Sin este patrón, un crash durante `File.WriteAllBytes` deja el archivo truncado y `LoadFromDisk` falla con un `SerializationException`. El `_flushLock` serializa writes concurrentes al mismo path — dos triggers rápidos (p. ej. `RoomEnd` + `FloorEnd` back-to-back) no producen interleaving.

**Orden vs la próxima `LoadFromDisk`.** Si el usuario cierra el juego mientras un async flush está en vuelo, el flush completa antes del shutdown gracias al lock + `Application.wantsToQuit` que espera al task. `SaveTrigger.Exit` **siempre** corre sincrono (aunque supere el threshold) — es el último write y no hay frame que proteger.

```csharp
public static void Flush(SaveTrigger trigger)
{
    // ...
    // Excepción: Exit siempre sync — no podemos despedirnos con un Task.Run pending.
    bool forceSync = (trigger == SaveTrigger.Exit) || (json.Length < settings.AsyncFlushThresholdBytes);
    if (forceSync) { /* write sync */ } else { FlushAsync(...); }
}
```

**UniTask (opcional).** Si se adopta UniTask (§16.3 pendiente), `FlushAsync` se reescribe como `async UniTaskVoid FlushAsync(...)` con `await File.WriteAllBytesAsync(...)`. La interfaz pública de `Flush` no cambia — el caller no ve la diferencia. `Task.Run` funciona bien sin UniTask; el único beneficio real es evitar el thread-pool hop en platforms con thread budget ajustado.

### 15.4 `SaveSettingsSO`

Editor‑configurable. El Game Designer decide en qué triggers hacer flush a disco (puede habilitar/deshabilitar sin tocar código).

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Save/Settings")]
public class SaveSettingsSO : ScriptableObject
{
    [Title("Flush Triggers")]
    [InfoBox("Activá los triggers en los que querés que se escriba el cache a JSON.\n" +
             "Los triggers desactivados sólo capturan en memoria — útil para reducir I/O.")]
    [EnumToggleButtons]
    public SaveTrigger[] FlushOn = new[]
    {
        SaveTrigger.RunStart,
        SaveTrigger.FloorEnd,
        SaveTrigger.Manual,
        SaveTrigger.RunEnd,
        SaveTrigger.Exit,
    };

    [Title("File")]
    [InfoBox("Relativo a Application.persistentDataPath.")]
    public string SaveFilePrefix = "rollgeon";
    [InfoBox("Cantidad de slots de save. Estilo Isaac: 3 saves independientes.")]
    [MinValue(1)] public int MaxSaveSlots = 3;

    [Title("Flush strategy (§15.3.1)")]
    [InfoBox("Sobre este tamaño, Flush() corre async (Task.Run) en vez de sincrono. " +
             "500 KB cubre el caso normal (payload mid-dungeon con 5 rooms + modifiers).")]
    [MinValue(1)] public int AsyncFlushThresholdBytes = 500_000;

    [Title("Debug")]
    public bool PrettyPrint = false;
    public bool LogFlushes = false;

    public bool ShouldFlushOn(SaveTrigger trigger) =>
        FlushOn.Contains(trigger);

    /// <summary>
    /// Devuelve el path del save para el slot activo.
    /// Slot 0 → "rollgeon_0.save", Slot 1 → "rollgeon_1.save", etc.
    /// </summary>
    public string GetSavePath(int slotIndex = -1)
    {
        var slot = slotIndex >= 0 ? slotIndex : ActiveSlot;
        return Path.Combine(Application.persistentDataPath, $"{SaveFilePrefix}_{slot}.save");
    }

    /// <summary>Slot activo — seteado al elegir save en el menú principal.</summary>
    [NonSerialized] public int ActiveSlot = 0;

    public bool SlotExists(int slotIndex) =>
        File.Exists(GetSavePath(slotIndex));

    public void DeleteSlot(int slotIndex)
    {
        var path = GetSavePath(slotIndex);
        if (File.Exists(path)) File.Delete(path);
    }
}
```

**Decisiones de default:**

- `RoomEnd` está **desactivado** por default — flushear a disco en cada sala genera mucho I/O. El cache igual se actualiza en memoria (`CaptureAll` corre igual), pero no escribe a JSON hasta el siguiente trigger habilitado.
- `Exit` se maneja conectando `Application.wantsToQuit` al `Flush(SaveTrigger.Exit)`.

### 15.5 Snapshots tipados

Cada `ISaveable` debería devolver un struct/clase `[Serializable]` con su estado, no un anonymous object:

```csharp
[Serializable]
public class UnlockStateSnapshot
{
    public List<string> Heroes;
    public List<DiceType> Dice;
    public List<string> Items;
    public List<string> Passives;
    public List<string> Upgrades;
    public List<string> CompletedClasses;
}

[Serializable]
public class EntitySnapshot
{
    public Guid InstanceId;
    public string EntityId;
    public Dictionary<string, object> Stats;     // stat name → value
    public List<Modifier<int>> IntModifiers;
    public List<Modifier<float>> FloatModifiers;
}

[Serializable]
public class RunProgressSnapshot
{
    public string HeroId;                            // EntityId del ClassHeroSO activo
    public List<DiceType> Bag;
    public int CurrentFloor;
    public string CurrentRoomId;
    public HashSet<string> CrossedCombos;
    public int Gold;
}
```

### 15.6 Flujo durante una run

```
BootstrapScene.Awake
  ↓
ServiceBootstrap.RegisterAll()
SaveSystem.LoadFromDisk()                          // cache se popula si existe save previo
  ↓
MainMenu (UnlockState ya restaurado)
  ↓
Player elige clase + bolsa
  ↓
RunStart
  ↓
Player.Register() como ISaveable
RunProgress.Register() como ISaveable
SaveSystem.Flush(RunStart)
  ↓
Enter Room 1
  Enemies.Register() (cada uno como ISaveable)
  … combate …
  Exit Room → SaveSystem.CaptureAll(); SaveSystem.Flush(RoomEnd)  // flush sólo si habilitado
  Enemies.Unregister() (muertos) o permanecen en cache (vivos)
  ↓
Enter Room 2 … (mismo ciclo)
  ↓
Floor cleared → SaveSystem.CaptureAll(); SaveSystem.Flush(FloorEnd)
  ↓
Boss derrotado → avanza de piso
  ↓
RunEnd (win / death)
  RunRecord = Player.BuildRunRecord()
  Evaluate unlocks (§14.6)
  SaveSystem.CaptureAll()
  SaveSystem.Flush(RunEnd)
  Player.Unregister(); RunProgress.Unregister()
  ↓
Back to MainMenu
```

**Cross‑ref.** §1.1 (bootstrap y registro de `SaveSettingsSO` en `ServiceLocator`), §14 (unlocks también persisten por acá), §13.8 (persistencia de enemigos entre salas), §17 (changelog).

---

## 16. Packages del proyecto

> Listado de packages externos del proyecto. Se actualiza cuando se suman/sacan dependencias.

### 16.1 Core

| Package | Versión | Uso |
|---|---|---|
| **TextMeshPro** | built‑in Unity | Toda la UI de texto (HUD, menús, tooltips, floating numbers). Siempre `TMP_Text`, nunca `UnityEngine.UI.Text`. |
| **Odin Inspector & Serializer** | Asset Store | `SerializedScriptableObject`, `[OdinSerialize]` para colecciones polimórficas, atributos de editor (`[ValueDropdown]`, `[TypeFilter]`, `[TableMatrix]`, `[InfoBox]`, `[Title]`, `[InlineProperty]`, `[ListDrawerSettings]`), `SerializationUtility` para el save system (§15). |
| **PrimeTween** | Asset Store / GitHub | Tweens de UI, transiciones de cámara, floating numbers, shake, animaciones de dados tirándose. Preferido sobre DOTween por performance y zero‑allocation API. |
| **Input System** (`com.unity.inputsystem`) | package manager | Input unificado con `InputActionAsset` por mapa (UI / Gameplay / Dungeon). Habilita re‑bind en runtime y soporte de gamepad/teclado/touch sin código duplicado. **Reemplaza al legacy `Input`** — no mezclar. |
| **Addressables** (`com.unity.addressables`) | package manager | Carga async de prefabs de sala, enemigos, VFX y audio. Todo lo que antes sería `Resources.Load` en el bootstrap (§1.1) va por `AssetReference` / `Addressables.LoadAssetAsync<T>`. Permite build por label (p.e. cargar pool de sala solo al entrar). |
| **Localization** (`com.unity.localization`) | package manager | Tablas de strings (UI, nombres de combos, descripciones de unlocks, tooltips de efectos) y de assets (iconos/audio por locale si hace falta). Las tablas son los `LocalizedString` / `LocalizedAsset<T>` que se bindean a `TMP_Text` y a los SOs de descripción. |
| **Unity Analytics** (`com.unity.services.analytics`) | package manager | Telemetría de eventos de gameplay para análisis de balance e informe de tesis (§25). Registra eventos clave (combos usados, muertes, compras en shop, duración de combate). La implementación concreta (`FileAnalyticsService` §25.2) puede usar el backend local (JSON lines a disco) para desarrollo y el SDK de Unity Analytics para producción. |

### 16.2 Notas de uso

- **Odin es requisito duro.** El pipeline de efectos (§8), los contenedores `Dictionary<Type, …>` (§2, §4, §7), los inspectors de clases y unlocks (§4, §14) y el save system (§15) dependen de Odin.
- **TextMeshPro** viene con el engine — mencionarlo explícitamente para recordar que toda referencia de UI debe usar `TMP_Text`, no `UnityEngine.UI.Text`.
- **PrimeTween** no tiene dependencias cruzadas con Odin ni con el resto del stack — es reemplazable si el equipo prefiere DOTween o animaciones nativas con `Animator`/`Playables`. Pero el API de PrimeTween es más terso para efectos cortos (dice roll, hit shake, floating damage).
- **Input System**: un único `InputActionAsset` (`Rollgeon/Input/Rollgeon.inputactions`) con maps separados para UI, Gameplay y Dungeon. Los listeners se activan/desactivan por map según la `GamePhase` (§12) — nunca leer input raw desde `Update()`. Los bindings re‑asignables deben persistir por el save system (§15) como un `ISaveable` que serializa `actions.SaveBindingOverridesAsJson()`.
- **Addressables**: el `ServiceBootstrapSO` (§1.1.1) usa `AssetReference` en vez de referencias duras para los catálogos grandes (enemy pools, room prefabs, feedback VFX pesados). Para assets small y siempre necesarios (contract sheet, combos, passives) la referencia directa sigue siendo aceptable. Regla: si el asset solo se usa en una sala/bioma/encounter, hacerlo Addressable.
- **Addressables + `BaseEntitySO.PrefabRef`**: los prefabs 3D de **toda entidad** (héroe, enemigo, boss, objeto interactuable) viven como `AssetReferenceGameObject` en el campo `PrefabRef` del parent (§7.0). El `EntityCatalogSO` (§1.1.1) los preloadea en bloque durante el bootstrap — cuando un sistema instancia una entidad ya tiene el prefab resuelto. **Prohibido** tener un catálogo intermedio `string → GameObject` indexado por id (antipatrón que fuerza `Resources.Load` o un lookup extra en hot path). El id de la entidad es sólo el contrato de identidad; el visual se dereferencia directo desde el SO dueño.
- **Localization**: los strings expuestos a UI (nombre de clase, descripción de combo, descripción de unlock, tooltip de efecto) son `LocalizedString`, no `string` crudo. Los SOs (`BaseComboSO`, `BaseUnlockSO`, `ClassHeroSO`…) exponen un `LocalizedString DisplayName` / `LocalizedString Description`, y la tabla de localización vive en `Assets/Rollgeon/Localization/`. Las keys de las tablas siguen la regla transversal de dropdowns (§0) cuando estén referenciadas desde otro lado.

### 16.3 Pendientes a evaluar

| Package | Para qué | Estado |
|---|---|---|
| **UniTask** | `async/await` sin allocations para el save flush asíncrono (§15) y las `Addressables.LoadAssetAsync` del bootstrap | TBD — evaluar si el flush bloquea el main thread en saves grandes; útil también para encadenar cargas async sin coroutines |
| **Newtonsoft JSON** (`com.unity.nuget.newtonsoft-json`) | Si alguna vez sale algo del save system que Odin `SerializationUtility` no cubre bien | TBD — por ahora Odin alcanza |

**Descartado.** `Cinemachine` fue evaluado y descartado en v8 — la cámara isométrica del gameplay loop es scripteada (§17.E) con PrimeTween para el smoothing. Ver §17.E.11 para el rationale.

---

## 17. Sistemas transversales

> Sistemas globales que atraviesan varios dominios. A diferencia de §0–§16 (subsistemas de dominio), estos proveen servicios reutilizables por todo el stack: audio, movimiento en grilla, el mini‑juego de Craps como sala reutilizable y la arquitectura de UI/Screens. Cada uno se registra en el `ServiceLocator` (§1.1) y se consume por interfaz.

---

### §A. Audio

> Sistema global de audio. Cubre la capa que §10 Feedback usa para sus entries `SFX`, más música y mixing. Los entries de §10 **nunca** llaman `AudioSource.PlayClipAtPoint` directamente — siempre delegan a `IAudioService`.

#### A.1 `IAudioService`

Registrado en el `ServiceLocator` desde el bootstrap (§1.1.1):

```csharp
public interface IAudioService
{
    void PlaySfx(AudioClip clip, Vector3 worldPos, float volume = 1f, float pitch = 1f);
    void PlaySfx2D(AudioClip clip, float volume = 1f, float pitch = 1f);

    void PlayMusic(AudioClip clip, float fadeSeconds = 1f);
    void StopMusic(float fadeSeconds = 1f);
    void PauseMusic();
    void ResumeMusic();

    void SetVolume(AudioChannel channel, float value);   // [0, 1]
    float GetVolume(AudioChannel channel);
}

public enum AudioChannel { Master, Music, Sfx, Ui }
```

#### A.2 `AudioManager`

Implementación concreta. Responsabilidades:

1. **Pool de `AudioSource`** para SFX. Tamaño configurable desde `AudioSettingsSO`. Cuando una SFX necesita reproducirse, el manager toma el primer source inactivo del pool (o el más viejo si todos están activos), lo rutea al mixer group de SFX, le setea clip/volume/pitch/pos y llama `Play()`.
2. **Canal de música** con crossfade. Dos `AudioSource`s persistentes dedicados (A y B) para tener crossfade lineal entre pistas — `PlayMusic(clip)` levanta A mientras baja B, o viceversa.
3. **Volúmenes** — cada `AudioChannel` tiene su valor en `[0, 1]` expuesto como property. El cambio se aplica al `AudioMixer` vía `SetFloat` sobre un parámetro expuesto (dB). Los volúmenes son `ISaveable` (§15) — persisten entre sesiones.
4. **Reglas de pool overflow** — si todos los sources están activos y llega un nuevo request, el manager corta el más viejo (FIFO). Para SFX críticas (ej: "muerte del jugador") se puede marcar un flag `IsImportant` en el request que los saque del FIFO normal.

#### A.3 `AudioSettingsSO`

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Audio/Settings")]
public class AudioSettingsSO : SerializedScriptableObject
{
    [Title("Mixer")]
    public AudioMixer Mixer;
    public string MasterParam = "MasterVol";
    public string MusicParam  = "MusicVol";
    public string SfxParam    = "SfxVol";
    public string UiParam     = "UiVol";

    [Title("Pool")]
    [MinValue(1)] public int SfxPoolSize = 16;

    [Title("Default volumes")]
    [Range(0,1)] public float DefaultMaster = 1f;
    [Range(0,1)] public float DefaultMusic  = 0.8f;
    [Range(0,1)] public float DefaultSfx    = 1f;
    [Range(0,1)] public float DefaultUi     = 1f;

    [Title("Biome music")]
    [InfoBox("Música por biome/piso. El DungeonManager dispara PlayMusic al entrar a una sala nueva.")]
    public List<BiomeMusicEntry> BiomeMusic = new();
}

[Serializable]
public class BiomeMusicEntry
{
    public string BiomeId;
    public AudioClip Music;
}
```

Pre‑cargado desde el `ServiceBootstrapSO` (§1.1.1).

#### A.4 Integración con §10 Feedback

En §10.7 el snippet de dispatch para `FeedbackType.SFX` hoy dice `AudioSource.PlayClipAtPoint(...)`. En el modelo unificado queda:

```csharp
case FeedbackType.SFX:
    if (entry.AudioClip != null)
        ServiceLocator.GetService<IAudioService>()
            .PlaySfx(entry.AudioClip, position, entry.Volume);
    break;
```

Así toda la música y todo el SFX del juego pasan por el mismo mixer y respetan el master volume. El `FeedbackEntry.Volume` es un multiplicador local — el valor final que llega al mixer es `masterVolume × sfxVolume × entry.Volume`.

#### A.5 Integración con §15 Save

Los 4 volúmenes son `ISaveable`:

```csharp
public class AudioVolumeState : ISaveable
{
    public string SaveKey => "audio.volumes";
    public Dictionary<AudioChannel, float> Values = new();

    public object CaptureState() => new Dictionary<AudioChannel, float>(Values);
    public void RestoreState(object state) => Values = new Dictionary<AudioChannel, float>((Dictionary<AudioChannel, float>)state);
}
```

El `AudioManager` se registra como `ISaveable` en el bootstrap, captura en cualquier `Flush` con trigger `Manual` (ej: al salir del menú de settings) y restaura al `LoadFromDisk()`.

**Cross‑ref.** §1.1.1 (bootstrap), §10 (feedback routea a este servicio), §15 (save de volúmenes), §16 (packages — `AudioMixer` viene con Unity).

---

### §B. Movimiento y Pathfinding

> Movimiento del pawn en la grilla como sistema dedicado. §11 cubre selección de targets de efectos; acá cubrimos el grafo de casillas alcanzables, el pathfinding y cómo se anima el movimiento sin hardcodear animaciones en el servicio.

#### B.1 `IMovementService`

Registrado en el `ServiceLocator`:

```csharp
public interface IMovementService
{
    /// <summary>
    /// Devuelve todas las casillas alcanzables desde 'from' con un budget dado,
    /// respetando obstáculos, otras entidades y puertas cerradas.
    /// </summary>
    IReadOnlyList<GridCoord> GetReachableTiles(GridCoord from, int budget);

    /// <summary>
    /// Path más corto de 'from' a 'to'. Devuelve lista vacía si no hay camino.
    /// Incluye el tile inicial y el final.
    /// </summary>
    IReadOnlyList<GridCoord> FindPath(GridCoord from, GridCoord to);

    /// <summary>
    /// Mueve la entidad a lo largo de un path ya calculado. Devuelve la coroutine
    /// — el caller decide si esperarla o no. Durante el movimiento, dispara un
    /// FeedbackRequest (§10) con una secuencia que sincroniza animación, SFX
    /// y el desplazamiento del transform. El servicio no toca Animator directo.
    /// </summary>
    IEnumerator MoveAlongPath(Guid entityGuid, IReadOnlyList<GridCoord> path);

    event Action<Guid> OnMovementStarted;
    event Action<Guid> OnMovementCompleted;
    event Action<Guid, Guid> OnMovementBlocked;    // mover, bloqueador
}

public readonly struct GridCoord
{
    public readonly int X, Y;
    public GridCoord(int x, int y) { X = x; Y = y; }
    public int ManhattanTo(GridCoord other) => Math.Abs(X - other.X) + Math.Abs(Y - other.Y);
}

public enum GridDirection { North, East, South, West }
```

#### B.2 Algoritmo

**BFS** sobre la grilla del `RoomInstance` (§13). BFS y no A\* porque:
- Los costes de movimiento son uniformes (1 por tile).
- El fanout es chico (4 vecinos) y las rooms son chicas (≤ 15×15).
- `GetReachableTiles` necesita **todo** el frontier, no un path — BFS es directamente la respuesta.

Para `FindPath`, también BFS con `parent[]` reconstruido al encontrar el target. Si algún día hace falta terreno con costes variables, migrar a A\* es localizado al servicio.

#### B.3 Budget de movimiento

El `budget` sale de un stat del actor — `MovementRange` (`IModifiable<int>`) declarado en §4.2. Modificadores de §3 lo afectan normalmente:

- "Slow" (encantamiento hielo del GDD) → `Modifier<int>` con `Direction = Intrinsic`, `Lifetime = Turns`, `Duration = 1`, operación `Add(-1)`.
- "Haste" → operación inversa.
- "Root" → un `Modifier<int>` con `Operation = Override(0)` deja el budget en 0.

#### B.4 Movimiento como acción

El jugador mueve invocando un `EffMove : BaseEffect, IUsesSelection`. El efecto:

1. Resuelve el budget actual leyendo el stat.
2. Pide una selección al `SelectionController` (§11) con un `TargetQuery = TQ_ReachableTiles` (§11.2b) que internamente llama `IMovementService.GetReachableTiles` via `ServiceLocator`. El highlight visual es un `IUsesGridSelection` normal.
3. Con el tile elegido, llama `IMovementService.FindPath` y luego `MoveAlongPath`.
4. El movimiento es una acción registrada en §12.6 con `BlockOnRepeat = false` — el jugador puede mover varias veces en el mismo turno (única manera de escapar según el GDD).

#### B.5 Animación vía feedback

`MoveAlongPath` **no toca el Animator directamente**. Construye un `FeedbackRequest` de secuencia (§10.8) con steps:

- `InlineAnimation` con trigger `"Walk"` → arranca la animación de caminata en loop.
- Por cada tile del path, un step `InlineWait` de `tilesPerSecond` segundos, más un `FeedbackRef` SFX de "pasos".
- Al final del path, un `InlineAnimation` con trigger `"Idle"` → vuelve al estado idle.

Esto mantiene §10 como único dueño del pipeline de feedback y evita acoplar el `MovementService` al stack de animación. Si más adelante el movimiento necesita dash/slide/teleport, solo se cambia la secuencia de feedback — el servicio sigue igual.

#### B.6 Colisión y bloqueo

Si durante el avance por el path un tile se bloquea (otra entidad entró, una puerta se cerró), el servicio:
1. Para el movimiento.
2. Dispara `OnMovementBlocked(mover, blocker)`.
3. Deja al mover en el último tile válido alcanzado.

El caller decide si reintentar o abortar la acción completa (en cuyo caso el `EffMove` devuelve `false` y el `TurnManager` no marca la acción como usada — el `ActionTag` queda libre para reintentar).

#### B.7 `GridCoord` vs `TargetRef`

- `GridCoord` (§B) — coordenada lógica `(x, y)` de la grilla. Usada por el pathfinding y el movimiento.
- `TargetRef` (§11) — envoltorio que puede representar un slot **o** una entidad, usado por el sistema de selección de efectos.

Un `TargetRef.Slot(slotId)` se convierte a `GridCoord` via el `GridManager` cuando hace falta. Los dos coexisten — el sistema de selección (§11) trabaja con `TargetRef` porque los efectos pueden targetear entidades por guid; el movimiento trabaja con `GridCoord` porque siempre es tile‑to‑tile.

**Cross‑ref.** §4.2 (`MovementRange` stat), §3 (modifiers sobre movimiento), §8 (`EffMove`), §10 (feedback sequences driven by movement), §11 (selection pipeline), §12.6 (movimiento como `ActionDefinitionSO` no‑repetible), §13 (grid del `RoomInstance`), §I (`IGridManager`).

---

### §C. Craps — Mini‑juego de apuesta (overlay de combate)

> El GDD describe una mecánica de apuesta de dados **exclusiva de combate** donde el jugador apuesta oro a un combo y lo intenta ejecutar en una tirada. Se modela como un **`PhaseOverlay.Craps`** (§12.0) que suspende el `CombatTurnFSM` mientras el mini‑juego está activo, y como un servicio transitorio que reusa el `CombinationDetector` del §5 y el sistema de efectos del §8. Craps **no** es un sistema nuevo de combate, es "otra manera de disparar la evaluación de combos" — pero sólo disponible dentro de un combate.

#### C.1 `CrapsSessionService`

Servicio que vive solo durante la sesión — se registra al pushear el overlay Craps y se des‑registra al popearlo:

```csharp
public interface ICrapsSessionService
{
    CrapsSession Current { get; }

    void StartSession(Guid playerGuid, CrapsConfigSO config);
    void PlaceBet(int stake, BaseComboSO predictedCombo);
    void Roll();
    void Resolve();
    void EndSession();
}

public class CrapsSession
{
    public Guid SessionId;
    public Guid PlayerGuid;
    public CrapsConfigSO Config;
    public int Stake;
    public BaseComboSO PredictedCombo;
    public CrapsOutcome Outcome;
    public IReadOnlyList<int> FinalRoll;
    public int Payout;
}

public enum CrapsOutcome { Pending, Won, Lost, Pushed }
```

#### C.2 `CrapsConfigSO`

Referenciado desde el `PropEntitySO` de la mesa de craps (un prop interactuable en la sala de combate). Todo lo numérico sale del config — el servicio no hardcodea nada.

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Craps/Craps Config")]
public class CrapsConfigSO : SerializedScriptableObject
{
    [Title("Identity")]
    [ValueDropdown(nameof(GetCrapsConfigIds))]
    public string CrapsConfigId;

    [Title("Allowed bets")]
    public List<int> AllowedStakes = new() { 10, 25, 50 };

    [InfoBox("Combos ofrecidos como apuesta en esta mesa. El jugador elige uno.")]
    public List<BaseComboSO> OfferedCombos = new();

    [Title("Payouts")]
    [InfoBox("Multiplicador aplicado al stake cuando el combo apostado matchea exactamente.")]
    public float WinMultiplier = 2f;

    [Title("Effects")]
    [OdinSerialize] public EffectData OnWin  = new();     // ej: EffModifyIntAttribute(Gold, +stake*mult)
    [OdinSerialize] public EffectData OnLose = new();     // ej: EffModifyIntAttribute(Gold, -stake)
    [OdinSerialize] public EffectData OnPush = new();     // opcional — devolver el stake tal cual

#if UNITY_EDITOR
    private static IEnumerable<string> GetCrapsConfigIds() =>
        ServiceLocator.TryGetService<CrapsCatalogSO>(out var cat) ? cat.AllIds : Array.Empty<string>();
#endif
}
```

#### C.3 Trigger — prop interactuable en combate

Craps se dispara interactuando con un **prop de mesa de craps** durante `GamePhase.Combat`. La mesa es un `PropEntitySO` con `Category = CrapsTable`, spawneada en ciertas salas de combate desde el `PropPoolSO` (§13.4) o como parte de un setup fijo del `RoomSO`.

```
CrapsTablePrefab (spawned desde PropEntitySO "CrapsTable")
├── InteractableComponent
│   └── PhaseRules:
│       └── { Phase: Combat, Mode: Prompt, Range: 1,
│              LabelOverride: "Apostar en la mesa" }
└── PropEntitySO.Behaviors:
    └── BaseBehavior(OnInteract, AllowedPhases: Combat)
        └── EffectData:
            ├── PreCondition: PCGold >= config.AllowedStakes.Min()
            └── Effect: EffStartCraps(CrapsConfigRef)
```

**`EffStartCraps`** — effect concreto (§8) que:
1. Registra un `ICrapsSessionService` en `ServiceLocator`.
2. Llama `StartSession(playerGuid, config)`.
3. Llama `phaseService.PushOverlay(PhaseOverlay.Craps)` — esto congela el `CombatTurnFSM`.
4. Dispara `EventManager.Trigger(OnCrapsSessionStarted, sessionId, playerGuid)`.

#### C.4 Flujo

```
1. El player está en GamePhase.Combat, state PlayerInput del CombatTurnFSM.
   Se acerca a la mesa de craps → IInteractionService muestra prompt "Apostar".
   Presiona F → behavior OnInteract dispara EffStartCraps.

2. EffStartCraps:
   └─ registra ICrapsSessionService
   └─ phaseService.PushOverlay(PhaseOverlay.Craps)
   └─ EventManager.Trigger(OnCrapsSessionStarted, sessionId, playerGuid)
   └─ CombatTurnFSM queda congelado (no tickea mientras overlay activo)

3. UI (§D) se suscribe a OnCrapsSessionStarted → push(ScreenId.Craps, payload = sessionId)
   - La screen muestra los AllowedStakes y OfferedCombos
   - El jugador elige stake y combo

4. UI llama ICrapsSessionService.PlaceBet(stake, combo)
   └─ EventManager.Trigger(OnCrapsBetPlaced, sessionId, combo.ComboId, stake)

5. UI muestra botón "Tirar" → ICrapsSessionService.Roll()
   └─ DiceRoller.RollAll(player.DiceBag)                         (§6)
   └─ ContractSheet.EvaluateRoll (§5.3) evalúa qué combo matchea
   └─ Outcome = PredictedCombo matcheó? Won : Lost

6. ICrapsSessionService.Resolve()
   └─ EffectData correspondiente (OnWin / OnLose / OnPush) se ejecuta
      como un Effect normal (§8). El reward/penalty termina siendo
      EffModifyIntAttribute sobre Gold, o lo que el diseñador quiera.
   └─ EventManager.Trigger(OnCrapsResolved, sessionId, outcome, payout)

7. ICrapsSessionService.EndSession()
   └─ ServiceLocator.RemoveService<ICrapsSessionService>()
   └─ phaseService.PopOverlay()   → CombatTurnFSM se descongela
   └─ UI popea ScreenId.Craps
   └─ El combate continúa donde quedó (PlayerInput, con la acción
      de Interact consumida para este turno)
```

#### C.5 Reuso del combo system

Idéntico al draft anterior. El core de Craps reusa la evaluación de combos del `ContractSheet` (§5.3) — el mismo matching que ya usa el combate:

```csharp
public void Resolve()
{
    var player = ServiceLocator.GetService<IPlayerService>().CurrentEntity;
    var matched = player.Sheet.EvaluateRoll(Current.FinalRoll.ToArray());
    Current.Outcome = (matched != null && matched.ComboId == Current.PredictedCombo.ComboId)
        ? CrapsOutcome.Won
        : CrapsOutcome.Lost;

    var effect = Current.Outcome switch
    {
        CrapsOutcome.Won  => Current.Config.OnWin,
        CrapsOutcome.Lost => Current.Config.OnLose,
        _                 => Current.Config.OnPush,
    };
    effect.TryExecute(BuildEffectContext(), BuildPreConditionContext());

    EventManager.Trigger(EventName.OnCrapsResolved, Current.SessionId, Current.Outcome, Current.Payout);
}
```

#### C.6 Interacción con pasivas

La pasiva "**Craps anticipado**" del GDD (Gambler) amplía la disponibilidad de mesas de craps: más salas de combate incluyen el prop `CrapsTable` en su `PropPoolSO`. Se implementa sin tocar el servicio: la pasiva aplica un `Modifier<float>` (`Lifetime = Run`) sobre un weight `CrapsTableSpawnWeight` del pool. El `DungeonManager` al generar la sala lee el weight modificado y decide si spawneear la mesa.

La pasiva no hace una llamada directa al servicio — sigue el patrón estándar de §3 / §4 (pasivas = hooks + efectos).

#### C.7 CrapsConfigSO esperado

Tabla de configuración editable desde engine para cada mesa de craps:

| Campo | Tipo | Propósito | Ejemplo |
|---|---|---|---|
| `CrapsConfigId` | `string` | Identidad única para catálogo/save | `"craps.floor1"` |
| `AllowedStakes` | `List<int>` | Apuestas disponibles en esta mesa | `{ 10, 25, 50 }` |
| `OfferedCombos` | `List<BaseComboSO>` | Combos ofrecidos como apuesta | `{ Pair, ThreeOfKind }` |
| `WinMultiplier` | `float` | Multiplicador del stake al ganar | `2.0` |
| `OnWin` | `EffectData` | Effects al ganar (gold, item, buff) | `EffModifyGold(+stake*mult)` |
| `OnLose` | `EffectData` | Effects al perder (gold, debuff) | `EffModifyGold(-stake)` |
| `OnPush` | `EffectData` | Effects en empate (stake devuelto) | `EffModifyGold(0)` |

**Cross‑ref.** §1.2 (events de craps), §3 (modifiers para pasivas que alteran craps), §5 (`ContractSheet.EvaluateRoll` reusado), §6 (DiceRoller), §8 (effects de reward/penalty), §12.0 (`PhaseOverlay.Craps` + `IPhaseService.PushOverlay`), §13 (props en salas de combate), §D (UI se suscribe a los eventos).

---

### §D. UI architecture + ScreenManager

> La UI la construye el diseñador directamente en el engine (prefabs, canvases, layouts). El código **no** genera UI programáticamente. Lo único que hace el código es gestionar la navegación entre screens y pasar datos a los componentes vía `EventManager`.

#### D.1 Principios

1. **Prefabs autorados en engine.** Cada screen (MainMenu, HUD, Combat, Craps, Inventory, Unlocks, Settings, Reward, GameOver, Victory) es un prefab armado por el diseñador. El código no hace `Instantiate` de textos, layouts, imágenes. No hay `ShopScreen` — los ítems de shop se compran interactuando con props en el piso (§17.F) vía el `ItemInspectView` del HUD (§D.6b).
2. **Stack de screens.** Un `IScreenManager` expone `Push`/`Pop`/`Replace`. La screen top del stack recibe input; las que están debajo siguen vivas pero hidden (o parcialmente visibles si el diseño lo permite — p.e. HUD siempre arriba).
3. **Datos por eventos.** Las screens **nunca** leen managers directamente. Solo se suscriben a `EventManager` y reaccionan. Esto corta el acoplamiento en una sola dirección: los managers publican, la UI consume.
4. **Sin builders programáticos.** No existe un `UIBuilder` que cree elementos en `Awake`. Si una screen necesita sub‑vistas dinámicas (ej: lista de rewards), el diseñador prefabbea las N variantes y el código elige cuál instanciar como child del slot correspondiente.

#### D.2 `IScreenManager`

```csharp
public interface IScreenManager
{
    ScreenId Current { get; }
    IReadOnlyList<ScreenId> Stack { get; }
    bool IsPaused { get; }                                        // true si algún screen del stack pausa gameplay

    void Push(ScreenId id, object payload = null);
    void Pop();
    void Replace(ScreenId id, object payload = null);
    bool IsInStack(ScreenId id);

    event Action<ScreenId, ScreenId, object> OnScreenPushed;    // from, to, payload
    event Action<ScreenId, ScreenId> OnScreenPopped;
    event Action<bool> OnPauseChanged;                           // se dispara cuando IsPaused cambia
}

public enum ScreenId
{
    MainMenu,
    HUD,          // overlay permanente durante gameplay
    Combat,
    Reward,
    Craps,        // overlay UI durante PhaseOverlay.Craps (§17.C)
    Inventory,
    Unlocks,
    Settings,
    GameOver,
    Victory,
    Dialogue,     // walker del DialogueGraphSO (§7.6b)
    Tutorial,     // overlay de tutorial (§22)
    // Shop eliminada — ítems de shop se compran via props en el piso (§17.F)
}
```

El `ScreenManager` concreto mantiene:
- Un `Dictionary<ScreenId, BaseScreen>` con las screens prefabbeadas instanciadas bajo un root canvas (una sola vez, al arrancar el gameplay).
- Un `Stack<ScreenId>` para el orden de push/pop.
- Los events `OnScreenPushed` / `OnScreenPopped` / `OnPauseChanged` expuestos directamente por la interface. **No** hay re‑publicación en `EventManager` — canal único (§1.2.1). Sistemas que reaccionan a cambios de screen (audio duck, input remap, HUD opacity) se suscriben al evento C# del `IScreenManager` que obtienen del `ServiceLocator`.

Se registra en el bootstrap (§1.1.1).

**Política de pausa global.** Cada `BaseScreen` declara un flag `PausesGameplay` (default `false`). El `IScreenManager.IsPaused` es `true` sssi al menos un screen del stack tiene ese flag en `true`. Cuando `IsPaused` transiciona, el manager dispara `OnPauseChanged(bool)` y sistemas interesados reaccionan sin acoplarse al concepto de "screen":

| Sistema | Reacción al entrar pausa | Reacción al salir |
|---|---|---|
| Audio (§17.A) | Música y SFX ambient ducken a 50 %. Oneshot SFX siguen (ej: click del botón que abrió el menú). | Restaura volumen. |
| `IPhaseService` (§12.0) | Congela procesamiento de inputs del FSM de combate — el turno actual queda pegado en su state. Las transiciones de fase encoladas siguen encoladas, no se drenan. | Continúa drenando la queue. |
| `IStatusEffectService` (§20) | No tickea: `OnTurnStarted`/`OnTurnFinished` no se emiten mientras hay pausa. | — |
| `ICutsceneService` (§N) | Pausa el playhead si hay cutscene activa. | Reanuda. |
| Feedback (§10) | Los runners particulares **siguen** corriendo visualmente (VFX no se congelan — Unity timescale no se toca). Sólo se pausa la lógica. | — |
| Animaciones de entity | **No** se pausan (mismo motivo que feedback: visuales siguen). | — |

Screens canónicos con `PausesGameplay = true`: `Settings`, `Inventory`, `Unlocks`, `Tutorial` overlay bloqueante. Screens con `PausesGameplay = false` (UI sobre gameplay activo): `HUD`, `Combat`, `Craps`, `Reward`, `Dialogue` — el diálogo **no** pausa porque puede ocurrir durante combate (un NPC hablándote mid‑turno es legal por §7.6). `GameOver` y `Victory` no pausan pero terminan la run, así que el punto es moot.

#### D.3 `BaseScreen`

```csharp
public abstract class BaseScreen : MonoBehaviour
{
    public abstract ScreenId Id { get; }

    /// <summary>Se llama cuando la screen entra al stack (primera vez o re-push).</summary>
    public virtual void OnPush(object payload) { }

    /// <summary>Se llama cuando la screen sale del stack.</summary>
    public virtual void OnPop() { }

    /// <summary>Se llama cuando la screen vuelve a estar visible (top del stack, o después de pop de otra).</summary>
    public virtual void OnShow() { }

    /// <summary>Se llama cuando otra screen se pushea encima y esta queda oculta.</summary>
    public virtual void OnHide() { }
}
```

Todas las screens prefabbeadas extienden `BaseScreen`. El `IScreenManager` los encuentra por `Id`.

#### D.4 Regla de oro — screens leen solo por eventos

Ejemplo canónico del HUD:

```csharp
public class HUDScreen : BaseScreen
{
    public override ScreenId Id => ScreenId.HUD;

    [SerializeField] private Slider _healthBar;
    [SerializeField] private TMP_Text _healthText;
    [SerializeField] private Slider _energyBar;
    [SerializeField] private TMP_Text _goldText;
    [SerializeField] private TurnQueueView _turnQueue;   // componente autorado en el prefab

    private void OnEnable()
    {
        EventManager.Subscribe(EventName.OnPlayerHealthChanged,  HandleHealth);
        EventManager.Subscribe(EventName.OnPlayerEnergyChanged,  HandleEnergy);
        EventManager.Subscribe(EventName.OnGoldChanged,          HandleGold);
        EventManager.Subscribe(EventName.OnTurnQueueBuilt,       HandleTurnQueue);
    }

    private void OnDisable()
    {
        EventManager.UnSubscribe(EventName.OnPlayerHealthChanged, HandleHealth);
        EventManager.UnSubscribe(EventName.OnPlayerEnergyChanged, HandleEnergy);
        EventManager.UnSubscribe(EventName.OnGoldChanged,         HandleGold);
        EventManager.UnSubscribe(EventName.OnTurnQueueBuilt,      HandleTurnQueue);
    }

    private void HandleHealth(params object[] args)
    {
        // args schema: [Guid entityGuid, int current, int max]   (§1.2)
        var current = (int)args[1]; var max = (int)args[2];
        _healthBar.value = (float)current / max;
        _healthText.text = $"{current}/{max}";
    }

    // ... idem Energy, Gold, TurnQueue
}
```

**Qué NO hace `HUDScreen`**:
- No llama `ServiceLocator.GetService<AttributesManager>()`.
- No referencia al `PlayerEntity` directamente.
- No hace `Update()` con polling de stats.

Todo lo que hace es **suscribirse** a los events que el `AttributesManager` ya publica cuando los stats cambian.

#### D.5 Payloads

Los events que llegan al HUD están documentados en §1.2 con su schema exacto. Los casts son responsabilidad de cada handler, y cada handler debe ser tolerante (log + early return) a parámetros malformados — el objetivo es que un bug en un publisher nunca rompa otra screen.

#### D.6 Sub‑views prefabbeadas

Cuando una screen necesita listas (ej: `RewardScreen` muestra N cards ofrecidas):

1. El diseñador prefabbea `RewardCard.prefab` con todos sus children ya armados.
2. El `RewardScreen` tiene un campo `[SerializeField] private RewardCard _cardPrefab;` y `[SerializeField] private Transform _cardsContainer;`.
3. `OnPush(payload)` donde `payload` es `List<RewardOffer>`:
   ```csharp
   foreach (var offer in offers)
   {
       var card = Instantiate(_cardPrefab, _cardsContainer);
       card.Bind(offer);   // RewardCard tiene su propio Bind(RewardOffer) autorado
   }
   ```

El código instancia el prefab ya hecho — no construye el prefab. Esa es la línea que no se cruza.

#### D.6a `InteractionPromptView`

Vive en el prefab de `HUDScreen` como un slot más autorado por el diseñador (botón + label + canvas group). Expone el prompt de interacción compartido que sirve a todos los targets del `IInteractionService` (§7.7): el mismo botón cambia su texto y estado según el `CurrentTarget`, en vez de tener un botón distinto por tipo de interactable.

```csharp
public class InteractionPromptView : MonoBehaviour
{
    [SerializeField] private Button      _button;
    [SerializeField] private TMP_Text    _label;
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private float       _fadeSeconds = 0.15f;

    private void OnEnable()
    {
        EventManager.Subscribe(EventName.OnInteractionTargetChanged, OnTargetChanged);
        _button.onClick.AddListener(OnClick);
    }

    private void OnDisable()
    {
        EventManager.UnSubscribe(EventName.OnInteractionTargetChanged, OnTargetChanged);
        _button.onClick.RemoveListener(OnClick);
    }

    private void OnTargetChanged(params object[] args)
    {
        var targetGuid = (Guid)  args[0];
        var label      = (string)args[1];    // LocalizedString ya resuelto por el service
        var available  = (bool)  args[2];

        if (targetGuid == Guid.Empty)
        {
            Tween.CanvasGroupAlpha(_group, 0f, _fadeSeconds);       // PrimeTween §16.1
            _button.interactable = false;
            return;
        }

        _label.text = label;
        _button.interactable = available;                           // grayed out si !available
        Tween.CanvasGroupAlpha(_group, 1f, _fadeSeconds);
    }

    private void OnClick() =>
        ServiceLocator.GetService<IInteractionService>().ExecuteCurrent();
}
```

**Reglas.**

- El prefab del HUD contiene **una sola** `InteractionPromptView`. No hay prompts por tipo — un único botón compartido para chests, doors, NPCs, shop items, sensors.
- El botón y la tecla `Interact` del Input System (nueva acción en el map `Gameplay`, default `F`) invocan **el mismo path**: `IInteractionService.ExecuteCurrent()`. La lógica de decidir qué ejecutar y si aplica la tiene el service, no el HUD ni el input bind.
- El HUD sigue siempre arriba (§D.1 — las screens top del stack reciben input pero el HUD es permanente). `InteractionPromptView` simplemente modula su `CanvasGroup.alpha` según el último `OnInteractionTargetChanged`. No push/pop de screens.
- **Localización.** El `label` que llega por el evento ya viene **resuelto** por el `IInteractionService` contra el `LocalizationManager`, así el view no conoce de `LocalizedString` ni de tablas — consume un `string` puro.
- **Input binding.** El map `Gameplay` del `InputActionAsset` (§16.1) gana una acción `Interact` → `Keyboard/F`. Se activa y desactiva por `GamePhase` siguiendo la regla general de §16.1. Bindings re‑asignables persisten por el save system (§15) como el resto.

#### D.6b `ItemInspectView`

Widget del HUD (hermano de `InteractionPromptView`) que muestra información detallada de un shop item al acercarse. Estilo Isaac: tooltip flotante sobre el ítem en el mundo, sin modal ni screen separada.

```csharp
public class ItemInspectView : MonoBehaviour
{
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private TMP_Text   _itemName;
    [SerializeField] private TMP_Text   _description;
    [SerializeField] private TMP_Text   _price;
    [SerializeField] private Image      _icon;
    [SerializeField] private float      _fadeSeconds = 0.15f;

    private void Start()
    {
        EventManager.Subscribe(EventName.OnShopItemTargetChanged, OnTargetChanged);
        _group.alpha = 0;
    }

    private void OnTargetChanged(params object[] args)
    {
        var hasTarget = (bool)args[0];
        if (!hasTarget)
        {
            Tween.CanvasGroupAlpha(_group, 0f, _fadeSeconds);
            return;
        }

        _itemName.text    = (string)args[1];
        _description.text = (string)args[2];
        _price.text       = $"{(int)args[3]}G";
        _icon.sprite      = (Sprite)args[4];
        Tween.CanvasGroupAlpha(_group, 1f, _fadeSeconds);
    }
}
```

**Reglas.**

- Aparece **sólo** cuando el `CurrentTarget` del `IInteractionService` es un prop con `Category = ShopItem` (§17.F). Para otros tipos de interactables, sólo se muestra el `InteractionPromptView` normal.
- El `IShopManagerService` (§17.F) dispara `OnShopItemTargetChanged` cuando detecta que el target actual del interaction service es un shop item, pasando la metadata del ítem como payload.
- No reemplaza al `InteractionPromptView` — ambos coexisten. El prompt dice `[F] Comprar (25G)`, el inspect view muestra nombre, descripción, ícono y precio. Si el jugador no tiene oro suficiente, el prompt se grayed out pero el inspect view sigue visible (el jugador ve qué es y por qué no puede comprarlo).
- El inspect view es un componente del `HUDScreen.prefab`, no un screen del stack.

#### D.7 Feedback vs Screens

Los dos coexisten sin conflicto:

- **Feedback (§10)** = popups transitorios (FloatingNumber, ScreenFlash, Combat VFX, Camera shake). Dura lo que dura el efecto, no hay "stack".
- **Screens (§D)** = pantallas persistentes con input (HUD, menús, shops). Tienen stack, reciben input, se navegan.

Un `EffPlayFeedback` nunca pushea una screen, y un `IScreenManager.Push` nunca dispara un feedback. Si un evento de gameplay necesita las dos cosas (p.e. "matar al boss → VFX + pantalla de victoria"), los dos side‑effects son orthogonales: el combate dispara `OnEntityDestroyed`, el feedback se suscribe y lanza su secuencia, **y** un `VictoryChecker` se suscribe y hace `_screens.Push(ScreenId.Victory, runSummary)`.

#### D.8 Registro en el bootstrap

```csharp
// En ServiceBootstrapSO.RegisterAll()
var screenManager = new ScreenManager(screenRoot);     // screenRoot = canvas root de la scene de gameplay
ServiceLocator.AddService<IScreenManager>(screenManager);

// En el arranque del gameplay
screens.Push(ScreenId.HUD);     // HUD siempre presente durante gameplay
screens.Push(ScreenId.MainMenu); // o la screen inicial
```

**Cross‑ref.** §1.1.1 (bootstrap registra el ScreenManager), §1.2 (events del HUD y de screens + schemas), §10 (feedback es ortogonal a screens), §12.7 (`OnTurnQueueBuilt` alimenta el turn queue view), §A (audio reacciona a cambios de screen — pause music en settings).

### §E. Cámara

> Cámara isométrica scripteada con PrimeTween. No usa Cinemachine (ver §16.3 — decisión resuelta a "no" en v8). Todo el comportamiento se expresa como comandos sobre un `ICameraService` único registrado en `ServiceLocator`, y todo el tuning vive en `CameraConfigSO` para que el diseñador pueda habilitar/deshabilitar cada función sin tocar código.

#### E.1 Responsabilidades

1. **Seguimiento del jugador** — la cámara mantiene al `FollowTarget` (transform del héroe) en un offset configurable.
2. **Rotación snapped a 45°** — en runtime, mientras se sostiene click derecho, el drag horizontal del mouse rota el yaw en pasos discretos de 45°.
3. **Pan libre** — click central + arrastre mueve la cámara libremente. El pan **suspende el seguimiento** hasta que se dispare un recenter explícito.
4. **Zoom** — rueda del mouse, clamp a `[ZoomMin, ZoomMax]`.
5. **Recenter** — tecla (default `F`) o botón de UI vuelven la cámara al jugador con smooth.
6. **Wall occlusion** — las paredes cuya dirección es "cercana" a la cámara se ocultan dinámicamente según el yaw actual.
7. **Floor view (zoom out)** — al cruzar un umbral de zoom, se esconden las paredes/interior de la sala actual y se muestran shells procedurales (silueta) de **todas** las salas del piso en world‑space.
8. **Smoothing universal** — toda rotación, pan, zoom y recenter es interpolado con PrimeTween (§16.1). Nada es instantáneo salvo que el config lo fuerce.
9. **Placement inicial (editor‑only)** — distancia al target, ángulo de pitch y yaw inicial se configuran en editor y **no** se modifican en runtime.
10. **Todo toggleable** — cada función (rotación, pan, zoom, wall occlusion, floor view) puede deshabilitarse desde `CameraConfigSO` sin tocar código.

#### E.2 `ICameraService`

Servicio scripteado registrado en el bootstrap (§1.1.1). La implementación concreta (`CameraService : MonoBehaviour, ICameraService`) vive en la scene de gameplay como un único `Camera` + rig, inicializado desde el `CameraConfigSO` y registrado al despertar.

```csharp
public interface ICameraService
{
    // --- State readonly --------------------------------------------------
    CameraFacing CurrentFacing { get; }   // yaw discreto actual
    float        CurrentZoom   { get; }
    Transform    FollowTarget  { get; }   // null si todavía no hay run activa
    bool         IsPanning     { get; }   // true si el follow está suspendido por pan
    bool         IsFloorView   { get; }   // true si el zoom cruzó el umbral de floor view

    // --- Commands --------------------------------------------------------
    void RotateBy45(bool clockwise);      // snap — dispara tween de yaw
    void PanBy(Vector2 screenDelta);      // suspende follow al primer llamado
    void ZoomBy(float scrollDelta);       // clamp + tween
    void RecenterOnPlayer(bool instant = false);   // recupera follow
    void SetFollowTarget(Transform target);        // llamado por DungeonManager al EnterRoom

    // --- Events (expuestos además vía EventManager §1.2) ----------------
    event Action<CameraFacing> FacingChanged;
    event Action<bool>         FloorViewToggled;
}

public enum CameraFacing
{
    N = 0, NE = 45, E = 90, SE = 135,
    S = 180, SW = 225, W = 270, NW = 315,
}
```

`CameraFacing` como enum con valor = grados del yaw hace trivial el mapeo a tween targets y a `OcclusionMap` (§E.8).

#### E.3 `CameraConfigSO`

SO editor‑authored, registrado en `ServiceBootstrapSO` (§1.1.1) como un settings‑catalog más. Todo aquí debe ser modificable desde el inspector; el código nunca hardcodea ninguno de estos valores.

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Camera Config")]
public class CameraConfigSO : SerializedScriptableObject
{
    // === Placement inicial (editor‑only) ================================
    [Title("Placement — editor only")]
    [InfoBox("Se lee una sola vez al inicializar la cámara. No se modifica en runtime.")]
    public float    DistanceFromTarget = 12f;     // distancia horizontal cámara ↔ jugador
    public float    PitchDegrees       = 45f;     // ángulo hacia abajo (isométrico clásico)
    public CameraFacing StartingFacing = CameraFacing.NE;

    // === Rotation =======================================================
    [Title("Rotation"), ToggleGroup(nameof(EnableRotation))]
    public bool  EnableRotation         = true;
    public float RotationStepDegrees    = 45f;    // snap step
    public float DragPixelsPerStep      = 50f;    // cuánto hay que arrastrar el mouse para disparar un step
    public float RotationTweenSeconds   = 0.25f;
    public Ease  RotationEase           = Ease.OutQuad;

    // === Pan ============================================================
    [Title("Pan"), ToggleGroup(nameof(EnablePan))]
    public bool  EnablePan              = true;
    public float PanSpeed               = 18f;    // unidades world por pixel de delta
    public bool  PanClampToFloorBounds  = true;   // evita salirse del área del piso generado
    public float PanLerpSeconds         = 0.08f;  // suavizado del delta frame a frame

    // === Zoom ===========================================================
    [Title("Zoom"), ToggleGroup(nameof(EnableZoom))]
    public bool  EnableZoom             = true;
    public float ZoomMin                = 6f;
    public float ZoomMax                = 22f;
    public float ZoomStep               = 1.5f;   // unidades por notch del scroll
    public float ZoomTweenSeconds       = 0.18f;
    public Ease  ZoomEase               = Ease.OutQuad;

    // === Recenter =======================================================
    [Title("Recenter")]
    public bool  EnableRecenterInput    = true;
    public float RecenterTweenSeconds   = 0.4f;
    public Ease  RecenterEase           = Ease.InOutQuad;

    // === Wall Occlusion =================================================
    [Title("Wall Occlusion"), ToggleGroup(nameof(EnableWallOcclusion))]
    public bool  EnableWallOcclusion    = true;
    public float WallFadeSeconds        = 0.2f;
    [InfoBox("Por cada yaw discreto, qué direcciones de pared se ocultan. " +
             "Dejar vacío para no ocultar nada en ese yaw.")]
    [OdinSerialize]
    public Dictionary<CameraFacing, List<WallDirection>> OcclusionMap = new()
    {
        { CameraFacing.N,  new() { WallDirection.S } },
        { CameraFacing.NE, new() { WallDirection.S, WallDirection.W } },
        { CameraFacing.E,  new() { WallDirection.W } },
        { CameraFacing.SE, new() { WallDirection.W, WallDirection.N } },
        { CameraFacing.S,  new() { WallDirection.N } },
        { CameraFacing.SW, new() { WallDirection.N, WallDirection.E } },
        { CameraFacing.W,  new() { WallDirection.E } },
        { CameraFacing.NW, new() { WallDirection.E, WallDirection.S } },
    };

    // === Floor view (zoom out) ==========================================
    [Title("Floor View"), ToggleGroup(nameof(EnableFloorView))]
    public bool  EnableFloorView        = true;
    [InfoBox("Si CurrentZoom >= este valor se activa la vista del piso (sala actual oculta, shells visibles).")]
    public float FloorViewZoomThreshold = 18f;
    public float FloorViewTweenSeconds  = 0.3f;
    public Color ShellColor             = new(0.1f, 0.1f, 0.15f, 0.85f);
}
```

**Regla de toggles.** Cada `[ToggleGroup]` tiene que colapsar todos los sub‑campos cuando el toggle está en `false` — así el diseñador puede apagar una feature completa sin ver los parámetros residuales. Si se apaga `EnableFloorView`, el zoom sigue funcionando pero nunca crea shells ni esconde la sala actual.

#### E.4 Input bindings

Nuevo action map `Camera` en el `InputActionAsset` de §16.1 (`Rollgeon/Input/Rollgeon.inputactions`). Activo durante `GamePhase.Exploration` y `GamePhase.Combat` (§12.0), desactivado durante `GamePhase.Loading`/`GamePhase.GameOver`/`GamePhase.None` y cuando hay un `PhaseOverlay` activo (`Pause`, `Cutscene`, `Craps`).

| Action | Binding | Semántica |
|---|---|---|
| `RotateModifier` | Mouse / Right Button | Mientras se sostiene, la siguiente action está activa. |
| `RotateDrag`     | Mouse / Delta (X)    | Sólo se lee cuando `RotateModifier.IsPressed()`. Se acumula en `_pendingDragPixels` y cada `DragPixelsPerStep` pixels dispara un `RotateBy45`. |
| `PanModifier`    | Mouse / Middle Button | Idem — gate para el pan. |
| `PanDrag`        | Mouse / Delta         | Aplica `PanBy` mientras el modifier está sostenido. |
| `Zoom`           | Mouse / Scroll Y      | `ZoomBy(scroll.y * ZoomStep)`. |
| `Recenter`       | Keyboard / F          | `RecenterOnPlayer(instant: false)`. |

El botón UI "Recenter" (vive en `HUD.prefab` como un `UIButton` normal) dispatcha directamente `ICameraService.RecenterOnPlayer()` vía el binding que arma el HUD en su `OnEnable`. No pasa por `EventManager` porque es una acción local al HUD.

**Nota sobre el drag accumulator.** `RotateBy45` es discreto, pero el input es continuo. El `CameraService` mantiene `_pendingDragPixels` y hace:

```csharp
private void OnRotateDrag(float deltaX)
{
    if (!_config.EnableRotation) return;
    _pendingDragPixels += deltaX;

    while (_pendingDragPixels >= _config.DragPixelsPerStep)
    {
        RotateBy45(clockwise: true);
        _pendingDragPixels -= _config.DragPixelsPerStep;
    }
    while (_pendingDragPixels <= -_config.DragPixelsPerStep)
    {
        RotateBy45(clockwise: false);
        _pendingDragPixels += _config.DragPixelsPerStep;
    }
}
```

Al soltar el modifier (`RotateModifier.canceled`), el accumulator se resetea a 0 — movimientos pequeños no quedan latched para el próximo drag.

#### E.5 Rotation — pipeline

1. `RotateBy45(clockwise)` calcula el `targetYaw = (int)CurrentFacing + sign * 45` y wrappea en `[0, 360)`.
2. Cancela cualquier rotation tween en curso (un handle guardado en `_rotationHandle`).
3. Lanza `Tween.Rotation(transform, target, RotationTweenSeconds, RotationEase)`.
4. Al completarse, actualiza `CurrentFacing`, emite `FacingChanged` (y `OnCameraFacingChanged` en `EventManager`).
5. El listener de wall occlusion (§E.8) reacciona a ese evento.

**Invariante.** `CurrentFacing` siempre refleja el destino del tween activo, no el yaw real del transform frame a frame. Eso evita que el wall occlusion flickee durante la interpolación.

#### E.6 Pan — suspende follow

1. Primera llamada a `PanBy(delta)` en un frame setea `IsPanning = true` y **desconecta** el seguimiento (`_followActive = false`).
2. Cada frame subsiguiente aplica `_desiredPanOffset += delta * PanSpeed * dt` y lo smooth‑lerpea en `LateUpdate` con `PanLerpSeconds`.
3. Si `PanClampToFloorBounds == true`, el offset se clampea contra el bounding box del piso generado (consultado desde `IDungeonManager.GetFloorBounds()` — método nuevo a exponer en §13.6).
4. `RecenterOnPlayer(instant)` tweena el offset a cero, setea `IsPanning = false` y reactiva el follow.
5. El turn start **no** auto‑recentra (decisión de esta versión — opción descartada de propósito). Si después el GD lo pide, se agrega como toggle del config.

#### E.7 Zoom

`ZoomBy(scroll)` ajusta `_targetZoom = Clamp(_targetZoom + scroll * ZoomStep, ZoomMin, ZoomMax)` y tweena hacia ese valor con `ZoomTweenSeconds` / `ZoomEase`. La implementación del zoom depende del tipo de cámara:

- **Cámara ortogonal** (recomendado para isométrica pura): interpola `Camera.orthographicSize`.
- **Cámara perspectiva**: interpola la distancia del rig al target (el `DistanceFromTarget` del config es el valor inicial, el zoom lo modula).

`CameraConfigSO.IsOrthographic` (bool) puede agregarse si queremos soportar ambos — por ahora se asume ortogonal.

**Gate del floor view.** Cada update del zoom compara `_targetZoom` contra `FloorViewZoomThreshold`. Si cruza el umbral en cualquier dirección, dispara el transition de §E.9.

#### E.8 Wall occlusion

Cada pared del `RoomPrefab` tiene un `WallOccluder` component:

```csharp
public enum WallDirection { N, NE, E, SE, S, SW, W, NW }

public class WallOccluder : MonoBehaviour
{
    [EnumToggleButtons] public WallDirection Direction;
    [SerializeField] private Renderer[] _renderers;  // populado en OnValidate

    private void OnValidate()
    {
        if (_renderers == null || _renderers.Length == 0)
            _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
    }

    public void SetHidden(bool hidden, float fadeSeconds)
    {
        float targetAlpha = hidden ? 0f : 1f;
        foreach (var r in _renderers)
            Tween.MaterialAlpha(r.material, targetAlpha, fadeSeconds);
    }
}
```

**Regla.** `WallOccluder` es agnóstico del service. El que lo controla es el `CameraService`, que en `FacingChanged`:

1. Pide al `IDungeonManager` la `RoomInstance` actual.
2. Itera `GetComponentsInChildren<WallOccluder>(roomInstance.SpawnedPrefab)`.
3. Para cada uno, resuelve `hidden = config.OcclusionMap[CurrentFacing].Contains(occluder.Direction)`.
4. Llama `SetHidden(hidden, config.WallFadeSeconds)`.

**Placement de `WallOccluder`s.** El diseñador los mete en las paredes (o grupos de paredes) del `RoomPrefab` durante la autoría del prefab. Una pared = un `WallOccluder`. Si un prefab de sala tiene 4 paredes cardinales, tiene 4 `WallOccluder`s con `Direction` = N/E/S/W. Para formas más complejas (L‑shape, salas diagonales), el diseñador decide qué `WallDirection` corresponde y el `OcclusionMap` del config define el comportamiento.

**`OcclusionMap` es editable.** El default del config es simétrico (ver §E.3), pero el diseñador puede cambiarlo si el feel pide otra cosa (ej: ocultar 3 paredes en ángulos diagonales).

#### E.9 Floor view — shells procedurales

Cuando el zoom cruza `FloorViewZoomThreshold`:

1. `CameraService` emite `FloorViewToggled(true)`.
2. Se dispara una transition de `FloorViewTweenSeconds`:
   - `SpawnedPrefab` de la **sala actual** → fade out (alpha 0).
   - Shells del piso → fade in.
3. Al salir (cruzar el umbral para abajo):
   - Shells → fade out.
   - `SpawnedPrefab` actual → fade in.

**De dónde salen las shells.** Shell procedural desde el bounding box. El `DungeonManager.GenerateFloor` (§13.6) lo hace una sola vez al generar el piso:

```csharp
// Pseudo, completo en §13.6
foreach (var roomInstance in floor.Rooms)
{
    var bounds = roomInstance.Template.RoomPrefab
                     .GetComponent<RoomLayout>().Bounds;   // §13.3
    var shell = CreateShellBox(bounds, config.ShellColor);
    shell.transform.position = roomInstance.WorldPosition;
    shell.SetActive(false);                                  // default hidden
    floor.FloorShells[roomInstance.InstanceId] = shell;
}
```

`CreateShellBox` es un helper interno del `CameraService` (o del `DungeonManager`, por cercanía al código que lo usa) que arma un `GameObject` con un `MeshFilter` + `MeshRenderer` usando un cube mesh escalado al `bounds.size`, con un material transparente definido por `ShellColor`. Cero asset manual por sala.

**Regla.** Las shells **no** son prefabs autorados. Son mesh runtime generados procedurales. Si el diseñador quiere shells más elaboradas por sala, se puede agregar un `ShellPrefab` override en `RoomSO` más adelante, pero v8 de este doc lo deja fuera — procedural alcanza.

**Posicionamiento.** Las shells se ubican según la topología del piso (`RoomInstance.WorldPosition` — que `DungeonManager` ya resuelve al generar el piso). Al zoomear out, el jugador ve el piso como un mapa 3D en world‑space, no como un overlay UI.

#### E.10 Integración con otros sistemas

- **§1.1.1 `ServiceBootstrapSO`** — `CameraConfigSO` se registra como servicio para que el `CameraService` lo consulte al despertar.
- **§1.2 `EventManager`** — `OnCameraFacingChanged`, `OnCameraFloorViewToggled`, `OnCameraRecentered`. Schemas documentados al lado del enum `EventName` siguiendo la convención de §1.2.
- **§10 `FeedbackManager`** — camera shake deja de manipular el `Transform` de la cámara directo. En su lugar, el feedback dispara `ICameraService.Shake(amplitude, duration)` (método a agregar en una sub‑versión) o emite un `OnCameraShakeRequested` que el service consume. Queda marcado como TODO v8.
- **§12 `TurnManager`** — no fuerza recenter al empezar turno del jugador (decisión §E.6). Si en el futuro se pide, agregar `CameraConfigSO.AutoRecenterOnPlayerTurnStart`.
- **§13 `DungeonManager`** — al `EnterRoom`, llama `ICameraService.SetFollowTarget(player.transform)` y (opcional) fuerza un `RecenterOnPlayer(instant: true)` para evitar un smooth largo entre salas.
- **§D `IScreenManager`** — el HUD tiene un botón `RecenterButton` que llama al service directo. Los pop‑ups (pause menu, inventory) no deshabilitan la cámara — sólo desactivan su action map `Camera` via `IInputService.SetMapEnabled("Camera", false)`.
- **§16.1 `Input System`** — map `Camera` nuevo.
- **§15 `SaveSystem`** — el estado runtime de la cámara (zoom, facing, pan offset) **no** se persiste; se regenera al cargar una run desde el `CameraConfigSO`. Si en el futuro se agrega un `CameraSettingsSO : ISaveable` para preferencias de usuario (sensitivity, invert axes), va separado del config general.

#### E.11 Cinemachine — resuelto

El TBD de §16.3 queda resuelto a **descartado**:

- Los requisitos del juego (snap rotate a 45°, wall occlusion direccional, floor culling con shells procedurales, pan clampeado al piso) son lógica custom que Cinemachine no expresa natural — terminaría siendo un wrapper alrededor de comandos scripteados, sin agregar valor.
- PrimeTween (§16.1) ya cubre todo el smoothing con zero allocations.
- Quitar Cinemachine del stack reduce una dependencia, el build size y el área de aprendizaje del equipo.

Si aparece un caso puntual que pide una transición dramática (ej: cutscene de boss, muerte del jugador con zoom épico), se puede resolver con un tween compuesto puntual, sin adoptar Cinemachine como dependencia.

**Cross‑ref.** §1.1.1 (bootstrap registra `CameraConfigSO`), §1.2 (events de cámara), §10 (camera shake ahora pasa por el service), §13.3 (`RoomLayout.Bounds` alimenta las shells), §13.6 (`DungeonManager.GenerateFloor` genera las shells + `GetFloorBounds` para el clamp del pan), §16.1 (map `Camera` del Input System + PrimeTween para smoothing), §16.3 (Cinemachine descartado).

---

### §F. Shop — ítems en el piso (estilo Isaac)

> Los ítems de shop son **objetos físicos en el piso de la sala** — no hay modal de shop ni `GamePhase.Shop`. El jugador camina entre pedestales, se acerca a uno, lee la descripción y precio en el `ItemInspectView` del HUD (§D.6b), y compra con `F` vía el `IInteractionService` (§7.7). Misma ruta que abrir un cofre o forzar una puerta.

#### F.1 `IShopManagerService`

Servicio que coordina la lógica de shop a nivel de sala. Se registra en `ServiceLocator` al entrar a una sala de tipo `Shop` y se desregistra al salir. **No es global** — la shop es estado de sala, no de run (aunque los ítems comprados persisten vía `ShopItemState` de §13.6).

```csharp
public interface IShopManagerService
{
    IReadOnlyList<ShopSlot> Slots { get; }

    void Initialize(RoomInstance room, ShopConfigSO config, ShopPoolSO pool, Random rng);
    void NotifyItemPurchased(string spawnPointId);
    bool CanRestock { get; }
    void Restock(Random rng);
}

public class ShopSlot
{
    public string SpawnPointId;
    public PropEntitySO ItemProp;
    public RewardEntrySO RewardData;
    public int Price;
    public bool Purchased;
}
```

**Responsabilidades:**

- **Inicialización.** Al entrar a una shop room por primera vez, el `DungeonManager` llama `Initialize`. El service toma N spawn points del prefab de la sala (`RoomLayout.RewardSpawnPoints`) y por cada uno rolea un ítem del `ShopPoolSO`. Crea el `ShopItemState` en la `RoomInstance` (§13.6) con `ReservedItemId` y `ReservedPrice`. Si la sala ya fue visitada, los `ShopItemState` ya existen y el service los lee en vez de rolear de nuevo.
- **Spawning.** Por cada slot no purchased, instancia un `PropEntitySO` con `Category = ShopItem` en el spawn point correspondiente. El prefab del prop lleva un `InteractableComponent` con `PhaseRules: { Exploration: Prompt }` y un `BaseBehavior(OnInteract)` que ejecuta la compra.
- **Metadata para UI.** Se suscribe a `OnInteractionTargetChanged` del `IInteractionService`. Si el target actual es un shop item, dispara `OnShopItemTargetChanged` con la metadata del ítem (nombre, descripción, precio, ícono) para que el `ItemInspectView` (§D.6b) lo muestre.
- **Purchase callback.** El behavior `OnInteract` del shop item ejecuta `EffPurchaseShopItem(spawnPointId)`, que cobra gold, agrega el ítem al inventario, marca `ShopItemState.Purchased = true`, destruye el prop visual, y notifica al service vía `NotifyItemPurchased`.
- **Restock.** Si la sala tiene un prop `RestockMachine` (§F.5), el jugador puede pagar un costo para re‑rolear los ítems no comprados. `Restock` limpia los slots no purchased, rolea nuevos ítems del pool, actualiza los `ShopItemState`, y re‑instancia los props.

#### F.2 `ShopPoolSO`

Pool editable desde engine con ítems pesados por weight. El diseñador lo configura arrastrando rewards y seteando pesos.

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Shop/Shop Pool")]
public class ShopPoolSO : SerializedScriptableObject
{
    [Title("Items disponibles")]
    [InfoBox("Pool de ítems de shop. Se rolea uno por spawn point al entrar " +
             "a la sala por primera vez. Los pesos son relativos.")]
    [ListDrawerSettings(ShowFoldout = false, DraggableItems = true)]
    [OdinSerialize]
    public List<WeightedShopItem> Items = new();

    public ShopRollResult Roll(Random rng)
    {
        var item = WeightedRandom.Pick(Items, rng);
        return new ShopRollResult { Reward = item.Reward, BasePrice = item.BasePrice };
    }
}

[Serializable]
public struct WeightedShopItem
{
    public RewardEntrySO Reward;
    [MinValue(0)] public float Weight;
    [MinValue(1)] public int BasePrice;

    [Title("Rarity filter")]
    [InfoBox("Si no vacío, este item sólo aparece en pisos con rarity >= este valor.")]
    [MinValue(0)] public int MinFloorDepth;
}

public struct ShopRollResult
{
    public RewardEntrySO Reward;
    public int BasePrice;
}
```

#### F.3 `ShopConfigSO`

Config global de la shop para el run actual. Referenciado desde el `RulesetSO` (§14.7).

**Autoridad de pricing — regla explícita.** Hay dos SOs involucrados en la pricing y la confusión es natural:

| Campo | Autoridad | Por qué |
|---|---|---|
| `BasePrice` (por ítem) | `ShopPoolSO` | Depende del pool y del piso — un potion "común" vale menos que uno "legendario". El diseñador piensa en rarity por piso. |
| `PriceMultiplier` / `PriceVariance` | `ShopConfigSO` (via `RulesetSO`) | Globals del ruleset — hardcore x1.5, arcade x1.0. Un mismo pool se reescala entre rulesets sin duplicar assets. |
| `FirstPurchaseDiscountPercent` | `ShopConfigSO` | Regla de balance global, no del pool. |
| `RestockCost` / `MaxRestocks` | `ShopConfigSO` | Idem — propiedad del ruleset, no del pool. |
| Slots (`MaxItemSlots`) | `ShopConfigSO` | Propiedad del modo, no del contenido. |

**Fórmula final** aplicada por `IShopManagerService.Roll(...)`:
`finalPrice = pool.BasePrice * cfg.PriceMultiplier * rng.Range(1-cfg.PriceVariance, 1+cfg.PriceVariance)`.

El `FirstPurchaseDiscountPercent` se aplica **por instancia de shop room** (no global por sesión). Cada `RoomInstance` de tipo shop lleva un flag `HasHadFirstPurchase : bool` en su `ObjectStates`. Al re‑entrar a la misma shop tras comprar un ítem, el flag queda `true` y el descuento no se aplica más — cerrando el exploit de abrir/cerrar shop para re‑disparar el descuento.

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Shop/Shop Config")]
public class ShopConfigSO : SerializedScriptableObject
{
    [Title("Pricing")]
    [InfoBox("Multiplicador global de precios. Se aplica sobre el BasePrice del pool.")]
    [MinValue(0.1f)] public float PriceMultiplier = 1.0f;

    [InfoBox("Varianza aleatoria de precio: precio final = basePrice * mult * rng(1-var, 1+var).")]
    [Range(0, 0.5f)] public float PriceVariance = 0.1f;

    [Title("Slots")]
    [InfoBox("Cantidad de spawn points de shop items en las salas de tipo Shop. " +
             "Si el prefab tiene más spawn points que este número, se usan los primeros N.")]
    [MinValue(1)] public int MaxItemSlots = 4;

    [Title("Restock")]
    [ToggleLeft] public bool AllowRestock = true;
    [ShowIf(nameof(AllowRestock))]
    [MinValue(0)] public int RestockCost = 5;
    [ShowIf(nameof(AllowRestock))]
    [MinValue(0)] public int MaxRestocks = 1;

    [Title("Discount")]
    [InfoBox("Si > 0, el primer ítem comprado en cada shop tiene este % de descuento.")]
    [Range(0, 100)] public int FirstPurchaseDiscountPercent = 0;
}
```

#### F.4 Ejemplo canónico — shop item en el piso

```
ShopItemPedestal (spawned por IShopManagerService desde PropEntitySO "ShopPedestal")
├── Visual: mesh del ítem sobre un pedestal + particle glow
├── InteractableComponent
│   └── PhaseRules:
│       └── { Phase: Exploration, Mode: Prompt, Range: 1,
│              LabelOverride: "[F] Comprar (25G)" }
└── PropEntitySO.Behaviors:
    └── BaseBehavior(OnInteract, AllowedPhases: Exploration)
        └── EffectData:
            ├── PreCondition: PCGold >= resolvedPrice
            ├── Effect: EffDeductGold(resolvedPrice)
            ├── Effect: EffAddItemToInventory(rewardId)
            ├── Effect: EffConsumeProp   // marca ShopItemState.Purchased = true
            └── Effect: EffNotifyShopPurchase(spawnPointId)
```

El jugador se acerca al pedestal → `IInteractionService` muestra el prompt `[F] Comprar (25G)` + el `ItemInspectView` muestra nombre, descripción, ícono y precio. Si no tiene gold suficiente, el prompt aparece grayed out. Al presionar `F`, el behavior corre los effects en orden, cobra el gold, agrega el ítem, consume el prop (desaparece del piso), notifica al `IShopManagerService`.

#### F.5 Restock machine

Prop opcional en salas de shop que permite re‑rolear los ítems no comprados.

```
RestockMachine (PropEntitySO "RestockMachine")
├── InteractableComponent
│   └── PhaseRules:
│       └── { Phase: Exploration, Mode: Prompt, Range: 1,
│              LabelOverride: "[F] Restock (5G)" }
└── PropEntitySO.Behaviors:
    └── BaseBehavior(OnInteract, AllowedPhases: Exploration)
        └── EffectData:
            ├── PreCondition: PCGold >= restockCost
            ├── PreCondition: PCShopRestocksRemaining > 0
            ├── Effect: EffDeductGold(restockCost)
            └── Effect: EffRestockShop
```

`EffRestockShop` llama `IShopManagerService.Restock(rng)` que limpia los slots no purchased, rolea nuevos ítems, actualiza los `ShopItemState`, y re‑instancia los props visuales.

#### F.6 Tablas de configuración esperadas

| SO | Quién lo edita | Propósito | Campos clave |
|---|---|---|---|
| `ShopPoolSO` | Diseñador (por piso o por tema) | Pool pesado de ítems disponibles en shops de un piso | `List<WeightedShopItem>` con `Reward`, `Weight`, `BasePrice`, `MinFloorDepth` |
| `ShopConfigSO` | Diseñador (global, referenciado desde `RulesetSO` §14.7) | Tuning global de pricing, slots, restock | `PriceMultiplier`, `PriceVariance`, `MaxItemSlots`, `AllowRestock`, `RestockCost`, `MaxRestocks`, `FirstPurchaseDiscountPercent` |
| `PropEntitySO` "ShopPedestal" | Diseñador (template de prop) | Prefab visual + behavior de compra | `Category = ShopItem`, `SingleUse = true`, behavior `OnInteract` con effects de purchase |
| `PropEntitySO` "RestockMachine" | Diseñador (template de prop) | Prefab visual + behavior de restock | behavior `OnInteract` con `EffRestockShop` |

#### F.7 Integración con save (§15) y §13.6

- Los `ShopItemState` (`ReservedItemId`, `ReservedPrice`, `Purchased`) ya se persisten en `RoomInstance.ObjectStates` — el save system los captura gratis.
- Al re‑entrar a una shop room, el `IShopManagerService` lee los `ShopItemState` existentes, no re‑rolea. Los ítems purchased no se instancian.
- El restock counter se guarda como campo del `RoomInstance` (o como un `RoomObjectState` extra).

#### F.8 Integración con pasivas y modificadores

- **Descuento de pasiva** (ej: "Comerciante" del GDD) — `Modifier<float>` con `Lifetime = Run` sobre un atributo `ShopPriceMultiplier` del player. El `IShopManagerService.Initialize` aplica el multiplicador al calcular el `resolvedPrice` de cada slot.
- **Restock gratis** (ej: "Suerte del apostador") — `Modifier<int>` que incrementa `MaxRestocks` o reduce `RestockCost` a 0.
- Las pasivas no hacen llamadas directas al service — siguen el patrón estándar de §3 / §4.

**Cross‑ref §F.** §7.7 (`IInteractionService` maneja la interacción con shop items), §8 (effects de compra/restock), §12.0 (no hay `GamePhase.Shop` — ítems en el piso estando en `Exploration`), §13.4 (`ShopPoolSO` análogo a `PropPoolSO`), §13.6 (`ShopItemState` persiste el estado de cada slot), §14.7 (`RulesetSO` referencia `ShopConfigSO`), §15 (save system captura `RoomInstance` con los `ShopItemState`), §D.6b (`ItemInspectView` muestra metadata del ítem).

---

### §G. `IPlayerService` — acceso centralizado al jugador

Servicio global que expone la `Entity` del jugador activo sin que cada sistema tenga que mantener su propia referencia. Registrado como `Global` en el bootstrap (§1.1.1).

```csharp
public interface IPlayerService
{
    Guid    PlayerGuid    { get; }
    Guid    RunId         { get; }
    Entity  CurrentEntity { get; }

    void SetPlayer(Entity player, Guid runId);
    void ClearPlayer();

    event Action<Entity> OnPlayerSet;
    event Action         OnPlayerCleared;
}
```

**Responsabilidades:**

- Expone `PlayerGuid` y `RunId` para que cualquier sistema los lea sin lookup.
- `SetPlayer` se llama en el flujo de inicio de run (§4.5) después de construir la `Entity` del héroe.
- `ClearPlayer` se llama al terminar la run (`OnRunEnd`).
- Los consumidores principales: `DeathWatchService` (§12.0.8), `StatusIconsView` (§20.5), `HUDScreen` (§D.4), `IInteractionService` (§7.7), `DamagePipeline` (§12.2), `CrapsSessionService` (§17.C).

**Cross‑ref.** §2.4 (runtime `Entity`), §4.5 (flujo de inicio de run), §12.0.8 (`DeathWatchService` usa `PlayerGuid`).

---

### §H. `PawnRegistryService` — lookup de transforms por GUID

Servicio que mantiene un mapping `Guid → Transform` para las entidades instanciadas en la scene. Registrado con `ServiceScope.Run`.

```csharp
public interface IPawnRegistryService
{
    void Register(Guid entityGuid, Transform pawnTransform);
    void Unregister(Guid entityGuid);
    Transform Get(Guid entityGuid);
    bool TryGet(Guid entityGuid, out Transform pawn);
}
```

**Responsabilidades:**

- Los spawners (§13.4, §13.6) registran cada `Entity` cuando instancian su prefab.
- `FeedbackPositionResolver` (§10.6) usa `TryGet` para resolver `SpawnPosition.AtSource`/`AtTarget`.
- `CameraService` (§E) puede consultar la posición del `FollowTarget` por guid.
- Al destruir una entidad (`EffRemoveRoomEntity`, muerte confirmada), el spawner llama `Unregister`.

**Cross‑ref.** §10.6 (position resolver), §13.4 (spawn de entities), §2.4 (runtime `Entity`).

---

### §I. `GridManager` — grilla de la sala activa

Servicio que expone la grilla lógica de la sala actualmente instanciada. Registrado con `ServiceScope.Run`.

```csharp
public interface IGridManager
{
    Vector2Int GridSize { get; }
    bool IsWalkable(GridCoord coord);
    bool IsOccupied(GridCoord coord);
    Guid GetOccupant(GridCoord coord);

    void SetOccupied(GridCoord coord, Guid entityGuid);
    void ClearOccupant(GridCoord coord);

    Vector3 GridToWorld(GridCoord coord);
    GridCoord WorldToGrid(Vector3 worldPos);

    void LoadFromRoom(RoomInstance room);    // llamado por DungeonManager al EnterRoom
}
```

**Responsabilidades:**

- `LoadFromRoom` parsea el `GridSnapshot` del `RoomLayout` (§13.3) y construye la grilla interna con celdas walkable/blocked.
- `IMovementService` (§B) delega a `IGridManager` para BFS y pathfinding.
- `SelectionController` (§11.4) consulta `IsWalkable`/`IsOccupied` para validar targets.
- `FeedbackPositionResolver` (§10.6) usa `GridToWorld` para `SpawnPosition.AtSlot`.
- El `DungeonManager` actualiza occupants al spawnear/mover/destruir entities.

**Cross‑ref.** §B (movement usa la grilla), §10.6 (position resolver), §11 (selection), §13.3 (`RoomLayout.GridSnapshot`).

---

### §J. `IInputService` — gestión de action maps

Servicio que wrappea el `PlayerInput` del Input System para activar/desactivar maps por fase. Registrado como `Global`.

```csharp
public interface IInputService
{
    void SetMapEnabled(string mapName, bool enabled);
    bool IsMapEnabled(string mapName);

    /// <summary>Activa los maps que corresponden a la fase dada y desactiva el resto.</summary>
    void ApplyPhaseProfile(GamePhase phase, PhaseOverlay overlay);

    /// <summary>Persiste los binding overrides como ISaveable (§15).</summary>
    string SaveBindingOverrides();
    void LoadBindingOverrides(string json);
}
```

**Perfiles de maps por fase.** Esta tabla es el **contrato fuente**: para cada combinación `(GamePhase, PhaseOverlay)` define qué maps están activos. `ApplyPhaseProfile` la consulta literalmente. **Regla de mantenimiento**: agregar un nuevo `GamePhase` o `PhaseOverlay` (§12.0.2) requiere agregar su fila acá en el mismo PR — un `ApplyPhaseProfile` que no encuentre match para el par actual tira `InvalidPhaseTransitionException` en Debug y activa **solo `UI`** en Release (fail‑soft para que el jugador no quede sin input).

| GamePhase base | Overlay activo | Maps activos | Maps inactivos |
|---|---|---|---|
| `None` | `None` | `UI` | `Gameplay`, `Camera`, `Dungeon` |
| `Exploration` | `None` | `Gameplay`, `Camera`, `Dungeon`, `UI` | — |
| `Exploration` | `Pause` | `UI` | `Gameplay`, `Camera`, `Dungeon` |
| `Exploration` | `Cutscene` | `UI` (sólo `Cancel` = skip) | `Gameplay`, `Camera`, `Dungeon` |
| `Combat` | `None` | `Gameplay`, `Camera`, `UI` | `Dungeon` |
| `Combat` | `Pause` | `UI` | `Gameplay`, `Camera`, `Dungeon` |
| `Combat` | `Cutscene` | `UI` (sólo `Cancel` = skip) | `Gameplay`, `Camera`, `Dungeon` |
| `Combat` | `Craps` | `UI` | `Gameplay`, `Camera`, `Dungeon` |
| `Loading` | `None` | (ninguno) | todos |
| `GameOver` | `None` | `UI` | `Gameplay`, `Camera`, `Dungeon` |

**Action maps del `InputActionAsset`:**

| Map | Acciones | Bindings default |
|---|---|---|
| `Gameplay` | `Attack`, `Defend`, `Move`, `Interact` (F), `EndTurn`, `UseActiveItem`, `OpenInventory` | Keyboard + Mouse |
| `Camera` | `RotateModifier` (RMB), `RotateDrag` (Mouse Delta X), `PanModifier` (MMB), `PanDrag` (Mouse Delta), `Zoom` (Scroll Y), `Recenter` (F) | Mouse |
| `Dungeon` | `MoveN/S/E/W`, `ConfirmDoor` | WASD / Arrows |
| `UI` | `Navigate`, `Submit`, `Cancel`, `Pause` (Esc) | Keyboard + Gamepad |

**Por qué no hay un `Craps` / `Cutscene` / `Dialogue` map separado.** Los overlays construyen su propia UI de pantalla completa (`CrapsScreen`, `CutsceneScreen`, `DialogueScreen`) y enrutan el input **a través del map `UI`** — `Submit`/`Cancel`/`Navigate` se resuelven contra los botones de la pantalla activa. No hace falta un map dedicado por overlay: la jerarquía `UI → current screen → button callback` es suficiente y evita proliferación de maps. La única excepción es `Cutscene`, que solo deja `Cancel` habilitado (para skip) — esto lo hace la pantalla misma ignorando el resto de las acciones del map, no el `IInputService`.

**Dialogue y Shop no son overlays.** Ambos corren durante `GamePhase.Exploration` sin overlay — `Dialogue` es un `Screen` que se pushea al `ScreenManager` (§17.D), `Shop` son props del piso que se interactúan vía `IInteractionService` (§17.F). Por eso no aparecen en la tabla de maps: no hay transición de fase cuando abrís un diálogo con un NPC, simplemente el `DialogueScreen` bloquea el input gameplay a nivel de UI. Si en el futuro se quisiera que un diálogo **sí** congele la fase base (p. ej. un jefe que habla mid-combat), se promovería a `PhaseOverlay.Dialogue` y se agregaría fila a la tabla.

**Integración.** `IInputService` se suscribe a `OnPhaseEnter` / `OnOverlayPushed` / `OnOverlayPopped` (§12.0) y llama `ApplyPhaseProfile(current.Phase, current.Overlay)` automáticamente. Si un listener externo llama `SetMapEnabled` directamente mientras hay una fase activa, el cambio **se pisa** al siguiente evento de fase — `SetMapEnabled` solo se usa para ajustes temporales dentro de la misma fase (p. ej. deshabilitar el map `Camera` durante la animación de un boss). Los bindings re-asignables se persisten como `ISaveable` (§15) vía `SaveBindingOverridesAsJson()` del Input System.

**Cross‑ref.** §12.0 (fases y overlays), §15 (save de bindings), §16.1 (Input System package), §E.4 (map Camera).

---

### §K. Scene Management — carga aditiva async

> Arquitectura de scenes del proyecto. Cada "capa" del juego es una scene aditiva independiente. No hay una scene monolítica — los managers globales viven en una scene persistente, la UI en otra, y cada piso del dungeon en una scene descargable.

#### K.1 Layout de scenes

| Scene | Tipo | Contiene | Lifecycle |
|---|---|---|---|
| `00_Bootstrap` | Startup | `GameBootstrap` (§1.1.2). Se auto-destruye después de `RegisterAll`. | Se carga una vez y se descarga. |
| `01_Persistent` | Aditiva, nunca se descarga | Managers globales (`AudioManager`, `IScreenManager`, `IInputService`, `IPlayerService`, `IPhaseService`, cámaras). | Vive toda la sesión. |
| `02_UI` | Aditiva, nunca se descarga | Canvas root con todas las `BaseScreen` prefabbeadas (§D). Exclusive: no comparte canvas con scenes de gameplay. | Vive toda la sesión. |
| `03_MainMenu` | Aditiva, descargable | Escenografía del menú principal. | Se carga al arrancar, se descarga al iniciar una run. Se re‑carga al volver del gameplay. |
| `Floor_XX` | Aditiva, descargable | Prefabs de salas del piso actual (`RoomPrefab` instanciados por `DungeonManager`). | Se carga async al generar un piso, se descarga al avanzar al siguiente o al terminar la run. |

#### K.2 `ISceneService`

Servicio registrado como `Global`. Wrappea `SceneManager.LoadSceneAsync` / `UnloadSceneAsync` con API tipada.

```csharp
public interface ISceneService
{
    /// <summary>Carga una scene aditiva async. Dispara OnSceneLoaded al completar.</summary>
    IEnumerator LoadSceneAsync(string sceneName, Action onComplete = null);

    /// <summary>Descarga una scene aditiva async.</summary>
    IEnumerator UnloadSceneAsync(string sceneName, Action onComplete = null);

    /// <summary>Transición de piso: descarga Floor actual, carga el nuevo, con loading screen.</summary>
    IEnumerator TransitionFloor(string currentFloorScene, string nextFloorScene);

    bool IsSceneLoaded(string sceneName);

    event Action<string> OnSceneLoaded;
    event Action<string> OnSceneUnloaded;
}
```

#### K.3 Flujo de transición de piso

```
1. IPhaseService.ReplacePhase(GamePhase.Loading)         // bloquea input
2. IScreenManager.Push(ScreenId.Loading, progress: 0f)   // pantalla de carga
3. ISceneService.UnloadSceneAsync("Floor_03")             // descarga piso actual
4. DungeonManager.GenerateFloor(layout, seed)             // genera topología + shells
5. ISceneService.LoadSceneAsync("Floor_04")               // carga piso nuevo
6. DungeonManager.EnterRoom(startRoom)                    // instancia primera sala
7. ICameraService.SetFollowTarget(player.Transform)
8. IScreenManager.Pop()                                   // quita loading screen
9. IPhaseService.ReplacePhase(GamePhase.Exploration)      // desbloquea input
```

#### K.4 Reglas

- **`01_Persistent` y `02_UI` nunca se descargan.** Los managers globales y la UI viven toda la sesión. Esto evita re-inicializar servicios, re-instanciar screens, y perder estado de audio/settings.
- **Cada piso es una scene aditiva.** Al avanzar de piso, se descarga la scene del piso anterior y se carga la nueva. El `DungeonManager` instancia los `RoomPrefab` dentro de la scene del piso.
- **La UI no vive en la scene del piso.** Es una scene exclusiva (`02_UI`) para que no se destruya/recree con cada transición de piso. Las screens se registran una vez en el bootstrap.
- **`GamePhase.Loading`** dura exactamente lo que dure la carga async. Input 100% bloqueado. No hay gameplay durante `Loading`.
- **El menú principal es descargable.** Al iniciar una run, `03_MainMenu` se descarga para liberar recursos. Al terminar la run (`GameOver` / `Victory`), se re-carga.

**Cross‑ref.** §1.1.2 (bootstrap scene), §12.0 (`GamePhase.Loading`), §13.6 (`DungeonManager` opera sobre el piso cargado), §D (screens viven en `02_UI`).

---

### §L. Enums comunes no declarados en otras secciones

Enums referenciados por múltiples sistemas pero que no tienen sección propia.

```csharp
/// <summary>Resultado de una run completa.</summary>
public enum RunOutcome { Victory, Death, Abandon }

/// <summary>Resultado de un combate individual.</summary>
public enum CombatOutcome { Victory, Defeat }

/// <summary>Tipo de floating number para el feedback visual (§9.2, §10.7).</summary>
public enum FloatingNumberType { Damage, Heal, Shield, Crit, Miss, Gold, StatusApplied }

/// <summary>Operador de comparación genérico para preconditions y AI conditions.</summary>
public enum ComparisonOp { Equal, NotEqual, Less, LessOrEqual, Greater, GreaterOrEqual }
```

**Cross‑ref.** §1.2 (payloads de `OnRunEnd`, `OnCombatEnd`), §9.2 (`FloatingNumberType` en `FloatingNumberBehaviorValue`), §7.5 (`ComparisonOp` en `AICond_RoundNumber`), §14.5 (`RunOutcome` en `RunRecord`).

---

### §M. Heal Pipeline

> Pipeline centralizada para resolver curación entre entidades. Simétrica al `DamagePipeline` (§12.2) — aplica modificadores `Outgoing` del source y `Incoming` del target para que pasivas como "tus curaciones curan +20%" funcionen sin código especial.

```csharp
public interface IHealPipeline
{
    int Resolve(Entity source, Entity target, int rawHeal, HealContext ctx);
}

public class HealContext
{
    public string SourceId;          // id del ítem / skill / poción que curó
    public bool   IsPercentOfMax;    // true si rawHeal es % del HP max
}

public class HealPipeline : IHealPipeline
{
    public int Resolve(Entity source, Entity target, int rawHeal, HealContext ctx)
    {
        // 1. Resolver % si aplica
        if (ctx.IsPercentOfMax)
        {
            var maxHp = target.Attributes.GetStat<Health>().GetValue<int>();
            rawHeal = Mathf.RoundToInt(maxHp * rawHeal / 100f);
        }

        // 2. Outgoing: modificadores "mis curaciones curan X%" del source
        float outMult = source.Attributes
            .GetAttributeModifiedValue<OutgoingHealMultiplier, float>();
        int afterOut = Mathf.RoundToInt(rawHeal * outMult);

        // 3. Incoming: modificadores "las curaciones que recibo valen X%" del target
        float inMult = target.Attributes
            .GetAttributeModifiedValue<IncomingHealMultiplier, float>();
        int finalHeal = Mathf.RoundToInt(afterOut * inMult);

        // 4. Clamp: no curar más del HP max
        var currentHp = target.Attributes.GetAttributeModifiedValue<Health, int>();
        var maxHpVal  = target.Attributes.GetStat<Health>().GetValue<int>();
        finalHeal = Mathf.Min(finalHeal, maxHpVal - currentHp);

        if (finalHeal > 0)
        {
            target.Attributes.SetAttributeValue<Health, int>(currentHp + finalHeal);
            EventManager.Trigger(EventName.OnHealthChanged,
                target.InstanceId, currentHp + finalHeal, maxHpVal);
        }

        return finalHeal;
    }
}
```

**Stats nuevos** (agregar a la tabla de §4.2 si se quiere granularidad de heal):
- `OutgoingHealMultiplier` (`IModifiable<float>`) — base 1.0. Modificable por pasivas/ítems.
- `IncomingHealMultiplier` (`IModifiable<float>`) — base 1.0. Modificable por debuffs ("heal reduction").

El `EffHeal` (§8.7) delega al `HealPipeline` en vez de setear `Health` directamente:

```csharp
public class EffHeal : BaseEffect, IUsesSelection
{
    public int HealAmount;
    public bool IsPercent;

    public override bool ApplyEffect(EffectContext ctx)
    {
        var pipeline = ServiceLocator.GetService<IHealPipeline>();
        var source = ctx.SourceEntity;
        var target = /* resolved from selection */;
        pipeline.Resolve(source, target, HealAmount, new HealContext
        {
            SourceId = "EffHeal",
            IsPercentOfMax = IsPercent
        });
        return true;
    }
}
```

Registrado con `ServiceScope.Run`. Si el juego no necesita modificadores de heal (decisión de balance), se puede usar una implementación noop que ignore los multiplicadores y solo aplique el raw heal — el contrato de interfaz sigue siendo el mismo.

**Cross‑ref.** §3.2 (tabla de `ModifierDirection` — el ejemplo "tus curaciones curan +20%" ahora tiene pipeline), §8.7 (`EffHeal` delega acá), §12.2 (`DamagePipeline` — simétrico).

---

### §N. Cutscene overlay — flujo básico

`PhaseOverlay.Cutscene` (§12.0.2) suspende la fase base y bloquea input salvo skip. La implementación es un servicio transitorio que se registra al pushear el overlay.

```csharp
public interface ICutsceneService
{
    void PlayCutscene(CutsceneDataSO data);
    void Skip();
    bool IsPlaying { get; }

    event Action OnCutsceneStarted;
    event Action OnCutsceneCompleted;
}

[CreateAssetMenu(menuName = "Rollgeon/Cutscene Data")]
public class CutsceneDataSO : SerializedScriptableObject
{
    public string CutsceneId;
    [OdinSerialize] public List<CutsceneStep> Steps = new();
}

[Serializable]
public class CutsceneStep
{
    public CutsceneStepType Type;    // Dialogue | CameraMove | Wait | FadeIn | FadeOut | PlayFeedback
    [ShowIf("@Type == CutsceneStepType.Dialogue")]
    public DialogueSpeaker Speaker;
    [ShowIf("@Type == CutsceneStepType.Dialogue")]
    public LocalizedString Text;
    [ShowIf("@Type == CutsceneStepType.Wait")]
    public float Duration;
    [ShowIf("@Type == CutsceneStepType.PlayFeedback")]
    [ValueDropdown(nameof(GetFeedbackIds))]
    public string FeedbackId;
}

public enum CutsceneStepType { Dialogue, CameraMove, Wait, FadeIn, FadeOut, PlayFeedback }
```

**Flujo:**

1. Un trigger (comportamiento de boss, quest, evento de sala) llama `EffPlayCutscene(cutsceneData)`.
2. El effect registra `ICutsceneService` y pushea `PhaseOverlay.Cutscene`.
3. La UI pushea `ScreenId.Cutscene` (overlay) con botón "Skip".
4. El service itera los steps. `Dialogue` steps usan el mismo rendering que `DialogueScreen` (§7.6b) pero sin choices.
5. Al terminar (o al skip), el service popea el overlay, la UI popea la screen, y la fase base se reanuda.

**Cross‑ref.** §12.0.2 (`PhaseOverlay.Cutscene`), §7.6b (`DialogueGraphSO` para diálogos complejos — cutscenes usan steps lineales), §10 (feedback steps), §J (`IInputService` — solo UI active durante cutscene).

---

### §O. `IRngService` — aleatoriedad determinista por sub‑stream

Todo call a `UnityEngine.Random` o `new System.Random()` dentro del código de gameplay está **prohibido**. El juego consume aleatoriedad exclusivamente a través del `IRngService`, que expone streams nombrados. Esto apunta a dos problemas concretos:

1. **Testing y debug de balance.** Para reproducir un bug de "este combo casi nunca matchea" o "el shop siempre me ofrece basura", necesitamos poder correr la misma run con los mismos rolls. Un single `Random` global lo hace imposible porque cualquier cambio en el orden de los calls (p. ej., un listener nuevo que consume un rng call antes) mueve todos los rolls downstream.
2. **`RunRecord` reproducible.** Guardar el seed en el `RunRecord` (§14.5) permite re‑jugar una run hasta la decisión del jugador. No hay UI de replay; es una herramienta de QA.

```csharp
public interface IRngService
{
    /// <summary>Seed global de la sesión/run. Persiste en RunRecord.</summary>
    int Seed { get; }

    /// <summary>
    /// Devuelve un generador independiente para un propósito dado. Cada
    /// nombre es un sub-stream determinista — dos calls con el mismo nombre
    /// devuelven el MISMO generador (acumula estado), y dos streams distintos
    /// no se interfieren. Ej: "dice" y "loot" roliean independiente.
    /// </summary>
    System.Random Stream(string name);

    /// <summary>Re-inicializa todos los streams con un seed nuevo.</summary>
    void Reseed(int seed);
}
```

**Implementación canónica:**

```csharp
public class RngService : IRngService, ISaveable
{
    public int Seed { get; private set; }
    private Dictionary<string, System.Random> _streams = new();

    public RngService(int seed) => Reseed(seed);

    public System.Random Stream(string name)
    {
        if (_streams.TryGetValue(name, out var r)) return r;
        // Sub-seed estable a partir del seed global + nombre → misma seed
        // global produce los mismos sub-streams, sin depender del orden de
        // creación.
        var subSeed = unchecked(Seed * 31 + name.GetHashCode());
        var stream  = new System.Random(subSeed);
        _streams[name] = stream;
        return stream;
    }

    public void Reseed(int seed)
    {
        Seed = seed;
        _streams.Clear();
    }

    // ── ISaveable ─────────────────────────────────────────────────────────
    public string SaveKey => "run.rng";
    public object CaptureState() => Seed;
    public void RestoreState(object state) => Reseed((int)state);
}
```

**Streams canónicos:**

| Nombre | Consumidor | Qué rolea |
|---|---|---|
| `"dice"` | `DiceRoller` (§6.3) | Valores de cada dado en cada tirada y reroll. |
| `"loot"` | `IRewardService` (§19), pools weighted | Rewards de cofres y fin de combate. |
| `"shop"` | `IShopManagerService` (§17.F) | Composición y pricing de shops. |
| `"dungeon"` | `DungeonManager` (§13.6) | Layout de pisos, selección de salas. |
| `"enemies"` | Spawners de sala (§13.4) | Enemigos elegidos del `EnemyPoolSO`. |
| `"ai"` | `AIDecisionNode` nodos probabilísticos (§7.5) | Cualquier decisión que no sea determinística. |

**Registro.**
- `IRngService` es `ServiceScope.Run` (§1.1). Se instancia al `RunStart` con seed = timestamp por default, o un seed fijo vía cheat/debug menu.
- El seed se guarda en `RunRecord.Seed` al terminar la run.
- Al restaurar un save mid‑run, `RestoreState` re‑crea los streams con el seed original — pero **los streams pierden el offset acumulado**. En la práctica esto significa que re-cargar una run no garantiza los mismos rolls a partir de ese punto; lo que sí se reproduce es una run corrida end‑to‑end desde el mismo seed. Es el compromiso aceptable en fase de diseño: no hay replay real, sólo rerun.

**Regla.** Cualquier code review flagea `UnityEngine.Random.*` y `new System.Random()` fuera de `RngService`. No hay excepciones — ni siquiera en UI (ej: flavor text random en loading screens usa `Stream("ui")`).

**Cross‑ref §O.** §1.1 (scope `Run`), §6.3 (dice), §7.5 (AI probabilístico), §13.6 (layout procedural), §14.5 (`RunRecord.Seed`), §19 (loot), §17.F (shop).

---

## 18. Sistema de Items e Inventario

> Inspirado en Slay the Spire e Isaac. Los ítems son **pasivos** (siempre activos al tenerlos) o **activos** (equipados en un slot limitado, se activan manualmente). No hay sistema de equipamiento por partes del cuerpo — un ítem se tiene o no se tiene.

### 18.1 `ItemSO`

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Items/Item")]
public class ItemSO : SerializedScriptableObject
{
    [Title("Identity")]
    [ValueDropdown(nameof(GetItemIds))]
    public string ItemId;
    public string DisplayName;
    [TextArea] public string Description;
    public Sprite Icon;
    public ItemRarity Rarity;

    [Title("Type")]
    [EnumToggleButtons]
    public ItemType Type;                              // Passive | Active

    [Title("Passive Effects")]
    [InfoBox("Se aplican automáticamente al obtener el ítem. Se remueven si el ítem se pierde.")]
    [ShowIf("@Type == ItemType.Passive")]
    [ListDrawerSettings(ShowFoldout = false)]
    [OdinSerialize]
    public List<PassiveItemHook> PassiveHooks = new();

    [Title("Active Effects")]
    [InfoBox("Se ejecutan cuando el jugador activa el ítem. Pueden tener cooldown.")]
    [ShowIf("@Type == ItemType.Active")]
    [OdinSerialize]
    public EffectData OnActivate = new();              // §8

    [ShowIf("@Type == ItemType.Active")]
    [InfoBox("Cooldown en turnos. 0 = usable cada turno.")]
    [MinValue(0)] public int Cooldown = 0;

    [Title("Action economy (§12.6)")]
    [ShowIf("@Type == ItemType.Active")]
    [InfoBox("Si true, usar este ítem activo consume un slot del turno — queda registrado en " +
             "TurnManager._actionsUsedThisTurn bajo ActionId 'item.<ItemId>'. Si false, es side-effect " +
             "gratis que no ocupa slot (ej: lectura de lore, teletransporte fuera del turno).")]
    public bool ConsumesAction = true;

    [ShowIf("@Type == ItemType.Active && ConsumesAction")]
    [InfoBox("ActionId que se registra en action economy. Default: item.<ItemId>. " +
             "Permite que múltiples ítems compartan el mismo tag si deben mutuamente excluirse " +
             "(ej: todos los activos de 'potion' podrían share 'item.potion' para que solo uno por turno).")]
    public string ActionId;                            // default resuelto a $"item.{ItemId}"

    public string ResolvedActionId => string.IsNullOrEmpty(ActionId) ? $"item.{ItemId}" : ActionId;

    [Title("Visual")]
    [InfoBox("Prefab opcional para la representación 3D del ítem en el mundo (pedestal, drop).")]
    public AssetReferenceGameObject WorldPrefabRef;    // Addressables §16.2

#if UNITY_EDITOR
    private static IEnumerable<string> GetItemIds() =>
        ServiceLocator.TryGetService<ItemCatalogSO>(out var cat) ? cat.AllIds : Array.Empty<string>();
#endif
}

public enum ItemType { Passive, Active }
public enum ItemRarity { Common, Uncommon, Rare, Legendary }
```

### 18.2 `PassiveItemHook`

```csharp
[Serializable]
public class PassiveItemHook
{
    [InfoBox("Evento que dispara el efecto pasivo. Ej: OnTurnStarted, OnComboMatched, OnDamageResolved.")]
    public EventName TriggerEvent;

    [OdinSerialize]
    public EffectData Effect = new();                  // §8

    [InfoBox("Modificadores que se aplican mientras el ítem esté en el inventario. " +
             "Se remueven automáticamente si el ítem se pierde.")]
    [OdinSerialize]
    public List<PersistentModifierDef> PersistentModifiers = new();
}

/// <summary>
/// Definición de un modificador que se aplica mientras se tenga el ítem.
/// No es un Modifier<T> directo porque necesita saber a qué stat aplicar.
/// </summary>
[Serializable]
public class PersistentModifierDef
{
    [TypeFilter(nameof(GetStatTypes))]
    public Type TargetStat;                            // qué IModifiable afectar
    public ModifierOperation Operation;
    public float Amount;
    public ModifierDirection Direction = ModifierDirection.Intrinsic;
}
```

**Ejemplos estilo Isaac/Slay the Spire:**

| Ítem | Type | Efecto |
|---|---|---|
| "Guante de espinas" | Passive | Hook en `OnDamageResolved` → si el player recibió daño, `EffDealDamage(3)` al atacante |
| "Amuleto de regeneración" | Passive | PersistentModifier: `Health regen +2` via hook en `OnTurnFinished` |
| "Dado de la suerte" | Passive | Hook en `OnRollStarted` → `EffAddIntModifier` en `RerollBudget` (+1 reroll gratuito) |
| "Bomba de humo" | Active | `OnActivate`: `EffAddStatusEffect(Invisible, 2 turnos)`, Cooldown = 5 |
| "Poción de furia" | Active | `OnActivate`: `EffAddFloatModifier(OutgoingDamageMultiplier, ×1.5, 3 turnos)`, Cooldown = 8 |

### 18.3 `IInventoryService`

```csharp
public interface IInventoryService
{
    IReadOnlyList<InventorySlot> PassiveItems { get; }
    IReadOnlyList<InventorySlot> ActiveItems  { get; }

    bool AddItem(ItemSO item);                         // false si activos llenos y es Active
    bool RemoveItem(string itemId);
    bool HasItem(string itemId);
    ItemSO GetItem(string itemId);

    /// <summary>Activa un ítem activo por slot index. false si en cooldown.</summary>
    bool ActivateItem(int activeSlotIndex, EffectContext ctx);

    /// <summary>Tick de cooldowns — llamado por TurnManager.OnTurnFinished.</summary>
    void TickCooldowns();

    int MaxActiveSlots { get; }                        // configurable desde RulesetSO

    event Action<ItemSO, bool> OnItemChanged;          // item, wasAdded
}

[Serializable]
public class InventorySlot
{
    public ItemSO Item;
    public int CurrentCooldown;                        // 0 = listo para usar
}
```

**Reglas.**

- **Pasivos**: sin límite de cantidad. Al obtener un pasivo, el service itera sus `PassiveHooks`, suscribe los effects a los `TriggerEvent`s correspondientes, y aplica los `PersistentModifiers` como `Modifier<T>` con `Lifetime = Permanent`. Al perder el ítem, deshace todo.
- **Activos**: limitados a `MaxActiveSlots` (configurable en `RulesetSO` §14.7, default 1). Si el jugador obtiene un activo y los slots están llenos, se **rechaza la adquisición** — el caller (shop, reward) debe chequear antes de intentar dar el ítem. No hay reemplazo automático por el momento.
- **Cooldown**: los ítems activos con `Cooldown > 0` no pueden activarse mientras `CurrentCooldown > 0`. El `TickCooldowns` decrementa todos los cooldowns al final del turno del jugador.
- **Action economy (§12.6).** `ActivateItem` consulta `ItemSO.ConsumesAction`. Si es `true`, antes de ejecutar el `OnActivate` el service construye un `ActionDefinitionSO` virtual con `ActionId = item.ResolvedActionId` y llama `TurnManager.CanExecute` — si la acción ya fue usada este turno o `ForbiddenActionIds` la bloquea, la activación falla con `reason`. Si es `false` (activo "gratis"), el pipeline salta a la ejecución directa sin tocar `_actionsUsedThisTurn`. Regla: un activo gratis **no** reabre el combate: si ya pasaste a `EnemyBranch` en el FSM, solo activos que no consumen acción son invocables (el botón de activos free queda habilitado; los que consumen slot se bloquean).

```csharp
public bool ActivateItem(int activeSlotIndex, EffectContext ctx)
{
    var slot = _activeItems[activeSlotIndex];
    if (slot.CurrentCooldown > 0) return false;
    var item = slot.Item;
    if (item.Type != ItemType.Active) return false;

    if (item.ConsumesAction)
    {
        var action = new ActionDefinitionSO {
            ActionId = item.ResolvedActionId,
            Type = ActionType.UseItem,
            BackingAsset = item,
            EnergyCost = 0,             // los activos no cobran energy — si hace falta, hooks específicos
            BlockOnRepeat = true,
            Effect = item.OnActivate,
        };
        var tm = ServiceLocator.GetService<TurnManager>();
        if (!tm.CanExecute(action, ctx.SourceGuid, out _)) return false;
        var ok = tm.TryExecute(action, ctx.SourceGuid, ctx);  // registra en _actionsUsedThisTurn
        if (!ok) return false;
    }
    else
    {
        if (!item.OnActivate.TryExecute(ctx, ctx.BuildPreConditionContext())) return false;
    }

    slot.CurrentCooldown = item.Cooldown;
    EventManager.Trigger(EventName.OnActiveItemUsed, ctx.SourceGuid, item.ItemId);
    return true;
}
```

### 18.4 `InventoryState : ISaveable`

```csharp
public class InventoryState : ISaveable
{
    public string SaveKey => "run.inventory";

    public List<string> PassiveItemIds = new();
    public List<InventorySlotSnapshot> ActiveSlots = new();

    public object CaptureState() => new InventorySnapshot { /* ... */ };
    public void RestoreState(object state) { /* ... */ }
}

[Serializable]
public class InventorySlotSnapshot
{
    public string ItemId;
    public int CurrentCooldown;
}
```

### 18.5 `ItemCatalogSO`

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Items/Item Catalog")]
public class ItemCatalogSO : SerializedScriptableObject
{
    [OdinSerialize] private List<ItemSO> _entries = new();

    public IEnumerable<string> AllIds => _entries.Select(e => e.ItemId);
    public ItemSO GetById(string id) => _entries.FirstOrDefault(e => e.ItemId == id);
    public IEnumerable<ItemSO> GetByType(ItemType type) => _entries.Where(e => e.Type == type);
    public IEnumerable<ItemSO> GetByRarity(ItemRarity rarity) => _entries.Where(e => e.Rarity == rarity);
}
```

Pre-cargado en `ServiceBootstrapSO` (§1.1.1). Alimenta los `[ValueDropdown]` de todo SO que referencie un ítem.

**Cross‑ref §18.** §1.1.1 (bootstrap registra `ItemCatalogSO`), §3 (modificadores persistentes de pasivos), §8 (effects de activación y hooks), §14 (unlocks de ítems `ItemUnlockSO`), §15 (save via `InventoryState`), §17.F (shop vende ítems via `EffAddItemToInventory`).

---

## 19. Sistema de Rewards / Loot

> Define qué recibe el jugador al abrir cofres, ganar combates, completar quests o interactuar con eventos. Los rewards no son SOs individuales por valor — un `RewardEntrySO` define el **tipo** y los **rangos/posibilidades**, y el valor concreto se resuelve en runtime combinando datos del nivel, configuración del diseñador y aleatoriedad.

**Capas — autoridad de cada SO** (para evitar confusión con §18 y §17.F):

| SO | Autoridad | Ejemplo |
|---|---|---|
| `ItemSO` (§18.1) | Definición canónica del ítem: rarity, type, hooks. **Única fuente de verdad del ítem.** | `GuanteDeEspinas` |
| `ItemCatalogSO` (§18.5) | Lookup `itemId → ItemSO`. Populator de dropdowns. | — |
| `RewardEntrySO` (acá) | Abstracción "esto se puede dar como reward": gold, ítem (con pool pesado), dado, heal, upgrade, effect custom. **Polimórfico por categoría**, lo consume `IRewardService`. | `CofreComun` (Item weighted pool) |
| `ShopPoolSO` (§17.F.2) | Composición específica de shops: lista de `RewardEntrySO` con precio base. | `ShopFloor1Pool` |

Ningún SO re‑describe los datos de los otros — las referencias son directas (`ItemSO`, no copias). Un ítem cambia en un único sitio (`ItemSO`) y los drops/shops lo reflejan automáticamente via el catalog.

### 19.1 `RewardEntrySO`

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Rewards/Reward Entry")]
public class RewardEntrySO : SerializedScriptableObject
{
    [Title("Identity")]
    [ValueDropdown(nameof(GetRewardIds))]
    public string RewardId;
    public string DisplayName;
    public Sprite Icon;
    public RewardCategory Category;

    [Title("Resolution")]
    [InfoBox("El valor concreto se resuelve en runtime. Cada categoría tiene " +
             "sus propios campos de configuración — los no relevantes se ocultan.")]

    // --- Gold ---------------------------------------------------------------
    [ShowIf("@Category == RewardCategory.Gold")]
    [MinValue(0)] public int GoldMin;
    [ShowIf("@Category == RewardCategory.Gold")]
    [MinValue(0)] public int GoldMax;
    [ShowIf("@Category == RewardCategory.Gold")]
    [InfoBox("Si true, el rango se multiplica por GoldRewardMultiplier del RulesetSO (§14.7) " +
             "evaluado al piso actual. Permite que el diseñador defina un rango base y el " +
             "scaling lo ajuste automáticamente.")]
    public bool ScaleGoldWithFloor = true;

    // --- Item ---------------------------------------------------------------
    [ShowIf("@Category == RewardCategory.Item")]
    [InfoBox("Pool de ítems posibles con pesos. Se rolea uno al resolver.")]
    [OdinSerialize]
    public List<WeightedItemDrop> ItemPool = new();

    // --- Dice ---------------------------------------------------------------
    [ShowIf("@Category == RewardCategory.Dice")]
    [InfoBox("Pool de dados posibles. Se elige uno al resolver.")]
    [OdinSerialize]
    public List<WeightedDiceDrop> DicePool = new();

    // --- Heal ---------------------------------------------------------------
    [ShowIf("@Category == RewardCategory.Heal")]
    [MinValue(0)] public int HealMin;
    [ShowIf("@Category == RewardCategory.Heal")]
    [MinValue(0)] public int HealMax;
    [ShowIf("@Category == RewardCategory.Heal")]
    [InfoBox("Si true, los valores son porcentaje del HP máximo en vez de valor fijo.")]
    public bool HealIsPercent = false;

    // --- Upgrade (§19.6 — mejoras de combos, scope run-only) ---------------
    [ShowIf("@Category == RewardCategory.Upgrade")]
    [InfoBox("Pool de UpgradeSO posibles. Se rolea uno al resolver. Ver §19.6.")]
    [OdinSerialize]
    public List<WeightedUpgradeDrop> UpgradePool = new();

    // --- Effect (genérico) --------------------------------------------------
    [ShowIf("@Category == RewardCategory.Effect")]
    [InfoBox("Un EffectData libre — el diseñador define lo que pase. " +
             "Útil para rewards custom (buff temporal, unlock de pasiva, etc.).")]
    [OdinSerialize]
    public EffectData CustomEffect = new();

    /// <summary>
    /// Resuelve el reward concreto dado el contexto de la run.
    /// </summary>
    public ResolvedReward Resolve(RewardContext ctx)
    {
        return Category switch
        {
            RewardCategory.Gold => new ResolvedReward
            {
                Category = Category,
                GoldAmount = Mathf.RoundToInt(
                    ctx.Rng.Range(GoldMin, GoldMax + 1) *
                    (ScaleGoldWithFloor ? ctx.Ruleset.GoldRewardMultiplier.Evaluate(ctx.FloorIndex) : 1f))
            },
            RewardCategory.Item => new ResolvedReward
            {
                Category = Category,
                Item = WeightedRandom.Pick(ItemPool, ctx.Rng).Item
            },
            // ... análogo para cada categoría
            _ => default,
        };
    }

#if UNITY_EDITOR
    private static IEnumerable<string> GetRewardIds() =>
        ServiceLocator.TryGetService<RewardCatalogSO>(out var cat) ? cat.AllIds : Array.Empty<string>();
#endif
}

public enum RewardCategory { Gold, Item, Dice, Heal, Upgrade, Effect }
```

### 19.2 Weighted drops

```csharp
[Serializable]
public struct WeightedItemDrop
{
    public ItemSO Item;
    [MinValue(0)] public float Weight;
    [MinValue(0)] public int MinFloorDepth;            // no aparece antes de este piso
}

[Serializable]
public struct WeightedDiceDrop
{
    public DiceType Dice;
    [MinValue(0)] public float Weight;
}

[Serializable]
public struct WeightedUpgradeDrop
{
    public UpgradeSO Upgrade;
    [MinValue(0)] public float Weight;
}
```

### 19.3 `RewardContext`

```csharp
public class RewardContext
{
    public Random Rng;
    public int FloorIndex;
    public RulesetSO Ruleset;
    public Guid PlayerGuid;
}
```

### 19.4 `IRewardService`

```csharp
public interface IRewardService
{
    /// <summary>
    /// Resuelve una lista de RewardEntrySO en rewards concretos.
    /// Usado por cofres, fin de combate, quests.
    /// </summary>
    List<ResolvedReward> ResolveRewards(List<RewardEntrySO> entries, RewardContext ctx);

    /// <summary>
    /// Presenta los rewards al jugador (push RewardScreen) y espera la elección
    /// si hay opciones (ej: elegir 1 de 3 ítems).
    /// </summary>
    void PresentRewards(List<ResolvedReward> rewards, RewardPresentMode mode);

    /// <summary>Aplica un reward ya elegido al estado del jugador.</summary>
    void ApplyReward(ResolvedReward reward, Guid playerGuid);
}

public enum RewardPresentMode
{
    ShowAll,            // muestra todos, el jugador los recibe todos
    PickOne,            // elige 1 de N (estilo Slay the Spire)
    PickN,              // elige N de M
}

public struct ResolvedReward
{
    public RewardCategory Category;
    public int GoldAmount;
    public ItemSO Item;
    public DiceType? Dice;
    public int HealAmount;
    public UpgradeSO Upgrade;
    public EffectData CustomEffect;
    public Sprite Icon;
    public string DisplayText;
}
```

### 19.5 `RewardCatalogSO`

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Rewards/Reward Catalog")]
public class RewardCatalogSO : SerializedScriptableObject
{
    [OdinSerialize] private List<RewardEntrySO> _entries = new();
    public IEnumerable<string> AllIds => _entries.Select(e => e.RewardId);
    public RewardEntrySO GetById(string id) => _entries.FirstOrDefault(e => e.RewardId == id);
}
```

Pre-cargado en `ServiceBootstrapSO` (§1.1.1).

**Regla de diseño.** No se crea un SO por cada valor posible de oro. Un `RewardEntrySO` con `Category = Gold, GoldMin = 5, GoldMax = 15, ScaleGoldWithFloor = true` cubre todo el rango de un piso. El diseñador crea pocos `RewardEntrySO` ("Gold pequeño", "Gold mediano", "Ítem común", "Ítem raro") y los combina en pools con pesos.

### 19.6 `UpgradeSO` — upgrades de combo (scope run-only)

Los `UpgradeSO` son **mejoras de combos que el jugador gana durante una run**. No son un sistema genérico de upgrades para cualquier stat — apuntan específicamente a los `BaseComboSO` del `ContractSheet` activo. Su efecto es modificar los tres parámetros de la fórmula de cuenta del combo (§5.1.1) y/o agregar efectos extra. **Scope run-only**: al terminar la run, los upgrades se descartan con el resto del `ServiceScope.Run`; no persisten como meta-progression.

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Rewards/Combo Upgrade")]
public class UpgradeSO : SerializedScriptableObject
{
    [Title("Identity")]
    [ValueDropdown(nameof(GetUpgradeIds))]
    public string UpgradeId;
    public string DisplayName;
    [TextArea] public string Description;
    public Sprite Icon;

    [Title("Target")]
    [InfoBox("Combo al que se aplica. Si null, aplica a TODOS los combos de la sheet activa " +
             "(upgrades globales — p. ej. 'Todos los combos: +10% GeneralMultiplier').")]
    [OdinSerialize] public BaseComboSO TargetCombo;

    [Title("Deltas — qué modifica")]
    [InfoBox("Suma al BaseDamage del combo. 0 = sin cambio.")]
    public int DeltaBaseDamage = 0;

    [InfoBox("Sumado al GeneralMultiplier. 0 = sin cambio. Ejemplo: 0.2 → +20% al multiplicador general.")]
    public float DeltaGeneralMultiplier = 0f;

    [InfoBox("Sumado a cada entry de ValueMultipliers (index 0 = pip 1, …). Length debe ser 6 o 0.")]
    public float[] DeltaValueMultipliers = new float[6];

    [Title("Extra effects (opcional)")]
    [InfoBox("BaseEffects que se agregan a ComboSO.ExtraEffects cuando este upgrade está activo. " +
             "Disparan junto al daño del combo.")]
    [OdinSerialize] public List<EffectData> ExtraEffects = new();

#if UNITY_EDITOR
    private static IEnumerable<string> GetUpgradeIds() =>
        ServiceLocator.TryGetService<UpgradeCatalogSO>(out var cat) ? cat.AllIds : Array.Empty<string>();
#endif
}
```

**Estado run-scoped** — los upgrades no mutan los `BaseComboSO` (el SO es inmutable, compartido). Viven en un `RunUpgradeState` que el `AttackResolver` (§12.1) consulta al computar daño:

```csharp
public class RunUpgradeState : ISaveable
{
    public string SaveKey => "run.combo_upgrades";
    public List<string> ActiveUpgradeIds = new();            // ids de upgrades ganados en esta run

    /// <summary>
    /// Combina todos los upgrades aplicables a un combo dado y devuelve un
    /// snapshot con la fórmula final. Usado por AttackResolver al computar daño.
    /// </summary>
    public ComboResolvedSpec Resolve(BaseComboSO combo, UpgradeCatalogSO catalog)
    {
        var spec = new ComboResolvedSpec {
            BaseDamage = combo.BaseDamage,
            GeneralMultiplier = combo.GeneralMultiplier,
            ValueMultipliers = (float[])combo.ValueMultipliers.Clone(),
            ExtraEffects = new List<EffectData>(combo.ExtraEffects),
        };
        foreach (var id in ActiveUpgradeIds)
        {
            var upg = catalog.GetById(id);
            if (upg == null) continue;
            if (upg.TargetCombo != null && upg.TargetCombo != combo) continue;

            spec.BaseDamage        += upg.DeltaBaseDamage;
            spec.GeneralMultiplier += upg.DeltaGeneralMultiplier;
            for (int i = 0; i < 6; i++)
                if (i < upg.DeltaValueMultipliers.Length)
                    spec.ValueMultipliers[i] += upg.DeltaValueMultipliers[i];
            spec.ExtraEffects.AddRange(upg.ExtraEffects);
        }
        return spec;
    }

    public object CaptureState() => new List<string>(ActiveUpgradeIds);
    public void RestoreState(object state) => ActiveUpgradeIds = new List<string>((List<string>)state);
}

public struct ComboResolvedSpec
{
    public int BaseDamage;
    public float GeneralMultiplier;
    public float[] ValueMultipliers;
    public List<EffectData> ExtraEffects;

    /// <summary>
    /// Calcula el daño final con la fórmula de §5.1.1 — BaseDamage + Count × General,
    /// donde Count = Σ(dado × ValueMultipliers[dado-1]).
    /// </summary>
    public int ComputeDamage(int[] finalDice)
    {
        float sum = 0f;
        for (int i = 0; i < finalDice.Length; i++)
        {
            int pip = finalDice[i];
            if (pip >= 1 && pip <= 6) sum += pip * ValueMultipliers[pip - 1];
        }
        return BaseDamage + Mathf.RoundToInt(sum * GeneralMultiplier);
    }
}
```

**Registro.** `RunUpgradeState` se registra en `ServiceLocator` con `ServiceScope.Run` en `RunBootstrapper.StartRun` (§1.1.3.a, agregado al step 5 junto a `RunComboCounterState` y `RunStrikeState`). `ClearScope(Run)` al terminar la run lo limpia — no hay migración a meta.

**`UpgradeCatalogSO`** — análogo a `RewardCatalogSO`: lista de `UpgradeSO`, registrado en `ServiceBootstrapSO` (§1.1.1) como `ServiceScope.Global`.

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Rewards/Upgrade Catalog")]
public class UpgradeCatalogSO : SerializedScriptableObject
{
    [OdinSerialize] private List<UpgradeSO> _entries = new();
    public IEnumerable<string> AllIds => _entries.Select(e => e.UpgradeId);
    public UpgradeSO GetById(string id) => _entries.FirstOrDefault(e => e.UpgradeId == id);
}
```

**Cómo se obtienen los upgrades.** Un `RewardEntrySO` con `Category = Upgrade` y pool en `UpgradePool` los ofrece — ya definido en §19.1 (el `WeightedUpgradeDrop` apunta a `UpgradeSO`). El `IRewardService.ApplyReward` para `Category == Upgrade` agrega `upgrade.UpgradeId` a `RunUpgradeState.ActiveUpgradeIds` y dispara `OnItemObtained` con el upgradeId como payload (el HUD refresca icon list).

**Ejemplo de diseño.**

| `UpgradeId` | `TargetCombo` | Deltas | Interpretación |
|---|---|---|---|
| `upg.full_house.empowered` | `ComboFullHouse` | `DeltaBaseDamage = +20` | El Full House base pasa de 40 a 60 de daño flat. |
| `upg.odds.sharpen` | `ComboOdds` | `DeltaValueMultipliers = [0, 0, 1, 0, 0, 0]` | Los impares del 3 valen el doble (pasa de ×1 a ×2). |
| `upg.all.rage` | null (aplica a todos) | `DeltaGeneralMultiplier = 0.25` | Todos los combos multiplican su Count × 1.25. |
| `upg.three_sixes.burn` | `ComboThreeSixes` | `ExtraEffects = [EffAddStatusEffect(Burn, 2)]` | Cada `ThreeSixes` aplica Burn(2) al target además del daño. |

**Anti‑patrones.**
- No usar `UpgradeSO` para stats no-combo (HP max, velocidad, etc.) — para eso, `RewardEntrySO` con `Category = Effect` y un `EffAddIntModifier` con `Lifetime = Run`.
- No crear un `UpgradeSO` que modifique la lógica de `Matches` del combo — si se quiere cambiar cuándo un combo matchea, eso es un combo distinto, no un upgrade.

**Cross‑ref §19.** §5.1 (fórmula de cuenta), §5.1.1 (ejemplos de combos con formulita), §8 (effects de rewards custom + ExtraEffects de upgrades), §12.1 (`AttackResolver` consume `ComboResolvedSpec`), §13.4 (`RewardPoolSO` usa `RewardEntrySO`), §14.7 (`RulesetSO` scaling curves), §15 (save — `RunUpgradeState`), §17.F (shop usa `RewardEntrySO` en `ShopPoolSO`), §18 (ítems dados como reward).

---

## 20. Sistema de Status Effects

> Formaliza los estados de alteración (veneno, stun, quemadura, etc.) como entidades autorables con reglas de stack, inmunidades y feedback visual. Se apoya en el sistema de modificadores (§3) pero agrega semántica de "efecto de estado" con UI dedicada.

### 20.1 `StatusEffectSO`

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Status Effects/Status Effect")]
public class StatusEffectSO : SerializedScriptableObject
{
    [Title("Identity")]
    [ValueDropdown(nameof(GetStatusIds))]
    public string StatusId;
    public string DisplayName;
    [TextArea] public string Description;
    public Sprite Icon;
    public Color TintColor = Color.white;

    [Title("Behavior")]
    public StatusEffectCategory Category;              // Debuff | Buff | Neutral
    public StatusTickBehavior TickBehavior;             // OnTurnStart | OnTurnEnd | None

    [Title("Crowd Control")]
    [InfoBox("Si está en true, CheckTurnSkip del FSM de combate (§1.3) saltea el turno " +
             "de la entidad afectada. Uso: stun, freeze, sleep, paralysis. Este flag " +
             "reemplaza al hardcode viejo 'stun|freeze|sleep' — cualquier status nuevo " +
             "que impida actuar lo marca acá.")]
    public bool PreventsTurn;

    [Title("Tick Effect")]
    [InfoBox("Efecto que se ejecuta cada tick (si TickBehavior != None). " +
             "Ej: veneno hace EffDealDamage(amount), regen hace EffHeal(amount).")]
    [ShowIf("@TickBehavior != StatusTickBehavior.None")]
    [OdinSerialize]
    public EffectData OnTickEffect = new();

    [Title("Values")]
    [InfoBox("El valor del efecto puede ser fijo, porcentual del stat max, " +
             "o leído de un reader (§8.6). Configurable per-instancia.")]
    public StatusValueMode ValueMode;                  // Fixed | PercentOfMax | FromReader
    [ShowIf("@ValueMode == StatusValueMode.Fixed")]
    public float FixedValue;
    [ShowIf("@ValueMode == StatusValueMode.PercentOfMax")]
    [Range(0, 1)] public float PercentValue;
    [ShowIf("@ValueMode == StatusValueMode.FromReader")]
    [OdinSerialize] public IEntityReader<float> ValueReader;

    [Title("Application")]
    [InfoBox("Duración default. Puede ser overrideada al aplicar.")]
    [MinValue(1)] public int DefaultDuration = 3;

    [Title("Stacking")]
    public StatusStackRule StackRule;                   // Refresh | Stack | Replace | Immune

    [ShowIf("@StackRule == StatusStackRule.Stack")]
    [MinValue(1)] public int MaxStacks = 5;

    [Title("On Apply / On Remove")]
    [OdinSerialize] public EffectData OnApply  = new();    // ej: slow → EffAddIntModifier(Movement, -1)
    [OdinSerialize] public EffectData OnRemove = new();    // ej: limpiar modificadores aplicados

    [Title("Immunities")]
    [InfoBox("Status effects que hacen inmune a este. Ej: 'Fire Shield' inmuniza contra 'Burn'.")]
    [OdinSerialize] public List<StatusEffectSO> ImmuneTo = new();

    [Title("Feedback")]
    [ValueDropdown(nameof(GetFeedbackIds))]
    public string OnApplyFeedbackId;
    [ValueDropdown(nameof(GetFeedbackIds))]
    public string OnTickFeedbackId;

#if UNITY_EDITOR
    private static IEnumerable<string> GetStatusIds() =>
        ServiceLocator.TryGetService<StatusCatalogSO>(out var cat) ? cat.AllIds : Array.Empty<string>();
    private static IEnumerable<string> GetFeedbackIds() =>
        ServiceLocator.TryGetService<FeedbackDBSO>(out var db) ? db.GetAllFeedbackIds() : Array.Empty<string>();
#endif
}

public enum StatusEffectCategory { Debuff, Buff, Neutral }
public enum StatusTickBehavior { None, OnTurnStart, OnTurnEnd }
public enum StatusValueMode { Fixed, PercentOfMax, FromReader }
public enum StatusStackRule
{
    /// <summary>Re-aplicar refresca la duración sin stackear el efecto.</summary>
    Refresh,
    /// <summary>Cada aplicación suma un stack (hasta MaxStacks). Duración independiente por stack.</summary>
    Stack,
    /// <summary>Re-aplicar reemplaza la instancia anterior (nueva duración, nuevo valor).</summary>
    Replace,
    /// <summary>La entidad es inmune a este status — la aplicación falla silenciosamente.</summary>
    Immune,
}
```

### 20.2 `IStatusEffectService`

```csharp
public interface IStatusEffectService
{
    bool ApplyStatus(Guid targetGuid, StatusEffectSO status, int duration = -1, int stacks = 1);
    bool RemoveStatus(Guid targetGuid, string statusId);
    void RemoveAllStatuses(Guid targetGuid);
    bool HasStatus(Guid targetGuid, string statusId);
    int  GetStacks(Guid targetGuid, string statusId);
    IReadOnlyList<ActiveStatusEffect> GetStatuses(Guid targetGuid);

    /// <summary>Tick de todos los status effects — llamado por TurnManager.</summary>
    void TickStatuses(Guid entityGuid, StatusTickBehavior timing);

    event Action<Guid, StatusEffectSO, int> OnStatusApplied;    // target, status, stacks
    event Action<Guid, StatusEffectSO>      OnStatusRemoved;
    event Action<Guid, StatusEffectSO, int> OnStatusTicked;     // target, status, damage/heal
}

[Serializable]
public class ActiveStatusEffect
{
    public StatusEffectSO Definition;
    public int RemainingDuration;
    public int CurrentStacks;
    public List<Guid> AppliedModifierIds = new();       // para limpiar al remover
}
```

#### 20.2.1 Integración con `Modifier<T>` (§3) — qué crea `ApplyStatus` internamente

`IStatusEffectService` **no** es un bus paralelo al sistema de modificadores. Cada status effect se materializa en runtime como una composición de tres artefactos concretos, todos trackeados en la `ActiveStatusEffect.AppliedModifierIds` para limpiarlos en bloque al remover:

1. **Un `Modifier<T>` por cada efecto sobre stat.** Si `OnApply.Effects` contiene `EffAddIntModifier(Health, -5, OnTurnFinished, DurationOverride)`, el service lo ejecuta tal cual y captura el `Guid` del modifier retornado en `AppliedModifierIds`. Si `OnTickEffect` es `EffDealDamage(amount)`, se dispara vía `DamagePipeline.Resolve` (§12.2, con `Kind = DamageOverTime`, `IsWeaknessHit = false`) — **no** crea un modifier propio, el daño pasa por pipeline igual que cualquier otro.

2. **Un `Tag` sobre el entity** (`Status.<StatusId>`). Lo agrega al `Entity.Tags` para queries rápidas — `entity.HasTag("Status.Burn")` funciona sin consultar el service. El tag se remueve junto con el status.

3. **Una entry en `ActiveStatusEffect` del propio service.** Es la fuente de verdad para duración, stacks, immunities, tick behavior. El HUD `StatusIconsView` (§20.5) lee de acá.

**Ejemplo — `ApplyStatus(Burn, duration=3, stacks=1)`:**

```csharp
public bool ApplyStatus(Guid targetGuid, StatusEffectSO status, int duration, int stacks)
{
    // ... resolution de stack rule, immunities, duration default omitted

    var active = new ActiveStatusEffect {
        Definition = status,
        RemainingDuration = duration,
        CurrentStacks = stacks,
    };

    // (1) OnApply — crea Modifier<T>s que quedan trackeados
    var applyCtx = new EffectContext(source: null, target: ResolveEntity(targetGuid));
    foreach (var effect in status.OnApply.Effects)
    {
        if (effect is IEmitsModifier me)
            active.AppliedModifierIds.Add(me.EmittedModifierId);
        effect.Execute(applyCtx);
    }

    // (2) Tag
    ResolveEntity(targetGuid).Tags.Add($"Status.{status.StatusId}");

    // (3) Registro en el servicio
    _activeByEntity[targetGuid].Add(active);

    EventManager.Trigger(EventName.OnStatusApplied, targetGuid, status, stacks);
    return true;
}

public bool RemoveStatus(Guid targetGuid, string statusId)
{
    var active = _activeByEntity[targetGuid].FirstOrDefault(a => a.Definition.StatusId == statusId);
    if (active == null) return false;

    // (1) Remover todos los Modifier<T> que aplicó
    foreach (var modId in active.AppliedModifierIds)
        AttributesManager.RemoveModifierById(targetGuid, modId);

    // (2) Remover tag
    ResolveEntity(targetGuid).Tags.Remove($"Status.{statusId}");

    // (3) OnRemove effects + cleanup
    foreach (var effect in active.Definition.OnRemove.Effects)
        effect.Execute(new EffectContext(source: null, target: ResolveEntity(targetGuid)));

    _activeByEntity[targetGuid].Remove(active);
    EventManager.Trigger(EventName.OnStatusRemoved, targetGuid, active.Definition);
    return true;
}
```

**Tabla de ejemplos.**

| StatusEffectSO | Modifier(s) creados en `OnApply` | Tick | Tag |
|---|---|---|---|
| `Burn` | ninguno (sólo tick) | `EffDealDamage(5)` cada `OnTurnStart` via `DamagePipeline` (`Kind = DamageOverTime`) | `Status.Burn` |
| `Slow` | `IntModifier(Speed, -2, OnStatusRemoved, lifetime=EventTriggered)` | — | `Status.Slow` |
| `Stun` | ninguno — el `PreventsTurn` se chequea via query contra `GetStatuses` (§20.3) | — | `Status.Stun` |
| `Shield` | `IntModifier(Shield, +10, OnTurnStarted, duration=DefaultDuration)` | — | `Status.Shield` |
| `Regen` | ninguno (sólo tick) | `EffHeal(3)` cada `OnTurnEnd` via `HealPipeline` | `Status.Regen` |

**`IEmitsModifier` helper.** Los effects que crean modifiers (`EffAddIntModifier`, `EffAddFloatModifier`) implementan una interface de capability para que el status service pueda capturar el `Guid` del modifier emitido y guardarlo en `AppliedModifierIds`. Esto es necesario porque el statusservice **no** le dicta al effect qué modifier crear — el `EffectData` del `OnApply` es autoría libre del diseñador. La única restricción es que si un effect quiere ser trackable por el status para cleanup, debe implementar `IEmitsModifier`. Effects sin esta capability siguen ejecutándose pero no se limpian al remover el status — útil para efectos "one-shot" (ej: un `EffPlayFeedback` dentro del `OnApply` que solo reproduce un VFX, no necesita cleanup).

**Por qué no duplicar `Modifier<T>.OwnerId`.** Un modifier creado por un status tiene su `OwnerId = targetGuid` como cualquier otro (§3). Eso lo hace compatible con el lifecycle normal (cleanup por turno, OnLoad post-save). El `AppliedModifierIds` del `ActiveStatusEffect` es un back-reference para poder hacer removal **agrupado** al remover el status — sin eso, habría que iterar todos los modifiers del target buscando matches por metadata.

### 20.3 Integración con el FSM de combate

El `CheckTurnSkip` (§1.3) consulta al `IStatusEffectService` y salta el turno si **algún** status activo sobre la entidad tiene `PreventsTurn = true`. El chequeo es data‑driven: cada status marca el flag en su SO, el FSM no tiene ids hardcoded → un status nuevo que impida actuar (ej: `paralysis`) funciona desde el momento en que el diseñador marca el flag, sin cambios de código.

```csharp
// En CheckTurnSkip
var statusService = ServiceLocator.GetService<IStatusEffectService>();
bool isDisabled = statusService.GetStatuses(playerGuid)
    .Any(active => active.Definition.PreventsTurn);

if (isDisabled) => transicionar a CleanupPlayerModifiers sin pasar por PlayerInput
```

### 20.4 Efecto de aplicación: `EffAddStatusEffect`

```csharp
public class EffAddStatusEffect : BaseEffect, IUsesSelection
{
    [ValueDropdown(nameof(GetStatusIds))]
    public string StatusId;
    [MinValue(0)] public int DurationOverride = 0;     // 0 = usar DefaultDuration del SO
    public int Stacks = 1;

    public override bool ApplyEffect(EffectContext ctx)
    {
        var catalog = ServiceLocator.GetService<StatusCatalogSO>();
        var status = catalog.GetById(StatusId);
        if (status == null) return false;

        var duration = DurationOverride > 0 ? DurationOverride : status.DefaultDuration;
        return ServiceLocator.GetService<IStatusEffectService>()
            .ApplyStatus(ctx.TargetGuid, status, duration, Stacks);
    }
}
```

### 20.5 UI de status icons

El HUD muestra una fila de íconos de status activos sobre la barra de vida:

```csharp
public class StatusIconsView : MonoBehaviour
{
    [SerializeField] private StatusIconWidget _iconPrefab;
    [SerializeField] private Transform _container;

    private void OnEnable()
    {
        EventManager.Subscribe(EventName.OnStatusApplied, OnStatusChanged);
        EventManager.Subscribe(EventName.OnStatusRemoved, OnStatusChanged);
    }

    private void OnDisable()
    {
        EventManager.UnSubscribe(EventName.OnStatusApplied, OnStatusChanged);
        EventManager.UnSubscribe(EventName.OnStatusRemoved, OnStatusChanged);
    }

    /// <summary>args: [Guid targetGuid, string statusId, int stacks]</summary>
    private void OnStatusChanged(params object[] args)
    {
        var targetGuid = (Guid)args[0];
        if (targetGuid != ServiceLocator.GetService<IPlayerService>().PlayerGuid) return;
        RebuildIcons();
    }

    private void RebuildIcons()
    {
        // Limpia y recrea los íconos desde IStatusEffectService.GetStatuses(playerGuid)
    }
}
```

**Cross‑ref §20.** §1.3 (`CheckTurnSkip` usa statuses para CC), §3 (modificadores aplicados via `OnApply`), §8 (`EffAddStatusEffect`), §10 (feedback de aplicación/tick), §14.7 (`RulesetSO.MaxReactionDepth`).

---

## 21. Sistema de Quests

> Sistema liviano de quests para NPCs. No se muestra en UI dedicada por el momento — las quests se usan internamente para condicionar diálogos (§7.6b) y como condiciones de unlock (§14).

### 21.1 `QuestSO`

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Quests/Quest")]
public class QuestSO : SerializedScriptableObject
{
    [Title("Identity")]
    [ValueDropdown(nameof(GetQuestIds))]
    public string QuestId;
    public string DisplayName;
    [TextArea] public string Description;

    [Title("Completion")]
    [InfoBox("Condiciones que deben cumplirse para completar la quest. AND por default.")]
    [OdinSerialize]
    public List<BasePreCondition> CompletionConditions = new();      // §8.2

    [Title("Auto-tracking")]
    [InfoBox("EventNames que disparan la re-evaluación de CompletionConditions mientras la quest está Active. " +
             "Ej: 'defeat 5 enemies' listenea OnEntityDestroyed; 'reach floor 3' listenea OnFloorCleared.\n" +
             "Si la lista queda vacía, la quest sólo se completa vía EffCompleteQuest manual (útil para " +
             "quests narrativas tipo 'hablá con X NPC' que el diálogo o interacción dispara explícitamente).")]
    public List<EventName> ProgressTriggers = new();

    [Title("Rewards")]
    [OdinSerialize]
    public List<RewardEntrySO> Rewards = new();                       // §19

    [Title("On Complete")]
    [OdinSerialize]
    public EffectData OnComplete = new();                              // §8

#if UNITY_EDITOR
    private static IEnumerable<string> GetQuestIds() =>
        ServiceLocator.TryGetService<QuestCatalogSO>(out var cat) ? cat.AllIds : Array.Empty<string>();
#endif
}
```

### 21.2 `IQuestService`

```csharp
public interface IQuestService
{
    QuestState GetState(string questId);
    void StartQuest(string questId);
    void CompleteQuest(string questId);
    void FailQuest(string questId);
    bool IsCompleted(string questId);
    bool IsActive(string questId);
    IReadOnlyList<string> ActiveQuestIds { get; }

    /// <summary>
    /// Suscribe al EventManager todos los ProgressTriggers de las quests Active.
    /// Llamado una vez en RunBootstrapper.StartRun (§1.1.3.a) post-StartQuest inicial.
    /// Idempotente — re-llamarlo no duplica handlers.
    /// </summary>
    void SubscribeEventTriggers();

    event Action<string, QuestState> OnQuestStateChanged;
}

public enum QuestState { NotStarted, Active, Completed, Failed }
```

**Auto-complete por evento (tracking automático).** Cuando una quest pasa a `Active`, el service suscribe un handler para cada `EventName` de su `ProgressTriggers`. El handler re-evalúa `CompletionConditions` y si todas pasan, marca la quest `Completed` y ejecuta `OnComplete` + `Rewards`. No hay contador explícito: las condiciones mismas (p. ej. `PCEntitiesDefeatedSinceQuestStart(count=5, type="goblin")` §8.2) leen el estado persistente y deciden. Esto mantiene el modelo de quest declarativo — un `QuestSO` no tiene "progress fields" que haya que mutar.

```csharp
// Pseudocódigo interno del QuestService
private readonly Dictionary<string, List<(EventName, EventManager.EventReceiver)>> _handlersByQuest = new();

public void StartQuest(string questId)
{
    var quest = _catalog.GetById(questId);
    _state.States[questId] = QuestState.Active;
    RegisterTriggersFor(quest);
    EventManager.Trigger(EventName.OnQuestStateChanged, quest, QuestState.Active);
}

private void RegisterTriggersFor(QuestSO quest)
{
    if (_handlersByQuest.ContainsKey(quest.QuestId)) return;  // idempotente
    var list = new List<(EventName, EventManager.EventReceiver)>();
    foreach (var evt in quest.ProgressTriggers)
    {
        EventManager.EventReceiver handler = args => TryAutoComplete(quest.QuestId);
        EventManager.Subscribe(evt, handler);
        list.Add((evt, handler));
    }
    _handlersByQuest[quest.QuestId] = list;
}

private void TryAutoComplete(string questId)
{
    if (_state.States[questId] != QuestState.Active) return;
    var quest = _catalog.GetById(questId);
    var ctx = new PreConditionContext { /* ... */ };
    if (quest.CompletionConditions.All(c => c.Evaluate(ctx)))
        CompleteQuest(questId);
}

public void CompleteQuest(string questId)
{
    // Unsubscribe handlers al completar — no queremos re-evaluar una quest Completed.
    if (_handlersByQuest.TryGetValue(questId, out var handlers))
    {
        foreach (var (evt, h) in handlers) EventManager.UnSubscribe(evt, h);
        _handlersByQuest.Remove(questId);
    }
    // ... resto del flujo (OnComplete, Rewards, event)
}

public void Dispose()  // llamado por ClearScope(Run) — IDisposable (§1.1.3)
{
    foreach (var list in _handlersByQuest.Values)
        foreach (var (evt, h) in list) EventManager.UnSubscribe(evt, h);
    _handlersByQuest.Clear();
}
```

**Regla de diseño.** Si una quest necesita "tracking de progreso exacto" (p. ej. kill count 3/5 mostrado al jugador), el count vive en un `ISaveable` aparte (p. ej. `QuestKillCounterState` análogo a `RunComboCounterState` §5.5) que escucha el mismo event y mantiene el counter. La `CompletionCondition` de la quest solo lo consulta. **El `QuestService` no conoce números específicos** — solo ejecuta condiciones declarativas.

**`CompleteQuest` manual.** Sigue disponible para las quests cuya condición es "hablá con X NPC, fin" — en ese caso la quest puede no declarar `ProgressTriggers`, y el `EffCompleteQuest` (§8) lo dispara desde el `DialogueGraphSO` del NPC o el `OnInteract` del target. `StartQuest` + `CompleteQuest` manual es el patrón "scripted narrative" sin auto-tracking.

### 21.3 `QuestProgressState : ISaveable`

```csharp
public class QuestProgressState : ISaveable
{
    public string SaveKey => "run.quests";
    public Dictionary<string, QuestState> States = new();

    public object CaptureState() => new Dictionary<string, QuestState>(States);
    public void RestoreState(object state) =>
        States = new Dictionary<string, QuestState>((Dictionary<string, QuestState>)state);
}
```

### 21.4 PreConditions de quest

```csharp
public class PCQuestState : BasePreCondition
{
    [ValueDropdown(nameof(GetQuestIds))]
    public string QuestId;
    public QuestState RequiredState;

    public override string ConditionName => $"Quest {QuestId} is {RequiredState}";
    public override bool Evaluate(PreConditionContext ctx) =>
        ServiceLocator.GetService<IQuestService>().GetState(QuestId) == RequiredState;
}
```

### 21.5 Integración con unlocks (§14)

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Meta/Conditions/Quest Completed")]
public class UC_QuestCompleted : UnlockConditionSO
{
    [ValueDropdown(nameof(GetQuestIds))]
    public string QuestId;

    public override bool Evaluate(RunRecord record) =>
        ServiceLocator.GetService<UnlockStateSO>().IsQuestEverCompleted(QuestId);
}
```

**Cross‑ref §21.** §7.6b (diálogos condicionados por `PCQuestState`), §8 (effects de quest: `EffStartQuest`, `EffCompleteQuest`), §14 (unlock conditions), §15 (save via `QuestProgressState`), §19 (rewards de quest).

---

## 22. Tutorial / Onboarding

> Guía al jugador en las primeras runs. Se apaga automáticamente después de completar los pasos.

### 22.1 `TutorialStepSO`

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Tutorial/Step")]
public class TutorialStepSO : SerializedScriptableObject
{
    [Title("Identity")]
    public string StepId;
    public int OrderIndex;                             // orden de ejecución

    [Title("Trigger")]
    [InfoBox("Cuándo se activa este paso del tutorial.")]
    public EventName TriggerEvent;
    [OdinSerialize]
    public List<BasePreCondition> ActivationConditions = new();

    [Title("Content")]
    public LocalizedString Title;
    public LocalizedString Message;
    public Sprite Illustration;

    [Title("UI Highlight")]
    [InfoBox("ID del elemento UI a highlight (ej: 'HUD.AttackButton', 'HUD.DiceBag'). " +
             "Vacío = sin highlight.")]
    public string HighlightElementId;

    [Title("Forced Action")]
    [InfoBox("Si no vacío, el tutorial bloquea el input hasta que el jugador " +
             "realice esta acción específica.")]
    public TurnInput? ForcedAction;

    [Title("Completion")]
    public EventName CompletionEvent;
    [OdinSerialize]
    public List<BasePreCondition> CompletionConditions = new();
}
```

### 22.2 `ITutorialService`

```csharp
public interface ITutorialService
{
    bool IsActive { get; }
    TutorialStepSO CurrentStep { get; }
    void CompleteCurrentStep();
    void SkipAll();
    bool IsStepCompleted(string stepId);

    event Action<TutorialStepSO> OnStepActivated;
    event Action<TutorialStepSO> OnStepCompleted;
}
```

### 22.3 `TutorialProgressState : ISaveable`

```csharp
public class TutorialProgressState : ISaveable
{
    public string SaveKey => "global.tutorial";
    public HashSet<string> CompletedSteps = new();
    public bool AllCompleted = false;

    public object CaptureState() => new TutorialSnapshot { /* ... */ };
    public void RestoreState(object state) { /* ... */ }
}
```

**Cross‑ref §22.** §1.2 (events de trigger/completion), §8.2 (preconditions de activación), §15 (save de progreso), §17.D (UI de tutorial overlay en HUD).

---

## 23. Settings y Accessibility

> Sistema general de settings del jugador, persistido entre sesiones.

### 23.1 `GameSettingsSO`

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Settings/Game Settings")]
public class GameSettingsSO : SerializedScriptableObject
{
    [Title("Gameplay")]
    [InfoBox("Override de dificultad. Si no None, sobreescribe el RulesetSO activo.")]
    public DifficultyOverride Difficulty = DifficultyOverride.None;

    [Title("Accessibility")]
    [ToggleLeft] public bool ReduceScreenShake = false;
    [ToggleLeft] public bool ColorBlindMode = false;
    public ColorBlindType ColorBlindType = ColorBlindType.None;
    [ToggleLeft] public bool LargeText = false;
    [Range(0.8f, 2f)] public float TextSizeMultiplier = 1f;
    [ToggleLeft] public bool HighContrastUI = false;

    [Title("Controls")]
    [ToggleLeft] public bool InvertCameraY = false;
    [Range(0.1f, 3f)] public float CameraSensitivity = 1f;

    [Title("Display")]
    [ToggleLeft] public bool ShowDamageNumbers = true;
    [ToggleLeft] public bool ShowTurnOrder = true;
    [ToggleLeft] public bool ShowComboPreview = true;
}

public enum DifficultyOverride { None, Easy, Normal, Hard }
public enum ColorBlindType { None, Protanopia, Deuteranopia, Tritanopia }
```

### 23.2 `ISettingsService`

```csharp
public interface ISettingsService
{
    GameSettingsSO Current { get; }
    void Apply(GameSettingsSO settings);
    void ResetToDefaults();

    event Action OnSettingsChanged;
}
```

### 23.3 `GameSettingsState : ISaveable`

```csharp
public class GameSettingsState : ISaveable
{
    public string SaveKey => "global.settings";

    // Captura todos los campos del GameSettingsSO activo
    public object CaptureState() => new GameSettingsSnapshot { /* ... */ };
    public void RestoreState(object state) { /* ... */ }
}
```

El `ISettingsService` se registra como `Global` en el `ServiceLocator` (§1.1). Los settings se persisten como `ISaveable` con scope global (no por run).

**Cross‑ref §23.** §15 (save), §17.A (volúmenes de audio), §17.E (camera sensitivity/invert), §17.D (text size multiplier).

---

## 24. Object Pooling

> Pool genérico para reutilizar GameObjects instanciados frecuentemente. Elimina allocations y GC spikes en combate.

### 24.1 `IObjectPool<T>`

```csharp
public interface IObjectPool<T> where T : Component
{
    /// <summary>Obtiene una instancia del pool. Si no hay disponibles, instancia una nueva.</summary>
    T Get();

    /// <summary>Devuelve la instancia al pool para reutilización.</summary>
    void Return(T instance);

    /// <summary>Pre-instancia N objetos para evitar spikes en el primer uso.</summary>
    void Prewarm(int count);

    int CountActive { get; }
    int CountInactive { get; }
}
```

### 24.2 `ComponentPool<T>`

```csharp
public class ComponentPool<T> : IObjectPool<T> where T : Component
{
    private readonly T _prefab;
    private readonly Transform _parent;
    private readonly Queue<T> _inactive = new();
    private readonly HashSet<T> _active = new();

    public ComponentPool(T prefab, Transform parent, int prewarmCount = 0)
    {
        _prefab = prefab;
        _parent = parent;
        Prewarm(prewarmCount);
    }

    public T Get()
    {
        T instance;
        if (_inactive.Count > 0)
        {
            instance = _inactive.Dequeue();
        }
        else
        {
            instance = Object.Instantiate(_prefab, _parent);
        }
        instance.gameObject.SetActive(true);
        _active.Add(instance);
        return instance;
    }

    public void Return(T instance)
    {
        if (!_active.Remove(instance)) return;
        instance.gameObject.SetActive(false);
        _inactive.Enqueue(instance);
    }

    public void Prewarm(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var instance = Object.Instantiate(_prefab, _parent);
            instance.gameObject.SetActive(false);
            _inactive.Enqueue(instance);
        }
    }

    public int CountActive => _active.Count;
    public int CountInactive => _inactive.Count;
}
```

### 24.3 `IPoolService`

Servicio central que gestiona todos los pools del juego. Registrado en `ServiceLocator` como `Global`.

```csharp
public interface IPoolService
{
    /// <summary>
    /// Obtiene o crea un pool para el prefab dado. Si el pool no existe,
    /// lo crea con el prewarm indicado.
    /// </summary>
    IObjectPool<T> GetPool<T>(T prefab, int prewarmCount = 0) where T : Component;

    /// <summary>Limpia todos los pools (al cambiar de escena o al terminar run).</summary>
    void ClearAll();
}
```

### 24.4 Consumidores del pool

| Sistema | Qué poolea | Prewarm sugerido |
|---|---|---|
| `FeedbackManager` (§10) | VFX particles, floating numbers | 20 |
| `IAudioService` (§A) | AudioSource pool (ya existente — migrar a `IPoolService`) | 16 |
| `IMovementService` (§B) | Trail/step VFX | 10 |
| `DungeonManager` (§13) | Enemy/Prop GameObjects spawneados al entrar a sala | 10 |
| `StatusIconsView` (§20) | Status icon widgets | 8 |
| `RewardScreen` (§D) | Reward card prefabs | 5 |

**Regla.** Todo `Object.Instantiate` / `Object.Destroy` en hot paths (combate, feedback, movimiento) debe reemplazarse por `pool.Get()` / `pool.Return()`. Las instanciaciones one-shot (bootstrap, screen setup) no necesitan pool.

**Cross‑ref §24.** §10 (VFX/floating numbers), §13 (spawn de entidades), §17.A (AudioSource pool), §17.B (movement VFX).

---

## 25. Analytics y Telemetría

> Registro de eventos clave para análisis de balance, informe de tesis y mejora del GD.

### 25.1 `IAnalyticsService`

```csharp
public interface IAnalyticsService
{
    void TrackEvent(string eventName, Dictionary<string, object> properties = null);

    // --- Helpers tipados para eventos frecuentes ---
    void TrackRunStart(string heroId, List<DiceType> bag, string rulesetId);
    void TrackRunEnd(RunRecord record);
    void TrackComboUsed(string comboId, int damage, int floorIndex);
    void TrackItemObtained(string itemId, string source);           // source: "shop", "chest", "reward"
    void TrackDeath(string causeOfDeath, int floorIndex, string roomId);
    void TrackCombatDuration(int rounds, int enemyCount, int floorIndex);
    void TrackShopPurchase(string itemId, int price, int playerGold);
    void TrackStatusEffectApplied(string statusId, string sourceId);
}
```

### 25.2 Implementación

```csharp
public class FileAnalyticsService : IAnalyticsService
{
    private readonly string _logPath;
    private readonly List<AnalyticsEvent> _buffer = new();
    private const int FlushThreshold = 50;

    public void TrackEvent(string eventName, Dictionary<string, object> properties = null)
    {
        _buffer.Add(new AnalyticsEvent
        {
            Timestamp = DateTime.UtcNow,
            EventName = eventName,
            Properties = properties ?? new()
        });

        if (_buffer.Count >= FlushThreshold) Flush();
    }

    public void Flush()
    {
        // Append al archivo JSON lines — un evento por línea
        var lines = _buffer.Select(e => JsonUtility.ToJson(e));
        File.AppendAllLines(_logPath, lines);
        _buffer.Clear();
    }

    // ... helpers tipados delegan a TrackEvent con propiedades estructuradas
}

[Serializable]
public class AnalyticsEvent
{
    public DateTime Timestamp;
    public string EventName;
    public Dictionary<string, object> Properties;
}
```

### 25.3 Registro

`IAnalyticsService` se registra como `Global` en el bootstrap (§1.1.1). Se flushea automáticamente en `SaveTrigger.RunEnd` y `SaveTrigger.Exit`.

**Cross‑ref §25.** §1.1.1 (bootstrap), §14.5 (`RunRecord` alimenta `TrackRunEnd`), §15 (flush sincronizado con save triggers).

---

## 26. Content Tooling — Pipeline de autoría para diseñadores

> Herramientas de editor que aceleran la creación de contenido sin tocar código. Cada tool es un `EditorWindow` o un custom inspector que opera sobre los SOs del proyecto.

### 26.1 Principios

1. **Zero-code content.** Un diseñador crea un enemigo nuevo, una sala, un combo, un item o un unlock sin escribir una línea de C#.
2. **Validación en editor.** Cada tool valida el contenido al guardarlo (ids duplicados, stats fuera de rango, combos sin damage, salas sin spawn points, graphs de diálogo sin EndNode).
3. **Preview in-editor.** Donde sea posible, preview visual del contenido sin entrar a Play Mode.

### 26.2 Entity Wizard

`EditorWindow` que guía la creación de una nueva entidad paso a paso:

1. Elegir subtipo (`Hero` / `Enemy` / `Prop` / `NPC`).
2. Asignar id (auto-slug desde `DisplayName`, validado contra el catálogo para evitar duplicados).
3. Configurar stats base con sliders (rango por stat desde un `StatsRangeSO`).
4. Arrastrar prefab → auto-linkea `PrefabRef` como Addressable.
5. Asignar behaviors (desde `BehaviorLibrary` o inline).
6. Para enemigos: wizard de AI tree con templates predefinidos (`Melee Rusher`, `Ranged Kiter`, `Support Healer`, `Boss Pattern`).
7. Al finalizar: crea el SO, lo agrega al `EntityCatalogSO`, valida.

### 26.3 Room Designer

Custom `EditorWindow` que muestra la grilla de la sala con:

- Drag & drop de spawn points (enemy, reward, obstacle, door).
- Preview de setups fijos (`EnemySetupSO`) con siluetas de enemigos.
- Validación: al menos 1 door, al menos 1 enemy spawn (combat rooms), reachability check entre spawn points.
- Botón "Test Room" que abre Play Mode en esa sala aislada con un héroe de prueba.

### 26.4 Balance Dashboard

`EditorWindow` con:

- **Tabla de enemigos**: HP / Damage / Speed por piso (curva del `RulesetSO`).
- **Tabla de combos**: damage base, probabilidad de match con 5d6, expected DPS.
- **Simulador Monte Carlo**: N runs simuladas → distribución de floor reached, gold acumulado, combos más usados, causa de muerte más frecuente.
- **Warnings automáticos**: "ComboGenerala tiene 0.01% chance con 5d6 — ¿es intencional?"

### 26.5 Combo Probability Calculator

Inline en el inspector de cada `BaseComboSO`:

- Dado un `DiceBagSO`, calcula la probabilidad de match en 1 tirada.
- Muestra probabilidad acumulada con 1/2/3 rerolls.
- Compara `damage × probability = Expected Value` por combo.
- Permite comparar dos bags side-by-side.

### 26.6 Dialogue Graph Viewer

Custom `EditorWindow` basada en GraphView API que:

- Visualiza el `DialogueGraphSO` como nodos conectados.
- Muestra `VisibleIf`/`EnabledIf` inline en cada choice.
- Highlight de paths sin `EndNode` (dead ends).
- Preview de texto con localización activa.
- Drag & drop para reorganizar nodos.

### 26.7 Feedback Preview Tool

`EditorWindow` que permite:

- Seleccionar un `FeedbackEntry` del DB.
- Preview del VFX / SFX / Animation sin Play Mode.
- Timeline visual de `FeedbackSequences` (steps en orden con tiempos).
- Botón "Play Preview" que instancia el feedback en Scene View.

### 26.8 Unlock Tree Visualizer

`EditorWindow` que muestra el árbol de progresión (§14) como grafo:

- Nodos = `BaseUnlockSO`, aristas = dependencias (`UnlockConditionSO`).
- Color por estado: locked / unlocked / hidden.
- Click en nodo → selecciona el SO en el inspector.
- Validación: nodos inalcanzables, ciclos, condiciones imposibles.

### 26.9 Content Validators (CI-ready)

Scripts de editor que corren como tests o como pre-build step:

| Validación | Qué detecta |
|---|---|
| IDs duplicados | Cualquier catálogo con dos entries con el mismo id |
| SOs huérfanos | SOs no referenciados en ningún catálogo |
| Stats fuera de rango | `Health = 0`, `Energy < 0`, `BaseDamage = 0` en combos |
| Behaviors vacíos | `BaseBehavior` sin effects |
| AI trees con nodos null | `AIDecisionNode` hijos que son null |
| Dialogue dead ends | `DialogueGraphSO` con paths sin `EndNode` |
| Addressable refs rotas | `PrefabRef` apuntando a un prefab eliminado |
| Rooms sin doors | Salas de combate sin al menos 1 `DoorSlot` |
| Rooms sin spawn points | Salas sin al menos 1 spawn point de enemigos |
| Items sin ícono | `ItemSO` con `Icon = null` |
| Rewards sin resolución | `RewardEntrySO` con `Category = Item` pero `ItemPool` vacío |

Los validators exponen una interfaz común:

```csharp
public interface IContentValidator
{
    string ValidatorName { get; }
    List<ValidationResult> Validate();
}

public struct ValidationResult
{
    public ValidationSeverity Severity;    // Error | Warning | Info
    public string Message;
    public Object Asset;                   // el SO con el problema (para seleccionarlo)
}
```

### 26.10 Bulk Editor

Para cambios masivos de balance:

- "Subir HP de todos los enemigos Melee del piso 3+ en 20%."
- "Cambiar `BaseDamage` de todos los combos tipo `ThreeOfKind` a 30."
- Preview de cambios antes de aplicar.
- Undo support (Unity Undo system).

### 26.11 Content Templates

Menú `Assets > Create > Rollgeon > Templates >` con presets:

| Template | Qué crea | Pre-poblado con |
|---|---|---|
| "Basic Melee Enemy" | `EnemyDataSO` | Stats medios, AI tree melee approach+attack, behavior attack inline |
| "Basic Ranged Enemy" | `EnemyDataSO` | Stats bajos, AI tree keep distance+ranged attack |
| "Standard Combat Room" | `RoomSO` + prefab template | 4 enemy spawn points, 1 reward, 1 door N, 1 door S |
| "Simple NPC Vendor" | `NpcDataSO` + `DialogueGraphSO` | Hub comprar/charlar/salir, `InteractionLabel` |
| "Starter Hero" | `ClassHeroSO` | Stats medios, sheet estándar de 8 combos, 5xd6 bag |
| "Passive Item" | `ItemSO` | Type = Passive, 1 hook vacío |
| "Active Item" | `ItemSO` | Type = Active, 1 effect vacío, cooldown 5 |

**Cross‑ref §26.** Todos los sistemas del proyecto — cada tool opera sobre sus SOs respectivos.

---

## 27. Convenciones de cross‑ref y changelog

- **Numeración estable (0–16 para sistemas de dominio core; §17 agrupa §A–§N como sistemas transversales; §18–§25 para sistemas extendidos (items, rewards, status, quests, tutorial, settings, pooling, analytics); §26 content tooling; §27 convenciones + changelog)**. Para insertar secciones nuevas: agregar al final del bloque correspondiente o usar sufijos `Na`, `Nb` para no reshufflear referencias existentes.
- **Cross‑refs internas** con `(§N.x)` — no markdown links — para que sigan siendo válidas si el documento se parte en varios archivos (`docs/Technical/02-Attributes.md`, etc.).
- **Referencias a código** usan path desde root del repo + línea cuando aplica: `Assets/Rollgeon/Heroes/ClassHeroSO.cs:42`.
- **Al editar**: actualizar el Índice si se agrega/renombra/mueve una sección. Citar sub‑sección del `Game Design.pdf` cuando una decisión se apoya en el GD.
- **Dropdowns obligatorios**: cualquier string ID expuesto al inspector se edita con `[ValueDropdown(nameof(GetXxxIds))]` alimentado desde un catálogo SO. Regla transversal (§0).
- **GUID sobre int**: ownership de entidades, modificadores y eventos siempre va por `Guid`, nunca `int`. Regla transversal (§2.3).
- **Scope**: el documento cubre sólo código de dominio de Rollgeon. No documenta third‑party.
- **Lengua**: español. Nombres de tipos y archivos en inglés.

### 27.1 Changelog

- `2026-04-13` — **v1**: Secciones iniciales — patrones base, atributos, modificadores, clases+sheets, combos, dados, entidades, efectos, behavior values, feedback, selection, resolución de tirada, enemigos+dungeon, meta‑progresión.
- `2026-04-13` — **v2**: Reenfocado a Rollgeon. Removidas secciones de Cards, DeckBuilder y networking.
- `2026-04-13` — **v3**:
  - §0: regla transversal de dropdowns para IDs; referencia a §16.
  - §1.1: `ServiceBootstrapSO` + bootstrap scene para precarga de SOs sin depender de `Awake`.
  - §1.2: schema de payloads documentado al lado del enum `EventName`. Eventos `OnDamageOutgoing/Incoming/Resolved` agregados.
  - §2.3: ownership por `Guid`, razón y alcance.
  - §3: `Modifier<T>` usa `Guid OwnerId` + `ModifierDirection { Outgoing, Incoming, Intrinsic }`. Motivación del cambio explicada.
  - §4: `ClassSO` → `ClassHeroSO`. `Passive` sacada del diccionario de stats (es campo aparte). `[TypeFilter]` en el dictionary de stats. Stats estándar incluye `OutgoingDamageMultiplier` e `IncomingDamageMultiplier`.
  - §12: `DamagePipeline` bidireccional (Outgoing del source → Weakness → Incoming del target → Shield → Health). Ejemplo resuelto de "jugador recibe +50% daño".
  - §13: rediseñado con prefabs + pools procedurales: `RoomSO` con `RoomPrefab` + `RoomLayout` del prefab; `EnemyPoolSO`, `EnemySetupSO`, `RewardPoolSO`, `ObstaclePoolSO`. `FloorLayoutSO` con rango `[RoomCountMin, RoomCountMax]`, lista de `BossRooms` con roll aleatorio, lista de salas especiales obligatorias. `MinimapIconsSO` con prioridad a ícono + fallback text.
  - §14: abstracción `BaseUnlockSO` + subtipos concretos (`HeroUnlockSO`, `DiceUnlockSO`, `ItemUnlockSO`, `PassiveUnlockSO`, `UpgradeUnlockSO`). `UnlockConditionSO` separado, composable con `UC_Or` y AND por default. Multi‑condición por unlock.
  - §15: **nuevo**. Sistema unificado de save con `ISaveable` + cache en memoria + `SaveSettingsSO` con triggers editor‑configurables. Usa `SerializationUtility` de Odin para JSON polimórfico.
  - §16: **nuevo**. Listado de packages (TextMeshPro, Odin, PrimeTween) + pendientes a evaluar.
- `2026-04-13` — **v4**:
  - §10: reescrito completo contra el `FeedbackManager` real de Bot‑Game (`Assets/Scripts/Online/Managers/FeedbackManager.cs`). Ahora cubre DB + entries (10.2), enums (10.3), request DTO single‑player (10.4), pipeline single (10.5), position resolver con 6 modos (10.6), dispatch por type (10.7), secuencias con event bus latched + runners paralelos + patrones de autoría (10.8), efecto autor `EffPlayFeedback` + integración con `TurnManager` (10.9), `ApplyAnimatorFloats` y el contrato enum↔param (10.10), `FeedbackCallbackListener` (10.11), watchdog de timeout (10.12), guía de extensibilidad (10.13).
  - §16.1: **Input System**, **Addressables** y **Localization** promovidos a core (antes estaban en pendientes). Reglas de uso agregadas a §16.2 (maps por fase, `AssetReference` para catálogos grandes, `LocalizedString` sobre SOs).
  - §16.3: pendientes reducidos a Cinemachine, UniTask, Newtonsoft JSON.
- `2026-04-13` — **v5**:
  - §1.2: agregados eventos `OnRunStart/End`, `OnCombatStart/End`, `OnTurnQueueBuilt`, `OnPlayerHealthChanged`, `OnPlayerEnergyChanged`, `OnGoldChanged`, `OnFloatingNumberRequested`, `OnScreenPushed`, `OnScreenPopped`, `OnCrapsSessionStarted`, `OnCrapsBetPlaced`, `OnCrapsResolved`. Schemas de payload documentados.
  - §3: `ModifierLifetime { Turns, Permanent, Run, Encounter }` reemplaza la convención implícita del int. `Permanent` no se auto‑suscribe a nada; `Run`/`Encounter` se enganchan a `OnRunEnd`/`OnCombatEnd`. Absorbe los "run buffs" y "boss debuffs" del GDD sin inventar eventos nuevos.
  - §5.5: **Combo counters** (Balatro‑style) — `IComboCounterHook`, `RunComboCounterState : ISaveable`, `ComboCounterThresholdSO` que reutiliza `UnlockConditionSO` para evaluar y `EffectData` para disparar rewards.
  - §5.6: **Strike combos** — `IStrikableCombo` marker, `RunStrikeState : ISaveable`, `EffStrikeCombo`; `ContractSheet.EvaluateRoll` filtra combos strikados.
  - §6.5: **Reroll budget (energy re‑roll)** — `IRerollBudget`, `IDiceRoller.QueryReroll`, `RerollAvailability`. Aplicable a ataque, defensa y skill checks. Números en `RulesetSO`.
  - §7.5: **AI Decision Trees** — árbol polimórfico inline con `[SerializeReference]` (no SOs). Nodos action/question/sequence/selector/if/random, condiciones polimórficas, clonado del árbol al spawn via `SerializationUtility`. Los enums `EnemyBehavior` quedan descartados — cada enemigo tiene su propio árbol con overrides per‑enemigo.
  - §12.6: **Action economy y repetition constraint** — `ActionDefinitionSO` con `ActionTag`, `BlockOnRepeat`, `AllowsEnergyReroll`; `TurnManager` mantiene `_actionsUsedThisTurn` y lo limpia `OnTurnStarted`. Constraint es opt‑in por acción.
  - §12.7: **Turn order con velocidad oculta** — `IInitiativeProvider` + `TurnOrderService`, `Speed` marcado hidden, UI solo muestra orden vía `OnTurnQueueBuilt`.
  - §13.6: **Room runtime state granular** — `RoomInstance : ISaveable` con `Dictionary<string, RoomObjectState>` indexado por spawn point id. Subtipos `ChestState`, `PotionState`, `ShopItemState`, `DoorState`, `EnemySpawnState`. Re‑entrar a una sala consulta estos flags.
  - §14.7: **`RulesetSO`** — encarna "modo de juego". Contiene MaxEnergy, StartingEnergy, BaseEnergyRegenPerTurn, BaseRollsPerAttack, MaxExtraRerollsByEnergy, DefenseFromUnusedRolls, SpeedDie{Min,Max}, AnimationCurves de scaling (EnemyHP, EnemyDamage, Gold, EnemyCount, ObstacleCount), ForbiddenActionTags, ComboCounterThresholds. Absorbe toda la numerología del juego.
  - §A: **nuevo**. Audio (`IAudioService` con pool, música crossfade, channels Master/Music/Sfx/Ui, integración con §10).
  - §B: **nuevo**. Movement / pathfinding (`IMovementService` con BFS, animaciones via `FeedbackRequest` secuencia, integración con §11 selection).
  - §C: **nuevo**. Craps mini‑game (`CrapsSessionService` + `CrapsSession` + `CrapsConfigSO`, reusa `CombinationDetector` del §5 y `EffectData` del §8 para rewards/penalties).
  - §D: **nuevo**. UI architecture + `IScreenManager` — stack de screens, `BaseScreen` MonoBehaviour, regla de oro "screens solo leen por `EventManager`", sin builders programáticos (el diseñador arma prefabs en engine).
- `2026-04-14` — **v6**:
  - Reorganización estructural: §A–§D (sistemas transversales que v5 dejó sueltos después del changelog) se agrupan bajo una nueva sección paraguas `## 17. Sistemas transversales`. Los identificadores internos `§A`, `§B`, `§C`, `§D` se mantienen como subsecciones de §17, así que los cross‑refs existentes no cambian.
  - §17 previo ("Convenciones de cross‑ref y changelog") corrido a §18 para que el changelog quede efectivamente como cierre del documento.
  - Headings de §A–§D demotados un nivel (h2→h3 para las secciones principales, h3→h4 para las subsecciones A.1..A.5, B.1..B.7, C.1..C.5, D.1..D.8) para reflejar el nuevo anidado bajo §17.
- `2026-04-14` — **v7**: refactor de entidades a parent común.
  - §7.0: **nuevo**. `BaseEntitySO` como parent abstracto que concentra `EntityId`, `DisplayName`, `Description`, `PrefabRef` (Addressables), `Portrait`, `_baseStats`, `Behaviors` y la API común (`GetStat`, `GetStatValue`, `CreateRuntimeStats`). Elimina la duplicación de contrato entre héroe y resto de entidades y cierra el "gap del héroe" (antes `ClassHeroSO` no tenía campo `Prefab`).
  - §7.1: `EntityDataSO` pasa a heredar de `BaseEntitySO` — sólo conserva `Archetype` y el árbol de IA (`AIDecisionNode Root`, §7.5). El `Prefab` legacy (`GameObject` directo) se reemplaza por `PrefabRef` (`AssetReferenceGameObject`) heredado del parent.
  - §4.1: `ClassHeroSO` pasa a heredar de `BaseEntitySO` — sólo conserva `Passive`, `ContractSheet`, `StartingDiceBag`. `ClassId` reemplazado por `EntityId` heredado. `_stats` renombrado a `_baseStats` al moverse al parent. La pasiva sigue fuera del diccionario de stats por las razones del §4.2.
  - §1.1.1: **`HeroCatalogSO` fusionado en `EntityCatalogSO`**. El catálogo es ahora único, tipado sobre `BaseEntitySO`, con filtros por subtipo: `GetHeroes()` devuelve los `ClassHeroSO`, `GetEntities()` devuelve los `EntityDataSO`. `AllPassiveIds` sigue vivo como helper sobre `GetHeroes()`. `ServiceBootstrapSO.RegisterAll` ya no registra `HeroCatalogSO`. Todo `GetClassIds` / `HeroCatalogSO` en snippets de §4 se migró a `GetEntityIds` / `EntityCatalogSO`.
  - §16.2: nota nueva documentando que los prefabs 3D de toda entidad viven en `BaseEntitySO.PrefabRef` como `AssetReferenceGameObject`, preloadeados en bloque por `EntityCatalogSO` durante el bootstrap. Prohibición explícita de catálogos intermedios `string → GameObject` indexados por id (antipatrón que fuerza `Resources.Load` en hot paths — el id es contrato de identidad, no indirección para el visual).
  - **Nota de migración (código actual, fuera del scope de este doc):** el proyecto actual usa `PrefabManagerSO` (`Assets/Scripts/Scriptables/PrefabManagerSO.cs`) + atributo `MeshId` en `PawnAttributes` + `BasePawn.SpawnVisualPrefab` con `Resources.Load`. Al portar a la arquitectura Rollgeon, todo ese flujo desaparece: `PrefabManagerSO` se borra, `MeshId` deja de existir, `BasePawn` deja de instanciar visuales por lookup — el prefab se resuelve desde `BaseEntitySO.PrefabRef` del SO dueño de la entidad.
- `2026-04-14` — **v8**: sistema de cámara scripteada.
  - §17.E: **nuevo**. `ICameraService` registrado en `ServiceLocator`, con rotación snapped a 45° (RMB + drag horizontal del mouse con accumulator), pan libre con MMB que suspende el seguimiento hasta un recenter explícito, zoom con scroll clampeado, recenter por tecla o botón UI, wall occlusion direccional por `CameraFacing`, floor view con shells procedurales al cruzar un `FloorViewZoomThreshold`, smoothing universal con PrimeTween. Todo toggleable desde `CameraConfigSO` (§E.3) por feature individual.
  - §17.E.8: wall occlusion — `WallOccluder` component con `WallDirection` en cada pared del `RoomPrefab`; `CameraService` resuelve qué paredes esconder contra el `OcclusionMap` del config (`Dictionary<CameraFacing, List<WallDirection>>`), editable por el diseñador sin tocar código.
  - §17.E.9: floor view — shells procedurales generadas por `DungeonManager` desde el `Bounds` del `RoomLayout` (§13.3). Cero assets manuales por sala. Las shells son `GameObject`s runtime con un cube mesh escalado al bounds y un material transparente definido por `CameraConfigSO.ShellColor`.
  - §13.3: `RoomLayout` gana campo `Bounds` con `OnValidate` que lo recomputa a partir de los renderers children. Se lee desde el prefab asset sin instanciarlo.
  - §13.6: `GeneratedFloor` gana `Dictionary<Guid, GameObject> FloorShells`; `RoomInstance` gana `Vector3 WorldPosition` explícito; `IDungeonManager` gana `Bounds GetFloorBounds()` para el clamp del pan. `DungeonManager.GenerateFloor` instancia las shells al final. Al `EnterRoom` avisa a `ICameraService.SetFollowTarget`.
  - §1.1.1: `CameraConfigSO` agregado al `ServiceBootstrapSO` como settings‑catalog; `RegisterAll` lo registra para que el `CameraService` lo resuelva al despertar.
  - §16.3: **Cinemachine resuelto — descartado**. El gameplay loop usa scripted camera con PrimeTween; el rationale vive en §17.E.11. `Cinemachine` sale de la tabla de pendientes y queda listado como "descartado" abajo.
  - Índice: §17 lista §E explícitamente.
- `2026-04-14` — **v9**: NPCs + interacción con prompts + per‑phase rules.
  - §7.2: tabla nueva que clarifica la semántica de cada `BehaviorTrigger` (quién lo dispara y cuándo). `BaseBehavior` gana `AllowedPhases` (`GamePhaseMask` flags), un filtro per‑behavior de en qué fases es elegible (default: `All`). Los dispatchers (movement, interaction, turn, damage pipeline) filtran por este flag antes de ejecutar — behaviors que no pasan se ignoran silenciosamente en esa fase.
  - §7.6: **nuevo**. `NpcDataSO : BaseEntitySO` con `NpcRole` (Vendor | Dialogue | QuestGiver | Trainer | Generic), `InteractionLabel` (LocalizedString) y `DialogueLines` opcional. **La interactabilidad de un NPC es decisión del prefab**: se decide por la presencia (o ausencia) de un `InteractableComponent` en la jerarquía del prefab, no por un flag en el SO. NPCs decorativos simplemente no llevan el component — el service los ignora. Catalogados en `EntityCatalogSO.GetNpcs()`.
  - §7.7: **nuevo**. Sistema unificado de interacción:
    - `InteractionMode` = `Direct | Prompt | Both | Disabled`.
    - `PhaseInteractionRule` por `GamePhase` con `Mode`, `PromptRange`, `LabelOverride`, `Priority`.
    - `InteractableComponent` con `List<PhaseInteractionRule>`: una rule por fase en la que el interactable está activo; fases sin rule son equivalentes a `Disabled`.
    - `IInteractionService` con detección de proximidad, filtro por fase, best target por `Priority` + distancia, **precondition check al mostrar** (prompt grayed out si fallan — mejor UX), integración con `RoomObjectState.Consumed` (consumed gana sobre cualquier rule), dispatch del `BaseBehavior(OnInteract)` con filtrado por `AllowedPhases`.
    - Ejemplo canónico: la puerta con `Exploration: Direct` (auto‑pass al pisar el tile) + `Combat: Prompt "Forzar puerta"` (skill check que al success dispara `EffLeaveRoom`). Misma entity, dos behaviors con `AllowedPhases` disjuntas, dos `PhaseRules` con `Mode` distinto — cero conflicto.
    - **No hay regla global "prompts sólo fuera de combate"** — el diseñador decide por fase y por entidad qué está accionable. Cofres, shops y NPCs pueden ser interactuables en combate si así se autorea. Esto reemplaza el pattern que un draft previo había asumido.
  - §1.1.1: `EntityCatalogSO` gana el filtro `GetNpcs() => _entries.OfType<NpcDataSO>()` junto a `GetHeroes()` y `GetEntities()`.
  - §1.2: eventos nuevos `OnInteractionTargetChanged` (args: `[Guid, string, bool]`) y `OnInteractionExecuted` (args: `[Guid]`). Agregados al enum, al schema block y a la tabla de familias.
  - §13.6: bullet explícito sobre auto‑registro de `InteractableComponent`s al instanciar la sala (vía `OnEnable`) y desregistro al salir. Nota sobre `Consumed` bloqueando prompts como regla ortogonal a las `PhaseRules`.
  - §17.D: **nueva §D.6a `InteractionPromptView`** — un único botón compartido en el `HUDScreen.prefab` que cambia texto y estado según el `CurrentTarget` del service. Se suscribe a `OnInteractionTargetChanged`, modula `CanvasGroup.alpha` con PrimeTween, llama a `IInteractionService.ExecuteCurrent` al click. La acción `Interact` (default `F`) se agrega al map `Gameplay` del Input System — misma lógica que el resto (gate por `GamePhase`, re‑asignable, persistida por el save system).
  - **Deuda de doc:** el enum `GamePhase` se referencia desde §1.2, §7.2, §7.7, §16.1 y §17.E pero **no está formalmente definido** en ninguna sección. Queda anotado para formalización en §12 `TurnManager` en la próxima pasada — fuera de scope de v9 para no explotar la pasada.
- `2026-04-15` — **v10**: refactor del §7 y alignment del §8. Cuatro mejoras que responden al doc debt acumulado de v7/v9.
  - §7.1: **`EntityDataSO` renombrado a `EnemyDataSO`**, y el enum `EntityArchetype` pierde `Interactable` y pasa a `EnemyArchetype { Melee, Ranged, Support, Boss }`. El campo `Root : AIDecisionNode` se renombra a `AIRoot` para ser explícito. Motivo: los cofres, puertas y pociones arrastraban un `AIRoot` inútil y un `Archetype` inventado. Una entity es **ahora** o bien enemy (IA + combat archetype) o bien prop (decorativa/accionable estática) — el split disuelve la ambigüedad.
  - §7.1b: **nuevo — `PropEntitySO : BaseEntitySO`** para objetos inertes de sala (chests, doors, potions, traps, torches, decoration). Campo `Category : PropCategory` + flag `SingleUse`. Sin IA, sin archetype. Persistencia reusa `RoomObjectState.Consumed` (§13.6) sin cambios.
  - §7.2: tabla de triggers se extiende con una columna **Context subtype** que linkea cada `BehaviorTrigger` al subtipo de `BehaviorContext` que su dispatcher construye (§7.3). El doc debt de "§7.7.4 menciona `TriggeringEntity` pero la struct plana de §7.3 no lo tenía" queda cerrado.
  - §7.2b: **nuevo — `BehaviorSlot` + `BehaviorLibrarySO`**. `BaseEntitySO.Behaviors` pasa de `List<BaseBehavior>` a `List<BehaviorSlot>`. Cada slot es `Inline` (autoreado inline) o `FromLibrary` (referencia por id a un template del `BehaviorLibrarySO`). Al spawn, `BaseEntitySO.CreateRuntimeBehaviors(lib)` deep‑clonea vía `SerializationUtility.CreateCopy` — mismo patrón de §7.5 para AI trees. Overrides per‑entity: el slot `FromLibrary` expone un botón **"Break out to inline"** que copia el template al slot y rompe el vínculo. Sin overrides campo‑a‑campo (decisión explícita — evita reflection complexity sin pagar beneficio real). `BehaviorLibrarySO` se registra en `ServiceBootstrapSO` (§1.1).
  - §7.3: **`BehaviorContext` polimórfico por trigger**. La struct plana se reemplaza por una jerarquía `abstract` con subtipos `sealed`: `TurnBehaviorContext`, `DamageBehaviorContext` (con `DamageInfo` + `Attacker` + `BlockedByShield` + `WasLethal`), `InteractionBehaviorContext` (con `InteractableComponent` + `PhaseInteractionRule`), `MovementBehaviorContext` (con `PreviousTile`/`CurrentTile`/`DistanceToOwner`), `EventBehaviorContext` (con `EventName` + `Payload`). La base expone `OwnerGuid`, `SourceEntity`, `TriggeringEntity`, `CurrentRoomId`, `CurrentPhase : GamePhase`, `SourceBehavior`. Cada dispatcher es responsable de construir el subtipo correcto — el contrato queda documentado en la tabla de §7.2.
  - §7.6: **`NpcDataSO.DialogueLines : List<LocalizedString>` reemplazado por `Dialogue : DialogueGraphSO`**. Motivo: una lista plana no soportaba branching, preconditions de visibility/enabled por choice, ni effects al final de una rama. El cambio es breaking vs. v9 pero no hay contenido autoreado todavía (proyecto en fase de diseño técnico).
  - §7.6b: **nuevo — `DialogueGraphSO`**. Grafo polimórfico inline con nodos `[SerializeReference]`: `DialogueLineNode`, `DialogueChoiceNode` (con `List<DialogueChoice>`), `DialogueEffectNode` (dispara `EffectData`), `DialogueJumpNode` (saltos por `NodeId` para hubs con "Volver atrás"), `DialogueEndNode`. Cada `DialogueChoice` lleva `VisibleIf` (filtro) y `EnabledIf` (gray out) como `List<BasePreCondition>` — reusa §8.2 sin duplicar. Ejemplos canónicos en el doc: vendor con 4 ramas + quest giver con skill check. El `DialogueScreen` (§17.D) implementa el walker del grafo.
  - §7.7.4: `IInteractionService.ExecuteCurrent()` ahora construye explícitamente un `InteractionBehaviorContext` (§7.3) con `Interactable`, `Rule`, `DistanceToTriggerer` — cerrando el gap que v9 dejaba ambiguo ("construye el BehaviorContext").
  - §8.4: **`EffectContext` gana `TriggerContext : BehaviorContext`** (referencia polimórfica al contexto del trigger) + helper genérico `TryGetTriggerContext<T>(out T ctx) where T : BehaviorContext`. Un efecto `EffReflectDamage` ahora puede hacer `ctx.TryGetTriggerContext<DamageBehaviorContext>(out var dmg)` y leer `dmg.Attacker` + `dmg.Damage.FinalAmount` sin casteos manuales ni lookups. Ejemplo full en el doc.
  - §8.5: **nueva capability interface `IRequiresTriggerContext<TCtx>`** en la tabla. El inspector valida (soft, warning naranja) que los effects con este marcador sólo se enganchen a behaviors cuyos triggers construyen el subtipo pedido. Se documenta como soft check porque `OnEvent` puede transportar cualquier `Payload`.
  - §1.1.1: `EntityCatalogSO` gana `GetEnemies()` + `GetProps()` (reemplazan `GetEntities()`), `AllDialogueIds` para alimentar el dropdown del `DialogueGraphSO`, y `GetDialogueById(id)`. El bootstrap de §1.1 suma `BehaviorLibrarySO` a los catálogos pre‑cargados.
  - §13.4: **`WeightedEntity` renombrado a `WeightedEnemy`** y el pool tipado sobre `EnemyDataSO`. **Nuevo `PropPoolSO`** para resolver cofres/puertas/pociones por spawn point, tipado sobre `PropEntitySO`.
  - §4.5: el flujo de inicio de run suma `Player.RuntimeBehaviors = hero.CreateRuntimeBehaviors(behaviorLibrary)` junto al `CreateRuntimeStats()` existente.
  - §4.1, §13.8, §0 (namespaces + directorios): cross‑refs actualizadas a los nuevos subtipos.
  - **Razón del orden de ejecución** (split → context → library → dialogue): el split renombra un tipo central; hacerlo primero evita churn en las mejoras siguientes.
  - **No tocado en v10:** §9 `StoredValues` (su API pública en `BaseBehavior` quedó flagueada para una futura pasada, pero v10 no lo encapsula — el alcance ya es grande); el split de `GamePhase` sigue siendo deuda de §12.
- `2026-04-15` — **v11**: rediseño del `CombatTurnFSM` (§1.3) + formalización de `GamePhase` (§12.0). Cierra la deuda doc de v9/v10 y ataca ocho debilidades del draft previo del FSM.
  - **§1.3 — rediseño del `CombatTurnFSM`.**
    - **Convención de naming.** Ningún micro‑state lleva `Phase` en el nombre (palabra reservada al enum macro). Los viejos `RollPhase` / `RerollPhase` / `DefendPhase` se renombran a `Roll` / `Reroll` / `Defend`. Elimina la colisión con `GamePhase`.
    - **`CheckCombatEnd` explícito.** Corre **post cada `ApplyDamage`** (player attack, enemy attack, reactivos, traps, DOTs). Centraliza la evaluación de victoria/derrota y evita que cada state replique el check. Terminal states `Victory` / `Defeat`.
    - **Iteración de enemigos first‑class.** El `EnemyAction*` con asterisco del draft anterior se reemplaza por `EnterEnemyBranch → EnemyPickNext → EnemyAction → ApplyDamage → ResolveReactions → CheckCombatEnd → EnemyPickNext (loop)`. `EnemyPickNext` descarta enemigos que murieron mid‑turno por un reactivo. Sale del loop con `EnemyQueueEmpty`.
    - **`Interact` unificado.** El viejo `SecondaryAction (heal, door, chest)` desaparece. El nuevo state `Interact` delega al `IInteractionService` (§7.7), que resuelve heal, cofre, puerta forzada, NPC vendor, shop vía `PhaseInteractionRule(Combat, …)`. Los inputs legacy `Heal` / `ForceDoor` salen del `TurnInput` enum — ambos son ahora `Interact`. Una sola ruta unificada para cualquier acción estática en combate.
    - **Split de `CleanupModifiers`.** El viejo `CleanupModifiers` único se parte en **`CleanupPlayerModifiers`** (entre fin del turno del player y `EnterEnemyBranch`) y **`CleanupEnemyModifiers`** (después de que **todos** los enemigos hayan actuado). Motivo: un debuff `Duration = 1` aplicado al `Enemy 2` no debe gastarse cuando actúa el `Enemy 1` — el tick ocurre al cerrar la fase completa. Regla explícita: modificadores globales simétricos se modelan como dos modificadores espejados (uno `OwnerId = player`, otro `OwnerId = enemyBand`).
    - **`CheckTurnSkip` para CC.** Nuevo state entre `StartPlayerTurn` y `PlayerInput`. Chequea stun / freeze / sleep y, si aplica, transiciona directo a `CleanupPlayerModifiers` sin pasar por input. Reemplaza el pattern ad‑hoc de guardas dentro de `PlayerInput`.
    - **`ResolveReactions` explícito.** Nuevo state entre `ApplyDamage` y `CheckCombatEnd`. Dispara los behaviors `OnDamaged` del target (filtrados por `AllowedPhases`) y, si un reactivo vuelve a hacer daño, re‑entra en `ApplyDamage` recursivamente con stack depth cap. Evita que contragolpes y thorns se pierdan mid‑turno.
    - **`TurnInput` actualizado.** Gana `CombatEnded`, `ReactionResolved`, `EnemyQueueEmpty`, `TurnSkipped` (inputs system‑driven). Pierde `Heal` y `ForceDoor` (ahora `Interact`).
  - **§12.0 — nuevo: `GamePhase` formalizado.** Enum canónico `{ Exploration, Combat, Craps, Shop, Cutscene }` con `= 0..4` explícitos. Explica la relación enum plano (valor) vs. `GamePhaseMask` (filtro de behaviors), quién es el owner del estado (`TurnManager` con `SetPhase` + `OnPhaseChange`), regla "nadie lee `CurrentPhase` en `Update()`", tabla de transiciones típicas (quién dispara cada `Exploration ↔ Combat ↔ Shop ↔ Craps`), y la aclaración de que el `CombatTurnFSM` **vive dentro** de `GamePhase.Combat` — sus micro‑states son invisibles para listeners externos del `OnPhaseChange`. Cierra la deuda de doc que v9 y v10 habían flagueado.
  - **Cross‑refs actualizadas.** §7.5 (el bullet sobre "FSM sigue viva para el flow macro de combate" cita los nuevos state names). §12.3 (flujo completo de ataque — el paso 1 ahora referencia `el CombatTurnFSM está en el state Roll` en vez de `TurnManager está en RollPhase`). §7.2 (la nota de doc debt se reemplaza por un puntero a §12.0 — deuda cerrada).
  - **No tocado en v11.** §9 `StoredValues` sigue pendiente (arrastrado desde v10); la formalización del `CutsceneService` queda apuntada en la tabla de transiciones de §12.0 como TBD, sin implementación.
- `2026-04-17` — **v12**: rediseño de `GamePhase` como stack + overlays, shop estilo Isaac, craps como overlay de combate.
  - **§12.0 — reescrito: `GamePhase` + `PhaseOverlay` + `IPhaseService`.**
    - **Enum reducido.** `GamePhase { None, Exploration, Combat, Loading, GameOver }` — 5 valores, estables para saves. `Shop` eliminada (ítems en el piso, §17.F). `Craps` movido a overlay. `Cutscene` movido a overlay.
    - **`PhaseOverlay { None, Pause, Cutscene, Craps }`.** Overlays suspenden la fase base sin destruirla. Push/Pop con stack.
    - **`IPhaseService`** con `ReplacePhase(next)`, `PushOverlay(overlay)`, `PopOverlay()`. Reemplaza el viejo `TurnManager.SetPhase`. El `CombatTurnFSM` se congela automáticamente cuando hay un overlay activo y se reanuda al pop.
    - **Queue + drain** para reentrancy: si un listener llama `ReplacePhase` dentro de un handler, la transición se encola y se despacha al terminar el dispatch actual.
    - **`PhaseTransitionMatrixSO`** editable desde engine con `[TableMatrix]` de Odin para transiciones de fase base + `Dictionary<GamePhase, List<PhaseOverlay>>` para overlays válidos. Validación con assert en debug/editor. Tabla default documentada. Craps sólo válido como overlay de `Combat`.
    - **Dos eventos** en vez de uno: `OnPhaseExit(exiting)` + `OnPhaseEnter(entering)` para fase base, `OnOverlayPushed(overlay)` + `OnOverlayPopped(overlay)` para overlays. Reemplaza el viejo `OnPhaseChange(prev, next)`.
    - **`DeathWatchService`** — subscriber global de `OnHealthChanged` que detecta muerte fuera de combate (traps, ítems malditos, penalidades) y transiciona a `GamePhase.GameOver`. Dentro de combate, `CheckCombatEnd` del FSM ya cubre el caso.
    - **Save policy explícita.** Save sólo al terminar combate o finalizar sala/piso. Nunca mid‑combat, nunca durante overlay. `CombatTurnFSM` y `CrapsSession` no necesitan ser `ISaveable`.
    - **Regla de estabilidad del enum.** Nunca renumerar, siempre append, deprecados reservados.
  - **§1.2 — events actualizados.**
    - Tabla de familias: `OnPhaseChange` removido. Nueva familia `Phase (§12.0)` con `OnPhaseExit`, `OnPhaseEnter`, `OnOverlayPushed`, `OnOverlayPopped`. Nueva familia `Shop (§17.F)` con `OnShopItemTargetChanged`, `OnShopItemPurchased`, `OnShopRestocked`.
    - Enum `EventName`: schemas de payload documentados para los 7 nuevos eventos.
  - **§7.2 — `GamePhaseMask` reducido.** Sólo bits para `Exploration` y `Combat`. `Shop`, `Craps` y `Cutscene` eliminados del mask — Shop ya no es fase, Craps y Cutscene son overlays que no participan en el filtro de behaviors. Nota de doc debt reescrita con puntero a §12.0.
  - **§7.7 — shop items en ejemplos.** Intro actualizada: "shop items en el piso (§17.F)" en la lista de interactables. §7.7.5 gana ejemplo canónico de shop item con precondition de gold, `EffDeductGold`, `EffAddItem`, `EffConsumeProp`. NPC vendor actualizado: "no abre pantalla de shop — los ítems ya están en el piso".
  - **§17.C — Craps reescrito como overlay de combate.**
    - Trigger: prop `CrapsTable` en sala de combate con `AllowedPhases = Combat`, behavior `OnInteract` ejecuta `EffStartCraps` que registra `ICrapsSessionService` y pushea `PhaseOverlay.Craps`.
    - Flujo actualizado: empieza en `PlayerInput` del `CombatTurnFSM`, push congela FSM, mini‑juego corre, pop descongela, combate continúa.
    - `CrapsConfigSO` ahora referenciado desde el `PropEntitySO` de la mesa, no desde el `RoomSO`.
    - Pasiva "Craps anticipado" actualizada: modifica weight de spawn de mesas de craps en salas de combate.
    - **Tabla C.7** con campos esperados del `CrapsConfigSO`.
  - **§17.D — UI actualizada.**
    - `ScreenId.Shop` eliminado del enum.
    - **§D.6b `ItemInspectView`** — nuevo widget del HUD que muestra nombre, descripción, precio e ícono de un shop item al acercarse. Se suscribe a `OnShopItemTargetChanged`. Coexiste con `InteractionPromptView` (el prompt dice `[F] Comprar (25G)`, el inspect view muestra el detalle).
    - §D.1: lista de screens actualizada (sin Shop).
  - **§17.F — nuevo: Shop como ítems en el piso (estilo Isaac).**
    - **`IShopManagerService`** — servicio de sala que coordina spawning de ítems desde pools, metadata para UI, purchase callbacks, y restock. Se registra al entrar a sala Shop, se desregistra al salir.
    - **`ShopPoolSO`** — pool pesado de ítems editable desde engine. `List<WeightedShopItem>` con `Reward`, `Weight`, `BasePrice`, `MinFloorDepth`. Rolea un ítem por spawn point.
    - **`ShopConfigSO`** — config global de pricing, slots, restock. `PriceMultiplier`, `PriceVariance`, `MaxItemSlots`, `AllowRestock`, `RestockCost`, `MaxRestocks`, `FirstPurchaseDiscountPercent`. Referenciado desde `RulesetSO` (§14.7).
    - **Ejemplo canónico** — prop `ShopPedestal` con `InteractableComponent(Exploration, Prompt)` + behavior `OnInteract` con `EffDeductGold` + `EffAddItem` + `EffConsumeProp` + `EffNotifyShopPurchase`.
    - **Restock machine** — prop opcional con `EffRestockShop` que re‑rolea slots no comprados.
    - **Tabla F.6** con los 4 SOs esperados (`ShopPoolSO`, `ShopConfigSO`, `PropEntitySO` "ShopPedestal", `PropEntitySO` "RestockMachine").
    - Integración con save (§13.6 `ShopItemState` sin cambios), pasivas (modifiers sobre `ShopPriceMultiplier` y `MaxRestocks`).
  - **§0 TOC** — §17 index actualizado con §C (overlay de combate) y §F (shop). §18 numeración actualizada (§A–§F).
  - **No tocado en v12.** §9 `StoredValues` sigue pendiente; `CutsceneService` sigue TBD; `FloorLayoutSO.CrapsRooms` y `CrapsChance` quedan como legacy — las mesas de craps ahora se spawnean como props en salas de combate vía `PropPoolSO`, no como tipo de sala dedicado. La limpieza de `CrapsRooms` del `FloorLayoutSO` queda para una pasada futura.
- `2026-04-17` — **v13**: queries inline polimórficas — `GenericTargetQuerySO` reemplazado por `BaseTargetQuery`.
  - §11.2: campo `GenericTargetQuerySO GenericTargetQuery` reemplazado por `[OdinSerialize, SerializeReference] BaseTargetQuery TargetQuery` — query inline polimórfica en vez de referencia a SO compartido.
  - §11.2b: **nuevo**. `BaseTargetQuery` (abstracta, `[Serializable, HideReferenceObjectPicker]`) + `TargetQueryContext`. Catálogo inicial de 9 queries concretas con prefijo `TQ_`: `TQ_AdjacentEnemies`, `TQ_EnemiesInLineOfSight`, `TQ_AllEnemies`, `TQ_EmptySlots`, `TQ_ReachableTiles`, `TQ_OccupiedSlots`, `TQ_AvailableCombos`, `TQ_Self`, `TQ_Composite`. Sigue el patrón de `BasePreCondition` (§8.2) y `AIDecisionNode` (§7.5).
  - §11.4: `SelectionController` ahora resuelve targets via `settings.TargetQuery.Evaluate(ctx)`.
  - §5.6: comentario de `EffStrikeCombo` actualizado — referencia `TQ_AvailableCombos` (§11.2b) en vez de `GenericTargetQuerySO`.
  - §B.4: `EffMove` actualizado — referencia `TargetQuery = TQ_ReachableTiles` (§11.2b) en vez de `GenericTargetQuerySO = ReachableTilesQuery`.
  - §0: directorio `Effects/Queries/` agregado para `BaseTargetQuery` + `TQ_*` concretes.
  - **Rationale.** Misma motivación que §7.5 (AI trees): si las queries fueran SOs, editar la de un efecto afectaría a todos los que comparten el asset. Inline por defecto, cada efecto con su propia instancia configurable.
- `2026-04-17` — **v13**: mejoras arquitectónicas, 8 sistemas nuevos, tooling para diseño.
  - **§1.1 — `ServiceLocator` con `ServiceScope`.**  `ServiceScope { Global, Run }` permite limpiar los servicios de gameplay al terminar una run sin afectar catalogs ni settings. `ClearScope(Run)` reemplaza al `Clear()` que destruía todo.
  - **§1.2 — `TypedEvent<T>` bus tipado.** Bus complementario al `EventManager` legacy. Elimina casts `object[]` para eventos de alto tráfico. Structs de payload canónicos: `DamageResolvedPayload`, `HealthChangedPayload`, `ComboMatchedPayload`. Eventos nuevos deben usar `TypedEvent<T>`; migración de los existentes es oportunista.
  - **§2.2 — Validación de tipo en `IAttribute`.** `GetValue<T>()` ahora valida que `T` coincida con `GetValueType()` y lanza `InvalidCastException` con mensaje claro en debug.
  - **§3.1 — `Modifier<T>` con `ModifierOperation` enum.** El delegate `Func<T, T, T>` (no serializable) se reemplaza por `ModifierOperation` enum + `OperationResolver` estático. El save system persiste el enum y al restaurar se resuelve a delegate. Operaciones: `Add`, `Subtract`, `Multiply`, `Override`, `Min`, `Max`, `Percent`, `Set`, `And`, `Or`.
  - **§6.1 — D3 eliminado.** `DiceType.D3` removido del enum — no existe como dado físico real. `DiceType` ahora empieza en `D4`.
  - **§7.5 — `AINode_Wait` agregado.** Nodo de acción "pasar turno" para enemigos que esperan estratégicamente.
  - **§12.0.4 — Fix de reentrancy en `DrainQueue`.** Flag `_isDraining` separado de `_isDispatching` para prevenir re-entrada durante el drain de transiciones pendientes.
  - **§14.7 — `MaxReactionDepth` y `MaxActiveItemSlots` en `RulesetSO`.** Formaliza el cap de profundidad de cadenas reactivo→reactivo→… (default 3) y el límite de ítems activos (default 1).
  - **§15.3 — `WeakReference<ISaveable>` en `SaveSystem`.** Previene memory leaks de ISaveables que no llamaron `Unregister`. `CaptureAll`, `CaptureDirty` y `RestoreAll` purgan automáticamente las referencias huérfanas.
  - **§15.4 — Save slots (3 saves estilo Isaac).** `SaveSettingsSO` gana `MaxSaveSlots = 3`, `SaveFilePrefix`, `ActiveSlot`, `SlotExists(i)`, `DeleteSlot(i)`. Cada slot es un archivo independiente.
  - **§7.1b — Patrón de trampas documentado.** Spike trap (OnEntered + damage), poison vent (OnPlayerInRange + status), bear trap (OnEntered + single-use + root). Las trampas no necesitan `InteractableComponent`.
  - **§16.1 — Unity Analytics agregado a packages core** para telemetría de gameplay (§25).
  - **§18 — nuevo: Sistema de Items e Inventario.** `ItemSO` (Passive/Active), `IInventoryService`, `InventoryState : ISaveable`, `ItemCatalogSO`. Pasivos: sin límite, hooks + persistent modifiers. Activos: slot limitado por `RulesetSO.MaxActiveItemSlots`, cooldown, activación manual. Inspirado en Isaac/Slay the Spire.
  - **§19 — nuevo: Sistema de Rewards / Loot.** `RewardEntrySO` define tipo + rangos (no un SO por valor). Categorías: Gold (con rango min/max y scaling por piso), Item (pool pesado), Dice, Heal (fijo o %), Upgrade, Effect (genérico). `IRewardService` con `ResolveRewards` + `PresentRewards` (ShowAll/PickOne/PickN). `RewardCatalogSO`.
  - **§20 — nuevo: Sistema de Status Effects.** `StatusEffectSO` con `StatusValueMode` (Fixed/PercentOfMax/FromReader), `StatusStackRule` (Refresh/Stack/Replace/Immune), `StatusTickBehavior`, inmunidades, feedback. `IStatusEffectService` con apply/remove/tick. `EffAddStatusEffect`. UI de status icons. Integración con `CheckTurnSkip` del FSM para CC.
  - **§21 — nuevo: Sistema de Quests.** `QuestSO` con `CompletionConditions` (reusa `BasePreCondition` §8.2) y rewards. `IQuestService` con `QuestState { NotStarted, Active, Completed, Failed }`. `PCQuestState` precondition para diálogos. `UC_QuestCompleted` para unlocks.
  - **§22 — nuevo: Tutorial / Onboarding.** `TutorialStepSO` con trigger events, UI highlight, forced actions. `ITutorialService`. `TutorialProgressState : ISaveable` persistido como global.
  - **§23 — nuevo: Settings y Accessibility.** `GameSettingsSO` con difficulty override, reduce screen shake, color blind mode, large text, camera sensitivity. `ISettingsService`. Persistido como global.
  - **§24 — nuevo: Object Pooling.** `IObjectPool<T>` genérico, `ComponentPool<T>` con prewarm, `IPoolService` central. Consumidores: FeedbackManager, AudioService, MovementService, DungeonManager, StatusIconsView, RewardScreen.
  - **§25 — nuevo: Analytics y Telemetría.** `IAnalyticsService` con helpers tipados (run start/end, combo used, death, shop purchase). `FileAnalyticsService` con JSON lines a disco.
  - **§26 — nuevo: Content Tooling.** Entity Wizard, Room Designer, Balance Dashboard, Combo Probability Calculator, Dialogue Graph Viewer, Feedback Preview Tool, Unlock Tree Visualizer, Content Validators (CI-ready), Bulk Editor, Content Templates.
  - **§27 — renumerado desde §18.** Convenciones y changelog actualizados a la nueva numeración.
- `2026-04-17` — **v14**: auditoría de consistencia, sistemas faltantes, mejoras arquitectónicas.
  - **Inconsistencias corregidas (10):**
    - §0: namespaces y directorios actualizados con los 10 sistemas nuevos (Audio, Camera, Movement, Shop, Craps, Pool, Settings, Input, Scene, Player).
    - §1.1.1: `ServiceBootstrapSO.RegisterAll` ampliado con `ActionCatalogSO`, `RulesetCatalogSO`, `AudioSettingsSO`, `ShopConfigSO`, `PhaseTransitionMatrixSO`, `GameSettingsSO`.
    - §4.2: stats `Gold`, `MovementRange`, `ShopPriceMultiplier` agregados a la tabla del héroe. `Shield` corregido a "se resetea en `OnTurnStarted`".
    - §7.1: `EnemyDataSO` gana campos `WeakAgainst : BaseComboSO` y `WeaknessMultiplier : float` — cierra el gap del §12.2.
    - §8.7: catálogo de efectos ampliado con `EffLeaveRoom`, `EffConsumeProp`, `EffAddStatusEffect`, `EffRemoveStatusEffect`, `EffDeductGold`, `EffAddItemToInventory`, `EffRestockShop`, `EffNotifyShopPurchase`, `EffStartQuest`, `EffCompleteQuest`, `EffStartCraps`.
    - §10.7: dispatch SFX corregido de `AudioSource.PlayClipAtPoint` a `IAudioService.PlaySfx` (§A.4).
    - §13.2: `RoomType.Craps` removido (mesas de craps son props en combate, §17.C). §13.5: `CrapsRooms`/`CrapsChance` removidos de `FloorLayoutSO`.
    - §14.3: `UnlockStateSO.Instance` reemplazado por `ServiceLocator.GetService<UnlockStateSO>()` en `UC_WinWithNClasses` y `UC_QuestCompleted`.
    - §14.7: `RulesetSO` gana campo `ShopConfigSO ShopConfig`. `RulesetCatalogSO` definido.
    - §7.7.6: scope de `IInteractionService` corregido a `ServiceScope.Run` (no global).
    - §17.C.2: `CrapsConfigSO` dropdown corregido de `EntityCatalogSO.AllCrapsConfigIds` a `CrapsCatalogSO.AllIds`.
    - §17.C.5: `CombinationDetector.DetectAll` reemplazado por `ContractSheet.EvaluateRoll` — reusa la evaluación existente.
    - §17.D.2: `ScreenId.Dialogue` y `ScreenId.Tutorial` agregados al enum.
    - §20.5: `StatusIconsView` migrado de suscripción directa a service events a `EventManager` (cumple regla §D.4).
    - §12.0.8: `PlayerService` reemplazado por `ServiceLocator.GetService<IPlayerService>()`.
  - **Sistemas nuevos (8):**
    - §2.4: **nuevo — runtime `Entity`**. Clase que representa cualquier entidad viva. Campos: `InstanceId`, `Template`, `Attributes`, `RuntimeBehaviors`, hero-specific (`Sheet`, `DiceBag`, `Passive`), enemy-specific (`AIRoot`), `Transform`, `GridPosition`. `ISaveable`. No hay subclases por subtipo de SO.
    - §12.6: **`ActionCatalogSO`** definido con `AllTags` y `GetByTag`.
    - §17.G: **nuevo — `IPlayerService`**. Servicio global con `PlayerGuid`, `RunId`, `CurrentEntity`.
    - §17.H: **nuevo — `IPawnRegistryService`**. Mapping `Guid → Transform` para feedback position resolver y cámara.
    - §17.I: **nuevo — `IGridManager`**. API de la grilla lógica: `IsWalkable`, `IsOccupied`, `GridToWorld`, `WorldToGrid`, `LoadFromRoom`.
    - §17.J: **nuevo — `IInputService`**. Gestión de action maps por fase. Tabla de perfiles por `GamePhase`/`PhaseOverlay`. Tabla de action maps (`Gameplay`, `Camera`, `Dungeon`, `UI`) con acciones y bindings default.
    - §17.K: **nuevo — Scene Management**. Arquitectura de scenes aditivas: `00_Bootstrap`, `01_Persistent` (managers), `02_UI` (canvas root), `03_MainMenu`, `Floor_XX`. `ISceneService` con carga async. Flujo de transición de piso documentado.
    - §17.L: **nuevo — Enums comunes**. `RunOutcome`, `CombatOutcome`, `FloatingNumberType`, `ComparisonOp`.
    - §17.M: **nuevo — `HealPipeline`**. Pipeline simétrica al `DamagePipeline`: outgoing heal multiplier → incoming heal multiplier → clamp a max HP. Stats `OutgoingHealMultiplier`, `IncomingHealMultiplier`. `EffHeal` delega acá.
    - §17.N: **nuevo — Cutscene overlay**. `ICutsceneService`, `CutsceneDataSO` con steps lineales, flujo push/pop de overlay, skip.
  - **Mejoras:**
    - §1.2: tabla de familias de eventos ampliada con Status, Items, Quest, Scene.
    - §27: numeración actualizada a §A–§N.
    - `UpgradeSO` marcado como TBD en §19.
  - `RoomType.Sacrifice` y `FloorLayoutSO.SacrificeRooms` removidos — no definidos en el GDD.
- `2026-04-17` — **v12**: pass de review sobre inconsistencias internas y gaps. Sin rescribir sistemas — arreglos quirúrgicos y un servicio nuevo.
  - §1.2: regla transversal `args[0] : Guid` documentada. Schemas faltantes agregados (`OnTurnStarted`, `OnTurnFinished`, `OnEnergyChanged`, `OnModifierAdded`, `OnModifierRemoved`). Eventos de screen removidos del enum legacy — ahora viven sólo en `IScreenManager` (§17.D).
  - §1.2.1: regla de **canal único** por evento — `EventManager` legacy **o** `TypedEvent<T>`, nunca ambos. Migración no‑oportunista.
  - §3.1: `Modifier<T>.OnLoad()` idempotente (`UnSubscribe` antes de `Subscribe`) y `_resolvedOp` se re‑resuelve en cada load para soportar el camino de restore.
  - §3.5: **nueva** — rehidratación de `Modifier<T>` tras save/load. Todo `ISaveable` que contenga modificadores debe invocar `OnLoad()` en su `RestoreState`.
  - §5.3: `ContractSheet.EvaluateRoll` usa `TryGetService<RunStrikeState>` — no crashea en UI de preview fuera de una run activa.
  - §7.0, §7.2b, §2.4, §4.5: `CreateRuntimeBehaviors()` **sin parámetro** — la `BehaviorLibrarySO` se resuelve internamente vía `ServiceLocator`. Un único punto de verdad.
  - §7.6b: política de **cancelación** del walker documentada — ESC aborta nodos Line/Choice; `DialogueEffectNode` en curso no se interrumpe (atomicidad); `OnDialogueEnded(cancelled: true)` por `TypedEvent<>`.
  - §14.4: `UnlockStateSO.Dirty()` hace `Flush(SaveTrigger.Manual)` inmediato — los unlocks no pueden perderse por crash entre el momento del unlock y el próximo trigger de save configurado.
  - §14.7: `ShopConfig` en `RulesetSO` explicita que son **globals** (pricing, variance, discount, restock), la composición local vive en `ShopPoolSO`.
  - §15.3: payload wrappeado en `SavePayload { SchemaVersion, Data }`. Mismatch de versión → descarta save con warning en vez de crashear al deserializar. No hay migration paths en fase de diseño.
  - §15.3: regla explícita de **rehidratación** — `ISaveable` con objetos que requieren re‑hook de eventos (canónicamente `Modifier<T>`) lo hace dentro de `RestoreState`.
  - §17.D: `IScreenManager` expone events C# directamente; se removió la duplicación con el bus legacy. **Nueva política de pausa global**: `PausesGameplay` por screen + `OnPauseChanged`; tabla de qué sistema congela qué al entrar pausa (audio ducking, tick de status, FSM de combate).
  - §17.F: tabla de **autoridad de pricing** — `BasePrice` en pool, `PriceMultiplier/Variance/Discount/Restock` en `ShopConfigSO`. `FirstPurchaseDiscountPercent` es per‑shop instance (flag en `RoomInstance`), cerrando el exploit de re-entrada.
  - §17.O: **nuevo — `IRngService`**. Sub-streams nombrados con seed determinista por nombre. Prohibición de `UnityEngine.Random` y `new System.Random()` directo en gameplay. `RunRecord.Seed` + `RunRecord.RulesetId` agregados para reproducibilidad de QA.
  - §19: capas de catálogos clarificadas — `ItemSO` autoridad única del ítem, `RewardEntrySO` abstracción polimórfica del reward, `ShopPoolSO` composición específica. Referencias directas (no copias) → cambiar un ítem se refleja en todos los pools automáticamente. No se hizo consolidación — las capas tienen roles legítimos.
  - §20: `StatusEffectSO.PreventsTurn` flag reemplaza el hardcode `"stun"|"freeze"|"sleep"` en `CheckTurnSkip`. Nuevo CC se marca en el SO, sin cambios de código.
- `2026-04-17` — **v15**: cierre de ambigüedades y lifecycles post-review. Doc-only, sin cambios de arquitectura fundamental.
  - **§1.1.3 + §1.1.3.a — spec completa de registro de managers `ServiceScope.Run`.** Nuevo `RunBootstrapper.StartRun(classSO, ruleset, runId)` que instancia y registra **en orden de dependencia** los ~22 managers Run (RNG → Phase → Player → Grid → Movement → TurnOrder → TurnManager → AttackResolver → DamagePipeline → HealPipeline → DiceRoller → Interaction → Status → Reward → Quest → Shop → Craps → DungeonManager) + hookups cross‑service (`Player.BindPassive`, `QuestService.SubscribeEventTriggers`). Simetría: `EndRun` dispara `OnRunEnd` → `SaveSystem.Flush` → `ClearScope(Run)`. Managers con subscriptions en ctor deben implementar `IDisposable`.
  - **§2.4 — `Entity : ISaveable, IDisposable` (partial).** `Passive` pasa a `{ get; private set; }` — sólo mutable via `BindPassive`. `Dispose()` llama `UnbindPassive()` para cleanup en `ClearScope(Run)`.
  - **§4.4.1 — `ClassPassiveSO` es ref compartida e inmutable; el hookup vive en `Entity.BindPassive`.** Los handlers se cierran sobre `InstanceId` y filtran por `OwnerId == args[0]`. Dos `Entity` del mismo SO suscriben independientemente (sin cross-talk). Load de save reinstancia `Entity` y vuelve a llamar `BindPassive`. Anti-patrón: `passive.Subscribe(entity)` en el SO.
  - **§4.5 — flujo de run usa `Player.BindPassive(hero.Passive)`** en vez del viejo `Player.Passive.Subscribe(InstanceId)` que no existía como API.
  - **§5.1 — `BaseComboSO` formaliza la fórmula de cuenta.** Nuevos campos: `ValueMultipliers : float[6]` (index 0 = pip 1, …), `GeneralMultiplier : float`, `ExtraEffects : List<EffectData>`. Método `ComputeCount(finalDice) : float` = `(Σ dado × multi_valor[dado-1]) × multi_general`. Daño final: `BaseDamage + ComputeCount`.
  - **§5.1.1 — nueva sub-sección "Cuenta del combo — fórmula y ejemplos".** Tabla canónica de combos con `BaseDamage`, `ValueMultipliers`, `GeneralMultiplier`. La multiplicación general se aplica sobre la suma, no dado por dado.
  - **§5.6 — guard de `!CanAct` en Strike.** No accesible si `CheckTurnSkip` rechazó el turno; la UI graya el botón. Misma regla aplica a Attack/Defend/Interact — no hay camino para gastar energía en un turno skipped.
  - **§7.5 — `AINode_Attack` + `AINode_UseAbility` usan `ActionId` (cross-ref §12.6).** Los `GetActionIds()` filtran por `ActionType` (`Attack` / `Ability`). Elimina la referencia a un catálogo inexistente.
  - **§9.5 — nueva sub-sección "Lifecycle" de `StoredValues`.** Cierra el pending arrastrado desde v10. Escritura: effects con `IShouldStoreValuesOnBehavior` durante resolve. Captura: `FeedbackRequest.CaptureState` copia snapshot. Lectura: `FeedbackBus` consume del snapshot. Limpieza: (1) `BaseBehavior.Execute.finally` post-resolve, (2) red de seguridad en `OnTurnFinished`. Valores no sobreviven entre turnos.
  - **§12.0.3 — regla `ReplacePhase` prohibido desde overlay.** Si `CurrentOverlay != None`, `ReplacePhase` tira `InvalidPhaseTransitionException` en Debug y es no-op en Release. Patrón legal para "cutscene que lleva a GameOver": `PopOverlay()` primero, luego listener de `OnOverlayPopped` hace `ReplacePhase`.
  - **§12.2.1 — nueva sub-sección "Fuentes de daño no-combo".** DOTs, pasivas, trampas y efectos ambientales pasan por `DamagePipeline.Resolve` igual que combos, pero **no disparan `IsWeaknessHit = true` por default**. Weakness solo se activa cuando hay un combo explícito declarado. Nuevo enum `AttackKind { ComboAttack, BasicAttack, DamageOverTime, Environmental, Reaction, ScriptedAbility }`.
  - **§12.2.2 — nueva sub-sección "Reactions y post-mortem".** `ResolveReactions` corre **antes** de `CheckCombatEnd`: un thorns on-death alcanza a golpear al attacker aunque el target muera. Cap de recursión (default 8). Enemigos muertos mid-turno se descartan de la cola. Daño post-mortem del caster se modela con `OnDamaged` + `WasLethal = true`.
  - **§12.6.0 — `ActionCatalogSO` catálogo maestro unificado.** `ActionType { Combo, Attack, Ability, Defend, Move, Interact, UseItem, SkillCheck }`. `ActionDefinitionSO` gana `Type` + `BackingAsset : ScriptableObject` (puede apuntar a `BaseComboSO` o `ItemSO`). Convención de naming `<tipo>.<subtipo>.<nombre>`. `TurnManager._actionsUsedThisTurn` ahora indexa por `ActionId`, no `ActionTag`. `RulesetSO.ForbiddenActionTags` renombrado a `ForbiddenActionIds`.
  - **§13.6.1 — nueva sub-sección "Regla de serialización polimórfica".** `RoomInstance.ObjectStates : Dictionary<string, RoomObjectState>` lleva `[SerializeReference]`; cada subtipo (`ChestState`, `ShopItemState`, …) marcado `[Serializable]`. Sin `[SerializeReference]` el serializer degrada a la base y pierde campos al round-trip — bug silencioso de save corruption solo visible cross-session. Test canónico `RoomObjectState_Polymorphism_SurvivesRoundTrip` documentado.
  - **§15.3 — `WeakReference<ISaveable>` cleanup formalizado.** `CaptureAll` / `CaptureDirty` / `RestoreAll` iteran en reverso y purgan entries cuyo `TryGetTarget` devuelve `false`. No hace falta sweep explícito.
  - **§15.3.1 — nueva sub-sección "Flush strategy".** Bajo `AsyncFlushThresholdBytes` (default 500 KB): sincrono. Sobre threshold: `Task.Run` con write-to-temp + atomic rename. `SaveTrigger.Exit` **siempre** sincrono. Lock serializa writes concurrentes al mismo path. UniTask (§16.3) es opcional — `Task.Run` funciona sin él.
  - **§15.4 — `SaveSettingsSO` gana `AsyncFlushThresholdBytes`.**
  - **§17.J — tabla de perfiles de action maps expandida** con fila por combinación `(GamePhase, PhaseOverlay)`. Regla de mantenimiento: agregar phase/overlay nuevos requiere agregar fila. Fail-soft en Release (solo `UI` activo si no hay match). Clarificación: Shop y Dialogue **no** son overlays — corren durante `Exploration` sin congelar fase base. Sin `Craps/Cutscene/Dialogue` map dedicado — todos usan `UI` + screen buttons.
  - **§18.1 — `ItemSO` gana `ConsumesAction : bool` + `ActionId : string`** (default `item.<ItemId>`). `IInventoryService.ActivateItem` consulta `TurnManager.CanExecute` antes de ejecutar si `ConsumesAction == true`. Activos "gratis" (lectura de lore, side-effects) no ocupan slot.
  - **§19.6 — nueva sub-sección "`UpgradeSO` — upgrades de combo".** Cierra el TBD de v14. Scope **run-only**, apuntan a `BaseComboSO` del `ContractSheet` activo. Deltas: `DeltaBaseDamage`, `DeltaGeneralMultiplier`, `DeltaValueMultipliers[6]`, `ExtraEffects`. `TargetCombo = null` → upgrade global a todos los combos. `RunUpgradeState.Resolve(combo, catalog) : ComboResolvedSpec` compone todos los upgrades aplicables. `UpgradeCatalogSO` registrado en bootstrap. Ejemplos: `upg.full_house.empowered` (+20 BaseDamage), `upg.odds.sharpen` (pip 3 ×2), `upg.all.rage` (+25% GeneralMultiplier a todos).
  - **§20.2.1 — nueva sub-sección "Integración con `Modifier<T>`".** `ApplyStatus` crea concretamente: (1) uno o más `Modifier<T>` via effects del `OnApply`, trackeados en `ActiveStatusEffect.AppliedModifierIds`; (2) un `Tag` sobre el entity (`Status.<StatusId>`); (3) entry en `_activeByEntity` del servicio. `RemoveStatus` limpia los tres en bloque. Nueva capability `IEmitsModifier` para que effects expongan el modifier `Guid` emitido al service. Tabla de ejemplos (Burn, Slow, Stun, Shield, Regen) con desglose.
  - **§21.1 — `QuestSO` gana `ProgressTriggers : List<EventName>`.** §21.2 — `IQuestService.SubscribeEventTriggers()` se auto-suscribe a cada `EventName` de las quests `Active` y re-evalúa `CompletionConditions` en cada fire. `CompleteQuest` manual sigue disponible para quests narrativas sin tracking. `QuestService : IDisposable` para cleanup en `ClearScope(Run)`.
  - **Tabla de migración de IDs (#11 del review).** `ComboId` sigue existiendo como `BaseComboSO.ComboId` para el lookup tipado dentro del dominio de combos; pero cuando se despacha contra el action economy, va como `ActionId = "combo.<ComboId>"` en `ActionCatalogSO`. `AttackId` **removido** — los `AINode_Attack` ahora usan `ActionId`. `AbilityId` **removido** — ídem via `AINode_UseAbility`. `ItemId` sigue existiendo como identidad del `ItemSO`, pero el activo se ejecuta bajo `ActionId = item.<ItemId>` en action economy. Unificación transparente — los catálogos específicos preservan su tipado, `ActionCatalogSO` es el punto único de verdad para "cosa ejecutable en combate".
  - **No tocado en v15 (explícito).**
    - `Packages/manifest.json` — decisión del usuario: se ignora la discrepancia de packages (Addressables, Localization, Odin, PrimeTween) hasta decisión de diseño posterior. El doc sigue describiendo el stack objetivo.
    - UniTask / Newtonsoft JSON siguen TBD en §16.3. `Task.Run` en §15.3.1 cubre el caso del flush async sin dependencia adicional.
    - Polish items (Movement/Grid ownership, IPlayerService/PawnRegistry roles, Tutorial × overlays, accessibility module, analytics opt-in, dropdowns scale): listados como baja prioridad en el plan de review, no abordados en este pass.
