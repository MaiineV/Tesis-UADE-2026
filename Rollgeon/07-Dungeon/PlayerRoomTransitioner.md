---
title: PlayerRoomTransitioner
type: service
domain: 07-Dungeon
status: done
tags: [dungeon, player, grid]
---

# PlayerRoomTransitioner

> Run-scope listener that places the player on the correct grid tile
> every time a new room is entered.

## Overview

Subscribed to [[EventName|OnRoomEntered]]. When fired, it pulls
[[IDungeonService.CurrentRoomInstance]], grabs the [[RoomLayout]] off
the spawned prefab, and resolves a grid coord via:

1. If [[IDungeonService.LastEntryDirection]] has a value, the
   [[DoorSlotRef.Anchor]] for that direction (player walked through a
   door from the opposite side).
2. Otherwise (initial spawn or `EnterRoomByInstanceId` teleport),
   [[RoomLayout.PlayerSpawnPoint]].
3. Falls back to `GridCoord.Zero` if neither is configured.

Then registers the player on [[IGridManager]] and snaps the
[[IEntityVisualService]] pawn — both are needed so combat and visuals
agree on the new tile immediately, no smooth.

Priority 82 (boots after [[RoomGridLoader]] @ 80) so the grid is loaded
before the player is registered onto it.

## API / Shape

```csharp
public sealed class PlayerRoomTransitioner : IDisposable {
    public PlayerRoomTransitioner(IGridManager grid, IPlayerService player);
    public void Dispose();
}
```

## Dependencies

**Uses:** [[IGridManager]], [[IPlayerService]], [[IDungeonService]],
[[IEntityVisualService]], [[RoomLayout]], [[DoorSlotRef]],
[[EventManager]], [[EventName]].

**Used by:** Run wiring (bootstrapped by `PlayerRoomTransitionerBootstrap`).

## Code

`Assets/Scripts/Rollgeon/Dungeon/PlayerRoomTransitioner.cs`

## External references

- TECHNICAL.md: §13.6 Isaac topology, §17.§I Grid loading.
