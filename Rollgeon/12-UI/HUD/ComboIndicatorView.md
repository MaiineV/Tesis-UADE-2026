---
title: ComboIndicatorView
type: view
domain: 12-UI/HUD
status: done
tags: [ui, hud, view, combat, combo]
---

# ComboIndicatorView

> Combat HUD piece that flashes the name of the last matched combo and
> its counter multiplier. Complemented by [[ComboRowView]] for the full
> contract display.

## Event binding

- [[TypedEvent]]`<ComboMatchedPayload>` → show combo name + base damage.
- [[EventName]] `OnComboCounterIncremented(comboId, newCount)` →
  update the counter badge.
- [[EventName]] `OnComboCrossed(sourceGuid, comboId)` → grey out the
  crossed combo if it was currently displayed.

## Dependencies

- **Uses:** [[BaseComboSO]] (display metadata),
  [[ComboCountersService]], [[EventManager]], [[TypedEvent]].
- **Used by:** [[CombatHUDView]], [[ContractDisplayView]] (sibling
  that shows the whole contract).

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/HUD/ComboIndicatorView.cs`,
  `.../ComboRowView.cs`
- Tests: `.../Tests/ComboIndicatorViewTests.cs`

## External references

- Setup: `docs/setup/UI#0095b_CombatHUD.md`
- TECHNICAL.md: §D / §5.5 Combo indicator
