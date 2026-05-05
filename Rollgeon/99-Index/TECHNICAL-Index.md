---
title: TECHNICAL-Index
type: index
domain: 99-Index
status: done
tags: [index, technical, crossref]
---

# TECHNICAL.md Section Index

> Map from the 27 sections of `TECHNICAL.md` (repo root, ~9.1k lines) to
> the folders and atomic notes in this vault. Use this to jump from a
> spec section to the notes that materialize it.

## Sections §0 – §27

| § | Topic | Vault entry points |
|---|---|---|
| §0  | Conventions & stack | [[Home]], `CLAUDE.md` |
| §1.1 | ServiceLocator | [[ServiceLocator]], [[ServiceScope]] |
| §1.1.1 | Bootstrap | [[Bootstrap]], [[ServiceBootstrapSO]], [[IPreloadableService]] |
| §1.1.2 | BootstrapRunner | [[BootstrapRunner]] |
| §1.1.3 | Run lifecycle | [[RunBootstrapper]], [[RunController]], [[RunContext]] |
| §1.2 | EventManager | [[EventManager]], [[EventName]] |
| §1.2.1 | TypedEvent | [[TypedEvent]] |
| §1.3 | FSM framework | [[StateMachine]], [[BaseState]], [[IState]] |
| §2  | Attributes system | [[IAttribute]], [[IModifiable]], [[BaseAttribute]], [[ModifiableAttributes]], [[AttributesManager]] |
| §3  | Modifiers | [[Modifier]], [[ModifierDirection]], [[ModifierLifetime]], [[ModifierOperation]], [[OperationResolver]] |
| §4  | Classes & hero sheets | [[ClassHeroSO]], [[ContractSheet]] |
| §5  | Generala contract + combos | [[Combos-MOC]], [[BaseComboSO]], the 8 concretes |
| §5.3 | ContractSheet | [[ContractSheet]] |
| §5.5 | Combo counters / block | [[ComboCountersService]], [[ComboCountersConfig]], [[ComboBlockService]], [[IComboBlockService]], [[BossComboImmunityBehavior]] |
| §5.6 | Strike combos (TBD) | **deferred** — see [[Sprint03-Status]] TBDs |
| §6  | Dice & bag | [[Dice-MOC]] — partial ([[RerollBudgetService]]); DiceBagSO / Types are TBD |
| §7  | Entities & behaviors | [[BaseEntitySO]], [[EnemyDataSO]], [[BaseBehavior]] + concretes; AI trees via [[AINode_Sequence]] family + [[AIContext]] |
| §8  | Effects & preconditions | [[Effects-MOC]] — [[BaseEffect]], [[EffectData]]; pre-conditions live in `26-PreConditions/` (see [[BasePreCondition]]) |
| §9  | Behavior values | [[BaseBehavior]], [[BaseBehaviorStoredValue]], [[BehaviorValueKey]] |
| §10 | Feedback system | vault: `22-Feedback/` — [[FeedbackManager]], [[FeedbackDBSO]], [[FeedbackEntry]], [[FeedbackRequest]], [[IFeedbackService]], [[IPawnRegistry]]; HUD spawner via [[FloatingDamageSpawner]] |
| §11 | Selection / targeting | [[SelectionSettings]], [[ISelectionController]], [[TargetSelectionResult]], [[TargetQueries]] |
| §12 | Combat | [[Combat-MOC]] |
| §12.2 | Damage pipeline | [[DamagePipeline]], [[DamageContext]], [[AttackKind]] |
| §12.3 | Heal pipeline | [[HealPipeline]], [[HealContext]] |
| §12.6 | Action economy | [[TurnManager]], [[ActionDefinitionSO]], [[EnergyService]] |
| §12.7 | Turn order / initiative | [[TurnOrderService]], [[DefaultInitiativeProvider]], [[TurnOrderConfig]], [[Speed]] |
| §13 | Dungeon / rooms | [[Dungeon-MOC]] — [[FloorShell]], [[RoomInstance]], [[RoomState]], [[DoorSlotRef]] |
| §14 | Meta-progression | [[Meta-MOC]] — TBD ([[UnlockSystem]], [[RunRecord]]) |
| §14.7 | Ruleset | [[RulesetSO]], [[EnergyConfig]], [[WeaknessConfig]], [[TurnOrderConfig]] |
| §15 | Save / persistence | [[ISaveable]] + [[SaveSystem]] (TBD); inventory side via [[InventorySnapshot]] / [[InventorySlotSnapshot]] |
| §16 | Unity packages | repo `Packages/` — not in vault |
| §17 | Transversal systems | [[Crosscutting-Overview]] — most subsystems now graduated into their own sections (17–26 in this vault) |
| §17.A | Audio service | [[Audio-MOC]] — [[IAudioService]], [[AudioManager]], [[AudioChannel]], [[AudioSettingsSO]], [[BiomeMusicEntry]] |
| §17.B | Movement & pathfinding | [[Movement-MOC]] — [[IMovementService]], [[MovementService]]; nav graph in [[Grid-MOC]] |
| §17.E | Camera | vault: `23-Camera/` — [[ICameraService]], [[CameraService]], [[CameraConfigSO]], [[CameraInputRouter]], [[WallDirection]], [[WallOccluder]] |
| §17.F | Shop | [[Shop-MOC]] — [[IShopManagerService]], [[ShopManagerService]], [[ShopConfigSO]], [[ShopPoolSO]], [[WeightedShopItem]], [[ShopItemPedestalInteractable]] |
| §17.G | Player service | [[Player-MOC]], [[PlayerService]] |
| §17.H | PawnRegistryService | [[PawnRegistry]], [[IPawnRegistry]] |
| §17.I | GridManager | [[Grid-MOC]] — [[GridManager]], [[IGridManager]], [[GridCoord]], [[NavGraph]], [[NavNode]], [[NavEdge]], [[ITileHighlightService]], [[TileMarker]] |
| §17.M | Heal pipeline | [[HealPipeline]], [[HealContext]] |
| §17.PHA | Phase service | [[Phase-MOC]], [[PhaseService]], [[PhaseTransitionMatrixSO]] |
| §17.UI | Screen manager | [[ScreenManager]], [[BaseScreen]] |
| §18 | Items / inventory | vault: `24-Items/` — [[ItemSO]], [[ItemCatalogSO]], [[IInventoryService]], [[InventoryService]], [[InventorySlot]], [[PassiveItemHook]], [[PersistentModifierDef]] |
| §19 | Rewards / loot | partial — [[EconomyService]] + [[EnemyGoldDropService]] cover the gold side (see [[Economy-MOC]]); rewards catalog still TBD |
| §20 | Status effects | TBD — see [[Crosscutting-Overview]] |
| §21 | Quests | TBD |
| §22 | Tutorial | TBD |
| §23 | Settings | TBD |
| §24 | Object pooling | TBD |
| §25 | Analytics | TBD |
| §26 | Content tooling | TBD |
| §27 | Cross-ref conventions | [[Home]] |

## New vault sections (17–26)

These were promoted out of `16-Crosscutting/` between 2026-04-20 and
2026-04-28 as their subsystems shipped real code. The mapping back to
`TECHNICAL.md` is:

| Vault section | TECHNICAL.md anchor |
|---|---|
| `17-Grid` | §17.I (GridManager) — see vault: 17-Grid |
| `18-Movement` | §17.B (Movement & Pathfinding) — see vault: 18-Movement |
| `19-Economy` | §19 (Rewards/Loot — gold side) — see vault: 19-Economy |
| `20-Shop` | §17.F (Shop) — see vault: 20-Shop |
| `21-Audio` | §17.A (Audio) — see vault: 21-Audio |
| `22-Feedback` | §10 (Feedback system) — see vault: 22-Feedback |
| `23-Camera` | §17.E (Camera) — see vault: 23-Camera |
| `24-Items` | §18 (Items / Inventory) — see vault: 24-Items |
| `25-Exploration` | §13 (Dungeon, exploration controller side) — see vault: 25-Exploration |
| `26-PreConditions` | §8.2 (BasePreCondition + concretes) — see vault: 26-PreConditions |

## Reading flow

For each code area you're about to touch, open the matching section in
`TECHNICAL.md` first (source of truth), then drop into the vault note
for implementation context (code path, tests, cross-refs).

---

*Last updated: 2026-04-28*
