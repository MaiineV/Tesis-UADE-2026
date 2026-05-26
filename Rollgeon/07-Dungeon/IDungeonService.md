---
title: IDungeonService
type: interface
domain: 07-Dungeon
status: done
tags: [dungeon, service, interface]
---

# IDungeonService

> Read-mostly facade over the runtime floor graph. Owns the active
> [[RoomInstance]], its [[FloorShell]] metadata, and door-based
> navigation in Isaac topology (TECHNICAL §13.6).

## Overview

Registered in [[ServiceScope]] `Run` by [[DungeonManager.CreateAndRegister]].
Most consumers in the run touch the dungeon only through this interface
— it is the central facade for any system that needs "what room is the
player in, what's connected, can they move". The 2026-04-22 rewrite
swapped the legacy linear sequence (`NextRoom`, `CurrentRoomIndex`,
`IsLastRoom`, `RoomCount`, `GetFloorRooms`) for the door-based
[[EnterRoomByDoor]] + the `Guid`-keyed graph from
[[GetAllRoomInstances]] / [[GetFloorShells]].

## API / Shape

```csharp
public interface IDungeonService {
    RoomSO        CurrentRoom         { get; } // == CurrentRoomInstance?.Template
    RoomInstance  CurrentRoomInstance { get; } // null pre-GenerateFloor
    DoorDirection? LastEntryDirection { get; } // null on initial spawn / teleport

    void GenerateFloor(FloorLayoutSO layout, int seed);

    IReadOnlyDictionary<Guid, RoomInstance> GetAllRoomInstances();
    IReadOnlyDictionary<Guid, FloorShell>   GetFloorShells();

    bool CanEnterRoomByDoor(DoorDirection direction, out Guid neighborInstanceId);
    bool EnterRoomByDoor(DoorDirection direction);
    bool EnterRoomByInstanceId(Guid instanceId); // debug / minimap teleport

    Bounds                       GetFloorBounds();           // for camera clamp
    IReadOnlyList<WallOccluder>  GetCurrentRoomOccluders();  // for camera occlusion
}
```

The nested [[FloorShell]] struct (`InstanceId`, `WorldPosition`, `Size`)
is co-declared in this file and consumed by the floor view (§17.E.9).

## Behaviour notes

- **Door gating** — `CanEnterRoomByDoor` requires both a connected
  neighbour AND the current room being [[RoomState|Cleared]] OR the door
  in [[DoorState|Forced]]. Combat locks all doors (Isaac-style).
- **State persistence** — `EnterRoomByDoor` snapshots the current room's
  enemy HP and door flags into [[RoomInstance.ObjectStates]] before
  swapping, then restores the destination's snapshot if it was visited.
- **Direct teleport** — `EnterRoomByInstanceId` skips connectivity /
  lock checks; intended for minimap clicks and debug tooling.

## Dependencies

**Uses:** [[RoomSO]], [[RoomInstance]], [[FloorShell]],
[[DoorDirection]], [[FloorLayoutSO]], [[WallOccluder]].

**Used by:** [[DungeonManager]] (impl), [[ExplorationController]],
[[ShopManagerService]], [[GridManager]], [[CameraService]],
[[CombatHandoffService]], [[FloorShellVisibilityController]],
[[PlayerRoomTransitioner]], [[RoomGridLoader]].

## Code

`Assets/Scripts/Rollgeon/Dungeon/IDungeonService.cs`

## External references

- TECHNICAL.md: §13.6 Isaac dungeon topology, §17.E camera floor view.
