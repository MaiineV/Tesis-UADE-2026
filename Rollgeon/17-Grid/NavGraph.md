---
title: NavGraph
type: class
domain: 17-Grid
status: done
tags: [grid, nav]
---

# NavGraph

> Navigation graph over the active room — nodes are walkable tiles,
> edges are weighted connections.

## Overview
Serializable as flat `List<NavNode>` and `List<NavEdge>`. Lazy-builds a
`Coord → NavNode` and `Coord → adjacency` lookup on first query and
invalidates them via a `_dirty` flag whenever the graph mutates. Empty
graphs are treated as "open world" — `HasNode` returns true and
`GetNeighbors` yields synthetic 4-neighbour edges so tests and unauthored
rooms still work.

## API / Shape

```csharp
public class NavGraph {
    public bool IsEmpty   { get; }
    public int  NodeCount { get; }
    public int  Width     { get; }
    public int  Height    { get; }
    public IReadOnlyList<NavNode> Nodes { get; }
    public IReadOnlyList<NavEdge> Edges { get; }

    public bool HasNode(GridCoord);
    public bool TryGetNode(GridCoord, out NavNode);
    public IEnumerable<NavEdge> GetNeighbors(GridCoord);
    public bool  HasEdge(GridCoord, GridCoord);
    public float GetEdgeCost(GridCoord, GridCoord);
    public bool  InBounds(GridCoord);
    public IEnumerable<GridCoord> AllCoords();

    public void AddNode(NavNode);
    public void AddEdge(NavEdge);
    public void AddBidirectionalEdge(GridCoord, GridCoord, float cost = 1f);
    public void RemoveEdge(GridCoord, GridCoord);
    public void RemoveBidirectionalEdge(GridCoord, GridCoord);
    public void Clear();

    public static NavGraph FromSnapshot(GridSnapshot);
    public static NavGraph Rect(int width, int height);
}
```

## Dependencies
**Uses:** [[GridCoord]], [[NavNode]], [[NavEdge]], [[GridSnapshot]]
**Used by:** [[IGridManager]], [[GridManager]], [[NavGraphBaker]],
[[MovementService]]

## Code
`Assets/Scripts/Rollgeon/Grid/NavGraph.cs`

## External references
- TECHNICAL.md: §17.§I Grid coordinates
