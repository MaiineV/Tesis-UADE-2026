---
title: PCHasInventoryItem
type: class
domain: 26-PreConditions
status: done
tags: [preconditions, gating, inventory, items]
---

# PCHasInventoryItem

> Passes when the player currently holds at least one inventory slot of
> the configured `ItemId`.

## Overview

Used to gate behaviors that consume an item — e.g. a heal action that
requires a healing potion. Reads `IInventoryService.HasItem(ItemId)`;
if the service is missing or `ItemId` is empty, returns `false` (with a
warning when service is missing). `_isConstantValue` is forced `false`
in the constructor since inventory state is purely runtime.

## Configuration

- `ItemId` (`string`) — drawn as a `ValueDropdown` populated from
  `ItemCatalogSO.GetEditorAllIds()` in editor builds.

## Dependencies

**Uses:** [[BasePreCondition]], [[PreConditionContext]],
[[IInventoryService]], [[ItemCatalogSO]]
**Used by:** Item-consuming [[EffectData]] groups (e.g.
[[EffRemoveInventoryItem]] gating, healing potion behaviors).

## Code

`Assets/Scripts/Rollgeon/PreConditions/Concretes/PCHasInventoryItem.cs`
