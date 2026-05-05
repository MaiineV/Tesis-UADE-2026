---
title: DistanceMetric
type: enum
domain: 26-PreConditions
status: done
tags: [preconditions, enum, grid, distance]
---

# DistanceMetric

> Selects the grid distance function used by [[PCEntityInRange]] (and
> any future range-gated PC). `Manhattan` for 4-connected grids,
> `Chebyshev` for 8-connected.

## Shape

```csharp
public enum DistanceMetric {
    Manhattan = 0, // |dx| + |dy|       — 4-grid
    Chebyshev = 1, // max(|dx|, |dy|)   — 8-grid
}
```

## Dependencies

**Used by:** [[PCEntityInRange]]

## Code

`Assets/Scripts/Rollgeon/PreConditions/Concretes/PCEntityInRange.cs`
