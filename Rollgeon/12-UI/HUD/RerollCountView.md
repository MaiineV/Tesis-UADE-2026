---
title: RerollCountView
type: view
domain: 12-UI/HUD
status: done
tags: [ui, hud, view, reroll]
---

# RerollCountView

> Shows the remaining free rerolls for the current action and the
> energy cost of the next paid reroll.

## Event binding

- Queries [[RerollBudgetService]]`.Query()` and renders the returned
  [[RerollQueryResult]].
- Refreshes on `OnRerollConsumed`, `OnActionStarted`,
  `OnPlayerEnergyChanged`.

## Dependencies

- **Uses:** [[RerollBudgetService]], [[RerollQueryResult]],
  [[EnergyService]], [[EventManager]].
- **Used by:** [[CombatHUDView]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/HUD/RerollCountView.cs`
- Tests: `.../Tests/RerollCountViewTests.cs`

## External references

- Setup: `docs/setup/Feature#0104_EnergyReroll.md`
- TECHNICAL.md: §D Reroll count
