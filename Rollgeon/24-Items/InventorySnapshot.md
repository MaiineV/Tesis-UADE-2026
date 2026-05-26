---
title: InventorySnapshot
type: class
domain: 24-Items
status: done
tags: [items, inventory, save]
---

# InventorySnapshot

> Whole-inventory save payload returned by [[InventoryState]]`.CaptureState` and consumed back by `RestoreState`.

## Overview

Holds two id-keyed lists: passive items (just ids — passives carry no per-slot runtime state) and active slots ([[InventorySlotSnapshot]]s with cooldowns). Restoration re-looks-up the [[ItemSO]]s via [[ItemCatalogSO]], so the save file stays asset-reference free.

## API / Shape

```csharp
[Serializable]
public class InventorySnapshot {
    public List<string> PassiveItemIds;
    public List<InventorySlotSnapshot> ActiveSlots;
}
```

## Dependencies

**Uses:** [[InventorySlotSnapshot]].
**Used by:** [[InventoryState]] (return type of `CaptureState`).

## Code

`Assets/Scripts/Rollgeon/Items/InventorySnapshot.cs`
