---
title: FloatingNumberBehaviorValue
type: class
domain: 05-Entities
status: done
tags: [entities, behavior, stored-value, feedback, floating-number]
---

# FloatingNumberBehaviorValue

> Stored-value payload for floating damage / heal numbers written by
> `EffDealDamage` / `EffHeal` during apply. Read post-resolve by the
> `FloatingDamageSpawner` to emit world-space text on the target's
> pawn. TECHNICAL.md §9.2.

## Shape

```csharp
[Serializable]
public class FloatingNumberBehaviorValue : BaseBehaviorStoredValue {
    public float   Value;             // numero a mostrar
    public Vector3 Offset;            // offset local desde el pawn
    public Guid    TargetEntityGuid;  // a quien pertenece el numero
    public float   Delay;             // delay antes de spawnear
}
```

## Why a struct-like payload

The damage / heal pipeline cant render its own UI — it just resolves
the math. Stashing the rendering-relevant data here lets the feedback
layer pick it up after `Execute` finishes, keeping the resolve loop
free of `MonoBehaviour` dependencies.

## Dependencies

- **Uses:** [[BaseBehaviorStoredValue]] (base), `Vector3`, `Guid`.
- **Used by:** [[BaseBehavior]]`.SetBehaviorValue` keyed by
  [[BehaviorValueKey]] (`FloatingDamage`, `FloatingHeal`,
  `FloatingShield`); `FloatingDamageSpawner` reads it via
  `TryGetBehaviorValues<FloatingNumberBehaviorValue>`.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Entities/Behaviors/BaseBehaviorStoredValue.cs`

## External references

- TECHNICAL.md: §9.2 / §10 Floating numbers feedback
