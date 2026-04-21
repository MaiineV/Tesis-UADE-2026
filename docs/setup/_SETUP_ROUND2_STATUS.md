# Sprint 03 MVP — Setup Status Post-MCP Automation

> **Generado:** 2026-04-20
> **Estado:** Round 2 completo. Escenas, jerarquias y SOs creados via MCP.
> **Pendiente:** Round 3 — mover assets, configurar valores, wiring de refs, layout visual.

---

## Lo que se creo automaticamente (Round 2)

### Escenas

| Escena | Build Index | Contenido |
|--------|-------------|-----------|
| `Assets/Scenes/00_Bootstrap.unity` | 0 | GO `Bootstrap` con componente `BootstrapRunner` |
| `Assets/Scenes/01_MainMenu.unity` | 1 | Jerarquia completa (~120 GameObjects, ver abajo) |

### Jerarquia de 01_MainMenu

```
EventSystem (EventSystem + StandaloneInputModule)
ScreenHost (ScreenHost, InitialScreenStringId="MainMenu")
CombatController (CombatController)
Canvas (Canvas + CanvasScaler 1920x1080 + GraphicRaycaster)
├── MainMenuScreen (DESACTIVADO) — MainMenuScreen.cs
│   ├── Background (Image, color #0E0E14)
│   ├── TitleLabel (TMP "Rollgeon", fontSize=120)
│   ├── PlayButton (Button + Text "Jugar")
│   └── QuitButton (Button + Text "Salir")
├── ClassSelectionScreen (DESACTIVADO) — ClassSelectionScreen.cs
│   ├── Background (Image, color #0E0E14)
│   ├── LeftPanel (VerticalLayoutGroup, spacing=10)
│   │   ├── WarriorButton (Button + Text "Guerrero" + SelectionIndicator GO desactivado)
│   │   ├── MagoButton (Button interactable=false + Text "Mago")
│   │   └── PicaroButton (Button interactable=false + Text "Picaro")
│   └── RightPanel (VerticalLayoutGroup, spacing=10)
│       ├── PortraitImage (Image)
│       ├── ContractDisplayView (ContractDisplayView.cs)
│       │   ├── HeaderLabel (TMP "Contrato del Guerrero")
│       │   ├── RowsContainer (VerticalLayoutGroup, spacing=4)
│       │   └── FooterLabel (TMP "Dano minimo = dado mas alto")
│       ├── PassiveLabel (TMP "Pasiva: TBD")
│       └── ConfirmButton (Button interactable=false + Text "Confirmar")
├── BuildSelectionScreen (DESACTIVADO) — BuildSelectionScreen.cs
│   ├── HeroNameLabel (TMP "Hero Name")
│   ├── HeroDescriptionLabel (TMP "Description")
│   ├── HeroPortrait (Image)
│   ├── DiceContainer (Transform vacio)
│   ├── DiceBagFallbackLabel (TMP "No dice bag configured")
│   ├── ConfirmButton (Button + Text "Confirm")
│   └── BackButton (Button + Text "Back")
├── ExplorationHUDView (DESACTIVADO) — ExplorationHUDView.cs
│   ├── HealthBarView (HealthBarView.cs)
│   │   ├── Slider (Slider, min=0, max=1)
│   │   └── HPText (TMP "100/100")
│   ├── EnergyBarView (EnergyBarView.cs)
│   │   ├── Slider (Slider, min=0, max=1)
│   │   └── EnergyText (TMP "4/4")
│   ├── GoldCounterView (GoldCounterView.cs)
│   │   └── GoldText (TMP "0G")
│   ├── ActiveItemsView (ActiveItemsView.cs)
│   │   ├── ArcoSlot (ActiveItemSlotView.cs)
│   │   │   ├── Icon (Image)
│   │   │   ├── InactiveOverlay (desactivado)
│   │   │   └── DepletedOverlay (desactivado)
│   │   └── PocionSlot (ActiveItemSlotView.cs)
│   │       ├── Icon (Image)
│   │       ├── InactiveOverlay (desactivado)
│   │       └── DepletedOverlay (desactivado)
│   ├── MinimapView (MinimapView.cs)
│   │   └── MapPivot
│   │       └── Placeholder (RawImage)
│   └── RoomNavigationView (RoomNavigationView.cs)
│       ├── RoomNameLabel (TMP "Room Name")
│       ├── RoomProgressLabel (TMP "Room ?/?")
│       ├── RoomTypeLabel (TMP "")
│       ├── ProceedButton (Button + Text "Proceed")
│       └── PauseButton (Button + Text "Pause")
├── CombatHUDView (DESACTIVADO) — CombatHUDView.cs
│   ├── DamageFlashGroup (Image rojo semi-transparente + CanvasGroup alpha=0)
│   ├── TurnQueueView (TurnQueueView.cs + HorizontalLayoutGroup)
│   ├── EnemyPanelView (EnemyPanelView.cs)
│   │   └── PanelRoot
│   │       ├── NameLabel (TMP "Enemy")
│   │       ├── HpSlider (Slider)
│   │       ├── HpText (TMP "HP")
│   │       └── WeaknessRoot > WeaknessIcon (Image)
│   ├── ComboIndicatorView (ComboIndicatorView.cs)
│   │   ├── CurrentComboLabel (TMP "---")
│   │   └── ContractRows (VerticalLayoutGroup)
│   ├── DiceZoneView (DiceZoneView.cs)
│   │   ├── RollArea
│   │   ├── HoldArea
│   │   └── DiceSlot1..DiceSlot5
│   ├── ActionButtonsView (ActionButtonsView.cs)
│   │   ├── AttackBtn (Button + Text "Attack")
│   │   ├── EnergyRerollBtn (Button + Text "Reroll")
│   │   └── EndTurnBtn (Button + Text "End Turn")
│   ├── PlayerActionButtonsView (PlayerActionButtonsView.cs)
│   │   ├── RollDiceButton (Button + Text "Roll Dice")
│   │   ├── RerollButton (Button + RerollLabel TMP "Reroll")
│   │   ├── ConfirmAttackButton (Button + Text "Confirm Attack")
│   │   └── EndTurnButton (Button + Text "End Turn")
│   ├── RerollCountView (RerollCountView.cs)
│   │   ├── CountLabel (TMP "-/-")
│   │   └── ExtraRollButton (Button)
│   └── FloatingDamageOverlay (FloatingDamageSpawner.cs)
├── FloorTransitionScreen (DESACTIVADO) — FloorTransitionScreen.cs
│   ├── FloorNumberLabel (TMP "Piso 1")
│   ├── FloorTitleLabel (TMP "")
│   └── ContinueButton (Button + Text "Continuar")
├── PauseMenuOverlay (DESACTIVADO) — PauseMenuOverlay.cs
│   ├── Panel (Image negro alpha=0.6)
│   ├── ResumeButton (Button + Text "Resume")
│   ├── SettingsButton (Button + Text "Settings")
│   └── QuitRunButton (Button + Text "Quit Run")
├── VictoryScreen (ACTIVO — necesario para Awake) — VictoryScreen.cs
│   ├── TitleLabel (TMP "Victory!", fontSize=64)
│   └── ReturnToMenuButton (Button + Text "Return to Menu")
└── DefeatScreen (ACTIVO — necesario para Awake) — DefeatScreen.cs
    ├── TitleLabel (TMP "Defeat", fontSize=64)
    └── ReturnToMenuButton (Button + Text "Return to Menu")
```

### Prefabs (en Assets/ root — mover a Assets/Rollgeon/Prefabs/UI/)

| Prefab | Componente |
|--------|------------|
| `ComboRow.prefab` | `ComboRowView` |
| `DiceSlotView.prefab` | `DiceSlotView` |
| `TurnSlot.prefab` | `TurnSlotView` |
| `FloatingDamage.prefab` | `FloatingDamageInstance` |

### ScriptableObjects creados (en Assets/ root — mover y renombrar)

| Asset actual | Mover a | Renombrar a |
|-------------|---------|-------------|
| `ActionDefinition.asset` | `Assets/Rollgeon/Actions/` | `AD_AttackBasic` |
| `ActionDefinition 1.asset` | `Assets/Rollgeon/Actions/` | `AD_AttackSpecial` |
| `ActionDefinition 2.asset` | `Assets/Rollgeon/Actions/` | `AD_Heal` |
| `ActionDefinition 3.asset` | `Assets/Rollgeon/Actions/` | `AD_ForceDoor` |
| `ActionDefinition 4.asset` | `Assets/Rollgeon/Actions/` | `AD_EndTurn` |
| `Combo_DoblePar.asset` | `Assets/Rollgeon/Combos/` | (nombre ok) |
| `Combo_SumaX.asset` | `Assets/Rollgeon/Combos/` | (nombre ok) |
| `Combo_FullHouse.asset` | `Assets/Rollgeon/Combos/` | (nombre ok) |
| `Combo_Generala.asset` | `Assets/Rollgeon/Combos/` | (nombre ok) |
| `Combo_Generala 1.asset` | **BORRAR** | (duplicado) |
| `PhaseTransitionMatrix.asset` | `Assets/Rollgeon/Phase/` | (nombre ok) |
| `PhaseServiceBootstrap.asset` | `Assets/Rollgeon/Bootstrap/` | (nombre ok) |
| `PlayerServiceBootstrap.asset` | `Assets/Rollgeon/Bootstrap/` | (nombre ok) |
| `TurnManagerBootstrap.asset` | `Assets/Rollgeon/Bootstrap/` | (nombre ok) |
| `TurnOrderServiceBootstrap.asset` | `Assets/Rollgeon/Bootstrap/` | (nombre ok) |
| `RerollBudgetServiceBootstrap.asset` | `Assets/Rollgeon/Bootstrap/` | (nombre ok) |
| `WeaknessServiceBootstrap.asset` | `Assets/Rollgeon/Bootstrap/` | (nombre ok) |
| `ComboCountersServiceBootstrap.asset` | `Assets/Rollgeon/Bootstrap/` | (nombre ok) |
| `Room.asset` | `Assets/Rollgeon/Dungeon/` | `Room_Combat01` |
| `Room 1.asset` | `Assets/Rollgeon/Dungeon/` | `Room_Combat02` |
| `Room 2.asset` | `Assets/Rollgeon/Dungeon/` | `Room_Combat03` |
| `Room 3.asset` | `Assets/Rollgeon/Dungeon/` | `Room_Shop01` |
| `Room 4.asset` | `Assets/Rollgeon/Dungeon/` | `Room_Potion01` |
| `EnemyPool.asset` | `Assets/Rollgeon/Dungeon/` | `Floor1_CombatPool` |
| `FloorLayout.asset` | `Assets/Rollgeon/Dungeon/` | `Floor1_Layout` |
| `EnemyData.asset` | `Assets/Rollgeon/Enemies/` | `EnemyData_Boss` |
| (2do EnemyData si existe) | `Assets/Rollgeon/Enemies/` | `EnemyData_Goblin` |

### SOs que ya existian (de sprints anteriores)

| Asset | Ubicacion |
|-------|-----------|
| `ServiceBootstrap.asset` | `Assets/Rollgeon/` |
| `Ruleset.asset` | `Assets/Rollgeon/Rulesets/` |
| `AD_Move.asset` | `Assets/Rollgeon/Actions/` |
| `ActionCatalog.asset` | `Assets/Rollgeon/Actions/` |
| `CH_Warrior.asset` | `Assets/Rollgeon/Classes/` |
| `ComboCatalog.asset` | `Assets/Rollgeon/Combos/` |
| `Combo_Par.asset` | `Assets/Rollgeon/Combos/` |
| `Combo_Trio.asset` | `Assets/Rollgeon/Combos/` |
| `Combo_Escalera.asset` | `Assets/Rollgeon/Combos/` |
| `Combo_Poker.asset` | `Assets/Rollgeon/Combos/` |
| `EnemyCatalog.asset` | `Assets/Rollgeon/Enemies/` |
| `EnemyData_Test.asset` | `Assets/Rollgeon/Enemies/` |

---

## Round 3 — Trabajo manual pendiente

### A. Mover y renombrar assets (15 min)

En el Project window de Unity, arrastrar cada asset de `Assets/` root a su carpeta
correcta (ver tabla arriba). Usar el Project window para mover preserva GUIDs.

**Borrar:** `Combo_Generala 1.asset` (duplicado).

**Mover prefabs** a `Assets/Rollgeon/Prefabs/UI/` (crear la carpeta si no existe):
- `ComboRow.prefab`
- `DiceSlotView.prefab`
- `TurnSlot.prefab`
- `FloatingDamage.prefab`

### B. Configurar valores de ScriptableObjects (30 min)

#### ActionDefinitions (5 nuevos)

| Asset (tras renombrar) | ActionId | Type | EnergyCost | BlockOnRepeat |
|------------------------|----------|------|------------|---------------|
| `AD_AttackBasic` | `attack.basic` | Attack | 1 | true |
| `AD_AttackSpecial` | `attack.special` | Attack | 2 | true |
| `AD_Heal` | `skill.heal` | SkillCheck | 2 | true |
| `AD_ForceDoor` | `skill.force_door` | SkillCheck | 1 | true |
| `AD_EndTurn` | `defend` | Defend | 0 | true |

#### Combos faltantes (4 — setear `_baseDamage`)

| Asset | BaseDamage |
|-------|------------|
| `Combo_DoblePar` | 18 |
| `Combo_SumaX` | 25 |
| `Combo_FullHouse` | 40 |
| `Combo_Generala` | 100 |

#### Rooms (5 — setear RoomId, DisplayName, Type)

| Asset (tras renombrar) | RoomId | DisplayName | Type | EnemyPool |
|------------------------|--------|-------------|------|-----------|
| `Room_Combat01` | `combat_01` | Sala de Combate 1 | Combat | Floor1_CombatPool |
| `Room_Combat02` | `combat_02` | Sala de Combate 2 | Combat | Floor1_CombatPool |
| `Room_Combat03` | `combat_03` | Sala de Combate 3 | Combat | Floor1_CombatPool |
| `Room_Shop01` | `shop_01` | Tienda | Shop | null |
| `Room_Potion01` | `potion_01` | Sala de Pociones | Potion | null |

#### EnemyData (2)

| Asset | BaseHP | BaseAttack | BaseSpeed | MaxEnergy |
|-------|--------|------------|-----------|-----------|
| `EnemyData_Boss` | 100 | 15 | 3 | 5 |
| `EnemyData_Goblin` | 20 | 8 | 4 | 3 |

#### EnemyPool

`Floor1_CombatPool`: agregar `EnemyData_Test` + `EnemyData_Goblin` con Weight=1 cada uno.

#### FloorLayout

`Floor1_Layout`: RoomCountMin=6, RoomCountMax=8, CombatRooms=[3 rooms], ShopRooms=[1],
PotionRooms=[1], BossCandidates=[EnemyData_Boss].

#### PhaseTransitionMatrix

Configurar la grilla 5x5:
- None -> Exploration: permitido
- Exploration -> Combat: permitido
- Exploration -> Loading: permitido
- Combat -> Exploration: permitido
- Combat -> GameOver: permitido
- * -> GameOver: permitido

Overlays: Exploration -> [Pause], Combat -> [Pause].

#### Ruleset (verificar/completar)

- Energy: Max=4, AtRunStart=2, RegenBase=2
- TurnOrder: SpeedDieMin=1, SpeedDieMax=6
- Weakness: DefaultMultiplier=1.5
- Counters: PerUseBonus=0.02, MaxBonus=0.20

### C. Wiring de referencias en Inspector (45-60 min)

El MCP no puede setear object references. Todo el drag-and-drop va manual.

#### 00_Bootstrap.unity

1. **BootstrapRunner** -> arrastrar `ServiceBootstrap.asset` a `_bootstrap`

#### ServiceBootstrap.asset

2. **Catalogs**: agregar ActionCatalog, ComboCatalog, EnemyCatalog
3. **Settings Assets**: agregar Ruleset, PhaseTransitionMatrix
4. **Extra Runtime Services**: agregar (en orden de Priority):
   - AttributesManagerBootstrap (5) — **requerido primero**; registra
     `AttributesManager` global. Sin esto, `EnergyService`, `TurnManager`
     y `RerollBudgetService` fallan en cascada al hacer Play.
   - PhaseServiceBootstrap (10)
   - PlayerServiceBootstrap (30)
   - EnergyService (50)
   - TurnManagerBootstrap (60)
   - TurnOrderServiceBootstrap (100)
   - RerollBudgetServiceBootstrap
   - WeaknessServiceBootstrap
   - ComboCountersServiceBootstrap

#### 01_MainMenu.unity — Screens

5. **MainMenuScreen**: `_playButton` -> PlayButton, `_quitButton` -> QuitButton
6. **ClassSelectionScreen**: `_warriorHero` -> CH_Warrior.asset, `_warriorButton`,
   `_magoButton`, `_picaroButton`, `_confirmButton`, `_contractDisplay`,
   `_portraitDisplay`, `_passiveDisplay`, `_warriorSelectionIndicator`
7. **ContractDisplayView**: `_headerLabel`, `_rowsContainer`, `_rowPrefab` -> ComboRow.prefab,
   `_footerLabel`
8. **BuildSelectionScreen**: `_heroNameLabel`, `_heroDescriptionLabel`, `_heroPortrait`,
   `_diceContainer`, `_diceSlotPrefab` -> DiceSlotView.prefab, `_diceBagFallbackLabel`,
   `_confirmButton`, `_backButton`

#### 01_MainMenu.unity — ExplorationHUD

9. **ExplorationHUDView**: `_healthBar`, `_energyBar`, `_goldCounter`, `_activeItems`,
   `_minimap`, `_roomNavigation` -> las 6 sub-views hijas
10. **HealthBarView**: `_slider` -> Slider hijo, `_text` -> HPText hijo
11. **EnergyBarView**: `_slider` -> Slider hijo, `_text` -> EnergyText hijo
12. **GoldCounterView**: `_text` -> GoldText hijo
13. **ActiveItemsView**: configurar Bindings array (item.arco -> ArcoSlot, item.pocion -> PocionSlot)
14. **ActiveItemSlotView** (x2): `_icon`, `_inactiveOverlay`, `_depletedOverlay`
15. **MinimapView**: `_mapPivot`, `_placeholder`
16. **RoomNavigationView**: `_roomNameLabel`, `_roomProgressLabel`, `_roomTypeLabel`,
    `_proceedButton`, `_pauseButton`

#### 01_MainMenu.unity — CombatHUD

17. **CombatHUDView**: `_turnQueue`, `_comboIndicator`, `_enemyPanel`, `_actionButtons`,
    `_diceZone`, `_rerollCount`, `_floatingDamage`, `_damageFlashGroup`,
    `_playerActionButtons`
18. **TurnQueueView**: `_slotPrefab` -> TurnSlot.prefab, `_container`
19. **EnemyPanelView**: `_panelRoot`, `_name`, `_hpSlider`, `_hpText`, `_weaknessRoot`,
    `_weaknessIcon`
20. **ComboIndicatorView**: `_currentComboLabel`, `_rows` array (8 entries)
21. **DiceZoneView**: `_rollArea`, `_holdArea`, `_diceSlots` array
22. **ActionButtonsView**: `_attackButton`, `_energyRerollButton`, `_endTurnButton`
23. **PlayerActionButtonsView**: `_rollDiceButton`, `_rerollButton`, `_confirmAttackButton`,
    `_endTurnButton`, `_rerollLabel`
24. **RerollCountView**: `_countLabel`, `_extraRollButton`
25. **FloatingDamageSpawner**: `_instancePrefab` -> FloatingDamage.prefab, `_overlayContainer`

#### 01_MainMenu.unity — Screens finales

26. **FloorTransitionScreen**: `_floorNumberLabel`, `_floorTitleLabel`, `_continueButton`
27. **PauseMenuOverlay**: `_resumeButton`, `_settingsButton`, `_quitRunButton`
28. **VictoryScreen**: `_returnToMenuButton`, `_titleLabel`
29. **DefeatScreen**: `_returnToMenuButton`, `_titleLabel`

### D. Layout visual (tiempo variable)

Posicionar los elementos UI con RectTransform anchors. Todo lo creado via MCP tiene
Transform default (0,0,0) — necesita posicionamiento manual.

Sugerencias:
- MainMenu: titulo centrado arriba, botones en pila vertical centrada abajo
- ExplorationHUD: HP/Energy arriba-izquierda, Gold debajo, Minimap arriba-derecha,
  RoomNavigation centrado abajo
- CombatHUD: TurnQueue arriba, EnemyPanel derecha, DiceZone centro, botones abajo

### E. RunController wiring (5 min)

Crear un MonoBehaviour en la escena (o en Bootstrap) que en Awake llame:

```csharp
RunController.CreateAndRegister(defaultLayout);
```

Con referencia serializada al `Floor1_Layout` FloorLayoutSO.

### F. Smoke test

1. Play desde `00_Bootstrap` — verifica bootstrap logs
2. Jugar -> ClassSelection -> Guerrero -> Confirm -> BuildSelection -> Confirm
3. Exploration: Proceed entre rooms, entrar a combat
4. Combat: turnos player/enemy, victoria o derrota
5. Victory/Defeat -> Return to Menu

---

## Limitaciones descubiertas del MCP

| Operacion | Funciona? | Nota |
|-----------|-----------|------|
| Crear escenas + Build Settings | SI | `create_scene` con `addToBuildSettings` |
| Crear GameObjects + jerarquias | SI | `update_gameobject` crea si no existe |
| Agregar componentes | SI | Solo con nombre de clase sin namespace |
| Setear campos simples (string, int, bool) | SI | Via `update_component.componentData` |
| Crear SOs via menu item | SI | Caen en carpeta activa del Project window (root) |
| Batch operations | SI | Hasta 100 ops por batch |
| **Setear refs a assets** | **NO** | Reporta success pero el campo queda null |
| **Setear refs entre GOs** | **NO** | Mismo problema — refs quedan null |
| Menu items de UI (Canvas, EventSystem) | NO | Falla silenciosa; workaround: crear GO + componentes manual |
| Crear carpetas | NO | No hay herramienta; se crean al mover assets |
