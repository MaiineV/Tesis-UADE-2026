---
title: ItemRarity
type: enum
domain: 24-Items
status: done
tags: [items, enum, rarity]
---

# ItemRarity

> Rarity tier for [[ItemSO]] — drives drop weights and HUD coloring.

## API / Shape

```csharp
public enum ItemRarity { Common, Uncommon, Rare, Legendary }
```

## Dependencies

**Used by:** [[ItemSO]], [[ItemCatalogSO]] (`GetByRarity`), shop / loot pickers, inventory HUD tinting.

## Code

`Assets/Scripts/Rollgeon/Items/ItemRarity.cs`
