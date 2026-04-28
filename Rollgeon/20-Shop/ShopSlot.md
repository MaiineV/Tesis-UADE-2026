---
title: ShopSlot
type: class
domain: 20-Shop
status: done
tags: [shop, runtime]
---

# ShopSlot

> Runtime projection of a shop slot — pairs a [[ShopItemDef]] (resolved from the pool) with its final price, purchased flag, spawn point id, and instantiated visual.

## Shape

```csharp
public sealed class ShopSlot {
    public string       SpawnPointId;
    public ShopItemDef  Item;
    public int          Price;
    public bool         Purchased;
    public GameObject   SpawnedVisual;
}
```

The slot is a **convenience cache** for runtime consumers — the persistent source of truth remains [[ShopItemState]] inside `RoomInstance.ObjectStates` (§13.6). The slot keeps the pedestal `GameObject` reference so the service can destroy it on purchase.

## Dependencies

**Uses:** [[ShopItemDef]].
**Used by:** [[IShopManagerService]] (returned from `GetSlots` / `FindActiveSlot`), [[ShopManagerService]] (constructs and tracks them), [[ShopItemPedestalInteractable]] (passed via `Configure`, drives label and hover payload).

## Code

`Assets/Scripts/Rollgeon/Shop/ShopSlot.cs`

## External references

- TECHNICAL.md §17.F.1 (slot lifecycle), §13.6 (object state persistence).
