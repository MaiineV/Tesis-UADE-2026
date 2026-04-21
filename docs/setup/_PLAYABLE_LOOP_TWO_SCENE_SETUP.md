# Guía paso a paso — Loop jugable (arquitectura de 2 escenas)

> **Audiencia:** agustin (solo dev) durante el setup manual post-MCP del Sprint03 FP.
> **Generado:** 2026-04-20.
> **Tiempo total estimado:** 2h 30min — 3h.
> **Resultado esperado al final:** dar Play desde `00_Bootstrap.unity`, pasar por Main Menu → Class Selection → Build Selection → Exploration → Combat → Victory/Defeat → Return to Menu, sin errores en consola.

---

## 0. Qué cambia respecto al estado actual

El round 2 (ver `_SETUP_ROUND2_STATUS.md`) dejó **todo** cableado dentro de `01_MainMenu.unity`: los screens de menú, de selección, los HUDs de exploración y combate, las overlays de pausa/victoria/derrota. Conveniente para el primer wireup pero tiene tres problemas:

1. **Acoplamiento.** Una sola escena con ~120 GameObjects mezcla UI de menú con UI de gameplay. Cualquier cambio a un canvas toca el otro.
2. **Testing.** No podés probar sólo el combate abriendo una escena chica — arrastrás 10 screens extra.
3. **Convención Unity.** El patrón estándar es `Bootstrap → MainMenu → Gameplay[/Game]`. Los HUDs viven en la escena de gameplay, no en la de menú.

Esta guía rearma el proyecto en **tres escenas** (Bootstrap + MainMenu + Gameplay) manteniendo el resto del trabajo que ya tenés hecho.

---

## 1. Arquitectura final de escenas

```
00_Bootstrap.unity                    [persistente — DontDestroyOnLoad]
├── Bootstrap                          Global services + scene chain
│   ├── BootstrapRunner                ← dispara RegisterAll + LoadScene("01_MainMenu")
│   ├── RunControllerBootstrapper      ← RunController.CreateAndRegister(Floor1_Layout)
│   ├── GameplaySceneLoader            ← listener OnRunStart → LoadScene("02_Gameplay")
│   └── PendingRunRequest              ← carrier liviano para pasar hero/runId entre escenas

01_MainMenu.unity                     [UI-only, sin lógica de run]
├── Main Camera                        Overlay camera (UI Canvas overlay ignora la cam, pero sirve para skybox/bg)
├── Directional Light                  (opcional, si hay 3D en el fondo)
├── EventSystem                        StandaloneInputModule / InputSystemUIInputModule
├── ScreenHost                         _initialScreenStringId = "MainMenu"
└── Canvas (Screen Space — Overlay, 1920×1080)
    ├── MainMenuScreen                 Jugar / Salir
    ├── ClassSelectionScreen           Guerrero seleccionable, Mago/Picaro locked
    └── BuildSelectionScreen           Hero preview + dice bag + Confirm/Back

02_Gameplay.unity                     [toda la jugabilidad]
├── Main Camera
├── Directional Light
├── EventSystem
├── GameplayBootstrapper              ← Awake: lee PendingRunRequest, RunBootstrapper.StartRun(), push ExplorationHUD
├── CombatController                  Scene MonoBehaviour (FSM host)
├── ScreenHost                         _initialScreenStringId = "" (lo setea GameplayBootstrapper)
└── Canvas (Screen Space — Overlay, 1920×1080)
    ├── ExplorationHUDView             HP, Energy, Gold, Items, Minimap, RoomNavigation
    ├── CombatHUDView                  TurnQueue, EnemyPanel, DiceZone, ComboIndicator, Action buttons, FloatingDamage
    ├── FloorTransitionScreen          "Piso 1" interstitial
    ├── PauseMenuOverlay               Resume / Settings / Quit Run
    ├── VictoryScreen                  Return to Menu
    └── DefeatScreen                   Return to Menu
```

### Ciclo de vida de servicios

| Scope | Servicio | Creado en | Destruido en |
|---|---|---|---|
| **Global** | `ServiceBootstrapSO` catalogs (Action, Combo, Enemy) | Bootstrap Awake | App quit |
| Global | Ruleset, PhaseTransitionMatrix | Bootstrap Awake | App quit |
| Global | `IPlayerService`, `IPhaseService`, `EnergyService`, `TurnManager`, `TurnOrder`, `RerollBudget`, `Weakness`, `ComboCounters` | Bootstrap Awake | App quit |
| Global | `IScreenManager` (por ScreenHost de la escena activa) | Scene load | Scene unload |
| Global | `IRunController` | Bootstrap Awake (via `RunControllerBootstrapper`) | App quit |
| **Run** | `IDungeonService`, `IExplorationController`, `ICombatHandoffService`, `ICombatReturnService`, `IDamagePipeline`, `IHealPipeline`, `IEnemyAIHandler`, `IEnemySpawnResolver`, `InMemoryEntityRegistry`, `IRunContextService` | RunController.OnRunStart | `RunBootstrapper.EndRun` → `ServiceLocator.ClearScope(Run)` |

**Regla:** los Global viven en `00_Bootstrap` (DontDestroyOnLoad). Los Run se recrean cada vez que arranca una partida. Si volvés al menú y empezás otra run, todo Run se destruye y se vuelve a crear limpio.

### Flujo de transición

```
Play (desde 00_Bootstrap)
  ↓
BootstrapRunner.Awake → RegisterAll (Global) → LoadScene("01_MainMenu")
  ↓
01_MainMenu.ScreenHost.Awake → registra IScreenManager → push MainMenu
  ↓
MainMenu.Jugar → push ClassSelection
  ↓
ClassSelection.Confirm → push BuildSelection (con payload: hero, runId, rulesetId)
  ↓
BuildSelection.Confirm → fill PendingRunRequest → LoadScene("02_Gameplay")
  ↓
02_Gameplay.GameplayBootstrapper.Awake:
  1. Lee PendingRunRequest
  2. RunBootstrapper.StartRun(hero, ruleset, runId)  ← dispara OnRunStart
  3. RunController.OnRunStart wirea todos los servicios Run + BeginExploration
  4. push ExplorationHUD
  ↓
Exploración: Proceed entre rooms, entrar a combat → CombatHandoff push CombatHUD
  ↓
Combat: turnos player/enemy → victoria o derrota
  ↓
Victoria/Derrota → ReturnToMenu:
  1. RunBootstrapper.EndRun(runId) → OnRunEnd → ClearScope(Run)
  2. LoadScene("01_MainMenu")
```

---

## 2. Scripts nuevos necesarios (2 archivos, ~60 LOC)

El MCP dejó el proyecto asumiendo una sola escena. Para el split necesitamos dos scripts chicos que hoy no existen.

### 2.1 `PendingRunRequest.cs`

Ruta: `Assets/Scripts/Rollgeon/Run/PendingRunRequest.cs`.

Carrier estático para pasar el hero seleccionado entre `BuildSelectionScreen` (01_MainMenu) y `GameplayBootstrapper` (02_Gameplay). Usamos static porque la `Run` scope no existe todavía cuando cruzamos escenas — no podemos registrar en `ServiceLocator` hasta que `StartRun` corra.

```csharp
using System;
using Rollgeon.Heroes;

namespace Rollgeon.Run
{
    /// <summary>
    /// Carrier estático para los datos de la run pendiente entre 01_MainMenu
    /// (BuildSelectionScreen) y 02_Gameplay (GameplayBootstrapper).
    /// No se registra en ServiceLocator porque la scope Run aún no existe.
    /// </summary>
    public static class PendingRunRequest
    {
        public static ClassHeroSO SelectedHero { get; private set; }
        public static Guid RunId { get; private set; }
        public static string RulesetId { get; private set; }
        public static bool HasRequest { get; private set; }

        public static void Set(ClassHeroSO hero, Guid runId, string rulesetId)
        {
            SelectedHero = hero;
            RunId = runId;
            RulesetId = rulesetId;
            HasRequest = true;
        }

        public static void Clear()
        {
            SelectedHero = null;
            RunId = Guid.Empty;
            RulesetId = null;
            HasRequest = false;
        }
    }
}
```

### 2.2 `GameplayBootstrapper.cs`

Ruta: `Assets/Scripts/Rollgeon/Run/GameplayBootstrapper.cs`.

MonoBehaviour que vive en `02_Gameplay`. Al cargar la escena, arranca la run y pushea ExplorationHUD.

```csharp
using Patterns;
using Rollgeon.Balance;
using Rollgeon.UI;
using UnityEngine;

namespace Rollgeon.Run
{
    /// <summary>
    /// MonoBehaviour escena-scoped para 02_Gameplay. Lee PendingRunRequest,
    /// arranca la run via RunBootstrapper.StartRun, y pushea ExplorationHUD.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class GameplayBootstrapper : MonoBehaviour
    {
        private const string LogPrefix = "[GameplayBootstrapper] ";

        private void Start()
        {
            if (!PendingRunRequest.HasRequest)
            {
                Debug.LogError(LogPrefix + "No pending run request. Cargaste 02_Gameplay sin pasar por BuildSelection?", this);
                return;
            }

            var hero = PendingRunRequest.SelectedHero;
            var runId = PendingRunRequest.RunId;
            var rulesetId = PendingRunRequest.RulesetId;

            RulesetSO ruleset = null;
            ServiceLocator.TryGetService<RulesetSO>(out ruleset);

            RunBootstrapper.StartRun(hero, ruleset, runId);
            Debug.Log(LogPrefix + $"Run started. hero={hero.EntityId}, runId={runId}");

            if (ServiceLocator.TryGetService<IScreenManager>(out var screens))
            {
                screens.PushByStringId("ExplorationHUD");
            }
            else
            {
                Debug.LogWarning(LogPrefix + "IScreenManager no esta registrado — el ScreenHost de 02_Gameplay no corrio todavia?", this);
            }

            PendingRunRequest.Clear();
        }
    }
}
```

### 2.3 Modificación chica a `BuildSelectionScreen.OnConfirmClicked`

Hoy llama `RunBootstrapper.StartRun(...)` y pushea `"ExplorationHUD"` directo. Con escenas separadas eso rompe. Cambiarlo por:

```csharp
private void OnConfirmClicked()
{
    if (_selectedHero == null) { /* ... warning ... */ return; }

    PendingRunRequest.Set(_selectedHero, _runId, _rulesetId);
    UnityEngine.SceneManagement.SceneManager.LoadScene("02_Gameplay");
}
```

> **Tests:** esto rompe `BuildSelectionScreenTests` si existen tests que asserten `OnRunStart` fires al confirmar. Adaptarlos para assertear que `PendingRunRequest.HasRequest == true` y `SceneManager` fue llamado (mock o skip el load con un seam).

### 2.4 `RunControllerBootstrapper.cs` (reemplaza el RunBootstrapper MonoBehaviour que estaba planeado para MainMenu)

Ruta: `Assets/Scripts/Rollgeon/Run/RunControllerBootstrapper.cs`.

Vive en `00_Bootstrap` (DontDestroyOnLoad). Registra `RunController` como servicio Global.

```csharp
using Rollgeon.Dungeon;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Run
{
    [DefaultExecutionOrder(-9000)]
    public sealed class RunControllerBootstrapper : MonoBehaviour
    {
        [Required("Arrastrar Floor1_Layout.asset")]
        [SerializeField] private FloorLayoutSO _defaultLayout;

        private void Awake()
        {
            if (_defaultLayout == null)
            {
                Debug.LogError("[RunControllerBootstrapper] _defaultLayout null.");
                return;
            }
            RunController.CreateAndRegister(_defaultLayout);
        }
    }
}
```

> **Nota.** Si ya agregaste un `RunBootstrapper.cs` MonoBehaviour en `01_MainMenu` (round 3), borralo — esta clase lo reemplaza en `00_Bootstrap`.

---

## 3. Round 3 — Setup en orden

### Checklist previo (5 min)

- [ ] Unity abre sin errores rojos en Console.
- [ ] `Window → Test Runner → EditMode → Run All` → ~250+ tests verdes.
- [ ] Odin Inspector (Sirenix) importado.
- [ ] TextMeshPro Essentials importado.
- [ ] Estás en branch `develop`.

Si algún punto falla, no sigas — es bug de código, no de setup.

---

### Round A — Project hygiene: mover y renombrar assets (15 min)

Hacer todo desde el **Project window de Unity** (no File Explorer — el arrastre en Unity preserva GUIDs y refs).

#### A.1 Crear carpetas faltantes

Click derecho en `Assets/Rollgeon/` → `Create → Folder`:
- `Prefabs/UI/`
- `Bootstrap/`
- `Dungeon/`
- `Phase/`

#### A.2 Borrar duplicados

- `Assets/Combo_Generala 1.asset` (duplicado).

#### A.3 Mover prefabs → `Assets/Rollgeon/Prefabs/UI/`

| Desde | A |
|---|---|
| `Assets/ComboRow.prefab` | `Assets/Rollgeon/Prefabs/UI/ComboRow.prefab` |
| `Assets/DiceSlotView.prefab` | `Assets/Rollgeon/Prefabs/UI/DiceSlotView.prefab` |
| `Assets/TurnSlot.prefab` | `Assets/Rollgeon/Prefabs/UI/TurnSlot.prefab` |
| `Assets/FloatingDamage.prefab` | `Assets/Rollgeon/Prefabs/UI/FloatingDamage.prefab` |

#### A.4 Mover + renombrar ActionDefinitions → `Assets/Rollgeon/Actions/`

| Asset actual | Renombrar a |
|---|---|
| `ActionDefinition.asset` | `AD_AttackBasic` |
| `ActionDefinition 1.asset` | `AD_AttackSpecial` |
| `ActionDefinition 2.asset` | `AD_Heal` |
| `ActionDefinition 3.asset` | `AD_ForceDoor` |
| `ActionDefinition 4.asset` | `AD_EndTurn` |

`AD_Move.asset` y `ActionCatalog.asset` ya están en la carpeta.

#### A.5 Mover combos faltantes → `Assets/Rollgeon/Combos/`

`Combo_DoblePar.asset`, `Combo_SumaX.asset`, `Combo_FullHouse.asset`, `Combo_Generala.asset`.

#### A.6 Mover Bootstrap SOs → `Assets/Rollgeon/Bootstrap/`

- `PhaseServiceBootstrap.asset`
- `PlayerServiceBootstrap.asset`
- `TurnManagerBootstrap.asset`
- `TurnOrderServiceBootstrap.asset`
- `RerollBudgetServiceBootstrap.asset`
- `WeaknessServiceBootstrap.asset`
- `ComboCountersServiceBootstrap.asset`

#### A.7 Mover + renombrar Dungeon assets → `Assets/Rollgeon/Dungeon/`

| Asset | Renombrar a |
|---|---|
| `Room.asset` | `Room_Combat01` |
| `Room 1.asset` | `Room_Combat02` |
| `Room 2.asset` | `Room_Combat03` |
| `Room 3.asset` | `Room_Shop01` |
| `Room 4.asset` | `Room_Potion01` |
| `EnemyPool.asset` | `Floor1_CombatPool` |
| `FloorLayout.asset` | `Floor1_Layout` |

#### A.8 Mover EnemyData → `Assets/Rollgeon/Enemies/`

- `EnemyData.asset` → `EnemyData_Boss`
- (si hay un 2do) → `EnemyData_Goblin`

#### A.9 Mover PhaseTransitionMatrix → `Assets/Rollgeon/Phase/`

`PhaseTransitionMatrix.asset` (sin renombrar).

**Verificación A:** `Assets/` raíz sólo debe tener carpetas y técnicos (`InputSystem_Actions.inputactions`, `Plugins/`, `Rollgeon/`, `Scenes/`, `Scripts/`, `Settings/`, `TextMesh Pro/`, `TutorialInfo/`, `Readme.asset`). Ningún `.asset` o `.prefab` suelto. Console sin errores ("missing reference" indicaría que algo no matcheó GUID).

---

### Round B — Configurar valores de SOs (30 min)

Todo en Inspector. Orden: empezá por los catálogos (ActionCatalog, ComboCatalog) para poder arrastrarlos después al Ruleset/Hero.

#### B.1 ActionDefinitions

| Asset | ActionId | Type | EnergyCost | FreeRollCount | BlockOnRepeat |
|---|---|---|---|---|---|
| `AD_Move` | `move` | Move | 1 | 0 | true |
| `AD_AttackBasic` | `attack.basic` | Attack | 1 | **3** | true |
| `AD_AttackSpecial` | `attack.special` | Attack | 2 | **3** | true |
| `AD_Heal` | `skill.heal` | SkillCheck | 1 | **1** | true |
| `AD_ForceDoor` | `skill.force_door` | SkillCheck | 2 | **1** | true |
| `AD_EndTurn` | `defend` | Defend | 0 | 0 | true |

`FreeRollCount` es del feature #0104 (Energy×Reroll): attacks = 3 tiradas gratis, heal/force door = 1.

#### B.2 ActionCatalog

`Assets/Rollgeon/Actions/ActionCatalog.asset` → lista `Actions`: arrastrar las 6 ADs.

#### B.3 Combos (4 faltantes)

| Asset | BaseDamage |
|---|---|
| `Combo_DoblePar` | 18 |
| `Combo_SumaX` | 25 |
| `Combo_FullHouse` | 40 |
| `Combo_Generala` | 100 |

Los otros 4 ya deberían tener sus valores: Par=10, Trio=28, Escalera=35, Poker=60. Verificá.

#### B.4 ComboCatalog

`Assets/Rollgeon/Combos/ComboCatalog.asset` → lista `Combos`: arrastrar los 8 combos.

#### B.5 CH_Warrior (ClassHero)

`Assets/Rollgeon/Classes/CH_Warrior.asset`:
- `EntityId = "hero.warrior"`
- `DisplayName = "Guerrero"`
- `Description`: "Clase body. Golpea duro con combos clásicos de dados. Su contrato prioriza Generala."
- `Sheet.Combos` (8 refs en orden canónico): Par, DoblePar, SumaX, Trio, Escalera, FullHouse, Poker, Generala.
- `Sheet._displayLabel` = `"Contrato del Guerrero"`.
  > Si el campo es `[SerializeField] private` y no aparece en Inspector, usar el menú `Tools → Rollgeon → Build Warrior Contract` (si existe, viene con `ContractWarriorFactory.Build(catalog)`). Si tampoco aparece, dejarlo vacío — `ContractDisplayView` tiene fallback `"Contrato"`.
- `Portrait`: placeholder o null.

#### B.6 EnemyData

| Asset | BaseHP | BaseAttack | BaseSpeed | MaxEnergy |
|---|---|---|---|---|
| `EnemyData_Boss` | 100 | 15 | 3 | 5 |
| `EnemyData_Goblin` | 20 | 8 | 4 | 3 |
| `EnemyData_Test` | 15 | 5 | 5 | 2 |

#### B.7 EnemyPool

`Floor1_CombatPool.asset` → lista `Entries`: `EnemyData_Test` (weight=1) + `EnemyData_Goblin` (weight=1).

#### B.8 Rooms

| Asset | RoomId | DisplayName | Type | EnemyPool |
|---|---|---|---|---|
| `Room_Combat01` | `combat_01` | Sala de Combate 1 | Combat | `Floor1_CombatPool` |
| `Room_Combat02` | `combat_02` | Sala de Combate 2 | Combat | `Floor1_CombatPool` |
| `Room_Combat03` | `combat_03` | Sala de Combate 3 | Combat | `Floor1_CombatPool` |
| `Room_Shop01` | `shop_01` | Tienda | Shop | null |
| `Room_Potion01` | `potion_01` | Sala de Pociones | Potion | null |

#### B.9 FloorLayout

`Floor1_Layout.asset`:
- `RoomCountMin = 6`, `RoomCountMax = 8`
- `CombatRooms`: [Room_Combat01, Room_Combat02, Room_Combat03]
- `ShopRooms`: [Room_Shop01]
- `PotionRooms`: [Room_Potion01]
- `BossCandidates`: [EnemyData_Boss]

#### B.10 PhaseTransitionMatrix

Grilla 5×5 (checkbox ON):
- `None → Exploration`
- `Exploration → Combat`
- `Exploration → Loading`
- `Combat → Exploration`
- `Combat → GameOver`
- Cualquiera `→ GameOver`

Overlays: `Exploration → [Pause]`, `Combat → [Pause]`.

#### B.11 Ruleset

`Assets/Rollgeon/Rulesets/Ruleset.asset`:
- **Energy:** `Max=4`, `AtRunStart=2`, `RegenBase=2`
- **TurnOrder:** `SpeedDieMin=1`, `SpeedDieMax=6`
- **Weakness:** `DefaultMultiplier=1.5`
- **Counters:** `PerUseBonus=0.02`, `MaxBonus=0.20`

#### B.12 ServiceBootstrap

`Assets/Rollgeon/ServiceBootstrap.asset` → **mover a `Assets/Rollgeon/Bootstrap/`** para consistencia. Después configurar:
- **Catalogs:** `ActionCatalog`, `ComboCatalog`, `EnemyCatalog`.
- **Settings Assets:** `Ruleset`, `PhaseTransitionMatrix`.
- **Extra Runtime Services** (ordenar por Priority ascendente):
  1. `PhaseServiceBootstrap` (10)
  2. `PlayerServiceBootstrap` (30)
  3. `TurnManagerBootstrap` (60)
  4. `TurnOrderServiceBootstrap` (100)
  5. `RerollBudgetServiceBootstrap`
  6. `WeaknessServiceBootstrap`
  7. `ComboCountersServiceBootstrap`
- `_nextSceneName = "01_MainMenu"`.

**Verificación B:** ningún `[Required]` de Odin en rojo. Console sigue limpia.

---

### Round C — Escena `00_Bootstrap.unity` (10 min)

Abrir `Assets/Scenes/00_Bootstrap.unity`.

#### C.1 GameObject `Bootstrap`

Debe existir del round 2. Seleccionarlo en la jerarquía.

- Componente `BootstrapRunner`:
  - `_bootstrap` → arrastrar `Assets/Rollgeon/Bootstrap/ServiceBootstrap.asset`
  - `_nextScene` → vacío
  - `_dontDestroyOnLoad` → **`true`** (crítico: el GO con RunController + GameplaySceneLoader debe sobrevivir al load de 01_MainMenu)
  - `_preloadCatalogs` → `true`

#### C.2 Agregar componente `RunControllerBootstrapper`

Al mismo GO `Bootstrap` (o uno hijo si preferís aislarlo):
- Add Component → `Run Controller Bootstrapper` (clase que creás en Round §2.4).
- `_defaultLayout` → arrastrar `Floor1_Layout.asset`.

#### C.3 Agregar componente `GameplaySceneLoader`

Script nuevo (ruta: `Assets/Scripts/Rollgeon/Run/GameplaySceneLoader.cs`):

```csharp
using Patterns;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rollgeon.Run
{
    /// <summary>
    /// Vive en 00_Bootstrap (DontDestroyOnLoad). No hace nada automático —
    /// BuildSelectionScreen decide cuándo cargar 02_Gameplay. Este stub
    /// existe por si más adelante querés centralizar transiciones.
    /// </summary>
    public sealed class GameplaySceneLoader : MonoBehaviour
    {
        private const string LogPrefix = "[GameplaySceneLoader] ";

        public void LoadGameplay()
        {
            Debug.Log(LogPrefix + "Loading 02_Gameplay");
            SceneManager.LoadScene("02_Gameplay");
        }

        public void LoadMainMenu()
        {
            Debug.Log(LogPrefix + "Loading 01_MainMenu");
            SceneManager.LoadScene("01_MainMenu");
        }
    }
}
```

> Para Sprint03 FP este helper es cosmético: BuildSelection ya carga la escena directo. Sirve como punto único si más adelante querés agregar transitions/fades.

#### C.4 Build Settings

`File → Build Settings…`:
- Index 0: `00_Bootstrap.unity`
- Index 1: `01_MainMenu.unity`
- Index 2: `02_Gameplay.unity` (se agrega en Round E, dejar TBD por ahora)

Guardar con `Ctrl+S`.

**Verificación C:** Play desde `00_Bootstrap`. Console:
```
[Bootstrap] RegisterAll() invoked
[Bootstrap] Registered 3 catalogs, 2 settings, 8 extra services
[Bootstrap] Preload complete
[Bootstrap] Hooks installed (OnRunStart, OnRunEnd)
[Bootstrap] Loading scene 01_MainMenu
```
No tenés `01_MainMenu` todavía reorganizada — vas a ver la escena cruda o un warning. OK.

---

### Round D — Escena `01_MainMenu.unity` (30 min) — SOLO menú

Esta escena va a contener **sólo** los 3 screens de menú. Todo lo que es HUD de gameplay se mueve a `02_Gameplay`.

Abrir `01_MainMenu.unity`.

#### D.1 Borrar los GameObjects que no pertenecen al menú

Bajo el Canvas, eliminar:
- `ExplorationHUDView` (y toda su descendencia)
- `CombatHUDView`
- `FloorTransitionScreen`
- `PauseMenuOverlay`
- `VictoryScreen`
- `DefeatScreen`

Fuera del Canvas, eliminar:
- `CombatController` (va a 02_Gameplay)

Deben quedar:
```
EventSystem
ScreenHost (_initialScreenStringId = "MainMenu")
Canvas
├── MainMenuScreen
├── ClassSelectionScreen
└── BuildSelectionScreen
```

> **Antes de borrar:** asegurate de haber committeado el estado actual (o usar un branch nuevo). Si algo sale mal tenés cómo volver.

#### D.2 Wirear `MainMenuScreen`

| Field | Arrastrar |
|---|---|
| `_playButton` | `MainMenuScreen/PlayButton` |
| `_quitButton` | `MainMenuScreen/QuitButton` |

No cablear `OnClick()` en el Inspector del Button — el script lo hace en `OnEnable`.

#### D.3 Wirear `ClassSelectionScreen`

| Field | Arrastrar |
|---|---|
| `_warriorHero` | `Assets/Rollgeon/Classes/CH_Warrior.asset` |
| `_nextScreenStringId` | `"BuildSelectionScreen"` (dejar el default) |
| `_rulesetId` | `"default"` |
| `_warriorButton` | `LeftPanel/WarriorButton` |
| `_magoButton` | `LeftPanel/MagoButton` |
| `_picaroButton` | `LeftPanel/PicaroButton` |
| `_confirmButton` | `RightPanel/ConfirmButton` |
| `_contractDisplay` | `RightPanel/ContractDisplayView` |
| `_portraitDisplay` | `RightPanel/PortraitImage` |
| `_passiveDisplay` | `RightPanel/PassiveLabel` |
| `_warriorSelectionIndicator` | `WarriorButton/SelectionIndicator` |

**ContractDisplayView** (mismo GO):
| Field | Arrastrar |
|---|---|
| `_headerLabel` | `HeaderLabel` |
| `_rowsContainer` | `RowsContainer` (Transform) |
| `_rowPrefab` | `Assets/Rollgeon/Prefabs/UI/ComboRow.prefab` |
| `_footerLabel` | `FooterLabel` (opcional) |

#### D.4 Modificar `BuildSelectionScreen.cs`

Aplicar el cambio de §2.3 de esta guía. Después, wirear en Inspector:

| Field | Arrastrar |
|---|---|
| `_heroNameLabel` | `HeroNameLabel` |
| `_heroDescriptionLabel` | `HeroDescriptionLabel` |
| `_heroPortrait` | `HeroPortrait` |
| `_diceContainer` | `DiceContainer` |
| `_diceSlotPrefab` | `Assets/Rollgeon/Prefabs/UI/DiceSlotView.prefab` |
| `_diceBagFallbackLabel` | `DiceBagFallbackLabel` |
| `_confirmButton` | `ConfirmButton` |
| `_backButton` | `BackButton` |

#### D.5 ScreenHost

- `_initialScreenStringId` → `"MainMenu"`
- `_includeInactive` → `true`

**Verificación D:** Play desde `00_Bootstrap`. Aparece Main Menu. Jugar → Class Selection. Click Guerrero → panel derecho poblado con 8 rows (Par 10, DoblePar 18, SumaX 25, Trio 28, Escalera 35, FullHouse 40, Poker 60, Generala 100). Confirmar → Console: `[BuildSelectionScreen] ...` + warning `"02_Gameplay no esta en Build Settings"` (esperado — se crea en Round E).

---

### Round E — Crear escena `02_Gameplay.unity` (45 min)

Esta es la escena nueva. Dos opciones:

**Opción 1 (recomendada si el round 2 dejó todo en 01_MainMenu):** duplicá `01_MainMenu.unity`, renombralo a `02_Gameplay.unity`, y borrá lo que no va.

**Opción 2:** crear desde cero y reconstruir la jerarquía manualmente (más trabajo pero más prolijo).

Uso **opción 1** abajo.

#### E.1 Duplicar la escena

1. En Project: click derecho en `01_MainMenu.unity` → `Duplicate` → aparece `01_MainMenu 1.unity`.
2. Renombrar a `02_Gameplay`.
3. Moverla (si hace falta) a `Assets/Scenes/02_Gameplay.unity`.

#### E.2 Abrir `02_Gameplay.unity` y limpiar

Borrar del Canvas:
- `MainMenuScreen`
- `ClassSelectionScreen`
- `BuildSelectionScreen`

> Atención: si hiciste el Round D antes de duplicar, estos ya no están. Si duplicaste ANTES del round D, `02_Gameplay.unity` tiene todo. En ese caso borrá los 3 screens de menú pero dejá los HUDs y overlays.

Deben quedar bajo Canvas:
```
ExplorationHUDView
CombatHUDView
FloorTransitionScreen
PauseMenuOverlay
VictoryScreen
DefeatScreen
```

Fuera del Canvas, deben estar:
```
EventSystem
ScreenHost
CombatController
Main Camera
Directional Light
```

Si el `CombatController` fue borrado en round D, recrearlo: `GameObject → Create Empty` → renombrar `CombatController` → Add Component → `Combat Controller`.

#### E.3 Agregar `GameplayBootstrapper`

`GameObject → Create Empty` → renombrar `GameplayBootstrapper` → Add Component → `Gameplay Bootstrapper` (script de §2.2).

No tiene fields serializados en la versión simple.

#### E.4 Wirear `ExplorationHUDView`

| Field | Arrastrar |
|---|---|
| `_healthBar` | `HealthBarView` hijo |
| `_energyBar` | `EnergyBarView` hijo |
| `_goldCounter` | `GoldCounterView` hijo |
| `_activeItems` | `ActiveItemsView` hijo |
| `_minimap` | `MinimapView` hijo |
| `_roomNavigation` | `RoomNavigationView` hijo |

**HealthBarView:** `_slider` → Slider hijo, `_text` → HPText hijo.
**EnergyBarView:** `_slider` → Slider hijo, `_text` → EnergyText hijo.
**GoldCounterView:** `_text` → GoldText hijo.
**ActiveItemsView:** `Bindings` → 2 entries: `"item.arco"` → ArcoSlot, `"item.pocion"` → PocionSlot.
**ActiveItemSlotView** (ArcoSlot y PocionSlot): `_icon`, `_inactiveOverlay`, `_depletedOverlay`.
**MinimapView:** `_mapPivot`, `_placeholder`.
**RoomNavigationView:** `_roomNameLabel`, `_roomProgressLabel`, `_roomTypeLabel`, `_proceedButton`, `_pauseButton`.

#### E.5 Wirear `CombatHUDView`

| Field | Arrastrar |
|---|---|
| `_turnQueue` | `TurnQueueView` |
| `_comboIndicator` | `ComboIndicatorView` |
| `_enemyPanel` | `EnemyPanelView` |
| `_actionButtons` | `ActionButtonsView` |
| `_diceZone` | `DiceZoneView` |
| `_rerollCount` | `RerollCountView` |
| `_floatingDamage` | `FloatingDamageOverlay` (FloatingDamageSpawner) |
| `_damageFlashGroup` | `DamageFlashGroup` |
| `_playerActionButtons` | `PlayerActionButtonsView` |

**TurnQueueView:** `_slotPrefab` → `TurnSlot.prefab`, `_container` → el propio GO (HLG).
**EnemyPanelView:** `_panelRoot`, `_name`, `_hpSlider`, `_hpText`, `_weaknessRoot`, `_weaknessIcon`.
**ComboIndicatorView:** `_currentComboLabel`, `_rows` → array de 8 (podés crear 8 TMP hijos en `ContractRows/` si no están; cada row representa un combo del contrato).
**DiceZoneView:** `_rollArea`, `_holdArea`, `_diceSlots` (array 5).
**ActionButtonsView:** `_attackButton`, `_energyRerollButton`, `_endTurnButton`.
**PlayerActionButtonsView:** `_rollDiceButton`, `_rerollButton`, `_confirmAttackButton`, `_endTurnButton`, `_rerollLabel`.
**RerollCountView:** `_countLabel`, `_extraRollButton`.
**FloatingDamageSpawner:** `_instancePrefab` → `FloatingDamage.prefab`, `_overlayContainer` → el mismo overlay.

#### E.6 Wirear overlays

**FloorTransitionScreen:** `_floorNumberLabel`, `_floorTitleLabel`, `_continueButton`.
**PauseMenuOverlay:** `_resumeButton`, `_settingsButton`, `_quitRunButton`.
**VictoryScreen:** `_titleLabel`, `_returnToMenuButton`.
**DefeatScreen:** `_titleLabel`, `_returnToMenuButton`.

#### E.7 Wirear `ReturnToMenu` en Victory/Defeat

Los dos screens tienen un `_returnToMenuButton`. El handler debe:
1. `RunBootstrapper.EndRun(currentRunId)`
2. `SceneManager.LoadScene("01_MainMenu")`

Si el script `VictoryScreen`/`DefeatScreen` ya implementa esto, perfecto. Si no, chequeá el código — pero por diseño del round 2 ya debería estar. Si no lo está, agregalo:

```csharp
private void OnReturnClicked()
{
    if (ServiceLocator.TryGetService<IRunContextService>(out var ctx))
    {
        Rollgeon.Run.RunBootstrapper.EndRun(ctx.RunId);
    }
    UnityEngine.SceneManagement.SceneManager.LoadScene("01_MainMenu");
}
```

#### E.8 ScreenHost

- `_initialScreenStringId` → vacío (el `GameplayBootstrapper` pushea "ExplorationHUD" explícitamente en Start).
- `_includeInactive` → `true`.

#### E.9 Build Settings

`File → Build Settings…` → agregar `02_Gameplay.unity` en index 2. Guardar.

**Verificación E:** todavía no probar Play. Primero terminamos Round F.

---

### Round F — Layout visual mínimo (30 min)

Todo lo creado vía MCP tiene RectTransform en `(0,0,0)`. Sin posicionamiento no ves nada. Para smoke test basta con anchors estándar.

#### F.1 01_MainMenu

**MainMenuScreen:**
- `Background`: anchors `stretch-stretch`, margins 0.
- `TitleLabel`: anchor `top-center`, Y=-150, fontSize=120, text "Rollgeon".
- `PlayButton`: anchor `center`, size 300×80, Y=0.
- `QuitButton`: anchor `center`, size 300×80, Y=-100.

**ClassSelectionScreen:**
- `Background`: stretch full.
- `LeftPanel`: anchor `left-stretch`, ancho 400, margin 50.
- `RightPanel`: anchor `right-stretch`, ancho 900, margin 50.
- Botones dentro de LeftPanel: height 100 c/u con spacing (VLG ya está).

**BuildSelectionScreen:**
- `HeroNameLabel`: top-center.
- `HeroPortrait`: izquierda, size 400×400.
- `DiceContainer`: centro-abajo.
- `ConfirmButton`: bottom-right.
- `BackButton`: bottom-left.

#### F.2 02_Gameplay

**ExplorationHUDView:**
- `HealthBarView`: anchor top-left, pos (30, -30), width 300.
- `EnergyBarView`: anchor top-left, pos (30, -80), width 300.
- `GoldCounterView`: top-left, pos (30, -130).
- `ActiveItemsView`: bottom-left.
- `MinimapView`: top-right, pos (-30, -30), size 200×200.
- `RoomNavigationView`: bottom-center.

**CombatHUDView:**
- `TurnQueueView`: top-center.
- `EnemyPanelView`: top-right.
- `ComboIndicatorView`: left-center.
- `DiceZoneView`: center.
- `ActionButtonsView`: bottom-right.
- `PlayerActionButtonsView`: bottom-center.
- `RerollCountView`: debajo de DiceZone.
- `DamageFlashGroup`: stretch full (Image rojo, CanvasGroup alpha 0).
- `FloatingDamageOverlay`: stretch full, encima del resto.

**Overlays (Victory, Defeat, FloorTransition, Pause):**
- `Background`/`Panel`: stretch full con Image negra alpha 0.6.
- `TitleLabel`: center, fontSize 64.
- Botones: stacked center-bottom.

No hace falta que sea hermoso. Que se vea y se clickee.

---

### Round G — Smoke test end-to-end (15 min)

#### G.1 Play desde `00_Bootstrap`

Console esperada (en orden):
```
[Bootstrap] RegisterAll() invoked
[Bootstrap] Registered 3 catalogs, 2 settings, 7-8 extra services
[Bootstrap] Preload complete
[Bootstrap] Hooks installed (OnRunStart, OnRunEnd)
[Bootstrap] Loading scene 01_MainMenu
```

#### G.2 Main Menu aparece

- Título "Rollgeon" arriba.
- Botones Jugar / Salir centrados abajo.
- Click **Salir** → editor sale del playmode.

#### G.3 Play again → Jugar → Class Selection

- Guerrero habilitado.
- Mago/Pícaro grayed-out.
- Panel derecho vacío.
- Confirmar disabled.

#### G.4 Click Guerrero

- Panel derecho se puebla:
  - Portrait.
  - Header "Contrato del Guerrero".
  - 8 rows con combos y daños.
  - "Pasiva: TBD".
- Selection indicator visible.
- Confirmar habilitado.

#### G.5 Confirmar → Build Selection

- Hero name / description / portrait populados.
- Dice bag: posiblemente vacío (la feature "dice bag config" está diferida).
- Confirmar habilitado.

#### G.6 Confirmar Build → Gameplay scene load

Console:
```
[BuildSelectionScreen] Navigating to gameplay. runId=<guid>
[GameplayBootstrapper] Run started. hero=hero.warrior, runId=<guid>
[RunController] (logs de registro de Dungeon, Exploration, CombatHandoff, etc.)
[ExplorationController] Entered room combat_01
```

Aparece el ExplorationHUD:
- HP bar lleno.
- Energy 2/4.
- Gold 0.
- MinimapView placeholder.
- RoomNavigationView muestra "Sala de Combate 1", Room 1/N, botón Proceed.

#### G.7 Click Proceed

- Entra a la siguiente room.
- Si es combat → CombatHUD aparece por `CombatHandoffService`:
  - TurnQueue muestra player + enemies.
  - EnemyPanel con HP y weakness.
  - DiceZone con 5 slots.
  - Botones Roll/Reroll/Confirm/EndTurn.

#### G.8 Jugar el turno

- Click Roll Dice → dados rollean.
- (Opcional) Click Reroll → rerollea.
- Click Confirm Attack → daño al enemigo.
- Click End Turn → turno enemigo (stub: solo loggea y termina).

Si el enemigo muere → combat ends → vuelve a Exploration via `CombatReturnService`. Si player muere → DefeatScreen.

#### G.9 Last room (boss)

- Proceed hasta last room → `Boss` combat.
- Derrotar al boss → `OnFloorCleared` → VictoryScreen.

#### G.10 Return to Menu

- Click Return → `OnRunEnd` fires → `ClearScope(Run)` → `LoadScene("01_MainMenu")`.
- Main Menu aparece limpio, podés empezar otra run.

---

## 4. Troubleshooting

| Síntoma | Causa | Fix |
|---|---|---|
| `KeyNotFoundException` al Play desde `01_MainMenu` o `02_Gameplay` | Saltaste el Bootstrap | SIEMPRE Play desde `00_Bootstrap.unity`. Opcional: instalar [Scene Autoload Loader](https://forum.unity.com/threads/scene-autoload-loader.402476/) para dev. |
| `[GameplayBootstrapper] No pending run request` | Cargaste `02_Gameplay` directo, sin pasar por BuildSelection | Volvé al menú, empezá de vuelta. |
| Click Jugar → warning `'ClassSelectionScreen' no esta registrada` | ClassSelectionScreen no está como hijo del Canvas de `01_MainMenu` | Verificá jerarquía. `ScreenHost` busca con `GetComponentsInChildren<BaseScreen>(true)`. |
| `02_Gameplay` carga pero queda en negro | `ScreenHost._initialScreenStringId` NO es `""`, y pushea algo que no existe. O `GameplayBootstrapper` no corrió (ver execution order). | Dejar `_initialScreenStringId` vacío y confiar en `GameplayBootstrapper.Start`. |
| HP bar queda en 0/0 al entrar a Gameplay | `IPlayerService` no tiene player seteado, o los stats del hero están en 0 | Verificar que `RunBootstrapper.StartRun` corrió. Ver logs `[PlayerService] SetPlayer(...)`. |
| Enemy turn no hace nada | Esperado en MVP — `StubEnemyAIHandler` fue reemplazado por `BasicEnemyAI` vía `RunController`, pero el delegate `EnemyActionHandler` del `CombatContext` sigue stub. Ver TBDs. |
| Combat no transita de player a enemy | `ICombatSignaller` no está registrado, o `TurnManager._actionsUsedThisTurn` no se limpia | Ver logs `[RunController] ICombatSignaller not available — using no-op`. Si no-op, el combate nunca avanza. Registrar un `ICombatSignaller` real en `ServiceBootstrap.ExtraServices`. |
| Volver al menú y empezar otra run: `Service already registered` | `ClearScope(Run)` no se llamó | Verificar que `VictoryScreen.OnReturnClicked` llama `RunBootstrapper.EndRun(runId)` ANTES del `LoadScene`. |
| `DontDestroyOnLoad` spawnea dos Bootstrap GOs | Volviste a 00_Bootstrap desde código | Nunca cargar `00_Bootstrap` después del arranque. Para reset completo, quit + restart. |
| Odin warnings `[Required]` en rojo en Console | Falta wirear algún field | Releer el Round correspondiente. |

---

## 5. TBDs post-Sprint03 (no son bugs)

Del `_SPRINT03_VERIFICATION_GUIDE.md`:

- **§5.6 Strike combos** — diferido, no implementar.
- **IPlayerService** — stub con solo PlayerGuid. Extender cuando venga hero spawn real.
- **§12.0 IPhaseService overlays** — hooks presentes, publisher no existe.
- **§15 SaveSystem** — stubs `ISaveable` sin service de save real.
- **Damage pipeline real** — combates hacen `AttributesManager.Modify<Health,int>` directo; falta mitigación/crit/etc.
- **Enemy AI delegate** — `CombatContext.EnemyActionHandler` stub que siempre termina turno.
- **`HealStrength` en `Rollgeon.Entities.Behaviors`** en vez de `Attributes.Stats` — follow-up de relocation.

---

## 6. Orden resumen (si te perdés)

```
[ 5 min] Checklist previo — compile + tests verdes
[15 min] Round A — mover + renombrar assets
[30 min] Round B — configurar SO values
[10 min] Round C — escena 00_Bootstrap (BootstrapRunner + RunControllerBootstrapper + GameplaySceneLoader)
[30 min] Round D — escena 01_MainMenu (solo 3 screens de menú)
[45 min] Round E — escena 02_Gameplay (duplicar + limpiar + wirear HUDs + overlays)
[30 min] Round F — layout visual mínimo
[15 min] Round G — smoke test end-to-end
```

Cuando el loop completo corra sin errores rojos, volvé a la conversación y arrancamos **Phase 3** — T101 balance Inspector audit + 5 tools editor (ver `_SPRINT03_VERIFICATION_GUIDE.md §Paso 4`).

---

## 7. Cambios que hay que hacer en código (resumen)

| Archivo | Cambio |
|---|---|
| `Assets/Scripts/Rollgeon/Run/PendingRunRequest.cs` | **Nuevo** (~25 LOC) |
| `Assets/Scripts/Rollgeon/Run/GameplayBootstrapper.cs` | **Nuevo** (~30 LOC) |
| `Assets/Scripts/Rollgeon/Run/RunControllerBootstrapper.cs` | **Nuevo** (~20 LOC) |
| `Assets/Scripts/Rollgeon/Run/GameplaySceneLoader.cs` | **Nuevo** (~20 LOC, opcional pero recomendado) |
| `Assets/Scripts/Rollgeon/UI/Screens/BuildSelectionScreen.cs` | **Modificar** `OnConfirmClicked`: fill `PendingRunRequest` + `SceneManager.LoadScene("02_Gameplay")` en lugar de `RunBootstrapper.StartRun` + push. |
| `Assets/Scripts/Rollgeon/UI/Screens/VictoryScreen.cs` | Verificar que `OnReturnClicked` llama `RunBootstrapper.EndRun` + `LoadScene("01_MainMenu")`. Agregar si falta. |
| `Assets/Scripts/Rollgeon/UI/Screens/DefeatScreen.cs` | Idem. |
| `Assets/Scripts/Rollgeon/UI/Tests/BuildSelectionScreenTests.cs` (si existe) | Actualizar asserts — ya no fire OnRunStart desde BuildSelection, ahora fire desde GameplayBootstrapper. |

Total: ~100 LOC nuevos + 1 método modificado + 2 verificaciones de métodos existentes + ajuste de tests.

---

*Guía generada 2026-04-20. Si pegás un bug irrecuperable, `git reset --hard HEAD` y volvemos al round 2 sin penalidad.*
