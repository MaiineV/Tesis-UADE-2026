---
title: Home
type: index
domain: 99-Index
status: done
tags: [index, home]
---

# Rollgeon — Vault Home

> Knowledge base for the `Rollgeon.*` Unity roguelite (Tesis UADE 2026,
> Sprint 03 Final Project). Atomic notes mirror the current state of the
> codebase, the `TECHNICAL.md` specification, and the setup guides under
> `docs/setup/`.

This vault is a **navigable index over three source-of-truth artifacts**:

| Source | Where it lives | Role |
|---|---|---|
| `TECHNICAL.md` | repo root (~9.1k lines, §0–§27) | canonical spec |
| `Assets/Scripts/Rollgeon/**` | Unity project | implementation |
| `docs/setup/*.md` | 39 setup guides | scene/SO wiring steps |

Notes **reference** those artifacts — they do **not** replace them. When
in doubt, read the source.

---

## Domains (one folder per functional area)

The numbering mirrors the dependency order — lower numbers have fewer
dependencies and are read first by the Bootstrap pipeline. Sections
17–26 were promoted out of `16-Crosscutting/` once their subsystems
shipped real code (Sprint 03 mid-late).

| # | Folder | What lives here |
|---|---|---|
| 00 | [[Foundations-MOC\|00-Foundations]] | ServiceLocator, EventManager, FSM, Bootstrap, ISaveable, RNG |
| 01 | [[Attributes-MOC\|01-Attributes]] | Modifiable stat engine (Health, Energy, Attack, Speed, Shield, multipliers) |
| 02 | [[Combat-MOC\|02-Combat]] | CombatTurnFSM, DamagePipeline, TurnManager, Energy, Weakness, Initiative, Handoff, AI |
| 03 | [[Combos-MOC\|03-Combos]] | ComboCatalogSO, 8 concrete combos (Par…Generala), counters (Balatro-style), contracts |
| 04 | [[Effects-MOC\|04-Effects]] | BaseEffect, EffDamage/EffHeal, target queries |
| 05 | [[Entities-MOC\|05-Entities]] | BaseEntitySO, enemies, props, behaviors, bosses, Auditor |
| 06 | [[Heroes-MOC\|06-Heroes]] | ClassHeroSO, Warrior contract, passives, factories |
| 07 | [[Dungeon-MOC\|07-Dungeon]] | FloorLayoutSO, RoomSO, DungeonManager, BossFloor, FloorShell |
| 08 | [[Dice-MOC\|08-Dice]] | DiceType, DiceBagSO, DiceRoller, reroll budget |
| 09 | [[Phase-MOC\|09-Phase]] | GamePhase, PhaseService, transition matrix, overlay |
| 10 | [[Run-MOC\|10-Run]] | RunController, RunContext |
| 11 | [[Player-MOC\|11-Player]] | PlayerService |
| 12 | [[UI-MOC\|12-UI]] | Screens, HUDs (Exploration + Combat), ScreenManager |
| 13 | [[Content-MOC\|13-Content]] | SO catalogs indexed by Bootstrap |
| 14 | [[Balance-MOC\|14-Balance]] | RulesetSO, EnergyConfig, TurnOrderConfig, WeaknessConfig |
| 15 | [[Meta-MOC\|15-Meta]] | Unlocks, RunRecord, SaveSystem |
| 16 | [[Crosscutting-MOC\|16-Crosscutting]] | Remaining transversal stubs (Input, Pool, Analytics, Scene, Cutscenes, Tutorial, Settings) |
| 17 | [[Grid-MOC\|17-Grid]] | GridManager, NavGraph/NavNode/NavEdge, TileMarker, ITileHighlightService |
| 18 | [[Movement-MOC\|18-Movement]] | IMovementService, MovementService, pathing on the nav graph |
| 19 | [[Economy-MOC\|19-Economy]] | EconomyService, IEconomyService, gold wallet, EnemyGoldDropService |
| 20 | [[Shop-MOC\|20-Shop]] | ShopManagerService, ShopConfigSO, ShopPoolSO, WeightedShopItem, pedestals |
| 21 | [[Audio-MOC\|21-Audio]] | IAudioService, AudioManager, AudioChannel (Music/SFX/UI/Voice), BiomeMusicEntry |
| 22 | 22-Feedback | IFeedbackService, FeedbackManager, FeedbackRequest, FloatingNumberView, PawnRegistry |
| 23 | 23-Camera | ICameraService, CameraConfigSO, CameraInputRouter, WallDirection/WallOccluder |
| 24 | 24-Items | IInventoryService, InventoryService, ItemSO, ItemCatalogSO, PassiveItemHook, InventorySnapshot |
| 25 | 25-Exploration | IExplorationController, ExplorationController, ExplorationBehaviorService |
| 26 | 26-PreConditions | BasePreCondition + concretes (PCComboAvailable, PCFirstRollOfCombat, PCComposite, …) |

---

## Index notes

- [[Sprint03-Status]] — retrospective: Round 2 (MCP-automated setup)
  done vs. Round 3 (manual wiring) pending; known TBDs from
  `TECHNICAL.md`.
- [[Round3-Checklist]] — tickable checklist (~70–90 min) for the manual
  Unity setup that closes out Sprint 03.
- [[Implementation-Roadmap]] — forward-looking plan: P1/P2/P3
  priorities, dependency graph, per-domain outlook.
- [[TECHNICAL-Index]] — map from `TECHNICAL.md` sections §0–§27 to the
  vault folders that materialize them.
- [[Glossary]] — design vocabulary (Contract, Weakness Hit, Action
  Economy, Repetition, Hidden Speed, Combo Counter, …).

---

## Conventions

- **Note titles** use the exact code symbol (`DamagePipeline`, not
  "Damage Pipeline") so wikilinks match identifiers.
- **Cross-references** use `[[PageName]]` Obsidian wikilinks.
- **Status** is a frontmatter field *and* a tag: `#done`, `#wip`,
  `#pending`, `#tbd`.
- **Code paths** point to real files under `Assets/Scripts/Rollgeon/…`.
- **Setup guide refs** point to `docs/setup/<name>.md` (external to vault,
  listed in each note's `External references` block).
- **No copy-paste from `TECHNICAL.md`** — summarize in 2–3 sentences and
  cite the section.

---

## Reading order for newcomers

1. [[Sprint03-Status]] — what is done right now.
2. [[Implementation-Roadmap]] — what is next and why.
3. [[Glossary]] — vocabulary.
4. [[Foundations-MOC]] → [[Attributes-MOC]] → [[Phase-MOC]] → [[Run-MOC]]
   — the skeleton every other system plugs into.
5. [[Combat-MOC]] and [[Combos-MOC]] — the heart of the Sprint 03 FP.
6. [[Dungeon-MOC]] → [[Grid-MOC]] → [[Movement-MOC]] → 25-Exploration
   → [[UI-MOC]] — the shell that wraps combat.
7. [[Economy-MOC]] → [[Shop-MOC]] → 24-Items — the run-economy layer.
8. [[Audio-MOC]] → 22-Feedback → 23-Camera — the polish/feel layer.
9. 26-PreConditions — the predicate vocabulary that gates effects and AI.
10. [[TECHNICAL-Index]] — cross-reference to the full spec.

The dependency order is roughly: 00–04 (foundations + stat engine +
combat + combos) feed 05–08 (entities, heroes, dungeon, dice); 09–11
(phase, run, player) tie the lifecycle together; 12 (UI) sits on top of
all of them. 13–15 (content, balance, meta) are data and persistence.
17–18 (grid + movement) and 25 (exploration) depend on 07-Dungeon.
19–20 (economy + shop) and 24 (items) depend on 13-Content. 21–23
(audio, feedback, camera) and 26 (preconditions) are crosscutting and
plug in nearly everywhere.

---

## Progress

Initial population is complete across waves 0–9 (≈ 270 atomic notes +
22 MOCs + 5 index notes). Subsequent work reconciles this vault with
the code:

- **New system ships** → add its note in the right domain folder, link
  it from the domain's MOC, refresh [[Sprint03-Status]] if the sprint
  balance shifts.
- **Existing system refactored** → update `Code`, `API / Shape`, and
  `Uses` / `Used by` edges.
- **TBD graduates** → flip `status: tbd` → `status: done` and promote
  from [[Crosscutting-Overview]] into its own note when it deserves one.
- **MOC drift** → fix the MOC's diagram in the same PR as the code
  change; don't let diagrams rot.

---

*Last updated: 2026-04-28*
