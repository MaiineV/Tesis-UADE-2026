---
title: ActiveItemSlotView
type: view
domain: 12-UI/HUD
status: done
tags: [ui, hud, view, items]
---

# ActiveItemSlotView

> A single visual slot for an active item (bow / potion / etc.). Pure
> display + click — does not subscribe to events; [[ActiveItemsView]]
> drives it via `SetState` and `SetCount`.

## Overview

Plan §4.7. The slot toggles between `Active`, `Inactive`, and `Depleted`
overlays (see [[ActiveItemState]]) and optionally swaps icon sprites.
When clicked while `Active`, fires `OnClicked` so the parent
[[ActiveItemsView]] can call `IInventoryService.ActivateItem`.

Auto-resolves a `Button` and a transparent `Image` (raycast target) at
runtime if the prefab does not already have them, and auto-builds a
bottom-right TMP `CountLabel` if none is wired.

## API / Shape

```csharp
public class ActiveItemSlotView : MonoBehaviour {
    public ActiveItemState CurrentState { get; }
    public event Action<ActiveItemSlotView> OnClicked;

    public void SetState(ActiveItemState state);
    public void SetCount(int count);
}
```

Serialized: `_icon`, `_button`, `_inactiveOverlay`, `_depletedOverlay`,
`_iconActive`, `_iconInactive`, `_countLabel`, `_countLabelFormat`,
`_hideCountAtOrBelow`.

## Dependencies

- **Uses:** [[ActiveItemState]].
- **Used by:** [[ActiveItemsView]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/HUD/ActiveItemSlotView.cs`
