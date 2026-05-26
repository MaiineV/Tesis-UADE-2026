---
title: ShopManagerService
type: service
domain: 20-Shop
status: done
tags: [shop, service]
---

# ShopManagerService

> MVP implementation of [[IShopManagerService]]. Lazy-initializes shop rooms on `OnRoomEntered`, rolls slots against [[ShopPoolSO]], persists selections in [[ShopItemState]], and instantiates pedestal + item visuals.

## Overview

Subscribes to `OnRoomEntered` in the constructor; ignores rooms whose `Template.Type` is not `Shop` and rooms already initialized. Uses `room.InstanceId.GetHashCode()` to seed the per-room `System.Random` so the same room re-rolls deterministically inside a run. On re-entry it hydrates slots from `RoomInstance.ObjectStates` (`ShopItemState`) without re-rolling. Implements `IDisposable` for clean teardown.

Spawn points are resolved from the prefab's [[RoomLayout]]`.RewardSpawnPoints`; the slot count is `min(spawnPoints.Count, ShopConfigSO.MaxItemSlots)`. Each unpurchased slot instantiates `ShopConfigSO.PedestalPrefab` (with a [[ShopItemPedestalInteractable]] component) plus the [[ItemSO]]`.WorldPrefab` (resolved through [[ItemCatalogSO]]) on top.

## Key methods

- `OnRoomEntered` (private) — entry filter + dispatch to `InitializeInternal`.
- `BuildOrHydrateSlot` — re-entry vs. first-visit branch, persists new `ShopItemState`.
- `SpawnPedestalVisual` / `SpawnItemVisualOnTop` — instantiates prefabs under the room.
- `NotifyItemPurchased` — flips `ShopItemState.Purchased = Consumed = true`, destroys visual, fires `OnShopItemPurchased(spawnPointId, itemId, pricePaid)`.
- `Restock` — MVP no-op; logs a warning. Wired in §17.F.5 follow-up.

Spawn-point keys follow the `shop_<index>` convention.

## Dependencies

**Uses:** [[IShopManagerService]], [[ShopConfigSO]], [[ShopPoolSO]], [[ShopSlot]], [[ShopItemDef]], [[ShopItemState]], [[RoomInstance]], [[RoomLayout]], [[IDungeonService]], [[ItemCatalogSO]] (in `24-Items/`), [[ItemSO]], [[EventManager]], [[EventName]] (`OnRoomEntered`, `OnShopItemPurchased`).
**Used by:** [[ShopManagerBootstrap]] (constructs), [[ShopItemPedestalInteractable]] (passed via `Configure`).

## Code

`Assets/Scripts/Rollgeon/Shop/ShopManagerService.cs`

## External references

- TECHNICAL.md §17.F (shop system), §13.6 (object state persistence).
