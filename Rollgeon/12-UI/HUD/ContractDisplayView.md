---
title: ContractDisplayView
type: view
domain: 12-UI/HUD
status: done
tags: [ui, hud, view, contract]
---

# ContractDisplayView

> Shows every combo in the player's active [[ContractSheet]] as a row,
> grey-out crossed / blocked ones, and highlight the last matched combo.

## Event binding

- On show: reads [[IPlayerService]]`.CurrentHero.Sheet.Combos` and
  renders one row per entry.
- [[EventName]] `OnComboCrossed(sourceGuid, comboId)` → grey out.
- [[TypedEvent]]`<ComboMatchedPayload>` → flash row.
- [[ComboBlockService]] state → grey out blocked rows.

## Dependencies

- **Uses:** [[ContractSheet]], [[BaseComboSO]], [[ComboBlockService]],
  [[IPlayerService]], [[EventManager]], [[TypedEvent]].
- **Used by:** [[CombatHUDView]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/HUD/ContractDisplayView.cs`

## External references

- Setup: `docs/setup/UI#0095b_CombatHUD.md`
- TECHNICAL.md: §D / §5.3 Contract display
