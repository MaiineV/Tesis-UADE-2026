---
title: Implementation-Roadmap
type: index
domain: 99-Index
status: done
tags: [index, roadmap, plan, implementation]
---

# Implementation Roadmap

> Forward-looking plan: what to build next, in what order, and why.
> Complements [[Sprint03-Status]] (retrospective on what's done) with a
> prioritised list of the pending and TBD work surfaced across the vault.

Snapshot date: **2026-04-20**. Revise whenever a P1 item graduates.

---

## Current state (one paragraph)

Sprint 03 ships the Warrior end-to-end (bootstrap → main menu → class
select → build select → exploration → combat → boss → victory/defeat).
Code is in place and tested. The Unity scene/SO wiring that closes the
loop (Round 3 manual setup) is the single biggest unblock. Beyond that,
several subsystems are **spec-complete in `TECHNICAL.md`** but not yet
implemented — they cluster naturally into three horizons.

---

## P1 — Unblock Sprint 03 & fill foundation gaps

Do these first. They either close out Sprint 03 or remove placeholders
inside systems already used by gameplay.

1. **Round 3 manual Unity setup** — organise prefabs/SOs into their
   folders, configure SO values, wire Inspector references, lay out
   RectTransforms. Owner: user (CLAUDE.md forbids Unity MCP). Working
   checklist: [[Round3-Checklist]] (tickable, ~70–90 min). Reference:
   `docs/setup/_SETUP_ROUND2_STATUS.md` + [[Sprint03-Status]].
   Note: scenes live at `Assets/Scenes/` (the empty
   `Assets/Rollgeon/Scenes/` folder from earlier drafts is inert — leave
   as-is or delete in Unity so its `.meta` sibling goes with it).
2. **Finish [[DamagePipeline]]** — land the `OutgoingDamageMultiplier`,
   `IncomingDamageMultiplier`, and `Shield` stats; wire stages 1, 3 and
   4 of `DamagePipeline.Resolve` (today placeholders). No public API
   change — drop-in extension.
3. **Strike combos (§5.6)** — alternate combo variant still TBD.
   Extends [[BaseComboSO]] / [[ContractSheet]]; probably a new
   `StrikeComboSO` concrete + contract activation rule.
4. **Balance#0101 — complete [[RulesetSO]]** — add the remaining
   sub-configs (`RollConfig`, `ScalingConfig`, `CritConfig`,
   `LootConfig`, `ShopConfig`, `CrapsConfig`) and the
   `ForbiddenActionIds` hook that [[TurnManager]] already stubs.
5. **Hero Template task** — elevate the stub fields in
   [[ClassHeroSO]] (`BaseMaxHp`, `BaseSpeed`, `Portrait`,
   `StartingDiceBagRef`, `PassiveRef`) so multi-class is viable.
   Depends on DiceBagSO + PassiveAbilitySO (see P2).

**Exit criteria for P1:** the game is fully playable end-to-end with
complete balance hooks and no foundation-level placeholders in the
combat path.

---

## P2 — Content depth & persistence

After P1, these expand the run's breadth: progression across sessions,
items, rewards, status effects, feedback, dice variety.

6. **[[SaveSystem]] (§15)** — implement the manager around the
   [[ISaveable]] contract. Unblocks run resume and any meta-progression.
   First consumer: [[RunComboCounterState]].
7. **[[UnlockSystem]] + [[RunRecord]] (§14)** — depends on SaveSystem.
   Drives the "greyed-out classes in the main menu" story.
8. **Item system (§18)** — `ItemSO`, `ItemCatalogSO`,
   `IInventoryService`, `PassiveItemHook`. Gates [[ActiveItemsView]].
9. **Rewards / loot (§19)** — `RewardEntrySO`, `RewardCatalogSO`,
   weighted drops. Depends on item system.
10. **Status effects (§20)** — `StatusEffectSO`, `IStatusEffectService`;
    leverages [[Modifier]] internally. Gates status icons in
    [[CombatHUDView]].
11. **Full feedback pipeline (§10)** — `FeedbackManager`, `FeedbackDBSO`,
    sequenced VFX/SFX/animation. Today only [[FloatingDamageSpawner]]
    exists.
12. **Audio service (§17.A)** — `IAudioService`, music/SFX layer,
    mixer groups. No audio in Sprint 03.
13. **Dice bag (§6)** — `DiceType`, `DiceBagSO`, `DiceRoller`,
    `DiceEnchantmentSO`. Today rerolls are tracked but dice variety is
    hard-coded. Unlocks hero-specific starting bags (feeds the Hero
    Template task).

**Exit criteria for P2:** the run has persistent progression,
procedural loot, meaningful dice choices, and polish-ready combat
feedback.

---

## P3 — Full game shell (backlog)

Nice-to-have systems from `TECHNICAL.md §17–§26` that stretch out past
Sprint 05. No strict ordering; pick based on player-value vs. scope.

| System | §   | Blocks |
|---|---:|---|
| Quests (§21)              | 21 | side-content loops, narrative beats |
| Tutorial (§22)            | 22 | first-run onboarding |
| Settings (§23) + accessibility | 23 | polish bar |
| Object pooling (§24)      | 24 | perf, VFX burst handling |
| Analytics (§25)           | 25 | telemetry-driven balance |
| Cutscenes (§17.CS)        | 17 | narrative beats, boss intros |
| Shop (§17.SHP)            | 17 | run economy exits |
| Camera service (§17.C)    | 17 | cinemachine wiring |
| Movement service (§17.M)  | 17 | dungeon graph navigation polish |
| Interaction service (§17.INT) | 17 | rich NPC / prop interactions |
| Scene service (§17.SCE)   | 17 | async scene orchestration |
| Input service (§17.IN)    | 17 | rebindable input abstraction |
| Content tooling (§26)     | 26 | room designer, combo calc, bulk editors |

---

## Dependency graph (at a glance)

```
 Round 3 setup  ─────────────────────────────┐
 DamagePipeline mult + Shield                │
 Strike combos (§5.6)                        │ P1 complete →
 RulesetSO sub-configs (Balance#0101)        │
 Hero Template (needs DiceBagSO from P2)     │
                                             │
 SaveSystem ───┬─ UnlockSystem ── Main menu gating
               │
               │                              P2 complete →
 Item system ──┴─ Rewards / loot ── Shop (P3)
 Status effects ── UI status icons
 Feedback pipeline ── polish pass
 Audio service ── polish pass
 DiceBagSO ───── Hero Template (closes P1 item #5)
                                             │
                                             │ P3 = backlog
 Quests · Tutorial · Settings · Pooling · Analytics ·
 Cutscenes · Shop · Camera · Movement · Interaction ·
 Scene · Input · Content tooling
```

---

## Per-domain outlook

What each vault folder has pending. Lets you pick a domain and see its
roadmap in one glance.

| Domain | Pending / TBD items |
|---|---|
| [[Foundations-MOC\|00-Foundations]] | [[ISaveable]] → real [[SaveSystem]]. Deterministic RNG service (§17.RNG). |
| [[Attributes-MOC\|01-Attributes]] | `OutgoingDamageMultiplier`, `IncomingDamageMultiplier`, `Shield` stats (P1 #2). `EnergyMaxBonus` for item buffs. |
| [[Combat-MOC\|02-Combat]] | [[DamagePipeline]] stages 1/3/4 (P1 #2). [[HealPipeline]] equivalent multiplier stages. Boss AI richness (post P1). |
| [[Combos-MOC\|03-Combos]] | Strike combos (§5.6, P1 #3). `AttackResolver` to stitch combo match → damage pipeline. |
| [[Effects-MOC\|04-Effects]] | `EffAddModifier` / `EffRemoveModifier` concretes. Capability-driven inspector UI for selection. |
| [[Entities-MOC\|05-Entities]] | `PropEntitySO`, `NpcDataSO`, `DialogueGraphSO`. Full behavior lifecycle dispatcher. |
| [[Heroes-MOC\|06-Heroes]] | Hero Template task — elevate [[ClassHeroSO]] stubs (P1 #5). Additional classes beyond Warrior. |
| [[Dungeon-MOC\|07-Dungeon]] | `EntityCatalogSO` / `RoomCatalogSO` unification. Branching floor layouts. |
| [[Dice-MOC\|08-Dice]] | `DiceBagSO`, `DiceType`, `DiceEnchantmentSO`, `DiceRoller` service (P2 #13). |
| [[Phase-MOC\|09-Phase]] | —  (foundation complete). |
| [[Run-MOC\|10-Run]] | Save triggers on `OnRoomChanged` / `OnCombatEnd` after [[SaveSystem]] lands. |
| [[Player-MOC\|11-Player]] | — (foundation complete). |
| [[UI-MOC\|12-UI]] | Polish: [[GoldCounterView]] depends on rewards (P2 #9); [[ActiveItemsView]] depends on items (P2 #8). Settings screen (§23). |
| [[Content-MOC\|13-Content]] | `EntityCatalogSO`, `RoomCatalogSO`, `RewardCatalogSO`, `ItemCatalogSO`, `StatusCatalogSO`, `QuestCatalogSO`, `FeedbackDBSO`, `UnlockCatalogSO`, `DiceCatalogSO`, `RulesetCatalogSO` (see [[Content-Catalogs]]). |
| [[Balance-MOC\|14-Balance]] | `RollConfig`, `ScalingConfig`, `CritConfig`, `LootConfig`, `ShopConfig`, `CrapsConfig` sub-configs (P1 #4). |
| [[Meta-MOC\|15-Meta]] | [[SaveSystem]], [[UnlockSystem]], [[RunRecord]] (P2 #6-7). |
| [[Crosscutting-MOC\|16-Crosscutting]] | 13 TBD services — see [[Crosscutting-Overview]] for the matrix. |

---

## How to use this note

- **Starting a new work session:** scan P1 first. If P1 is empty,
  pick from P2 based on blocking dependencies.
- **Reviewing scope:** compare a proposal against the table above. If
  it sits in P3 while P1 is incomplete, question the ordering.
- **Updating:** when an item graduates, delete it from the list, flip
  its note's `status: tbd` → `done`, and fix any dependency edge that
  used to point to it.

---

## Status — 2026-04-28

Eight days of focused work converted a big chunk of P1/P2/P3 into
shipped code. Kept the original 2026-04-20 plan above for history;
this section is the live overlay on top.

### Completed since 2026-04-20

- **Heal pipeline + potion system** (was P2-adjacent) — `HealPipeline`
  + `HealContext` wired, potion items consumable via inventory.
- **Shop & Economy systems** (was P3 §17.SHP / new) — `EconomyService`
  + `IEconomyService` own gold, `EnemyGoldDropService` drops it,
  `ShopManagerService` + `ShopConfigSO` + `ShopPoolSO` +
  `WeightedShopItem` build the shop room.
- **Item / inventory system** (was P2 #8) — `IInventoryService` +
  `InventoryService` shipped, `ItemCatalogSO`, `PassiveItemHook`,
  `PersistentModifierDef`. `InventorySnapshot` is ready for the future
  `SaveSystem`. Active items can now be granted to the run.
- **Boss combo immunity** (was an open bug in §5.5/§7) —
  `BossComboImmunityBehavior` makes bosses ignore the combo-effect
  side of an attack. Closes the "boss treats Generala like a normal
  hit" issue.
- **Grid / Movement subsystems** (was P3 §17.M / §17.G) — `17-Grid`
  and `18-Movement` are now their own folders. `GridManager`,
  `NavGraph`/`NavNode`/`NavEdge`, `NavGraphBaker`,
  `ITileHighlightService`, `TileMarker`, `IMovementService`,
  `MovementService` all shipped.
- **Audio / Feedback / Camera as dedicated sections** (was P2 #11–12 /
  P3 §17.C) — `IAudioService` + `AudioManager` with `AudioChannel`,
  full `FeedbackManager` + `FeedbackDBSO` + `FeedbackRequest`
  pipeline, `ICameraService` with `WallOccluder` / `WallDirection` for
  wall-aware framing.
- **Exploration promoted** (was nested in `07-Dungeon`) —
  `IExplorationController`, `ExplorationController`,
  `IExplorationBehaviorService`, `ExplorationBehaviorService`.
- **PreConditions promoted** (was nested in `04-Effects`) —
  `BasePreCondition` + concretes split into `26-PreConditions/` so the
  predicate vocabulary is browsable on its own.
- **Hero Class Editor window** (3-column layout) — Odin-based editor
  tooling for hero authoring.
- **Boss run loop fixes** — empty shells / dead-end rooms regression
  closed; bosses now complete their floor pass cleanly.
- **Pixel render shader sharper** — render target resolution bumped
  for a less-blurry presentation.

### Remaining P1

1. **Round 3 manual Unity setup** — still required to close out
   Sprint 03 in the editor (organise prefabs/SOs, set SO values, wire
   Inspector references, lay out RectTransforms). Owner: user. See
   [[Round3-Checklist]].
2. **Finish [[DamagePipeline]]** — `OutgoingDamageMultiplier`,
   `IncomingDamageMultiplier`, `Shield` stats; pipeline stages 1, 3, 4.
3. **Strike combos (§5.6)** — alternate combo variant still TBD.
4. **Balance#0101 — complete [[RulesetSO]]** — remaining sub-configs
   (`RollConfig`, `ScalingConfig`, `CritConfig`, `LootConfig`,
   `CrapsConfig`). `ShopConfig` is now covered by [[ShopConfigSO]] in
   `20-Shop` — keep the `RulesetSO`-level shop hook in mind when
   integrating.
5. **Hero Template task** — elevate stubs in [[ClassHeroSO]]; depends
   on DiceBagSO (still in P2).

### Remaining P2

- **[[SaveSystem]] (§15)** — still pending. `InventorySnapshot` is
  already shaped for it.
- **[[UnlockSystem]] + [[RunRecord]] (§14)** — depends on SaveSystem.
- **Rewards / loot (§19)** — still pending; depends on item system
  (now landed) so this item is unblocked.
- **Status effects (§20)** — still pending.
- **Dice bag (§6)** — `DiceBagSO`, `DiceType`, `DiceEnchantmentSO`,
  `DiceRoller`. Still pending; gates the Hero Template task.

### Remaining P3

P3 is now smaller. Removed from the previous list because they
shipped: `Audio service (§17.A)`, `Shop (§17.SHP)`, `Camera service
(§17.C)`, `Movement service (§17.M)`, full feedback pipeline (§10).

| System | §   | Blocks |
|---|---:|---|
| Quests (§21)              | 21 | side-content loops, narrative beats |
| Tutorial (§22)            | 22 | first-run onboarding |
| Settings (§23) + accessibility | 23 | polish bar |
| Object pooling (§24)      | 24 | perf, VFX burst handling |
| Analytics (§25)           | 25 | telemetry-driven balance |
| Cutscenes (§17.CS)        | 17 | narrative beats, boss intros |
| Interaction service (§17.INT) | 17 | rich NPC / prop interactions |
| Scene service (§17.SCE)   | 17 | async scene orchestration |
| Input service (§17.IN)    | 17 | rebindable input abstraction |
| Content tooling (§26)     | 26 | room designer, combo calc, bulk editors |

---

*Last updated: 2026-04-28*

