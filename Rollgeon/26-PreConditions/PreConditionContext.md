---
title: PreConditionContext
type: class
domain: 26-PreConditions
status: done
tags: [preconditions, context]
---

# PreConditionContext

> Plain data carrier handed to every [[BasePreCondition]]`.Evaluate`. Holds
> the owner / opponent guids and a typed entity reference so concrete PCs
> can read runtime state without re-querying the world.

## Overview

Built by the caller (typically the [[EffectData]] dispatcher or a
behavior's `Execute` setup) right before evaluating the AND-fold of
preconditions. The shape is intentionally minimal — only fields the
initial PC catalog actually consults — and is extended additively as new
PCs land (non-breaking).

## Shape

```csharp
public class PreConditionContext {
    public Guid   OwnerGuid;     // entity owning the effect
    public Guid   OpponentGuid;  // attacker / defender / combo partner
    public Entity Entity;        // typed snapshot of the owner
}
```

## Dependencies

**Uses:** `Rollgeon.Entities.Entity`
**Used by:** [[BasePreCondition]] and every concrete `PC*`
([[PCComboAvailable]], [[PCCurrentPhase]], [[PCEntityInRange]],
[[PCFirstRollOfCombat]], [[PCHasIntAttribute]], [[PCHasModifier]],
[[PCAdjacentToDoor]], [[PCHasInventoryItem]]); built by [[EffectData]]
during pipeline dispatch.

## Code

`Assets/Scripts/Rollgeon/PreConditions/PreConditionContext.cs`

## External references

- TECHNICAL.md: §8.2 PreConditions
