---
title: ItemCatalogSO
type: catalog
domain: 24-Items
status: done
tags: [items, catalog, so]
---

# ItemCatalogSO

> Catalog SO that groups every [[ItemSO]] available in the run, registered globally so [[InventoryService]] and content tools can resolve items by id.

## Overview

Concrete `BaseCatalogSO<ItemSO>` keyed by `ItemSO.ItemId`. Adds typed lookups by [[ItemType]] and [[ItemRarity]], plus an editor-only `GetEditorAllIds()` that falls back to `AssetDatabase` when the catalog isn't registered yet (used by `[ValueDropdown]`s on item-id fields).

## API / Shape

- `IEnumerable<ItemSO> GetByType(ItemType type)`
- `IEnumerable<ItemSO> GetByRarity(ItemRarity rarity)`
- `static IEnumerable<string> GetEditorAllIds()` — editor fallback for inspector dropdowns.
- Inherited from [[BaseCatalogSO]]: `AllIds`, `GetById(string)`, validation hooks.

## Dependencies

**Uses:** [[ItemSO]], [[ItemType]], [[ItemRarity]], [[BaseCatalogSO]], [[ServiceLocator]].
**Used by:** [[InventoryService]] (restore-by-id), [[InventoryServiceBootstrap]] (constructor dep), shop / loot tables, item-id `[ValueDropdown]`s.

## Code

`Assets/Scripts/Rollgeon/Items/ItemCatalogSO.cs`
