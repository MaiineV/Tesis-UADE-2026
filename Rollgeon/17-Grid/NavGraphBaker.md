---
title: NavGraphBaker
type: system
domain: 17-Grid
status: done
tags: [grid, nav, bake]
---

# NavGraphBaker

> Static utility that walks a room `GameObject` hierarchy and emits a
> baked [[NavGraph]] from its child renderers.

## Overview
For every `Renderer` under the room root the baker derives a tile
coordinate from the renderer's local position (`x/z` divided by
`TileSize`, rounded) and stores the local Y as `NavNode.Height`. Then it
pairs every two nodes whose Manhattan distance is 1 and whose height
delta is within `HeightThreshold`, adding bidirectional edges of cost 1.
Used by editor tools to pre-bake rooms; the result is later loaded into
the runtime [[GridManager]].

## API / Shape

```csharp
public static class NavGraphBaker {
    public static NavGraph Bake(GameObject roomRoot, NavGraphBakeSettings settings);
}
```

Returns an empty graph if `roomRoot` or `settings` are null.

## Dependencies
**Uses:** [[NavGraph]], [[NavNode]], [[GridCoord]],
[[NavGraphBakeSettings]]
**Used by:** Editor bake tooling, room authoring scripts.

## Code
`Assets/Scripts/Rollgeon/Grid/NavGraphBaker.cs`

## External references
- TECHNICAL.md: §17.§I Grid coordinates
