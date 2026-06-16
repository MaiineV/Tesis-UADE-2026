---
title: PlayerActionButtonsView
type: view
domain: 12-UI/HUD
status: done
tags: [ui, hud, view, combat, actions]
---

# PlayerActionButtonsView

> Strip of buttons that expose the player's currently-available
> [[ActionDefinitionSO]]s. Each button asks
> [[TurnManager]]`.CanExecute` and reflects the answer via enabled /
> disabled state + tooltip.

## Sub-pieces

- `ActionButtonsView` — generic variant used for enemy intent or
  previews.

## Event binding

- [[EventName]] `OnTurnStarted` / `OnTurnFinished` →
  enable / disable the whole strip.
- `OnPlayerEnergyChanged`, `OnAttributeChanged` → re-query
  `CanExecute`.

## Dependencies

- **Uses:** [[ActionDefinitionSO]], [[ActionCatalogSO]],
  [[TurnManager]], [[EnergyService]], [[EventManager]].
- **Used by:** [[CombatHUDView]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/HUD/PlayerActionButtonsView.cs`,
  `.../ActionButtonsView.cs`
- Tests: `.../Tests/PlayerActionButtonsViewTests.cs`,
  `.../Tests/ActionButtonsViewTests.cs`

## External references

- Setup: `docs/setup/UI#0012c_PlayerActionButtons.md`
- TECHNICAL.md: §D / §12.6 Player action buttons
