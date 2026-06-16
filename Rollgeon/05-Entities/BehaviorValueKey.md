---
title: BehaviorValueKey
type: enum
domain: 05-Entities
status: done
tags: [entities, behavior, enum, stored-value]
---

# BehaviorValueKey

> Keys for the runtime stored-value bag on [[BaseBehavior]].
> TECHNICAL.md §9.2.

## Shape

```csharp
public enum BehaviorValueKey {
    None           = 0,
    FloatingDamage = 1,
    FloatingHeal   = 2,
    FloatingShield = 3,
    HitImpulse     = 4,
}
```

## Stability rule

**Never renumber, only append.** Saved replays / serialized state may
reference older numeric values; reordering would re-bind them to a
different payload kind. New keys always go at the end with the next
unused integer.

## Dependencies

- **Used by:** [[BaseBehavior]]`.SetBehaviorValue` /
  `TryGetBehaviorValues`, [[BaseBehaviorStoredValue]] subclasses
  ([[FloatBehaviorValue]], [[FloatingNumberBehaviorValue]],
  [[ImpulseBehaviorValue]]), feedback consumers.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Entities/Behaviors/BehaviorValueKey.cs`

## External references

- TECHNICAL.md: §9.2 Behavior stored value keys
