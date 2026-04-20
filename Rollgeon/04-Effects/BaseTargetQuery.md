---
title: BaseTargetQuery
type: system
domain: 04-Effects
status: done
tags: [effects, selection, target]
---

# BaseTargetQuery

> Abstract base for queries that resolve a set of targets given a
> [[ReadInfo]] (owner, phase, combat state). Concrete queries live in
> `Effects/Selection/Queries/`.

## Shape

```csharp
public abstract class BaseTargetQuery {
    public EntityFilterMask Filter;    // include/exclude allies/enemies/self
    public abstract IReadOnlyList<TargetRef> Query(ReadInfo info);
}
```

## Concretes shipped

- [[TQ_AllEnemies]] — every enemy entity currently registered.
- [[TQ_Self]] — the owner entity.

## Dependencies

- **Uses:** [[EntityFilterMask]], [[ReadInfo]], [[TargetRef]].
- **Used by:** [[SelectionSettings]] (as the source of auto-selection
  in effects that don't require player input), AI ability planners.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Effects/Selection/BaseTargetQuery.cs`

## External references

- TECHNICAL.md: §11 Selection / targeting
