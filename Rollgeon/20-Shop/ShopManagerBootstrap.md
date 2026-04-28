---
title: ShopManagerBootstrap
type: bootstrap
domain: 20-Shop
status: done
tags: [shop, bootstrap, so]
---

# ShopManagerBootstrap

> Preloadable SO that wires a [[ShopConfigSO]] + [[ShopPoolSO]] into a [[ShopManagerService]] and registers it as [[IShopManagerService]] in `ServiceScope.Global`.

## Overview

Implements `IPreloadableService` with `Priority = 60` — runs after Feedback (55) and Audio (50), but before any run-scoped dungeon service so the shop subscriber is alive when `OnRoomEntered` fires for the start room. Aborts registration with an error log if either SO is unassigned.

## Serialized fields

- `_config` (`ShopConfigSO`, required) — pricing/slots/restock tuning.
- `_pool` (`ShopPoolSO`, required) — items eligible for rolling. MVP uses one global pool; multi-floor branching lands later.

## API

```csharp
public sealed class ShopManagerBootstrap : ScriptableObject, IPreloadableService {
    public int  Priority { get; } // 60
    public void Register();
}
```

## Dependencies

**Uses:** [[ShopManagerService]], [[IShopManagerService]], [[ShopConfigSO]], [[ShopPoolSO]], [[ServiceLocator]], `IPreloadableService`.
**Used by:** [[Bootstrap]] preload pipeline.

## Code

`Assets/Scripts/Rollgeon/Shop/ShopManagerBootstrap.cs`

## External references

- TECHNICAL.md §17.F (shop system).
