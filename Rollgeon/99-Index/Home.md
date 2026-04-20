---
title: Home
type: index
domain: 99-Index
status: wip
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
dependencies and are read first by the Bootstrap pipeline.

| # | Folder | What lives here |
|---|---|---|
| 00 | [[Foundations-MOC\|00-Foundations]] | ServiceLocator, EventManager, FSM, Bootstrap, ISaveable, RNG |
| 01 | [[Attributes-MOC\|01-Attributes]] | Modifiable stat engine (Health, Energy, Attack, Speed, Shield, multipliers) |
| 02 | [[Combat-MOC\|02-Combat]] | CombatTurnFSM, DamagePipeline, TurnManager, Energy, Weakness, Initiative, Handoff, AI |
| 03 | [[Combos-MOC\|03-Combos]] | ComboCatalogSO, 8 concrete combos (Par…Generala), counters (Balatro-style), contracts |
| 04 | [[Effects-MOC\|04-Effects]] | BaseEffect, EffDamage/EffHeal, target queries, pre-conditions |
| 05 | [[Entities-MOC\|05-Entities]] | BaseEntitySO, enemies, props, behaviors, bosses, Auditor |
| 06 | [[Heroes-MOC\|06-Heroes]] | ClassHeroSO, Warrior contract, passives, factories |
| 07 | [[Dungeon-MOC\|07-Dungeon]] | FloorLayoutSO, RoomSO, DungeonManager, ExplorationController, BossFloor |
| 08 | [[Dice-MOC\|08-Dice]] | DiceType, DiceBagSO, DiceRoller, reroll budget |
| 09 | [[Phase-MOC\|09-Phase]] | GamePhase, PhaseService, transition matrix, overlay |
| 10 | [[Run-MOC\|10-Run]] | RunController, RunContext |
| 11 | [[Player-MOC\|11-Player]] | PlayerService |
| 12 | [[UI-MOC\|12-UI]] | Screens, HUDs (Exploration + Combat), ScreenManager |
| 13 | [[Content-MOC\|13-Content]] | 13 SO catalogs indexed by Bootstrap |
| 14 | [[Balance-MOC\|14-Balance]] | RulesetSO, EnergyConfig, TurnOrderConfig, WeaknessConfig |
| 15 | [[Meta-MOC\|15-Meta]] | Unlocks, RunRecord, SaveSystem |
| 16 | [[Crosscutting-MOC\|16-Crosscutting]] | Audio, Movement, Camera, Input, Scene, Pool, Analytics (most are TBD) |

---

## Index notes

- [[Sprint03-Status]] — Round 2 (MCP-automated setup) done vs. Round 3
  (manual wiring) pending; known TBDs carried over from `TECHNICAL.md`.
- [[TECHNICAL-Index]] — map from `TECHNICAL.md` sections §0–§27 to the
  vault folders that materialize them.
- [[Glossary]] — design vocabulary (Contract, Weakness Hit, Action
  Economy, Repetition, Hidden Speed, Action Economy, Combo Counter, …).

---

## Conventions

- **Note titles** use the exact code symbol (`DamagePipeline`, not
  "Damage Pipeline") so wikilinks match identifiers.
- **Cross-references** use `[[Name]]` Obsidian wikilinks.
- **Status** is a frontmatter field *and* a tag: `#done`, `#wip`,
  `#pending`, `#tbd`.
- **Code paths** point to real files under `Assets/Scripts/Rollgeon/…`.
- **Setup guide refs** point to `docs/setup/<name>.md` (external to vault,
  listed in each note's `External references` block).
- **No copy-paste from `TECHNICAL.md`** — summarize in 2–3 sentences and
  cite the section.

---

## Reading order for newcomers

1. [[Sprint03-Status]] — what is done, what is pending.
2. [[Glossary]] — vocabulary.
3. [[Foundations-MOC]] → [[Attributes-MOC]] → [[Phase-MOC]] → [[Run-MOC]]
   — the skeleton every other system plugs into.
4. [[Combat-MOC]] and [[Combos-MOC]] — the heart of the Sprint 03 FP.
5. [[Dungeon-MOC]] → [[UI-MOC]] — the shell that wraps combat.
6. [[TECHNICAL-Index]] — cross-reference to the full spec.

---

## Progress

Population of this vault proceeds in waves (see the plan in
`C:\Users\agust\.claude\plans\tenemos-que-revisar-el-groovy-bee.md`).
Current wave: **0 — Scaffold**. Most MOCs and domain notes are still
unresolved — this is expected and will be filled in Waves 1–9.
