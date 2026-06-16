---
title: ImpulseBehaviorValue
type: class
domain: 05-Entities
status: done
tags: [entities, behavior, stored-value, feedback, impulse]
---

# ImpulseBehaviorValue

> Stored-value payload for knockback / impulse vectors written by
> `EffApplyImpulse`. Consumed by camera shake magnitude, hitstop
> scaling and (later) the physics / animation layer that actually
> moves the target pawn. TECHNICAL.md §9.2.

## Shape

```csharp
[Serializable]
public class ImpulseBehaviorValue : BaseBehaviorStoredValue {
    public Vector3 Impulse;
    public Guid    TargetEntityGuid;
}
```

## Dependencies

- **Uses:** [[BaseBehaviorStoredValue]] (base), `Vector3`, `Guid`.
- **Used by:** [[BaseBehavior]]`.SetBehaviorValue` keyed by
  [[BehaviorValueKey]] (`HitImpulse`); feedback consumers
  (camera shake, hitstop), eventual physics reaction layer.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Entities/Behaviors/BaseBehaviorStoredValue.cs`

## External references

- TECHNICAL.md: §9.2 / §10 Impulse feedback
