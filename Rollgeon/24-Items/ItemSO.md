---
title: ItemSO
type: so
domain: 24-Items
status: done
tags: [items, so]
---

# ItemSO

> ScriptableObject describing a single inventory item — passive (event-driven hooks + persistent modifiers) or active (cooldown-gated [[EffectData]] activation).

## Overview

Single content asset for every passive or active item in the run. Passive items bind [[PassiveItemHook]]s to [[EventManager]] events on pickup; active items expose an `OnActivate` effect that the player triggers, optionally consuming a turn slot via the action economy. The `Type` enum gates which inspector fields are visible (Odin `[ShowIf]`).

## API / Shape

Serialized fields:

- Identity: `ItemId`, `DisplayName`, `Description`, `Icon`, `Rarity` ([[ItemRarity]]).
- Type: `Type` ([[ItemType]]) — toggles passive vs active blocks.
- Passive: `PassiveHooks` (`List<`[[PassiveItemHook]]`>`).
- Active: `OnActivate` ([[EffectData]]), `Cooldown` (int turns), `ConsumedOnUse` (bool), `ConsumesAction` (bool), `ActionId` (string, default `item.<ItemId>`).
- Computed: `ResolvedActionId` — falls back to `item.<ItemId>` if `ActionId` is empty.
- Visual: `WorldPrefab` (optional 3D prop for pedestal / drop).

## Dependencies

**Uses:** [[ItemRarity]], [[ItemType]], [[PassiveItemHook]], [[EffectData]].
**Used by:** [[ItemCatalogSO]], [[InventoryService]], [[InventorySlot]], [[IInventoryService]], shop / drop pipelines.

## Code

`Assets/Scripts/Rollgeon/Items/ItemSO.cs`
