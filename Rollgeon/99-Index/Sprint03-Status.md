---
title: Sprint03-Status
type: index
domain: 99-Index
status: wip
tags: [index, sprint, status]
---

# Sprint 03 — Final Project Status

> Snapshot of the Sprint 03 state as of **2026-04-20**. Derived from
> `Sprint03_Tareas_AgustinMartinez.md`, `docs/setup/_SETUP_ROUND2_STATUS.md`,
> and `docs/setup/_SPRINT03_VERIFICATION_GUIDE.md`.

## Headline

- **C# code**: ✅ 100 % implemented for Sprint 03 scope. ~311 files across
  15 runtime asmdefs + ~76 tests.
- **Round 2 setup (MCP-automated)**: ✅ done. Scenes + ~120 GameObjects +
  ~23 ScriptableObjects + 4 prefabs created.
- **Round 3 setup (manual)**: ⏳ pending. Asset organization, SO value
  configuration, Inspector reference wiring, and UI layout still require
  the user in the Unity editor (~45–90 min estimated).

---

## Round 2 — DONE (via MCP automation)

**Scenes (live at `Assets/Scenes/`):**
- `Assets/Scenes/00_Bootstrap.unity` (build index 0) with `BootstrapRunner`.
- `Assets/Scenes/01_MainMenu.unity` (build index 1) with ~120 hierarchical GameObjects:
  - `MainMenuScreen`, `ClassSelectionScreen`, `BuildSelectionScreen`.
  - `ExplorationHUDView` (+ 5 sub-views: Health, Energy, Gold, Items, Minimap).
  - `CombatHUDView` (+ 7 sub-views: TurnQueue, Enemy, Combo, DiceZone, Actions, Reroll, FloatingDamage).
  - `FloorTransitionScreen`, `PauseMenu`, `Victory`, `Defeat`.

**Prefabs (created but unorganised in repo root):**
- `ComboRow.prefab`, `DiceSlotView.prefab`, `TurnSlot.prefab`,
  `FloatingDamage.prefab`.

**ScriptableObjects (~23, created in-project but unorganised):**
- 5 `ActionDefinitions` (AttackBasic, AttackSpecial, Heal, ForceDoor, EndTurn).
- 8 `BaseComboSO` concretes (Par, DoblePar, SumaX, Trio, Escalera,
  FullHouse, Poker, Generala).
- 5 `RoomSO` (Combat01–03, Shop, Potion).
- 1 `FloorLayoutSO`, 1 `EnemyPoolSO`, 1 `ServiceBootstrap.asset`, misc.

---

## Round 3 — PENDING (manual, user in Unity editor)

> Tickable version: [[Round3-Checklist]].


**Organization (~15 min):**
- Move 4 prefabs to `Assets/Rollgeon/Prefabs/UI/`.
- Move ~23 SOs into their proper folders (`Actions/`, `Combos/`,
  `Dungeon/`, `Enemies/`, etc.) and rename per convention.

**Configuration (~30 min):**
- Set `EnergyCost` (1–2) and `BlockOnRepeat` on each ActionDefinition.
- Set `BaseDamage` on each Combo (Par 10, DoblePar 18, SumaX 25, Trio 28,
  Escalera 35, FullHouse 40, Poker 60, Generala 100).
- Configure Rooms (RoomId, DisplayName, EnemyPool refs).
- Configure EnemyData (BaseHP, BaseAttack, BaseSpeed, MaxEnergy).

**Reference wiring (~45–60 min):**
- Inspector drag-and-drop of ~29 SO references into scene GameObjects.
- Set catalogs on `ServiceBootstrap`.
- Wire HUD sub-views (e.g. `HealthBarView._slider` → child `Slider`).

**Visual layout:**
- RectTransform positioning, anchors, margins, responsive layout.

---

## Known TBDs (from `TECHNICAL.md`)

Deferred out of Sprint 03 scope:

- **Strike combos** (`§5.6`) — alternate combo variant not yet specified.
- **Damage pipeline extensions** — full modifier chain beyond the Sprint 03
  happy path.
- **Full SaveSystem** — currently stub / in-memory; JSON flush deferred.
- **Most `16-Crosscutting/` systems** — Audio/Movement/Interaction/
  Cutscenes/Pool/Analytics specified in `TECHNICAL.md` but not yet
  implemented in code.

Each TBD lives in its own atomic note tagged `#tbd` when we reach it.

---

## References

- `Sprint03_Tareas_AgustinMartinez.md`
- `docs/setup/_SETUP_ROUND2_STATUS.md`
- `docs/setup/_SPRINT03_VERIFICATION_GUIDE.md`

---

## Update — 2026-04-28

Eight days of progress since the 2026-04-20 snapshot. Several
TBD/post-Sprint-03 systems graduated to working code, and the vault
itself was re-synced to mirror the new code shape.

### Commits since 2026-04-20

- `0a0c856` feat(ui): add Hero Class Editor window with 3-column layout
- `dd9e093` fix: boss run completa, fix de las salas que no llevan a nada, shells vacias
- `a9e8570` feat(shader): increase pixel render resolution for sharper output
- `2e9f697` feat: potion system y shop con economy
- `bd1a083` feat: heal and potion system
- `23e162a` fix: boss ignores combos
- `3495466` feat(model): add temporal boss mesh
- `ad91a1f` Clean up old docs

### Systems that shipped

- **Economy / Wallet** — `EconomyService` + `IEconomyService` (gold as a
  first-class run resource) and `EnemyGoldDropService` for combat drops.
- **Shop** — `ShopManagerService`, `ShopConfigSO`, `ShopPoolSO`,
  `WeightedShopItem`, `ShopItemPedestalInteractable`, `ShopRollResult`,
  `ShopSlot`. Real shop rooms now spawn wares against an economy budget.
- **Heal pipeline + potion system** — `HealPipeline` / `HealContext`
  consumers, potion items wired through `IInventoryService`.
- **Boss combo immunity** — `BossComboImmunityBehavior`: bosses can now
  ignore the combo-effect side of an attack entirely (fixes the bug
  where bosses treated combos as normal hits).
- **Grid + Movement subsystems** (`17-Grid`, `18-Movement`) — promoted
  out of `16-Crosscutting`. `GridManager` owns the live grid, `NavGraph`
  / `NavNode` / `NavEdge` describe pathing topology, `NavGraphBaker`
  bakes layouts at room load, `ITileHighlightService` + `TileMarker`
  drive selection feedback.
- **Audio / Feedback / Camera** as dedicated sections (`21-Audio`,
  `22-Feedback`, `23-Camera`) — `IAudioService` / `AudioManager` with
  `AudioChannel` (Music/SFX/UI/Voice), full `FeedbackManager` +
  `FeedbackDBSO` + `FeedbackRequest` pipeline replacing the
  Sprint-03-only `FloatingDamageSpawner` stub, `ICameraService` with
  wall-aware framing (`WallOccluder`, `WallDirection`).
- **Items / Inventory** (`24-Items`) — `IInventoryService` +
  `InventoryService` with `InventorySnapshot` for save/load,
  `ItemCatalogSO`, `PassiveItemHook`, `PersistentModifierDef`.
- **Exploration as its own section** (`25-Exploration`) — promoted out
  of `07-Dungeon`. `IExplorationController` + `ExplorationController`
  own room transitions, while `IExplorationBehaviorService` runs
  exploration-phase entity behaviors.
- **PreConditions promoted** (`26-PreConditions`) — `BasePreCondition`
  + concretes (`PCComboAvailable`, `PCFirstRollOfCombat`,
  `PCComposite`, `PCEntityInRange`, `PCHasInventoryItem`,
  `PCHasModifier`, `PCAdjacentToDoor`, …) split out of `04-Effects` so
  the predicate vocabulary is browsable on its own.

### Vault changes

- **10 new vault sections** added (`17-Grid`, `18-Movement`,
  `19-Economy`, `20-Shop`, `21-Audio`, `22-Feedback`, `23-Camera`,
  `24-Items`, `25-Exploration`, `26-PreConditions`).
- **~270 atomic pages** now total (up from ~170 in the 2026-04-20
  snapshot).
- **Vault re-synced with code** — orphan pages removed
  (`EnemyPanelView` deleted; replaced by `WorldSpaceHealthBar` on each
  pawn), `BaseTargetQuery` references swept in favour of
  `SelectionSettings` + `ISelectionController`.
- New MOCs created for `Grid`, `Movement`, `Economy`, `Shop`, `Audio`.
  MOCs for `Feedback`, `Camera`, `Items`, `Exploration`,
  `PreConditions` still pending — pages are reachable via the folder
  for now.

### Still pending

- Round 3 manual Unity setup (organise prefabs/SOs, configure SO values,
  wire Inspector references) — see [[Round3-Checklist]].
- Strike combos (§5.6) still TBD.
- Save / persistence pipeline (§15) still stubbed; `InventorySnapshot`
  is ready to plug in once `SaveSystem` lands.
- Quests / Tutorial / Settings / Object pooling / Analytics still
  TBD (§21–§25).

