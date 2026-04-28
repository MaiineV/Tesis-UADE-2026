---
title: InventoryServiceBootstrap
type: bootstrap
domain: 24-Items
status: done
tags: [items, inventory, bootstrap, so]
---

# InventoryServiceBootstrap

> Preloadable SO that constructs an [[InventoryService]] from an [[ItemCatalogSO]] + `MaxActiveSlots` cap and registers it as [[IInventoryService]] in `ServiceScope.Run`.

## Overview

`IPreloadableService` with `Priority = 60` — runs after catalogs and player services are up. The bootstrap is idempotent: if `_instance` is already built, `Register` is a no-op. Logs an error and bails if the catalog reference is missing.

## API / Shape

Serialized fields:

- `_catalog` ([[ItemCatalogSO]], `[Required]`).
- `_maxActiveSlots` (int, `[MinValue(1)]`, default `4`).

```csharp
public sealed class InventoryServiceBootstrap : ScriptableObject, IPreloadableService {
    public int  Priority { get; } // 60
    public void Register();        // adds InventoryService as IInventoryService (Run)
}
```

## Dependencies

**Uses:** [[InventoryService]], [[IInventoryService]], [[ItemCatalogSO]], [[ServiceLocator]], `IPreloadableService`.
**Used by:** [[Bootstrap]] preload pipeline.

## Code

`Assets/Scripts/Rollgeon/Items/InventoryServiceBootstrap.cs`
