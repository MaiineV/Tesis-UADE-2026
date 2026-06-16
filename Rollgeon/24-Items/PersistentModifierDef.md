---
title: PersistentModifierDef
type: class
domain: 24-Items
status: done
tags: [items, modifiers, attributes]
---

# PersistentModifierDef

> Authoring shape for a [[Modifier]] applied for the lifetime of a held passive item — target stat type, operation, amount, and direction.

## Overview

`[Serializable]` POCO nested in [[PassiveItemHook]]. On `AddItem`, [[InventoryService]] resolves the player's [[AttributesManager]] entry for `TargetStat`, builds a `Modifier<int>` with [[ModifierLifetime]]`.Permanent`, and tracks the modifier id so `RemoveItem` can pull it back off.

The `[ValueDropdown]` on `TargetStat` enumerates concrete `IModifiable` types in the loaded assemblies (editor only).

## API / Shape

Serialized fields:

- `TargetStat` (`Type`) — concrete `IModifiable` (e.g. `MaxHealth`, `Damage`).
- `Operation` ([[ModifierOperation]]).
- `Amount` (float, cast to int when the modifier is built).
- `Direction` ([[ModifierDirection]], default `Intrinsic`).

## Dependencies

**Uses:** [[Modifier]], [[ModifierOperation]], [[ModifierDirection]], [[ModifierLifetime]].
**Used by:** [[PassiveItemHook]] (`PersistentModifiers`), [[InventoryService]] (`ApplyPersistentModifiers` / `RemovePersistentModifiers`).

## Code

`Assets/Scripts/Rollgeon/Items/PersistentModifierDef.cs`
