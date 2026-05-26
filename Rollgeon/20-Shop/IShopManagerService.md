---
title: IShopManagerService
type: interface
domain: 20-Shop
status: done
tags: [shop, interface, service]
---

# IShopManagerService

> Floor-level coordinator for shop rooms — lazy-inits slots on `OnRoomEntered`, exposes per-room slot lists, and processes the purchase callback from pedestals.

## Overview

The shop service is global-scoped (registered by [[ShopManagerBootstrap]]). On the first entry to a `RoomType.Shop` room it rolls slots against [[ShopPoolSO]] and persists the selection as [[ShopItemState]] inside `RoomInstance.ObjectStates`. Re-entries hydrate from that state instead of re-rolling. Re-stock is contract-defined but currently no-op in the MVP.

## API

```csharp
public interface IShopManagerService {
    IReadOnlyList<ShopSlot> GetSlots(Guid roomInstanceId);
    bool                    IsInitialized(Guid roomInstanceId);
    ShopSlot                FindActiveSlot(Guid roomInstanceId, string spawnPointId);
    void                    NotifyItemPurchased(Guid roomInstanceId, string spawnPointId, int pricePaid);
    bool                    CanRestock(Guid roomInstanceId);
    void                    Restock(Guid roomInstanceId);   // MVP: log + no-op
    void                    Initialize(RoomInstance room, int floorDepth);
}
```

## Dependencies

**Uses:** [[ShopSlot]], [[RoomInstance]] (in `07-Dungeon/`), `Guid`.
**Used by:** [[ShopManagerService]] (impl), [[ShopManagerBootstrap]], [[ShopItemPedestalInteractable]] (purchase callback).

## Code

`Assets/Scripts/Rollgeon/Shop/IShopManagerService.cs`

## External references

- TECHNICAL.md §17.F.1 (shop room coordination), §13.6 (RoomInstance object states).
