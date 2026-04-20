---
title: Glossary
type: index
domain: 99-Index
status: pending
tags: [index, glossary, vocabulary]
---

# Glossary

> Design and engineering vocabulary used across the Rollgeon codebase and
> `TECHNICAL.md`. Each term links to the atomic note that owns it.

> **Status:** stub. Fully populated in **Wave 9**. The list below is the
> target outline — terms may be reworded or merged as atomic notes are
> written.

---

## Design vocabulary (gameplay-facing)

- **Contract** — class-specific Generala card; picks which combos the hero
  can activate during a run. → [[ContractSheet]].
- **Generala** — the Argentine 5-dice poker game the contract system is
  modeled on. → [[Combos-MOC]].
- **Combo** — a scored pattern of dice (Par, Escalera, Full House, …). →
  [[BaseComboSO]].
- **Combo Counter** — Balatro-style chip-and-multiplier runtime state per
  combo, accumulated across combat. → [[ComboCountersService]].
- **Strike** — alternate combo variant (TBD §5.6).
- **Weakness** — enemy-specific damage type; triggers a multiplier in
  `DamagePipeline` when an attack matches. → [[WeaknessChecker]].
- **Action Economy** — the rules that limit how many actions of a given
  `ActionId` a player can spend per turn. → [[TurnManager]].
- **Repetition** — per-action flag that blocks the same action from being
  used twice in a turn. → [[ActionDefinitionSO]].
- **Hidden Speed** — turn-order tiebreak stat not shown in UI. →
  [[TurnOrderConfig]].
- **Reroll Budget** — remaining dice re-rolls this turn, funded by Energy.
  → [[RerollBudgetService]].

## Engineering vocabulary (code-facing)

- **ServiceLocator** — typed global registry with Global and Run scopes.
  → [[ServiceLocator]].
- **EventManager** — typed event bus decoupling producers from consumers.
  → [[EventManager]].
- **Bootstrap** — startup pipeline that hydrates services and catalogs.
  → [[Bootstrap]].
- **ScriptableObject (SO)** — Unity asset class used for data-only
  content; suffix `SO` on the type name.
- **Catalog** — SO that groups other SOs for lookup by id at runtime. →
  [[Content-MOC]].
- **Phase** — global game phase (Exploration / Combat / etc.); gates
  `PhaseInteractionRule` and service availability. → [[GamePhase]].
- **Pipeline** — ordered chain of steps applied to a context
  (`DamagePipeline`, `HealPipeline`). → [[DamagePipeline]].
- **PreCondition** — predicate attached to an effect that decides whether
  the effect fires. → [[BasePreCondition]].
- **Behavior** — enemy AI decision rule with phase masks and behavior
  values. → [[BaseBehavior]].
- **Handoff** — transition from exploration to combat (and back),
  carrying room + enemy context. → [[CombatHandoffService]].

To be expanded during Wave 9.
