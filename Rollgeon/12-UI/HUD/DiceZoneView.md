---
title: DiceZoneView
type: view
domain: 12-UI/HUD
status: done
tags: [ui, hud, view, dice]
---

# DiceZoneView

> Area that displays the 5 dice of the current roll and lets the player
> lock / unlock individual dice before the next reroll.

## Sub-pieces

- `DiceSlotView` (×5) — per-die face + lock toggle.
- Binds to the dice roll state of the active combat action (future
  `ICombatDiceService`).

## Event binding

- `OnDiceRolled(rollId, diceValues[])` → update each slot.
- `OnDiceLocked(slotIndex, locked)` → toggle lock overlay.

## Dependencies

- **Uses:** `DiceSlotView` prefab, [[EventManager]], future combat
  dice service.
- **Used by:** [[CombatHUDView]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/HUD/DiceZoneView.cs`,
  `.../DiceSlotView.cs`

## External references

- Setup: `docs/setup/UI#0095b_CombatHUD.md`
- TECHNICAL.md: §6 / §D Dice zone
