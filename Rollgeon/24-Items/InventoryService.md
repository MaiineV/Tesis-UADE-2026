---
title: InventoryService
type: service
domain: 24-Items
status: done
tags: [items, inventory, service]
---

# InventoryService

> Concrete [[IInventoryService]] — owns the live passive / active slot lists, binds [[PassiveItemHook]]s to [[EventManager]], applies / removes [[PersistentModifierDef]]s on the player's attributes, and routes active items through the action economy.

## Overview

Built by [[InventoryServiceBootstrap]] with an [[ItemCatalogSO]] and a `MaxActiveSlots` cap. `AddItem` rejects active items past the cap, then either binds passive hooks + persistent modifiers or appends an active slot, and finally raises `OnItemChanged` plus [[EventName]] `OnItemObtained`. `RemoveItem` reverses everything (unbind hooks, strip modifiers, fire `OnItemRemoved`).

`ActivateItem` checks per-slot cooldown, then either runs `OnActivate` directly through [[EffectData]]`.TryExecute` (when `ConsumesAction == false`) or wraps it in a synthetic [[ActionDefinitionSO]] (`Type = UseItem`) and delegates to `TurnManager.TryExecute` so it counts against the player's turn slot. On success it sets `CurrentCooldown = item.Cooldown`, fires `OnActiveItemUsed`, and removes the slot if `ConsumedOnUse`.

`TickCooldowns` decrements every active slot once per turn. `Dispose` and `RestoreState` both run `ClearAllHooksAndModifiers` before the new state goes in. `CaptureState` / `RestoreState(InventoryState)` provide the save-layer hooks.

## API / Shape

```csharp
public sealed class InventoryService : IInventoryService, IDisposable {
    public InventoryService(ItemCatalogSO catalog, int maxActiveSlots);

    // IInventoryService surface — see [[IInventoryService]].

    public InventoryState CaptureState();
    public void           RestoreState(InventoryState state);
    public void           Dispose();
}
```

## Dependencies

**Uses:** [[ItemSO]], [[ItemCatalogSO]], [[InventorySlot]], [[InventoryState]], [[InventorySlotSnapshot]], [[PassiveItemHook]], [[PersistentModifierDef]], [[EffectData]], [[EffectContext]], [[PreConditionContext]], [[EventManager]], [[EventName]], [[ServiceLocator]], [[IPlayerService]], [[AttributesManager]], `TurnManager`, [[ActionDefinitionSO]].
**Used by:** [[InventoryServiceBootstrap]] (constructs and registers under [[IInventoryService]]), inventory HUD, shop / loot, save layer.

## Code

`Assets/Scripts/Rollgeon/Items/InventoryService.cs`
