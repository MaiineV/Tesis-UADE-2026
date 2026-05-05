---
title: BaseBehaviorStoredValue
type: class
domain: 05-Entities
status: done
tags: [entities, behavior, abstract, stored-value]
---

# BaseBehaviorStoredValue

> Polymorphic base for runtime stored values written by effects into
> a [[BaseBehavior]]'s `StoredValues` bag. TECHNICAL.md §9.2.

## Overview

Effects (`EffDealDamage`, `EffHeal`, `EffApplyImpulse`) stash
post-resolve data in the behavior under a [[BehaviorValueKey]] for
the feedback layer to read after the effect pipeline finishes.
Values are appended (one key -> list of values) and cleared in a
`finally` by the dispatcher.

Splitting the base out of the concrete payloads keeps the
`SetBehaviorValue` API typed-via-key without forcing every effect
to share a single struct shape. Concrete subclasses live in the
same file:

- [[FloatBehaviorValue]] — single float (generic).
- [[FloatingNumberBehaviorValue]] — damage / heal numbers + offset
  + target guid + delay.
- [[ImpulseBehaviorValue]] — impulse vector + target guid.

## API / Shape

```csharp
[Serializable]
public abstract class BaseBehaviorStoredValue { }
```

## Dependencies

- **Used by:** [[BaseBehavior]]`.StoredValues`, [[BehaviorValueKey]],
  feedback consumers (FloatingDamageSpawner, hitstop / camera shake
  layers).
- **Sub-classes:** [[FloatBehaviorValue]],
  [[FloatingNumberBehaviorValue]], [[ImpulseBehaviorValue]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Entities/Behaviors/BaseBehaviorStoredValue.cs`

## External references

- TECHNICAL.md: §9.2 Behavior stored values
