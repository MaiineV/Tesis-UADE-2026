---
title: EffAddItemToInventory
type: class
domain: 04-Effects
status: done
tags: [effects, concrete, inventory]
---

# EffAddItemToInventory

> Concrete [[BaseEffect]] that resolves an [[ItemSO]] from the catalog
> by `ItemId` and adds it through [[IInventoryService]]. Used by
> reward effects (chest open, combat drops, shop purchases).

## Overview

Selection-less by design (`ShowSelection => false`) — the operation is
on the player's inventory, not a tile/entity. Returns `false` and
short-circuits the [[EffectData]] chain when the catalog or inventory
services aren't registered, the id is empty, or the item can't be
added (e.g. inventory full). The `OnItemObtained` event is fired
centrally by `InventoryService.AddItem` to avoid double-fire.

## API / Shape

```csharp
[Serializable, HideReferenceObjectPicker]
public sealed class EffAddItemToInventory : BaseEffect {
    [ValueDropdown(nameof(GetItemIds))]
    public string ItemId;
}
```

## Dependencies
**Uses:** [[BaseEffect]], `ItemCatalogSO`, [[ItemSO]],
[[IInventoryService]], `ServiceLocator`.
**Used by:** chest / shop reward [[EffectData]] pipelines.

## Code
`Assets/Scripts/Rollgeon/Effects/Concretes/EffAddItemToInventory.cs`
