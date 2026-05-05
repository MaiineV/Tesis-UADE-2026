---
title: Items-MOC
type: moc
domain: 90-MOCs
status: done
tags: [moc, items, inventory]
---

# 24-Items — Map of Content

> Item catalog (passive + active) and the run-scoped player inventory.
> Passive items bind `PassiveItemHook`s to the event bus on pickup and
> push persistent modifiers; active items expose a cooldown-gated
> [[EffectData]] activation.

## Relationships

```
 ItemCatalogSO ── List<ItemSO>(by ItemId)
       │
 ItemSO ── ItemType (Passive | Active) · ItemRarity
       ├─ PassiveHooks : List<PassiveItemHook>
       │     ├─ TriggerEvent (EventName)
       │     ├─ Effect (EffectData)
       │     └─ PersistentModifiers (List<PersistentModifierDef>)
       └─ Active: OnActivate (EffectData) · Cooldown · ConsumedOnUse · ActionId

 ServiceLocator (Run scope)
       │
       ▼
 IInventoryService
       ├─ PassiveItems / ActiveItems  (IReadOnlyList<InventorySlot>)
       ├─ AddItem  → bind hooks via EventManager + apply modifiers
       ├─ RemoveItem → unbind + revert modifiers
       ├─ ActivateItem(slot, EffectContext) · TickCooldowns
       └─ event OnItemChanged(item, wasAdded)

 InventorySnapshot / InventorySlotSnapshot · InventoryState  (save layer)
```

## Pages

### Core service
- [[IInventoryService]] — public interface (run-scoped)
- [[InventoryService]] — default impl
- [[InventoryServiceBootstrap]]

### Catalog / data
- [[ItemSO]] · [[ItemCatalogSO]]
- [[ItemType]] · [[ItemRarity]]
- [[PassiveItemHook]] · [[PersistentModifierDef]]

### Inventory shape & save
- [[InventorySlot]] · [[InventorySlotSnapshot]]
- [[InventorySnapshot]] · [[InventoryState]]

## Cross-domain edges

- **Incoming** (consumers):
  - 20-Shop: [[ShopItemPedestalInteractable]] calls `AddItem` on purchase.
  - 04-Effects: `EffAddItemToInventory` and the [[PassiveItemHook]] effect
    graphs run through [[EffectData]] / [[EffectContext]].
  - 11-Player / 14-UI: HUD inventory views, drop pipelines.
  - 02-Combat: `OnTurnStarted` / `OnComboMatched` / `OnDamageResolved`
    fire passive hooks via [[EventManager]].
  - 26-PreConditions: [[PCHasInventoryItem]] gates effects on possession.
- **Outgoing** (dependencies):
  - 00-Foundations: [[ServiceLocator]], [[EventManager]], [[EventName]],
    [[IPreloadableService]].
  - 04-Effects: [[EffectData]] · [[EffectContext]] for activation graphs.
  - 03-Attributes / Modifiers: [[PersistentModifierDef]] feeds the
    modifier stack while items are held.
