---
title: RoomGridLoader
type: service
domain: 07-Dungeon
status: done
tags: [dungeon, grid]
---

# RoomGridLoader

> Run-scope listener that pushes the active room's nav graph into the
> [[IGridManager]] every time the player enters a room
> (TECHNICAL §17.§I + §13.6).

## Overview

On construction subscribes to [[EventName|OnRoomEntered]] and runs an
initial `LoadCurrent` so the very first room is loaded without waiting
for an event. Each call:

1. Resolves [[IDungeonService]] (explicit dep first, falls back to
   [[ServiceLocator]] for production wiring).
2. Reads the [[RoomLayout]] off [[RoomInstance.SpawnedPrefab]] and calls
   `IGridManager.LoadRoom(NavGraph, origin, tileSize)`.
3. If the room has no prefab or no layout (EditMode tests), loads an
   empty `NavGraph` so the grid stays in a consistent state.
4. Asks [[ICameraService]] for an instant recenter to avoid a long
   smooth pan between rooms (§17.E.10).

If `IDungeonService` isn't registered yet (no active run), the loader
no-ops and waits for the next event inside a run.

## API / Shape

```csharp
public sealed class RoomGridLoader : IDisposable {
    public RoomGridLoader(IGridManager grid, IDungeonService dungeon = null);
    public void Dispose();
}
```

The `dungeon` parameter is for tests / manual wiring; production passes
`null`.

## Dependencies

**Uses:** [[IGridManager]], [[IDungeonService]], [[RoomLayout]],
[[NavGraph]], [[ICameraService]], [[EventManager]], [[EventName]],
[[ServiceLocator]].

**Used by:** Run wiring (bootstrapped by `RoomGridLoaderBootstrap`).

## Code

`Assets/Scripts/Rollgeon/Dungeon/RoomGridLoader.cs`

## External references

- TECHNICAL.md: §17.§I Grid loading, §13.6 Isaac topology, §17.E.10
  Camera recenter.
