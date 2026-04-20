---
title: BehaviorTrigger
type: concept
domain: 05-Entities
status: done
tags: [entities, behavior, enum]
---

# BehaviorTrigger

> Enum of events that wake a [[BaseBehavior]]. Gated by the behavior's
> [[GamePhaseMask]] before execution.

## Shape

```csharp
public enum BehaviorTrigger {
    OnTurnStart,
    OnTurnEnd,
    OnHit,            // when carrier takes damage
    OnComboMatched,   // when a combo fires in combat
    OnSpawn,
    OnDeath,
    // … domain-specific extensions added as needed
}
```

The behavior dispatcher subscribes to the appropriate event in its
owner's lifecycle and calls `BaseBehavior.Execute` when both the
trigger and the phase mask allow it.

## Dependencies

- **Used by:** [[BaseBehavior]] and every concrete
  (SupportHealBehavior, Boss*, …).

## Code

- Runtime: `Assets/Scripts/Rollgeon/Entities/Behaviors/BehaviorTrigger.cs`

## External references

- TECHNICAL.md: §7.2 Behavior triggers
