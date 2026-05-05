---
title: ActiveItemState
type: enum
domain: 12-UI/HUD
status: done
tags: [ui, hud, enum, items]
---

# ActiveItemState

> Visual state of an active-item slot in the HUD. Plan §4.6.

## Shape

```csharp
public enum ActiveItemState {
    Inactive = 0, // player does not own the item
    Active   = 1, // owned and ready to use
    Depleted = 2, // used (e.g. potion consumed)
}
```

## Typical values

- `Inactive` — gray / hidden icon. Default state when the slot is
  bound but the inventory has no entry for the bound `ItemId`.
- `Active` — slot is interactable; clicking dispatches activation.
- `Depleted` — visually distinct from `Inactive` (post-consumption).

## Dependencies

- **Used by:** [[ActiveItemSlotView]], [[ActiveItemsView]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/HUD/ActiveItemState.cs`
