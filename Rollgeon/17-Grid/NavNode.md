---
title: NavNode
type: struct
domain: 17-Grid
status: done
tags: [grid, struct, nav]
---

# NavNode

> A single walkable tile in a [[NavGraph]] — its `GridCoord` plus the
> Y-height it was baked from.

## Overview
Identity is purely positional: two nodes with the same `Coord` are equal
regardless of `Height`. Height is metadata used by [[NavGraphBaker]] to
decide whether two adjacent tiles can be connected (drop / step
threshold).

## API / Shape

```csharp
public struct NavNode : IEquatable<NavNode> {
    public GridCoord Coord;
    public float     Height;

    public NavNode(GridCoord coord, float height = 0f);
}
```

## Dependencies
**Uses:** [[GridCoord]]
**Used by:** [[NavGraph]], [[NavGraphBaker]]

## Code
`Assets/Scripts/Rollgeon/Grid/NavNode.cs`

## External references
- TECHNICAL.md: §17.§I Grid coordinates
