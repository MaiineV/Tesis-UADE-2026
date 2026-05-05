---
title: WeightedShopItem
type: struct
domain: 20-Shop
status: done
tags: [shop, struct, pool]
---

# WeightedShopItem

> Serializable struct used as an entry in [[ShopPoolSO]]`.Items`. Carries the [[ShopItemDef]], its relative weight, base price, and a floor-depth gate.

## Shape

```csharp
[Serializable]
public struct WeightedShopItem {
    public ShopItemDef Item;          // required
    public float       Weight;        // 0 = disabled; relative to pool total
    public int         BasePrice;     // before mult + variance from ShopConfigSO
    public int         MinFloorDepth; // 0 = always eligible; > 0 gates by floor
}
```

`MinFloorDepth` filters legendary or late-game items out of early floors.

## Dependencies

**Uses:** [[ShopItemDef]].
**Used by:** [[ShopPoolSO]] (list entries + rolling), [[ShopRollResult]] (output), [[ShopManagerService]] (consumes via `ShopPoolSO.Roll`). Also relates to [[ItemSO]] (in `24-Items/`) via `ShopItemDef.ItemId`.

## Code

`Assets/Scripts/Rollgeon/Shop/WeightedShopItem.cs`

## External references

- TECHNICAL.md §17.F.2 (pool weights and floor gating).
