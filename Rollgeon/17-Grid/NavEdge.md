---
title: NavEdge
type: struct
domain: 17-Grid
status: done
tags: [grid, struct, nav]
---

# NavEdge

> A directed connection between two [[NavNode]]s with a traversal cost.

## Overview
Edges are stored on the [[NavGraph]] as a flat list and re-keyed by
`From` into an adjacency dictionary on demand. Equality only considers
the `From`/`To` pair — `Cost` is metadata for pathfinding weights.

## API / Shape

```csharp
public struct NavEdge : IEquatable<NavEdge> {
    public GridCoord From;
    public GridCoord To;
    public float     Cost;

    public NavEdge(GridCoord from, GridCoord to, float cost = 1f);
}
```

## Dependencies
**Uses:** [[GridCoord]]
**Used by:** [[NavGraph]], [[NavGraphBaker]], [[MovementService]] (via
`NavGraph.GetNeighbors`)

## Code
`Assets/Scripts/Rollgeon/Grid/NavEdge.cs`

## External references
- TECHNICAL.md: §17.§I Grid coordinates
