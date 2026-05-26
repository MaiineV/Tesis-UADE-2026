---
title: BehaviorContext
type: class
domain: 05-Entities
status: done
tags: [entities, behavior, abstract, context]
---

# BehaviorContext

> Polymorphic base for trigger contexts that feed into a
> [[BaseBehavior]]'s `Execute`. TECHNICAL.md §7.3.

## Overview

Carries the entity that owns the behavior (`SourceEntity`) and the
entity that triggered it (`TriggeringEntity`, e.g. the attacker on
`OnHit`). Concrete subtypes extend with trigger-specific payload —
`HeroBehaviorContext`, `DamageBehaviorContext`, `TurnBehaviorContext`,
etc. — without changing the `BaseBehavior.Execute` signature.

## API / Shape

```csharp
public abstract class BehaviorContext {
    public Entity SourceEntity;
    public Entity TriggeringEntity;
}
```

## Dependencies

- **Uses:** `Entity`.
- **Used by:** [[BaseBehavior]]`.Execute(BehaviorContext ctx)`,
  [[BaseBehavior]]`.CanExecute(BehaviorContext ctx)`, behavior
  dispatcher.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Entities/Behaviors/BehaviorContext.cs`

## External references

- TECHNICAL.md: §7.3 Behavior context
