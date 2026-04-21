---
title: Round3-Checklist
type: index
domain: 99-Index
status: wip
tags: [index, checklist, round3, setup]
---

# Round 3 — Manual Setup Checklist

> Working checklist for the manual Unity setup that closes out Sprint 03.
> All items are plain Markdown checkboxes — tick them in Obsidian as you
> go. Values inline; detailed reference lives in
> `docs/setup/_SETUP_ROUND2_STATUS.md`.
>
> Total estimated: **~70–90 min**. Status:
> [[Sprint03-Status]] · Plan: [[Implementation-Roadmap]] P1 #1.

---

## A · Asset organization (~15 min)

- [ ] Crear carpeta `Assets/Rollgeon/Prefabs/UI/`
- [ ] Mover `ComboRow.prefab` del root → `Assets/Rollgeon/Prefabs/UI/`
- [ ] Mover `DiceSlotView.prefab` del root → `Assets/Rollgeon/Prefabs/UI/`
- [ ] Mover `TurnSlot.prefab` del root → `Assets/Rollgeon/Prefabs/UI/`
- [ ] Mover `FloatingDamage.prefab` del root → `Assets/Rollgeon/Prefabs/UI/`
- [ ] Si existe `Combo_Generala 1.asset` duplicado → borrar
- [ ] (Opcional) Borrar carpeta vacía `Assets/Rollgeon/Scenes/` desde Unity

---

## B · Crear SOs faltantes (~20 min)

### B.1 ActionDefinitions — `Assets/Rollgeon/Actions/`

Renombrar `AD_Move.asset` si aplica o crear 5 nuevos (`Create → Rollgeon/Actions/Action Definition`):

- [ ] `AD_AttackBasic` — ActionId: `attack.basic` · Type: `Attack` · EnergyCost: **1** · BlockOnRepeat: `true`
- [ ] `AD_AttackSpecial` — ActionId: `attack.special` · Type: `Attack` · EnergyCost: **2** · BlockOnRepeat: `true`
- [ ] `AD_Heal` — ActionId: `skill.heal` · Type: `SkillCheck` · EnergyCost: **2** · BlockOnRepeat: `true`
- [ ] `AD_ForceDoor` — ActionId: `skill.force_door` · Type: `SkillCheck` · EnergyCost: **1** · BlockOnRepeat: `true`
- [ ] `AD_EndTurn` — ActionId: `defend` · Type: `Defend` · EnergyCost: **0** · BlockOnRepeat: `true`

### B.2 Combos faltantes — `Assets/Rollgeon/Combos/`

`Create → Rollgeon/Combos/<Tipo>`:

- [ ] `Combo_DoblePar` — BaseDamage: **18**
- [ ] `Combo_SumaX` — BaseDamage: **25**
- [ ] `Combo_FullHouse` — BaseDamage: **40**
- [ ] `Combo_Generala` — BaseDamage: **100**

Verificar los ya creados:

- [ ] `Combo_Par` — BaseDamage: 10
- [ ] `Combo_Trio` — BaseDamage: 28
- [ ] `Combo_Escalera` — BaseDamage: 35
- [ ] `Combo_Poker` — BaseDamage: 60

### B.3 EnemyData — `Assets/Rollgeon/Enemies/`

- [ ] `EnemyData_Boss` — BaseHP: **100** · BaseAttack: **15** · BaseSpeed: **3** · MaxEnergy: **5**
- [ ] `EnemyData_Goblin` — BaseHP: **20** · BaseAttack: **8** · BaseSpeed: **4** · MaxEnergy: **3**

### B.4 EnemyPool

- [ ] `Floor1_CombatPool` (EnemyPoolSO) con entries: `EnemyData_Test` (Weight=1) + `EnemyData_Goblin` (Weight=1)

### B.5 Rooms — `Assets/Rollgeon/` (crear carpeta `Rooms/` si no existe)

| Asset | RoomId | DisplayName | Type | EnemyPool |
|---|---|---|---|---|
| `Room_Combat01` | `combat_01` | "Sala de Combate 1" | Combat | `Floor1_CombatPool` |
| `Room_Combat02` | `combat_02` | "Sala de Combate 2" | Combat | `Floor1_CombatPool` |
| `Room_Combat03` | `combat_03` | "Sala de Combate 3" | Combat | `Floor1_CombatPool` |
| `Room_Shop01`   | `shop_01`   | "Tienda"            | Shop   | null |
| `Room_Potion01` | `potion_01` | "Sala de Pociones"  | Potion | null |

- [ ] `Room_Combat01`
- [ ] `Room_Combat02`
- [ ] `Room_Combat03`
- [ ] `Room_Shop01`
- [ ] `Room_Potion01`

### B.6 Floor + Matrix

- [ ] `Floor1_Layout` (FloorLayoutSO) — RoomCountMin: **6** · RoomCountMax: **8** · CombatRooms: [3 rooms] · ShopRooms: [Shop01] · PotionRooms: [Potion01] · BossCandidates: [EnemyData_Boss]
- [ ] `PhaseTransitionMatrix` (PhaseTransitionMatrixSO) — grilla 5×5:
  - [ ] None → Exploration ✓
  - [ ] Exploration → Combat ✓
  - [ ] Exploration → Loading ✓
  - [ ] Combat → Exploration ✓
  - [ ] Combat → GameOver ✓
  - [ ] * → GameOver ✓
  - [ ] Overlays: Exploration → [Pause] ; Combat → [Pause]

### B.7 Ruleset — verificar `Assets/Rollgeon/Rulesets/Ruleset.asset`

- [ ] Energy: Max=**4** · AtRunStart=**2** · RegenBase=**2**
- [ ] TurnOrder: SpeedDieMin=**1** · SpeedDieMax=**6**
- [ ] Weakness: DefaultMultiplier=**1.5**
- [ ] Counters: PerUseBonus=**0.02** · MaxBonus=**0.20**

---

## C · Wiring de referencias en Inspector (~45–60 min)

### C.1 Bootstrap (items 1–4)

**En `Assets/Scenes/00_Bootstrap.unity`:**

- [ ] (1) `BootstrapRunner._bootstrap` → `Assets/Rollgeon/ServiceBootstrap.asset`

**En `Assets/Rollgeon/ServiceBootstrap.asset`:**

- [ ] (2) `Catalogs`: ActionCatalog, ComboCatalog, EnemyCatalog
- [ ] (3) `SettingsAssets`: Ruleset, PhaseTransitionMatrix
- [ ] (4) `ExtraServices` en orden de Priority:
  - [ ] PhaseServiceBootstrap (10)
  - [ ] PlayerServiceBootstrap (30)
  - [ ] EnergyService (50)
  - [ ] TurnManagerBootstrap (60)
  - [ ] TurnOrderServiceBootstrap (100)
  - [ ] RerollBudgetServiceBootstrap
  - [ ] WeaknessServiceBootstrap
  - [ ] ComboCountersServiceBootstrap

> **Checkpoint C.1:** Play desde `00_Bootstrap` loggea `Registered N catalogs…` sin errores.

### C.2 Screens principales (items 5–8)

**En `Assets/Scenes/01_MainMenu.unity`:**

- [ ] (5) `MainMenuScreen`: `_playButton` → PlayButton ; `_quitButton` → QuitButton
- [ ] (6) `ClassSelectionScreen` — 9 refs:
  - [ ] `_warriorHero` → `CH_Warrior.asset`
  - [ ] `_warriorButton`, `_magoButton`, `_picaroButton`
  - [ ] `_confirmButton`
  - [ ] `_contractDisplay`
  - [ ] `_portraitDisplay`
  - [ ] `_passiveDisplay`
  - [ ] `_warriorSelectionIndicator`
- [ ] (7) `ContractDisplayView`: `_headerLabel`, `_rowsContainer`, `_rowPrefab` → `ComboRow.prefab`, `_footerLabel`
- [ ] (8) `BuildSelectionScreen` — 8 refs:
  - [ ] `_heroNameLabel`, `_heroDescriptionLabel`
  - [ ] `_heroPortrait`
  - [ ] `_diceContainer`, `_diceSlotPrefab` → `DiceSlotView.prefab`
  - [ ] `_diceBagFallbackLabel`
  - [ ] `_confirmButton`, `_backButton`

> **Checkpoint C.2:** Play → `Jugar` → `ClassSelection` → `BuildSelection` sin NullReference.

### C.3 ExplorationHUD (items 9–16)

- [ ] (9) `ExplorationHUDView` — 6 refs a sub-views: `_healthBar`, `_energyBar`, `_goldCounter`, `_activeItems`, `_minimap`, `_roomNavigation`
- [ ] (10) `HealthBarView`: `_slider` → `Slider` hijo ; `_text` → `HPText` hijo
- [ ] (11) `EnergyBarView`: `_slider` → `Slider` hijo ; `_text` → `EnergyText` hijo
- [ ] (12) `GoldCounterView`: `_text` → `GoldText` hijo
- [ ] (13) `ActiveItemsView`: `Bindings` array (`item.arco` → ArcoSlot ; `item.pocion` → PocionSlot)
- [ ] (14) `ActiveItemSlotView` × 2 (ArcoSlot + PocionSlot): `_icon`, `_inactiveOverlay`, `_depletedOverlay`
- [ ] (15) `MinimapView`: `_mapPivot`, `_placeholder`
- [ ] (16) `RoomNavigationView`: `_roomNameLabel`, `_roomProgressLabel`, `_roomTypeLabel`, `_proceedButton`, `_pauseButton`

### C.4 CombatHUD (items 17–25)

- [ ] (17) `CombatHUDView` — 9 refs: `_turnQueue`, `_comboIndicator`, `_enemyPanel`, `_actionButtons`, `_diceZone`, `_rerollCount`, `_floatingDamage`, `_damageFlashGroup`, `_playerActionButtons`
- [ ] (18) `TurnQueueView`: `_slotPrefab` → `TurnSlot.prefab` ; `_container`
- [ ] (19) `EnemyPanelView`: `_panelRoot`, `_name`, `_hpSlider`, `_hpText`, `_weaknessRoot`, `_weaknessIcon`
- [ ] (20) `ComboIndicatorView`: `_currentComboLabel`, `_rows` array (8 entries)
- [ ] (21) `DiceZoneView`: `_rollArea`, `_holdArea`, `_diceSlots` array
- [ ] (22) `ActionButtonsView`: `_attackButton`, `_energyRerollButton`, `_endTurnButton`
- [ ] (23) `PlayerActionButtonsView`: `_rollDiceButton`, `_rerollButton`, `_confirmAttackButton`, `_endTurnButton`, `_rerollLabel`
- [ ] (24) `RerollCountView`: `_countLabel`, `_extraRollButton`
- [ ] (25) `FloatingDamageSpawner`: `_instancePrefab` → `FloatingDamage.prefab` ; `_overlayContainer`

### C.5 Screens finales (items 26–29)

- [ ] (26) `FloorTransitionScreen`: `_floorNumberLabel`, `_floorTitleLabel`, `_continueButton`
- [ ] (27) `PauseMenuOverlay`: `_resumeButton`, `_settingsButton`, `_quitRunButton`
- [ ] (28) `VictoryScreen`: `_returnToMenuButton`, `_titleLabel`
- [ ] (29) `DefeatScreen`: `_returnToMenuButton`, `_titleLabel`

---

## D · RunController binder (~5 min)

- [ ] Crear script `RunBootstrapBehaviour.cs` (o similar) o MonoBehaviour en el GO `Bootstrap` de `00_Bootstrap.unity`
- [ ] Exponer `[SerializeField] FloorLayoutSO _floor1Layout;`
- [ ] En `Awake`, tras `BootstrapRunner`, llamar:
  ```csharp
  RunController.CreateAndRegister(_floor1Layout);
  ```
- [ ] Arrastrar `Floor1_Layout` al campo `_floor1Layout` en el Inspector

---

## E · Layout visual (tiempo variable)

Todos los `RectTransform` arrancan en `(0,0,0)`. Posicionar manualmente:

- [ ] `MainMenu`: título centrado arriba, botones en pila vertical centrada abajo
- [ ] `ExplorationHUD`: HP/Energy arriba-izquierda · Gold debajo · Minimap arriba-derecha · RoomNavigation centrado abajo
- [ ] `CombatHUD`: TurnQueue arriba · EnemyPanel derecha · DiceZone centro · botones abajo
- [ ] `ClassSelection`: split 40/60 entre LeftPanel (botones de clase) y RightPanel (detalles)
- [ ] `BuildSelection`: portrait + datos arriba · dice grid centro · botones abajo
- [ ] `FloorTransition`, `Pause`, `Victory`, `Defeat`: overlay centrado

---

## F · Smoke test

Desde `Assets/Scenes/00_Bootstrap.unity` en Play mode:

- [ ] Bootstrap loggea registración limpia
- [ ] Auto-load de `01_MainMenu`
- [ ] "Jugar" → ClassSelection
- [ ] Seleccionar Guerrero → Confirm habilita
- [ ] Confirm → BuildSelection
- [ ] Confirm → Exploration (RunController arrancó)
- [ ] Navegar entre rooms con `Proceed`
- [ ] Entrar a una combat room → CombatHUD aparece
- [ ] Player turn: seleccionar acción, reroll, end turn
- [ ] Enemy turn se ejecuta → vuelve al player
- [ ] Floor boss cleared → VictoryScreen ; o muerte → DefeatScreen
- [ ] `Return to Menu` → volver a MainMenu

---

## Cómo usar este checklist

- Tildá los items con `- [x]` en Obsidian (click en el checkbox).
- Al terminar una sección, cambiá el `status:` del frontmatter a `done` para esa fase si querés llevar estado por bloque.
- Cuando termine todo, mover `status: wip` → `done`, commit con
  `docs(obsidian): round 3 setup complete` y promover el item P1#1 del
  [[Implementation-Roadmap]] eliminándolo.
- Si aparece una ref null inesperada, Unity imprime el nombre del campo
  en el log — buscalo por texto en este archivo.

## Fuentes

- `docs/setup/_SETUP_ROUND2_STATUS.md` — doc canónico (tablas detalladas).
- [[Sprint03-Status]] — resumen de estado del sprint.
- [[Implementation-Roadmap]] P1 #1.
- Guías específicas por pantalla en `docs/setup/UI#*.md`.
