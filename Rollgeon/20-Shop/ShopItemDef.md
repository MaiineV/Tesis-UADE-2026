---
title: ShopItemDef
type: so
domain: 20-Shop
status: done
tags: [shop, item, so]
---

# ShopItemDef

> Authoring SO for a sellable shop item (id + display name + description + icon). Placeholder until `RewardEntrySO` (§19) lands.

## Overview

MVP definition with the same semantic shape as the future `RewardEntrySO`. The `ItemId` is the stable string that gets persisted in [[ShopItemState]]`.ReservedItemId` and is matched against [[ItemSO]]`.ItemId` (in `24-Items/`) to resolve the world prefab and inventory delivery. When §19 lands, this SO is deprecated and [[WeightedShopItem]] will reference `RewardEntrySO` directly while preserving the `ReservedItemId` contract.

## Serialized fields

- `ItemId` (required) — stable string; bridges to [[ItemSO]] via the catalog.
- `DisplayName`
- `Description` (TextArea)
- `Icon` (`Sprite`)

## Dependencies

**Used by:** [[WeightedShopItem]] (pool entry), [[ShopRollResult]], [[ShopSlot]], [[ShopManagerService]] (resolves to [[ItemSO]] via [[ItemCatalogSO]]), [[ShopItemPedestalInteractable]] (hover payload + inventory delivery).

## Code

`Assets/Scripts/Rollgeon/Shop/ShopItemDef.cs`

## External references

- TECHNICAL.md §17.F (shop), §19 (`RewardEntrySO` migration target).
