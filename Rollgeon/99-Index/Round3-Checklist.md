---
title: Round3-Checklist
type: index
domain: 99-Index
status: wip
scenes: 3
tags: [index, checklist, round3, setup, scenes]
---

# Round 3 — Manual Setup Checklist

> Working checklist del setup manual de Unity que cierra Sprint 03.
> **Arquitectura de 3 escenas**: `00_Bootstrap` + `01_MainMenu` +
> `02_Gameplay`. Todos los items son checkboxes Markdown — ticklos en
> Obsidian a medida que avanzás. Valores inline; referencia detallada en
> `docs/setup/_PLAYABLE_LOOP_TWO_SCENE_SETUP.md` (fuente canónica del
> split en 3 escenas).
>
> Total estimado: **~3h – 3h 30min** (incluye secciones G, H, J nuevas). Status:
> [[Sprint03-Status]] · Plan: [[Implementation-Roadmap]] P1 #1.

---

## A · Asset organization (~15 min)

- [x] Crear carpeta `Assets/Rollgeon/Prefabs/UI/`
- [x] Mover `ComboRow.prefab` del root → `Assets/Rollgeon/Prefabs/UI/`
- [x] Mover `DiceSlotView.prefab` del root → `Assets/Rollgeon/Prefabs/UI/`
- [x] Mover `TurnSlot.prefab` del root → `Assets/Rollgeon/Prefabs/UI/`
- [x] Mover `FloatingDamage.prefab` del root → `Assets/Rollgeon/Prefabs/UI/`
- [x] Si existe `Combo_Generala 1.asset` duplicado → borrar
- [x] (Opcional) Borrar carpeta vacía `Assets/Rollgeon/Scenes/` desde Unity

---

## B · Crear SOs faltantes (~20 min)

### B.1 ActionDefinitions — `Assets/Rollgeon/Actions/`

Renombrar `AD_Move.asset` si aplica o crear 5 nuevos (`Create → Rollgeon/Actions/Action Definition`):

- [x] `AD_AttackBasic` — ActionId: `attack.basic` · Type: `Attack` · EnergyCost: **1** · BlockOnRepeat: `true`
- [x] `AD_AttackSpecial` — ActionId: `attack.special` · Type: `Attack` · EnergyCost: **2** · BlockOnRepeat: `true`
- [x] `AD_Heal` — ActionId: `skill.heal` · Type: `SkillCheck` · EnergyCost: **2** · BlockOnRepeat: `true`
- [x] `AD_ForceDoor` — ActionId: `skill.force_door` · Type: `SkillCheck` · EnergyCost: **1** · BlockOnRepeat: `true`
- [x] `AD_EndTurn` — ActionId: `defend` · Type: `Defend` · EnergyCost: **0** · BlockOnRepeat: `true`

### B.2 Combos faltantes — `Assets/Rollgeon/Combos/`

`Create → Rollgeon/Combos/<Tipo>`:

- [x] `Combo_DoblePar` — BaseDamage: **18**
- [x] `Combo_SumaX` — BaseDamage: **25**
- [x] `Combo_FullHouse` — BaseDamage: **40**
- [x] `Combo_Generala` — BaseDamage: **100**

Verificar los ya creados:

- [x] `Combo_Par` — BaseDamage: 10
- [x] `Combo_Trio` — BaseDamage: 28
- [x] `Combo_Escalera` — BaseDamage: 35
- [x] `Combo_Poker` — BaseDamage: 60

### B.3 EnemyData — `Assets/Rollgeon/Enemies/`

- [x] `EnemyData_Boss` — BaseHP: **100** · BaseAttack: **15** · BaseSpeed: **3** · MaxEnergy: **5**
- [x] `EnemyData_Goblin` — BaseHP: **20** · BaseAttack: **8** · BaseSpeed: **4** · MaxEnergy: **3**

### B.4 EnemyPool

- [x] `Floor1_CombatPool` (EnemyPoolSO) con entries: `EnemyData_Test` (Weight=1) + `EnemyData_Goblin` (Weight=1)

### B.5 Rooms — `Assets/Rollgeon/` (crear carpeta `Rooms/` si no existe)

| Asset | RoomId | DisplayName | Type | EnemyPool |
|---|---|---|---|---|
| `Room_Start01`  | `start_01`  | "Entrada"           | Start  | null |
| `Room_Combat01` | `combat_01` | "Sala de Combate 1" | Combat | `Floor1_CombatPool` |
| `Room_Combat02` | `combat_02` | "Sala de Combate 2" | Combat | `Floor1_CombatPool` |
| `Room_Combat03` | `combat_03` | "Sala de Combate 3" | Combat | `Floor1_CombatPool` |
| `Room_Shop01`   | `shop_01`   | "Tienda"            | Shop   | null |
| `Room_Potion01` | `potion_01` | "Sala de Pociones"  | Potion | null |

- [x] `Room_Start01` — hub vacío donde arranca el piso (ExplorationHUD visible, sin combate)
- [x] `Room_Combat01`
- [x] `Room_Combat02`
- [x] `Room_Combat03`
- [x] `Room_Shop01`
- [x] `Room_Potion01`

### B.6 Floor + Matrix

- [x] `Floor1_Layout` (FloorLayoutSO) — RoomCountMin: **6** · RoomCountMax: **8** · CombatRooms: [3 rooms] · ShopRooms: [Shop01] · PotionRooms: [Potion01] · BossCandidates: [EnemyData_Boss] · **StartRoom: Room_Start01** ← campo nuevo, sin esto la primera room es combat
- [x] `PhaseTransitionMatrix` (PhaseTransitionMatrixSO) — grilla 5×5:
  - [x] None → Exploration ✓
  - [x] Exploration → Combat ✓
  - [x] Exploration → Loading ✓
  - [x] Combat → Exploration ✓
  - [x] Combat → GameOver ✓
  - [x] * → GameOver ✓
  - [x] Overlays: Exploration → [Pause] ; Combat → [Pause]

### B.7 Ruleset — verificar `Assets/Rollgeon/Rulesets/Ruleset.asset`

- [x] Energy: Max=**4** · AtRunStart=**2** · RegenBase=**2**
- [x] TurnOrder: SpeedDieMin=**1** · SpeedDieMax=**6**
- [x] Weakness: DefaultMultiplier=**1.5**
- [x] Counters: PerUseBonus=**0.02** · MaxBonus=**0.20**

---

## C · Wiring de escenas `00_Bootstrap` y `01_MainMenu` (~20 min)

### C.1 Bootstrap — `Assets/Scenes/00_Bootstrap.unity` (items 1–4)

**En el GameObject `Bootstrap` de la escena:**

- [x] (1) `BootstrapRunner._bootstrap` → `Assets/Rollgeon/ServiceBootstrap.asset`
- [x] (1b) `BootstrapRunner._dontDestroyOnLoad` → **`true`** (crítico: este GO sobrevive a los loads de 01_MainMenu y 02_Gameplay)
- [x] (1c) `BootstrapRunner._preloadCatalogs` → `true`
- [x] (1d) Add Component → `Run Controller Bootstrapper` → `_defaultLayout` = `Floor1_Layout.asset`

**En `Assets/Rollgeon/ServiceBootstrap.asset`:**

- [x] (2) `Catalogs`: ActionCatalog, ComboCatalog, EnemyCatalog
- [x] (3) `SettingsAssets`: Ruleset, PhaseTransitionMatrix
- [x] (4) `ExtraServices` en orden de Priority:
  - [x] **AttributesManagerBootstrap (5)** ← requerido primero; sin esto EnergyService/TurnManager/RerollBudget fallan en cascada
  - [x] PhaseServiceBootstrap (10)
  - [x] PlayerServiceBootstrap (30)
  - [x] EnergyService (50)
  - [x] TurnManagerBootstrap (60)
  - [x] TurnOrderServiceBootstrap (100)
  - [x] RerollBudgetServiceBootstrap
  - [x] WeaknessServiceBootstrap
  - [x] ComboCountersServiceBootstrap

- [x] `File → Build Settings` → index 0 `00_Bootstrap`, index 1 `01_MainMenu`, index 2 `02_Gameplay` (se agrega al crear la escena en D)

> **Checkpoint C.1:** Play desde `00_Bootstrap` loggea `Registered N catalogs…` + `[RunControllerBootstrapper] RunController registrado…` sin errores.

### C.2 Menu screens — `Assets/Scenes/01_MainMenu.unity` (items 5–8)

> **Alcance de esta escena.** Solo los 3 screens de menú (Main, Class,
> Build). Los HUDs de exploración/combate y las overlays de
> pause/victory/defeat se mueven a `02_Gameplay` en la sección D.

- [x] (0) Bajo el Canvas: **borrar** `ExplorationHUDView`, `CombatHUDView`,
  `FloorTransitionScreen`, `PauseMenuOverlay`, `VictoryScreen`,
  `DefeatScreen`. Fuera del Canvas: **borrar** `CombatController` si existe.
  Deben quedar: `EventSystem`, `ScreenHost` (`_initialScreenStringId = "MainMenu"`),
  Canvas con `MainMenuScreen` + `ClassSelectionScreen` + `BuildSelectionScreen`.
- [x] (5) `MainMenuScreen`: `_playButton` → PlayButton ; `_quitButton` → QuitButton
- [x] (6) `ClassSelectionScreen` — 9 refs:
  - [x] `_warriorHero` → `CH_Warrior.asset`
  - [x] `_warriorButton`, `_magoButton`, `_picaroButton`
  - [x] `_confirmButton`
  - [x] `_contractDisplay`
  - [x] `_portraitDisplay`
  - [x] `_passiveDisplay`
  - [x] `_warriorSelectionIndicator`
- [x] (7) `ContractDisplayView`: `_headerLabel`, `_rowsContainer`, `_rowPrefab` → `ComboRow.prefab`, `_footerLabel`
- [x] (8) `BuildSelectionScreen` — 8 refs:
  - [x] `_heroNameLabel`, `_heroDescriptionLabel`
  - [x] `_heroPortrait`
  - [x] `_diceContainer`, `_diceSlotPrefab` → `DiceSlotView.prefab`
  - [x] `_diceBagFallbackLabel`
  - [x] `_confirmButton`, `_backButton`

> **Checkpoint C.2:** Play desde `00_Bootstrap` → `Jugar` →
> `ClassSelection` → `BuildSelection` sin NullReference. **No** confirmar
> todavía — `02_Gameplay` aún no existe y `SceneManager.LoadScene("02_Gameplay")`
> va a fallar hasta completar la sección D.

---

## D · Escena `02_Gameplay.unity` (~60 min)

> **Cómo crear la escena.** Opción recomendada: click derecho en
> `Assets/Scenes/01_MainMenu.unity` → `Duplicate` → renombrar a
> `02_Gameplay.unity`. Trae los HUDs/overlays ya cableados de Round 2.
> Alternativa (más trabajo): crear desde cero y reconstruir el Canvas.

### D.1 Setup inicial

- [x] Duplicar `01_MainMenu.unity` → `02_Gameplay.unity` en `Assets/Scenes/`
- [x] Abrir la escena nueva. Bajo el Canvas: **borrar** `MainMenuScreen`,
  `ClassSelectionScreen`, `BuildSelectionScreen`. Deben quedar los HUDs +
  overlays (`ExplorationHUDView`, `CombatHUDView`, `FloorTransitionScreen`,
  `PauseMenuOverlay`, `VictoryScreen`, `DefeatScreen`) y fuera del Canvas
  `EventSystem`, `ScreenHost`, `CombatController`, `Main Camera`, `Directional Light`.
- [x] Si el `CombatController` fue borrado en C.2 al limpiar `01_MainMenu`,
  recrearlo: `GameObject → Create Empty` → `CombatController` → Add Component `Combat Controller`.
- [x] `GameObject → Create Empty` → renombrar `GameplayBootstrapper` →
  Add Component `Gameplay Bootstrapper` (no tiene fields serializados).
- [x] `ScreenHost._initialScreenStringId` → **vacío** (lo setea
  `GameplayBootstrapper.Start` empujando "ExplorationHUD").
- [x] `File → Build Settings` → agregar `02_Gameplay.unity` en index 2.

### D.2 ExplorationHUD (items 9–16)

- [x] (9) `ExplorationHUDView` — 6 refs a sub-views: `_healthBar`, `_energyBar`, `_goldCounter`, `_activeItems`, `_minimap`, `_roomNavigation`
- [x] (10) `HealthBarView`: `_slider` → `Slider` hijo ; `_text` → `HPText` hijo
- [x] (11) `EnergyBarView`: `_slider` → `Slider` hijo ; `_text` → `EnergyText` hijo
- [x] (12) `GoldCounterView`: `_text` → `GoldText` hijo
- [x] (13) `ActiveItemsView`: `Bindings` array (`item.arco` → ArcoSlot ; `item.pocion` → PocionSlot)
- [x] (14) `ActiveItemSlotView` × 2 (ArcoSlot + PocionSlot): `_icon`, `_inactiveOverlay`, `_depletedOverlay`
- [x] (15) `MinimapView`: `_mapPivot`, `_placeholder`
- [x] (16) `RoomNavigationView`: `_roomNameLabel`, `_roomProgressLabel`, `_roomTypeLabel`, `_proceedButton`, `_pauseButton`

### D.3 CombatHUD (items 17–25)

- [x] (17) `CombatHUDView` — **10 refs**: `_turnQueue`, `_comboIndicator`, `_diceZone`, `_rerollCount`, `_floatingDamage`, `_playerActionButtons`, `_healthBar`, `_energyBar`, `_endTurnButtonView`, `_damageFlashGroup`
  - ⚠️ Ya NO tiene `_enemyPanel` (reemplazado por WorldSpaceHealthBar §0206) ni `_actionButtons` (legacy).
- [x] (18) `TurnQueueView`: `_slotPrefab` → `TurnSlot.prefab` ; `_container`
- ~~(19) `EnemyPanelView`~~ — **ELIMINADO**. Reemplazado por `WorldSpaceHealthBar` sobre cada pawn (ver sección G).
- [ ] (20) `ComboIndicatorView`: `_currentComboLabel` → TMP del combo actual ; `_rows` → array de 8 `ComboRow` (`ComboId`, `Label`, `BlockedOverlay`). Un `ComboRow` por combo del contrato del Guerrero:
  - `combo.par` (Par), `combo.double_pair` (DoblePar), `combo.triple` (Trio), `combo.straight` (Escalera), `combo.full_house` (FullHouse), `combo.poker` (Poker), `combo.generala` (Generala), `combo.sum_x` (SumaX).
  - **Para el smoke test** podés dejar `_rows` **vacío** — el view no crashea; solo perdés la lista visual del contrato y el feedback de "combo bloqueado" (boss T103). `_currentComboLabel` funciona independiente. Armá las 8 rows cuando hagas el layout visual.
- [x] (21) `DiceZoneView`: `_rollArea`, `_holdArea`, `_diceSlots` array
- [x] (22) `ActionButtonsView` (legacy, puede coexistir): `_attackButton`, `_energyRerollButton`, `_endTurnButton`
  - [x] (22b) `ActionButtonsView._attackAction` → `Assets/Rollgeon/Actions/AD_AttackBasic.asset`
  - **Nota**: si solo usás `PlayerActionButtonsView` + `EndTurnButtonView` + `RerollCountView`, podés dejar `ActionButtonsView` sin cablear (null-safe).
- [x] (23) `PlayerActionButtonsView` — **5 refs** (post-refactor):
  - `_movementButton` → MovementBtn
  - `_attackButton` → AttackBtn
  - `_specialButton` → SpecialBtn
  - `_healButton` → HealBtn
  - `_confirmButton` → ConfirmBtn
  - ⚠️ Ya NO tiene `_rollDiceButton`, `_rerollButton`, `_confirmAttackButton`, `_endTurnButton`, ni `_rerollLabel`. Los botones de Reroll están en `RerollCountView`, EndTurn en `EndTurnButtonView`.
- [x] (23b) `EndTurnButtonView` — **NUEVO** componente separado:
  - Crear un hijo del CombatHUD: `EndTurnButtonPanel`
  - Add Component → `EndTurnButtonView`
  - Crear hijo `Button`: `EndTurnBtn`
  - `_endTurnButton` → EndTurnBtn
  - Cablear `CombatHUDView._endTurnButtonView` → este GO
- [x] (24) `RerollCountView` — **3 refs** (actualizado):
  - `_countLabel` → TMP que muestra "{used}/{cap}"
  - `_extraRollButton` → botón de extra roll
  - `_costLabel` → TMP opcional que muestra "Free" / "1E" (null = skip)
- [x] (25) `FloatingDamageSpawner`: `_instancePrefab` → `FloatingDamage.prefab` ; `_overlayContainer`

### D.4 Screens finales (items 26–29)

- [x] (26) `FloorTransitionScreen`: `_floorNumberLabel`, `_floorTitleLabel`, `_continueButton`
- [x] (27) `PauseMenuOverlay`: `_resumeButton`, `_settingsButton`, `_quitRunButton`
- [x] (28) `VictoryScreen`: `_returnToMenuButton`, `_titleLabel`
- [x] (29) `DefeatScreen`: `_returnToMenuButton`, `_titleLabel`

> **Checkpoint D:** Play desde `00_Bootstrap` y llegar hasta `02_Gameplay`.
> Console debería loggear `[GameplayBootstrapper] Run started. hero=hero.warrior…`.
> El ExplorationHUD aparece; si no, ver sección Troubleshooting del
> guide canónico (`_PLAYABLE_LOOP_TWO_SCENE_SETUP.md §4`).

---

## E · Layout visual (tiempo variable)

Todos los `RectTransform` arrancan en `(0,0,0)`. Posicionar manualmente.

### E.1 `01_MainMenu`

- [x] `MainMenu`: título centrado arriba, botones en pila vertical centrada abajo
- [x] `ClassSelection`: split 40/60 entre LeftPanel (botones de clase) y RightPanel (detalles)
- [x] `BuildSelection`: portrait + datos arriba · dice grid centro · botones abajo

### E.2 `02_Gameplay`

- [x] `ExplorationHUD`: HP/Energy arriba-izquierda · Gold debajo · Minimap arriba-derecha · RoomNavigation centrado abajo
- [x] `CombatHUD`: TurnQueue arriba · DiceZone centro · Behaviors + Confirm abajo-izquierda · EndTurn abajo-derecha · RerollCount junto a DiceZone · HealthBar/EnergyBar del player arriba-izquierda
  - ⚠️ `EnemyPanel` ya no existe — las barras de vida de los enemigos son world-space sobre cada pawn (sección G).
- [x] `FloorTransition`, `Pause`, `Victory`, `Defeat`: overlay centrado con backdrop semi-transparente

---

## G · World-Space Health Bars (§0206) (~15 min)

> Reemplaza el antiguo `EnemyPanelView` screen-space. Cada pawn de
> enemigo/boss muestra una barra de HP world-space sobre su cabeza.
> Ref: `docs/setup/System#0206_WorldSpaceHealthBars.md`.

### G.1 Prefab de enemy — agregar barra

Para cada prefab de enemy (ej. `Goblin.prefab`, `Boss.prefab`):

- [x] Crear hijo del root: `Canvas` con **Render Mode = World Space**
  - Scale ≈ `(0.01, 0.01, 0.01)` (ajustar según tamaño visual)
- [x] Dentro del Canvas:
  - [x] **Image** `Fill`: `Image Type = Filled`, `Fill Method = Horizontal`, `Fill Origin = Left`
  - [x] **TextMeshPro** `HpText` (opcional): centrado, font size apropiado
- [x] Add Component → `WorldSpaceHealthBar` al Canvas GO
- [x] Cablear en Inspector:
  - `_fillImage` → `Fill`
  - `_hpText` → `HpText` (o null)
  - `_barRoot` → el Canvas GO (para show/hide)
  - `_offset` → `(0, 2, 0)` (ajustar Y para que quede sobre la cabeza)

### G.2 EntityPawn — cablear referencia

- [x] En el `EntityPawn` del mismo prefab: `_healthBar` → el `WorldSpaceHealthBar`
- [x] Para prefabs de **hero**: dejar `_healthBar` en **null**

### G.3 Limpieza de escena

- [x] En `02_Gameplay`, si existe un GO `EnemyPanelRoot` bajo el Canvas del CombatHUD → **eliminarlo**
- [x] Verificar que `CombatHUDView` no tenga Missing References

---

## H · Hero Behavior Slots — Configurar `CH_Warrior.asset` (~20 min)

> El sistema de behavior slots permite configurar las 4 acciones de
> cada clase (Movement, Base Attack, Special Attack, Healing) desde el
> editor con precondiciones y efectos inline. Ya NO se usan los viejos
> `ActionDefinitionSO` hardcodeados.
> Ref: `docs/setup/UI#0012c_PlayerActionButtons.md`.

### H.1 Abrir ClassHeroSO del Guerrero

- [x] Abrir `Assets/Rollgeon/Heroes/CH_Warrior.asset` en Inspector (Odin)
- [x] Expandir el foldout **"Actions"** (`HeroBehaviorSet`)

### H.2 Configurar los 4 slots

Cada slot es un `HeroActionBehavior` con estos campos editables:

#### H.2a Movement

- [x] `ActionName` = `"Movement"`
- [x] `EnergyCost` = **1**
- [x] `BlockOnRepeat` = **false** (puede moverse varias veces por turno)
- [x] `FreeRollCount` = **1** (sin rerolls)
- [x] `AllowsReroll` = **false**
- [x] `AllowsEnergyReroll` = **false**
- [x] `ShowConditions` = [] (siempre visible)
- [x] `Effects` = al menos 1 `EffectData`:
  - Label: `"Move to target"`
  - PreConditions: las que correspondan (ej. `PCEntityInRange`, o vacío para smoke)
  - Effects: el `IEffect` concreto de movimiento (ej. `MoveToSlotEffect`)
  - ⚠️ Para smoke test: podés dejar `Effects` vacío — el behavior se ejecutará sin hacer nada pero no crashea.

#### H.2b Base Attack

- [x] `ActionName` = `"Base Attack"`
- [x] `EnergyCost` = **1**
- [x] `BlockOnRepeat` = **true**
- [x] `FreeRollCount` = **3** (1 roll inicial + 2 rerolls gratis)
- [x] `AllowsReroll` = **true**
- [x] `AllowsEnergyReroll` = **true** (puede gastar energía para rerolls extra)
- [x] `Effects` = al menos 1 `EffectData`:
  - Label: `"Deal damage"`
  - PreConditions: [] (siempre ejecuta)
  - Effects: el `IEffect` de daño (ej. `DealDamageEffect`)
  - El combo matcheo viene por context (`EffectContext.ComboResult`)

#### H.2c Special Attack

- [ ] `ActionName` = `"Special Attack"`
- [ ] `EnergyCost` = **2**
- [ ] `BlockOnRepeat` = **true**
- [ ] `FreeRollCount` = **1** (sin rerolls)
- [ ] `AllowsReroll` = **false**
- [ ] `AllowsEnergyReroll` = **false**
- [ ] `Effects` = EffectData con la lógica del ataque especial

#### H.2d Healing

- [ ] `ActionName` = `"Healing"`
- [ ] `EnergyCost` = **1**
- [ ] `BlockOnRepeat` = **true**
- [ ] `FreeRollCount` = **1**
- [ ] `AllowsReroll` = **false**
- [ ] `AllowsEnergyReroll` = **false**
- [ ] `Effects` = EffectData con efecto de curación (ej. `HealEffect`)

### H.3 Contextual Behaviors (opcional)

- [ ] En `CH_Warrior.asset` → `ContextualBehaviors` (lista)
- [ ] Agregar behaviors condicionales si aplica (ej. `Force Door` con `ShowConditions = [PCRoomType(Locked)]`)
- [ ] Dejar vacío para el smoke test

### H.4 Verificar

- [ ] En Inspector: expandir `Actions` → los 4 slots muestran nombre y config correctos
- [ ] `Actions.IsValid` = **true** (Odin muestra validación)
- [ ] Los botones del `PlayerActionButtonsView` corresponden 1:1:
  - Movement (index 0) → `_movementButton`
  - Base Attack (index 1) → `_attackButton`
  - Special Attack (index 2) → `_specialButton`
  - Healing (index 3) → `_healButton`

---

## I · Smoke test end-to-end

Desde `Assets/Scenes/00_Bootstrap.unity` en Play mode:

- [x] Bootstrap loggea registración limpia + `RunController registrado`
- [x] Auto-load de `01_MainMenu`
- [x] "Jugar" → ClassSelection
- [x] Seleccionar Guerrero → panel derecho poblado (8 combos en ContractDisplay) → Confirm habilita
- [x] Confirm → BuildSelection
- [x] Confirm en BuildSelection → console: `[BuildSelectionScreen] Navigating to gameplay. …` → carga `02_Gameplay`
- [x] `02_Gameplay` loggea `[GameplayBootstrapper] Run started. hero=hero.warrior, runId=…`
- [x] ExplorationHUD aparece con HP/Energy/Gold populados. Primera room muestra **"Entrada"** (Start), sin combate — si ves combate ya, falta cablear `StartRoom` en `Floor1_Layout`.
- [x] Click **Proceed** → entra a primera sala de combate → aparece `CombatHUD` encima del ExplorationHUD
- [x] Navegar entre rooms con `Proceed`
- [x] Player turn: 4 botones de behavior habilitados (Movement, Attack, Special, Heal)
  - [x] Click en behavior → dados ruedan → botones de behavior se deshabilitan, Confirm se habilita
  - [x] Si behavior tiene `AllowsReroll=true`: `RerollCountView` muestra "0/2", botón Extra Roll habilitado
  - [x] Click Reroll → counter actualiza "1/2", `_costLabel` muestra "Free" o "1E"
  - [x] Click Confirm → counter vuelve a "-/-", vuelve a WaitingForAction
  - [x] EndTurn funciona desde su componente separado (`EndTurnButtonView`)
  - [x] EndTurn se deshabilita durante roll, se re-habilita post confirm
- [x] Enemigos muestran barras de HP world-space sobre su cabeza
  - [x] Barra baja al recibir daño, sube al recibir heal
  - [x] Barra se oculta al morir
- [x] Enemy turn se ejecuta → vuelve al player
- [ ] Floor boss cleared → VictoryScreen ; o muerte → DefeatScreen
- [ ] `Return to Menu` → `01_MainMenu` limpio, nueva run arrancable (verifica que `ClearScope(Run)` corrió)
- [x] **Negativo:** abrir `02_Gameplay` directo en Play → console loggea
  `[GameplayBootstrapper] No pending run request` (guard del bootstrapper)

---

## J · EditMode Tests (~5 min)

Correr todos los tests en `Unity → Window → General → Test Runner → EditMode`:

- [ ] `PlayerActionButtonsViewTests` — 11 tests (behaviors + confirm, sin reroll/endturn/legacy)
- [ ] `EndTurnButtonViewTests` — 10 tests (enable/disable/bind/unbind)
- [ ] `RerollCountViewTests` — 7 tests (OnDiceRolled/OnRollResolved, no OnRerollBudgetChanged)
- [ ] `CombatHUDViewTests` — 4 tests (BindAll/UnbindAll smoke)
- [ ] `WorldSpaceHealthBarTests` — tests del health bar world-space
- [ ] Todos los demás tests pasan sin regresiones

---

## Cómo usar este checklist

- Tildá los items con `- [x]` en Obsidian (click en el checkbox).
- Al terminar una sección, cambiá el `status:` del frontmatter a `done`
  para esa fase si querés llevar estado por bloque.
- Cuando termine todo, mover `status: wip` → `done`, commit con
  `docs(obsidian): round 3 setup complete (3 scenes)` y promover el
  item P1#1 del [[Implementation-Roadmap]] eliminándolo.
- Si aparece una ref null inesperada, Unity imprime el nombre del campo
  en el log — buscalo por texto en este archivo.

## Fuentes

- `docs/setup/_PLAYABLE_LOOP_TWO_SCENE_SETUP.md` — **fuente canónica** del
  split en 3 escenas; tablas detalladas por round y troubleshooting.
- `docs/setup/_SETUP_ROUND2_STATUS.md` — doc del round 2 (2-scene).
- `docs/setup/UI#0012c_PlayerActionButtons.md` — setup detallado de PlayerActionButtonsView + EndTurnButtonView.
- `docs/setup/System#0206_WorldSpaceHealthBars.md` — setup detallado de WorldSpaceHealthBar.
- [[Sprint03-Status]] — resumen de estado del sprint.
- [[Implementation-Roadmap]] P1 #1.
- Guías específicas por pantalla en `docs/setup/UI#*.md`.
