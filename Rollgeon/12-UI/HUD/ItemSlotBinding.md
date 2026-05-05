---
title: ItemSlotBinding
type: struct
domain: 12-UI/HUD
status: done
tags: [ui, hud, items]
---

# ItemSlotBinding

> Inspector-configurable struct that maps a catalog `ItemId` to the
> [[ActiveItemSlotView]] that represents it on screen.

## Shape

```csharp
[Serializable]
public struct ItemSlotBinding {
    public string ItemId;             // e.g. "item.arco", "item.pocion"
    public ActiveItemSlotView Slot;   // visual slot in the HUD
}
```

## Overview

Used by [[ActiveItemsView]] to drive `SetState` / `SetCount` on the
correct slot when `OnItemObtained`, `OnActiveItemUsed`, or
`OnItemRemoved` fire. Bindings without a matching event id are ignored
silently — non-active items or items without a dedicated slot are
out-of-scope for this view.

## Dependencies

- **Uses:** [[ActiveItemSlotView]].
- **Used by:** [[ActiveItemsView]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/HUD/ActiveItemsView.cs`
  (declared as nested `[Serializable] public struct`).
