---
title: EffRemoveInventoryItem
type: class
domain: 04-Effects
status: done
tags: [effects, concrete, items]
---

# EffRemoveInventoryItem

> Concrete [[BaseEffect]] that removes an item from the player's
> inventory by `ItemId`. Returns `false` (cuts the chain — §8.8) when
> the item wasn't there, so callers should pair it with `PCHasInventoryItem`
> ([[BasePreCondition]]) to hide the button before energy is spent.

## Overview

No tile / entity selection — removal is by `ItemId` only. The effect
overrides `ShowSelection` / `HasSelectionRequirement` /
`RequiresSelectionAt` to return `false` so combat flow doesn't enter
selection mode for a stale `Selection` value left in the inspector.

## API / Shape

```csharp
[Serializable, HideReferenceObjectPicker]
public sealed class EffRemoveInventoryItem : BaseEffect {
    [ValueDropdown(nameof(GetItemIds))]
    public string ItemId;
}
```

## Dependencies
**Uses:** [[BaseEffect]], `IInventoryService`, `ItemCatalogSO`,
[[BasePreCondition]] gating.
**Used by:** [[EffectData]] inside item-use action pipelines.

## Code
`Assets/Scripts/Rollgeon/Effects/Concretes/EffRemoveInventoryItem.cs`
