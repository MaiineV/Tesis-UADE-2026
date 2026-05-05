---
title: TileRendererRegistrar
type: system
domain: 17-Grid
status: done
tags: [grid, highlight, event-driven]
---

# TileRendererRegistrar

> Subscribes to `OnRoomEntered` and wires every [[TileMarker]] in the
> spawned room prefab into [[ITileHighlightService]].

## Overview
Plain C# class instantiated by [[TileRendererRegistrarBootstrap]]. On
each `OnRoomEntered` event it:

1. Resolves [[ITileHighlightService]], `IDungeonService`, and
   [[IGridManager]] from `ServiceLocator` (logging warnings if any are
   missing).
2. Calls `highlight.UnregisterAll()` to wipe the previous room's tiles.
3. Walks the spawned prefab for `TileMarker` components, computes each
   marker's `Coord` via `WorldToGrid`, and registers its `Renderer`.

Logs a per-call summary (`found N markers, registered M renderers`) to
make staging issues visible.

## API / Shape

```csharp
public sealed class TileRendererRegistrar {
    public TileRendererRegistrar();    // subscribes to OnRoomEntered
    public void Dispose();             // unsubscribes
}
```

## Dependencies
**Uses:** [[GridCoord]], [[TileMarker]], [[ITileHighlightService]],
[[IGridManager]], `IDungeonService`, `RoomLayout`, `ServiceLocator`,
`EventManager`, `EventName.OnRoomEntered`
**Used by:** [[TileRendererRegistrarBootstrap]]

## Code
`Assets/Scripts/Rollgeon/Grid/TileRendererRegistrar.cs`

## External references
- TECHNICAL.md: §17.§I Grid coordinates
