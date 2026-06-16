---
title: TileMarker
type: class
domain: 17-Grid
status: done
tags: [grid, monobehaviour]
---

# TileMarker

> `MonoBehaviour` placed on each tile mesh in a room prefab so the
> runtime can map a `Renderer` to its [[GridCoord]].

## Overview
Authoring component: artists drop it on every tile renderer. At room
load [[TileRendererRegistrar]] reads the marker's transform position,
asks [[IGridManager]] for the matching coord, writes it back into
`Coord`, and registers the renderer with [[ITileHighlightService]].
[[TileClickHandler]] also grabs the marker via `GetComponentInParent`
on raycast hits to resolve the click → coord without hitting the grid
math again.

## API / Shape

```csharp
[AddComponentMenu("Rollgeon/Grid/Tile Marker")]
public sealed class TileMarker : MonoBehaviour {
    [HideInInspector] public GridCoord Coord;
}
```

## Dependencies
**Uses:** [[GridCoord]]
**Used by:** [[TileRendererRegistrar]], [[TileClickHandler]]

## Code
`Assets/Scripts/Rollgeon/Grid/TileMarker.cs`

## External references
- TECHNICAL.md: §17.§I Grid coordinates
