---
title: InventorySlot
type: class
domain: 24-Items
status: done
tags: [items, inventory, runtime]
---

# InventorySlot

> Runtime container pairing a held [[ItemSO]] with its current cooldown counter — the live shape `InventoryService` stores in its passive / active lists.

## Overview

Mutable, `[Serializable]` POCO. Cooldowns only matter for active items; passive slots keep `CurrentCooldown = 0`. For persistence, slots are projected to [[InventorySlotSnapshot]] (id-only) so saves don't pin asset references.

## API / Shape

```csharp
[Serializable]
public class InventorySlot {
    public ItemSO Item;
    public int    CurrentCooldown;
}
```

## Dependencies

**Uses:** [[ItemSO]].
**Used by:** [[InventoryService]] (`PassiveItems`, `ActiveItems`), [[IInventoryService]] read-only views, inventory HUD bindings.

## Code

`Assets/Scripts/Rollgeon/Items/InventorySlot.cs`
