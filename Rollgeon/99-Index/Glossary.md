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

- **Generala** — the Argentine 5-dice poker game the contract system is
  modelled on. → [[Combos-MOC]].
- **Contract** — class-specific Generala sheet; picks which combos the
  hero can activate during a run. → [[ContractSheet]].
- **Combo** — scored pattern of dice (Par, Escalera, Full House, …). →
  [[BaseComboSO]].
- **Combo id** — canonical string `combo.<snake_case>`. →
  [[ComboId]].
- **Combo counter** — Balatro-style chip-and-multiplier runtime state
  per combo. → [[ComboCountersService]].
- **Crossed combo** — a combo the contract has "crossed out" for the
  rest of the run. Still visible, but skipped by `MatchBest`. →
  [[ContractSheet]].
- **Blocked combo** — a combo currently unavailable due to a boss
  passive or status. → [[ComboBlockService]].
- **Strike combo** — alternate combo variant (§5.6, **TBD**). →
  [[Sprint03-Status]].
- **Weakness / Weakness hit** — enemy-specific damage amplifier. →
  [[WeaknessChecker]].
- **Action economy** — rules limiting action repetitions per turn. →
  [[TurnManager]].
- **Repetition** — flag on an action preventing it from firing twice
  the same turn. → [[ActionDefinitionSO]].
- **Hidden Speed** — turn-order stat invisible to the player. →
  [[Speed]], [[TurnOrderConfig]].
- **Reroll budget** — dice rerolls remaining this action, funded by
  Energy. → [[RerollBudgetService]].

## Engineering vocabulary (code-facing)

- **ServiceLocator / Global vs Run scope** → [[ServiceLocator]],
  [[ServiceScope]].
- **EventManager** (untyped, `object[]`) → [[EventManager]].
- **TypedEvent** (typed, struct payload) → [[TypedEvent]].
- **Single-channel rule** — an event ships through exactly one bus. →
  [[EventManager]].
- **Bootstrap** — `00_Bootstrap` startup pipeline. → [[Bootstrap]].
- **ScriptableObject (SO)** — Unity data-only class; suffix `SO`.
- **Catalog** — SO that groups other SOs for id lookup. →
  [[BaseCatalogSO]], [[Content-Catalogs]].
- **ISaveable** — contract for run-scoped state that will be
  save-rehydratable. → [[ISaveable]].
- **Phase** — global game phase. → [[GamePhase]],
  [[PhaseTransitionMatrixSO]].
- **Overlay** — transient phase modifier. → [[PhaseOverlay]].
- **Pipeline** — ordered step chain over a context. →
  [[DamagePipeline]], [[HealPipeline]].
- **PreCondition** — predicate attached to an [[EffectData]] group. →
  [[BasePreCondition]].
- **Behavior** — polymorphic AI decision rule on an entity. →
  [[BaseBehavior]], [[BehaviorTrigger]], [[GamePhaseMask]].
- **Target query** — resolves a set of targets from a
  [[BaseTargetQuery]]. → [[TargetQueries]].
- **Handoff** — exploration → combat transition. →
  [[CombatHandoffService]], [[CombatReturnService]].
- **Initiative** — speed + speed-die per round. →
  [[DefaultInitiativeProvider]], [[TurnOrderService]].
- **Floating damage** — HUD pop-up at the target's position. →
  [[FloatingDamageSpawner]].

## Status tags

| Tag | Meaning |
|---|---|
| `#done` | Implemented and under test in Sprint 03. |
| `#wip`  | Partially implemented; work in progress. |
| `#pending` | Scoped but not yet implemented (concrete plan). |
| `#tbd`  | Specified in `TECHNICAL.md`, no concrete implementation yet. |
