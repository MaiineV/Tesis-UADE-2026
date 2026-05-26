---
title: Glossary
type: index
domain: 99-Index
status: done
tags: [index, glossary, vocabulary]
---

# Glossary

> Design and engineering vocabulary used across the Rollgeon codebase
> and `TECHNICAL.md`. Each term points to the atomic note that owns the
> concept.

## Design vocabulary (gameplay-facing)

- **Action economy** — rules limiting action repetitions per turn. →
  [[TurnManager]].
- **Active item** — equipped consumable / triggered item held in an
  inventory slot. → [[ActiveItemSlotView]] / [[InventorySlot]].
- **Blocked combo** — a combo currently unavailable due to a boss
  passive or status. → [[ComboBlockService]].
- **Boss combo immunity** — passive that causes a boss to ignore combo
  effects entirely. → [[BossComboImmunityBehavior]].
- **Combo** — scored pattern of dice (Par, Escalera, Full House, …). →
  [[BaseComboSO]].
- **Combo block** — runtime contract that lets entities veto specific
  combos. → [[IComboBlockService]].
- **Combo counter** — Balatro-style chip-and-multiplier runtime state
  per combo. → [[ComboCountersService]].
- **Combo id** — canonical string `combo.<snake_case>`. →
  [[ComboId]].
- **Contract** — class-specific Generala sheet; picks which combos the
  hero can activate during a run. → [[ContractSheet]].
- **Crossed combo** — a combo the contract has "crossed out" for the
  rest of the run. Still visible, but skipped by `MatchBest`. →
  [[ContractSheet]].
- **Door slot** — typed reference to a specific door slot on a room
  layout. → [[DoorSlotRef]] / [[RoomLayout]].
- **Economy / Wallet** — gold balance and transactions per run. →
  [[EconomyService]], [[IEconomyService]].
- **Enemy gold drop** — service that resolves how much gold an enemy
  drops on death. → [[EnemyGoldDropService]].
- **Floating number** — HUD pop-up at the target's position
  (damage/heal/gold). → [[FloatingNumberType]],
  [[FloatingDamageSpawner]].
- **Floor shell** — light-weight floor descriptor served by the
  dungeon service. → [[FloorShell]] from [[IDungeonService]].
- **Generala** — the Argentine 5-dice poker game the contract system
  is modelled on. → [[Combos-MOC]].
- **Gold** — soft currency tracked per run by the economy service. →
  [[EconomyService]].
- **Hidden Speed** — turn-order stat invisible to the player. →
  [[Speed]], [[TurnOrderConfig]].
- **Inventory snapshot** — serialisable inventory state for save/load.
  → [[InventorySnapshot]].
- **Pool offering row** — one row of a shop offering, used by the shop
  UI. → [[PoolOfferingRow]].
- **Repetition** — flag on an action preventing it from firing twice
  the same turn. → [[ActionDefinitionSO]].
- **Reroll budget** — dice rerolls remaining this action, funded by
  Energy. → [[RerollBudget]], [[RerollBudgetService]].
- **Room instance** — runtime instance of a room, holds its
  per-spawn `RoomObjectStates`. → [[RoomInstance]].
- **Room state** — Uncleared / Cleared / Locked enum on a room
  instance. → [[RoomState]].
- **Shop config / pool** — data driving what shops sell and at what
  weights. → [[ShopConfigSO]], [[ShopPoolSO]],
  [[WeightedShopItem]].
- **Strike combo** — alternate combo variant (§5.6, **TBD**). →
  [[Sprint03-Status]].
- **Weakness / Weakness hit** — enemy-specific damage amplifier. →
  [[WeaknessChecker]].

## Engineering vocabulary (code-facing)

- **AI tree node** — node in an entity behavior tree (sequence,
  selector, …). → [[AINode_Sequence]] family; see [[AIContext]].
- **Audio channel** — Music / SFX / UI / Voice routing tag. →
  [[AudioChannel]].
- **Behavior** — polymorphic AI decision rule on an entity. →
  [[BaseBehavior]], [[BehaviorTrigger]], [[GamePhaseMask]].
- **Behavior stored value** — runtime value bag entry on a behavior,
  keyed by `BehaviorValueKey`. → [[BaseBehaviorStoredValue]] /
  [[BehaviorValueKey]].
- **Bootstrap** — `00_Bootstrap` startup pipeline. → [[Bootstrap]].
- **Catalog** — SO that groups other SOs for id lookup. →
  [[BaseCatalogSO]], [[Content-Catalogs]].
- **Damage context** — payload travelling through the damage pipeline.
  → [[DamageContext]] through [[DamagePipeline]].
- **Effect chain** — ordered chain of effects with phase-tagged steps.
  → [[EffChain]], [[ChainPhase]].
- **EventManager** (untyped, `object[]`) → [[EventManager]].
- **Feedback request** — payload submitted to the feedback service. →
  [[FeedbackRequest]] via [[IFeedbackService]].
- **Handoff** — exploration → combat transition. →
  [[CombatHandoffService]], [[CombatReturnService]].
- **Heal context** — payload travelling through the heal pipeline. →
  [[HealContext]] through [[HealPipeline]].
- **Initiative** — speed + speed-die per round. →
  [[DefaultInitiativeProvider]], [[TurnOrderService]].
- **ISaveable** — contract for run-scoped state that will be
  save-rehydratable. → [[ISaveable]].
- **Nav graph** — exploration / movement graph of nodes and edges. →
  [[NavGraph]] with [[NavNode]] and [[NavEdge]].
- **Overlay** — transient phase modifier. → [[PhaseOverlay]].
- **Passive item hook** — ScriptableObject hook that activates
  [[BaseEffect]] triggers from a passive item. → [[PassiveItemHook]].
- **Pawn registry** — runtime registry mapping entities to scene
  pawns. → [[IPawnRegistry]], used by [[IFeedbackService]].
- **PC condition** — predicate attached to an [[EffectData]] group.
  → [[BasePreCondition]] and `PC*` concretes (e.g.
  [[PCComboAvailable]], [[PCFirstRollOfCombat]]).
- **Persistent modifier** — modifier definition granted by an item or
  passive that lives for the run. → [[PersistentModifierDef]].
- **Phase** — global game phase. → [[GamePhase]],
  [[PhaseTransitionMatrixSO]].
- **Pipeline** — ordered step chain over a context. →
  [[DamagePipeline]], [[HealPipeline]].
- **ScriptableObject (SO)** — Unity data-only class; suffix `SO`.
- **Selection result** — typed result of a target selection
  controller. → [[TargetSelectionResult]] from
  [[ISelectionController]]. (Selection used to be modelled with
  `BaseTargetQuery`; the current shape is data-only via
  [[SelectionSettings]] and a service-side controller.)
- **ServiceLocator / Global vs Run scope** → [[ServiceLocator]],
  [[ServiceScope]].
- **Single-channel rule** — an event ships through exactly one bus. →
  [[EventManager]].
- **Tile highlight** — selection / hover marker rendered on a grid
  tile. → [[ITileHighlightService]], [[TileMarker]].
- **TypedEvent** (typed, struct payload) → [[TypedEvent]].
- **Wall direction / occluder** — camera-aware wall orientation and
  the runtime occluder that fades a wall. → [[WallDirection]],
  [[WallOccluder]].

## Status tags

| Tag | Meaning |
|---|---|
| `#done` | Implemented and under test in Sprint 03. |
| `#wip`  | Partially implemented; work in progress. |
| `#pending` | Scoped but not yet implemented (concrete plan). |
| `#tbd`  | Specified in `TECHNICAL.md`, no concrete implementation yet. |

---

*Last updated: 2026-04-28*
