---
title: FloatBehaviorValue
type: class
domain: 05-Entities
status: done
tags: [entities, behavior, stored-value]
---

# FloatBehaviorValue

> Generic single-float payload for the [[BaseBehavior]] stored-value
> bag. Used when an effect needs to publish a scalar (multiplier,
> ratio, accumulated value) without dragging extra fields. TECHNICAL.md §9.2.

## Shape

```csharp
[Serializable]
public class FloatBehaviorValue : BaseBehaviorStoredValue {
    public float Value;
}
```

## Dependencies

- **Uses:** [[BaseBehaviorStoredValue]] (base).
- **Used by:** [[BaseBehavior]]`.SetBehaviorValue` /
  `TryGetBehaviorValues<FloatBehaviorValue>` keyed by
  [[BehaviorValueKey]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Entities/Behaviors/BaseBehaviorStoredValue.cs`

## External references

- TECHNICAL.md: §9.2 Behavior stored values
