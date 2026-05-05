---
title: DoorController
type: behavior
domain: 07-Dungeon
status: done
tags: [dungeon, doors, mono-behaviour]
---

# DoorController

> Runtime `MonoBehaviour` on the door prefab. Owns the open / locked /
> tapiada (walled-off) visual state and toggles the matching child
> meshes (TECHNICAL §13.6).

## Overview

Parented under the room instance by [[DungeonManager]] when the
[[DoorSlot]] in the [[FloorLayoutSO]]'s `RoomLayout` has a neighbour
in that direction. If no neighbour exists, the slot's `WallPlug`
activates instead and no `DoorController` is instantiated.

## API / Shape

```csharp
public sealed class DoorController : MonoBehaviour {
    public Guid           OwnerRoomInstanceId;
    public DoorDirection  Direction;
    public string         SpawnPointId;
    public DoorVisualState CurrentState { get; private set; }

    public void SetState(DoorVisualState state);
}
```

## Dependencies
**Uses:** [[DoorDirection]], [[DoorVisualState]].
**Used by:** [[DungeonManager]], door interactable behaviors.

## Code
`Assets/Scripts/Rollgeon/Dungeon/Components/DoorController.cs`
