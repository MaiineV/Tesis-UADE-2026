---
title: ShopItemPedestalInteractable
type: component
domain: 20-Shop
status: done
tags: [shop, interactable, mono]
---

# ShopItemPedestalInteractable

> `MonoBehaviour` that lives on a shop pedestal prefab — wires the [[ShopSlot]] to the player's interaction loop, runs the inline buy flow, shows a worldspace prompt when the player is in range.

## Overview

Stub of the future [[IInteractionService]] (§7.7). Today the buy flow is inline:

1. `Configure(roomInstanceId, slot, service)` is called by [[ShopManagerService]] right after instantiation.
2. `Update` polls `Keyboard.current[_interactKey]` (default `F`) when the player is inside `_interactRange` (default `1.5`); on press it calls `Interact()`.
3. `Interact()` checks [[IEconomyService]]`.Spend`, on success delivers the item to the inventory through [[ItemCatalogSO]] + [[IInventoryService]], then calls `IShopManagerService.NotifyItemPurchased`.

When [[InteractableComponent]] (§7.7) and [[Effect]]s (§8) land, the inline `Spend` + `AddItem` path becomes `EffDeductGold` + `EffAddItemToInventory` + `EffConsumeProp`, with no contract change for [[IShopManagerService]].

## Hover feedback

`OnHoverEnter` / `OnHoverExit` publish `OnShopItemTargetChanged` with payload `[bool hasTarget, string itemName, string description, int price, Sprite icon]` — consumers (e.g. future `ItemInspectView`, §D.6b) subscribe directly.

## Auto-prompt

If `_promptVisual` is unassigned, the component first looks for a child named `Prompt`; failing that it builds a worldspace `Canvas` + `TextMeshProUGUI` at local `Y = 2.5`. The label is filled with `"[F] Comprar {DisplayName} ({Price}G)"`.

## Range check

Reads the player's grid position via [[IPlayerService]]`.PlayerGuid` + [[IGridManager]]`.TryGetPosition`/`GridToWorld` and compares squared distance to `_interactRange`.

## Dependencies

**Uses:** [[IShopManagerService]], [[ShopSlot]], [[IEconomyService]], [[ItemCatalogSO]] (in `24-Items/`), [[IInventoryService]], [[IPlayerService]] (in `11-Player/`), [[IGridManager]], [[ServiceLocator]], [[EventManager]], [[EventName]] (`OnShopItemTargetChanged`), [[ItemSO]]. Spawned indirectly inside [[RoomInstance]] / [[RoomLayout]] / [[FloorLayoutSO]] context (in `07-Dungeon/`).
**Used by:** [[ShopManagerService]] (instantiates and calls `Configure`).

## Code

`Assets/Scripts/Rollgeon/Shop/ShopItemPedestalInteractable.cs`

## External references

- TECHNICAL.md §17.F.4 (pedestal interactable), §7.7 (`IInteractionService`), §8 (effects).
