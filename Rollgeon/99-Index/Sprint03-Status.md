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
