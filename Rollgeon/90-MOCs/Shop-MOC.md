---
title: Shop-MOC
type: moc
domain: 90-MOCs
status: done
tags: [moc, shop]
---

# 20-Shop — Map of Content

> Floor-level shop coordinator: rolls slots from a weighted pool on
> first room entry, persists the selection inside the [[RoomInstance]],
> and processes the purchase callback from the pedestal interactable.

## Relationships

```
 ShopConfigSO ── pool weights & per-floor settings
       │
 ShopPoolSO ── List<WeightedShopItem(ShopItemDef)>
       │
 ServiceLocator (Global scope)
       │
       ▼
 IShopManagerService
       ├─ OnRoomEntered → Initialize(roomInstance, floorDepth)
       │     └ rolls ShopRollResult → List<ShopSlot>
       │     └ persists into RoomInstance.ObjectStates (ShopItemState)
       ├─ GetSlots(roomInstanceId)
       ├─ FindActiveSlot(roomInstanceId, spawnPointId)
       └─ NotifyItemPurchased(roomInstanceId, spawnPointId, pricePaid)
                │
        ShopItemPedestalInteractable ── Configure(roomId, ShopSlot, service)
                ├─ IEconomyService.Spend(price)
                └─ IInventoryService.AddItem(ItemSO from ItemCatalogSO)
```

## Pages

### Core service
- [[IShopManagerService]] — public interface (global-scoped)
- [[ShopManagerService]] — default impl
- [[ShopManagerBootstrap]] — registers the service

### Data / config
- [[ShopConfigSO]] · [[ShopPoolSO]]
- [[ShopItemDef]] · [[WeightedShopItem]]
- [[ShopRollResult]] · [[ShopSlot]]

### Interactable
- [[ShopItemPedestalInteractable]]

## Cross-domain edges

- **Incoming** (consumers):
  - 07-Dungeon: shop rooms (`RoomType.Shop`) carry the pedestal spawn
    points; [[RoomInstance]] persists [[ShopSlot]] state across re-entry.
  - 11-Player: [[IPlayerService]]`.PlayerGuid` + [[IGridManager]] for
    the in-range `[F]` interaction polling.
  - 14-UI: future `ItemInspectView` (§D.6b) and `PoolOfferingRow`
    consume `OnShopItemTargetChanged` hover events.
- **Outgoing** (dependencies):
  - 00-Foundations: [[ServiceLocator]], [[EventManager]], [[EventName]].
  - 19-Economy: [[IEconomyService]]`.Spend` on purchase.
  - 24-Items: [[ItemCatalogSO]] resolves the [[ItemSO]];
    [[IInventoryService]]`.AddItem` delivers it.
  - 17-Grid: [[IGridManager]] for player position / range check.
