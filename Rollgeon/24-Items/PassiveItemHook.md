---
title: PassiveItemHook
type: class
domain: 24-Items
status: done
tags: [items, passive, effects]
---

# PassiveItemHook

> Authoring shape for one passive item trigger: an [[EventName]] to listen on, the [[EffectData]] to run, and any [[PersistentModifierDef]]s applied while the item is held.

## Overview

`[Serializable]` POCO embedded inside [[ItemSO]]`.PassiveHooks`. When [[InventoryService]]`.AddItem` lands a passive item, each hook is bound to its `TriggerEvent` via [[EventManager]] and its `PersistentModifiers` are pushed onto the player's [[Modifier]] stacks; both are reverted on `RemoveItem`.

The hook handler also gates on the event's owner GUID so a hook only fires for the player's own events.

## API / Shape

Serialized fields:

- `TriggerEvent` ([[EventName]]) — when to run the effect (e.g. `OnTurnStarted`, `OnComboMatched`, `OnDamageResolved`).
- `Effect` ([[EffectData]], Odin-serialized) — the effect graph executed via `TryExecute(ctx, preCtx)`.
- `PersistentModifiers` (`List<`[[PersistentModifierDef]]`>`) — modifiers applied for the lifetime of the item.

## Dependencies

**Uses:** [[EventName]], [[EffectData]], [[PersistentModifierDef]], [[PreConditionContext]].
**Used by:** [[ItemSO]] (`PassiveHooks`), [[InventoryService]] (binds / unbinds via [[EventManager]]).

## Code

`Assets/Scripts/Rollgeon/Items/PassiveItemHook.cs`
