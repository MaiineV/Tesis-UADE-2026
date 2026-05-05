---
title: InventoryState
type: class
domain: 24-Items
status: done
tags: [items, inventory, save, saveable]
---

# InventoryState

> [[ISaveable]] adapter for the inventory — captures and restores the list of passive item ids and the active slot snapshots under save key `run.inventory`.

## Overview

Run-scoped saveable. `CaptureState` returns an [[InventorySnapshot]] cloning the current id lists; `RestoreState` accepts the same snapshot back and rebuilds its internal state. The actual rehydration into [[InventoryService]] (resolving ids via [[ItemCatalogSO]] and rebinding hooks) happens in `InventoryService.RestoreState(InventoryState)`.

## API / Shape

```csharp
public class InventoryState : ISaveable {
    public string SaveKey => "run.inventory";
    public List<string> PassiveItemIds;
    public List<InventorySlotSnapshot> ActiveSlots;

    public object CaptureState();          // returns InventorySnapshot
    public void   RestoreState(object s);  // expects InventorySnapshot
}
```

## Dependencies

**Uses:** [[ISaveable]], [[InventorySnapshot]], [[InventorySlotSnapshot]].
**Used by:** [[InventoryService]] (`CaptureState` / `RestoreState`), future Save System.

## Code

`Assets/Scripts/Rollgeon/Items/InventoryState.cs`
