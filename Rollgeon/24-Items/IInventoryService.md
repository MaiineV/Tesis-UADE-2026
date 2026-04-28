---
title: IInventoryService
type: interface
domain: 24-Items
status: done
tags: [items, inventory, service, interface]
---

# IInventoryService

> Run-scoped contract for the player's inventory — exposes read-only slot lists, add/remove/query operations, active-item activation, cooldown ticking, and a change event.

## API / Shape

```csharp
public interface IInventoryService {
    IReadOnlyList<InventorySlot> PassiveItems { get; }
    IReadOnlyList<InventorySlot> ActiveItems  { get; }
    int  MaxActiveSlots { get; }

    bool  AddItem(ItemSO item);
    bool  RemoveItem(string itemId);
    bool  HasItem(string itemId);
    ItemSO GetItem(string itemId);

    bool ActivateItem(int activeSlotIndex, EffectContext ctx);
    void TickCooldowns();

    event Action<ItemSO, bool> OnItemChanged; // (item, wasAdded)
}
```

## Dependencies

**Uses:** [[ItemSO]], [[InventorySlot]], [[EffectContext]].
**Used by:** [[InventoryService]] (impl), [[InventoryServiceBootstrap]] (registers), [[PlayerService]] / HUD / shop / drop pipelines, save layer.

## Code

`Assets/Scripts/Rollgeon/Items/IInventoryService.cs`
