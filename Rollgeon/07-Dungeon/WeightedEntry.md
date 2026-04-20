---
title: WeightedEntry
type: concept
domain: 07-Dungeon
status: done
tags: [dungeon, utility, weighted]
---

# WeightedEntry

> Small generic utility: `(T Value, int Weight)`. Used by pools that
> need a weighted sample, mostly [[EnemyPoolSO]].

## Shape

```csharp
[Serializable]
public struct WeightedEntry<T> {
    public T   Value;
    public int Weight;
}
```

Sampling helpers compute a running cumulative sum and do a single RNG
roll against the total. Used inline rather than extracted into a dedicated
class to keep the Unity inspector tree shallow.

## Dependencies

- **Used by:** [[EnemyPoolSO]], future reward / item pools.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Dungeon/WeightedEntry.cs`

## External references

- TECHNICAL.md: §13.3 Weighted sampling
