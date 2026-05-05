---
title: InventorySlotSnapshot
type: struct
domain: 24-Items
status: done
tags: [items, inventory, save]
---

# InventorySlotSnapshot

> Serializable, id-keyed projection of one active [[InventorySlot]] used by [[InventoryState]] and [[InventorySnapshot]] for save/restore.

## Overview

`[Serializable]` POCO that holds only the data the save layer needs: the item id and the surviving cooldown. Restored back to a live [[InventorySlot]] by resolving the id through [[ItemCatalogSO]].

## API / Shape

```csharp
[Serializable]
public class InventorySlotSnapshot {
    public string ItemId;
    public int    CurrentCooldown;
}
```

## Dependencies

**Used by:** [[InventoryState]], [[InventorySnapshot]], [[InventoryService]] (`CaptureState` / `RestoreState`).

## Code

`Assets/Scripts/Rollgeon/Items/InventorySlotSnapshot.cs`
