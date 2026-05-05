---
title: ShopPoolSO
type: so
domain: 20-Shop
status: done
tags: [shop, pool, so]
---

# ShopPoolSO

> Weighted pool of [[ShopItemDef]]s eligible for shop rolls; exposes `Roll(rng, floorDepth)` returning a [[ShopRollResult]].

## Overview

`SerializedScriptableObject` (Odin) that holds a `List<WeightedShopItem>`. The `Roll` method:

1. Sums `Weight` over entries that have a non-null item, `Weight > 0`, and `MinFloorDepth <= floorDepth`.
2. Picks `rng.NextDouble() * total`, walks the list with a cursor.
3. On floating-point drift, falls back to the last eligible entry.

Returns `default` (Item null) when nothing is eligible — [[ShopManagerService]] logs and skips that slot.

## API

```csharp
public sealed class ShopPoolSO : SerializedScriptableObject {
    public List<WeightedShopItem> Items;
    public ShopRollResult Roll(System.Random rng, int floorDepth);
}
```

## Dependencies

**Uses:** [[WeightedShopItem]], [[ShopItemDef]], [[ShopRollResult]].
**Used by:** [[ShopManagerService]] (rolls and resolves item defs by id), [[ShopManagerBootstrap]] (serialized ref). Also indirectly references [[ItemSO]] (in `24-Items/`) via `ShopItemDef.ItemId` matching.

## Code

`Assets/Scripts/Rollgeon/Shop/ShopPoolSO.cs`

## External references

- TECHNICAL.md §17.F.2 (pool + rolling).
