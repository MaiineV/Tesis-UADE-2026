---
title: ShopRollResult
type: struct
domain: 20-Shop
status: done
tags: [shop, struct]
---

# ShopRollResult

> Atomic result of a [[ShopPoolSO]] roll — the picked [[ShopItemDef]] and its raw `BasePrice`. Final pricing (multiplier + variance) is applied separately by [[ShopConfigSO]]`.ResolvePrice`.

## Shape

```csharp
public struct ShopRollResult {
    public ShopItemDef Item;
    public int         BasePrice;
}
```

`Item == null` (the `default` value) signals "no eligible entry" and tells [[ShopManagerService]] to skip the slot.

## Dependencies

**Uses:** [[ShopItemDef]].
**Used by:** [[ShopPoolSO]] (returns it from `Roll`), [[ShopManagerService]] (consumes it in `BuildOrHydrateSlot`).

## Code

`Assets/Scripts/Rollgeon/Shop/ShopRollResult.cs`

## External references

- TECHNICAL.md §17.F.2 (pool + rolling), §17.F.3 (price resolution).
