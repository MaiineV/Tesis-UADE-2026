---
title: DoorDirectionExtensions
type: class
domain: 07-Dungeon
status: done
tags: [dungeon, components, doors]
---

# DoorDirectionExtensions

> Static helpers for [[DoorDirection]] — opposite-direction lookup and
> the canonical `SpawnPointId` key used in
> [[SerializableObjectStates]].

## Overview

Two convenience extensions used across the dungeon layer:

- `Opposite()` — N↔S, E↔W. Used by [[IDungeonService.EnterRoomByDoor]]
  to figure out which side of the destination room the player is
  entering from (and therefore which `DoorSlot.Anchor` to spawn on).
- `DoorStateKey()` — returns the stable string used as the key in
  [[RoomInstance.ObjectStates]] for that door's [[DoorState]]
  (`door_N` / `door_S` / `door_E` / `door_W`).

## API / Shape

```csharp
public static class DoorDirectionExtensions {
    public static DoorDirection Opposite(this DoorDirection dir);
    public static string        DoorStateKey(this DoorDirection dir);
}
```

## Dependencies

**Uses:** [[DoorDirection]].

**Used by:** [[DungeonManager]], [[IDungeonService]],
[[PlayerRoomTransitioner]], [[DoorState]] persistence.

## Code

`Assets/Scripts/Rollgeon/Dungeon/Components/DoorDirectionExtensions.cs`

## External references

- TECHNICAL.md: §13.6 Isaac topology, §13.6.1 Per-object state.
