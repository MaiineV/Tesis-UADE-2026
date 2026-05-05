---
title: GridCoord
type: struct
domain: 17-Grid
status: done
tags: [grid, struct]
---

# GridCoord

> Integer (x, y) coordinate that addresses a tile in the combat grid.

## Overview
Lightweight, serializable, value-equatable struct used everywhere the
grid is involved (occupancy maps, pathfinding, AI ranges, selection).
Implements `IEquatable<GridCoord>` so `Dictionary<GridCoord, Guid>` and
`HashSet<GridCoord>` lookups stay allocation-free.

## API / Shape

```csharp
public struct GridCoord : IEquatable<GridCoord> {
    public int X;
    public int Y;
    public static GridCoord Zero { get; }

    public IEnumerable<GridCoord> Neighbors4();
    public int Manhattan(GridCoord other);
    public int Chebyshev(GridCoord other);

    public static GridCoord operator +(GridCoord a, GridCoord b);
    public static GridCoord operator -(GridCoord a, GridCoord b);
}
```

`Manhattan` is the default range metric for 4-grid movement;
`Chebyshev` covers the octogonal alternative.

## Dependencies
**Uses:** —
**Used by:** [[GridSnapshot]], [[NavNode]], [[NavEdge]], [[NavGraph]],
[[NavGraphBaker]], [[IGridManager]], [[GridManager]],
[[ITileHighlightService]], [[TileHighlightService]], [[TileMarker]],
[[TileClickHandler]], [[TileRendererRegistrar]], [[IMovementService]],
[[MovementService]], `TargetRef`, AI conditions, exploration behaviors.

## Code
`Assets/Scripts/Rollgeon/Grid/GridCoord.cs`

## External references
- TECHNICAL.md: §17.§I Grid coordinates
