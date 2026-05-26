---
title: ActiveItemsView
type: view
domain: 12-UI/HUD
status: done
tags: [ui, hud, view, items]
---

# ActiveItemsView

> Panel that shows the player's equipped / usable items. Composed of N
> [[ActiveItemSlotView]] slots + an internal `ActiveItemState` for
> cooldowns and charges.

## Sub-pieces

- `ActiveItemSlotView` — individual slot, binds to a
  `IInventoryService` entry (future).
- `ActiveItemState` — runtime struct (`cooldownRemaining`, `charges`).

## Status

UI wired; depends on the Item/Inventory subsystem (`§18`) which is
**TBD** for Sprint 03 beyond stubs.

## Dependencies

- **Uses:** [[EventManager]], `IInventoryService` (future).
- **Used by:** [[ExplorationHUDView]], [[CombatHUDView]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/HUD/ActiveItemsView.cs`
- Slot: `.../ActiveItemSlotView.cs`
- State: `.../ActiveItemState.cs`

## External references

- TECHNICAL.md: §D / §18 Items HUD
