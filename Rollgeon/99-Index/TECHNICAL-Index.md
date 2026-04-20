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
| §5.5 | Combo counters / block | [[ComboCountersService]], [[ComboCountersConfig]], [[ComboBlockService]] |
| §5.6 | Strike combos (TBD) | **deferred** — see [[Sprint03-Status]] TBDs |
| §6  | Dice & bag | [[Dice-MOC]] — partial ([[RerollBudgetService]]); DiceBagSO / Types are TBD |
| §7  | Entities & behaviors | [[BaseEntitySO]], [[EnemyDataSO]], [[BaseBehavior]] + concretes |
| §8  | Effects & preconditions | [[Effects-MOC]] — [[BaseEffect]], [[EffectData]], [[BasePreCondition]] |
| §9  | Behavior values | [[BaseBehavior]] (StoredValues API) |
| §10 | Feedback system | [[FloatingDamageSpawner]]; full `FeedbackManager` is TBD |
| §11 | Selection / targeting | [[SelectionSettings]], [[BaseTargetQuery]], [[TargetQueries]] |
| §12 | Combat | [[Combat-MOC]] |
| §12.2 | Damage pipeline | [[DamagePipeline]], [[DamageContext]], [[AttackKind]] |
| §12.3 | Heal pipeline | [[HealPipeline]] |
| §12.6 | Action economy | [[TurnManager]], [[ActionDefinitionSO]], [[EnergyService]] |
| §12.7 | Turn order / initiative | [[TurnOrderService]], [[DefaultInitiativeProvider]], [[TurnOrderConfig]], [[Speed]] |
| §13 | Dungeon / rooms | [[Dungeon-MOC]] |
| §14 | Meta-progression | [[Meta-MOC]] — TBD ([[UnlockSystem]], [[RunRecord]]) |
| §14.7 | Ruleset | [[RulesetSO]], [[EnergyConfig]], [[WeaknessConfig]], [[TurnOrderConfig]] |
| §15 | Save / persistence | [[ISaveable]] + [[SaveSystem]] (TBD) |
| §16 | Unity packages | repo `Packages/` — not in vault |
| §17 | Transversal systems | [[Crosscutting-Overview]] |
| §17.PHA | Phase service | [[Phase-MOC]], [[PhaseService]], [[PhaseTransitionMatrixSO]] |
| §17.G | Player service | [[Player-MOC]], [[PlayerService]] |
| §17.UI | Screen manager | [[ScreenManager]], [[BaseScreen]] |
| §18 | Items / inventory | TBD — see [[Crosscutting-Overview]] |
| §19 | Rewards / loot | TBD — see [[Crosscutting-Overview]] |
| §20 | Status effects | TBD — see [[Crosscutting-Overview]] |
| §21 | Quests | TBD |
| §22 | Tutorial | TBD |
| §23 | Settings | TBD |
| §24 | Object pooling | TBD |
| §25 | Analytics | TBD |
| §26 | Content tooling | TBD |
| §27 | Cross-ref conventions | [[Home]] |

## Reading flow

For each code area you're about to touch, open the matching section in
`TECHNICAL.md` first (source of truth), then drop into the vault note
for implementation context (code path, tests, cross-refs).
