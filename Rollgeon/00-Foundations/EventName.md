---
title: EventName
type: concept
domain: 00-Foundations
status: done
tags: [foundation, events, enum]
---

# EventName

> Enum that enumerates every string-keyed event on the [[EventManager]]
> bus. Acts as the canonical contract between publishers and subscribers.

## Purpose

Centralising event keys in an enum gives compile-time safety vs. raw
strings and a single place to document each event's payload schema
(payload positions + expected types).

## Entries (sample)

- `OnAttributeChanged(Guid entityId, Type attributeType)`
- `OnModifierAdded(Guid carrierId, Type attributeType, Guid modifierId)`
- `OnModifierRemoved(Guid carrierId, Guid modifierId)`
- `OnRunEnd`, `OnCombatEnd` — scope tick events consumed by
  [[Modifier]] to auto-remove.
- plus per-turn tick events used by `ModifierLifetime.Turns`.

## Removal rule

When an event is migrated to [[TypedEvent]], its entry must be **removed**
from this enum to enforce the single-channel rule.

## Dependencies

- **Uses:** —
- **Used by:** [[EventManager]], [[Modifier]], [[AttributesManager]],
  every system that subscribes or triggers untyped events.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Patterns/EventName.cs`

## External references

- TECHNICAL.md: §1.2 EventManager — event schema
