---
title: GridSnapshot
type: struct
domain: 17-Grid
status: done
tags: [grid, struct, serialization]
---

# GridSnapshot

> Serializable description of a room's walkable tile layout — bake-time
> input that the runtime turns into a [[NavGraph]].

## Overview
Unity does not serialize `bool[,]` directly, so the snapshot persists a
flat `bool[]` plus `Width` and `Height`. Authoring tools fill it from
the room prefab; at runtime [[NavGraph]] consumes it via `FromSnapshot`.
An empty snapshot (`Empty`) means "no layout authored yet" and consumers
treat the whole map as walkable — handy for tests and unfinished rooms.

## API / Shape

```csharp
public struct GridSnapshot {
    public int Width  { get; }
    public int Height { get; }
    public bool IsEmpty { get; }
    public static GridSnapshot Empty { get; }

    public GridSnapshot(int width, int height, bool[] walkable);
    public static GridSnapshot Rect(int width, int height); // all-walkable

    public bool InBounds(GridCoord c);
    public bool IsWalkable(GridCoord c);
    public IEnumerable<GridCoord> AllCoords();
}
```

The constructor validates that `walkable.Length == width * height`.

## Dependencies
**Uses:** [[GridCoord]]
**Used by:** [[NavGraph]] (via `NavGraph.FromSnapshot` / `NavGraph.Rect`),
room authoring tools.

## Code
`Assets/Scripts/Rollgeon/Grid/GridSnapshot.cs`

## External references
- TECHNICAL.md: §13.3 Room layout, §17.§I Grid coordinates
