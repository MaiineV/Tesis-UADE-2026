---
title: DoorSlotRef
type: class
domain: 07-Dungeon
status: done
tags: [dungeon, components, doors]
---

# DoorSlotRef

> Authoring slot on a [[RoomLayout]] that anchors one of the four
> cardinal door positions (TECHNICAL §13.3).

## Overview

Each [[RoomLayout]] exposes 4 slots (N/S/E/W). When [[DungeonManager]]
resolves connectivity, each slot either:

- **Connects** — instantiates [[DoorController]] (`DoorPrefab`) on
  `Anchor` and deactivates `WallPlug`.
- **Doesn't connect** — activates `WallPlug` (sealed wall) and skips
  the door instantiation.

`DoorRoot` is an optional opt-out for prefabs that bake an open-door
mesh directly into the room rather than spawning a `DoorPrefab`.

## API / Shape

```csharp
[Serializable]
public sealed class DoorSlotRef {
    public DoorDirection Direction;
    public Transform     Anchor;    // pose + rotation for the spawned door
    public GameObject    WallPlug;  // sealed wall, active when no neighbour
    public GameObject    DoorRoot;  // optional pre-authored open mesh
}
```

The enum [[DoorDirection]] (`North | South | East | West`) is declared
alongside this class in the same file.

## Dependencies

**Uses:** [[DoorDirection]].

**Used by:** [[RoomLayout]], [[DungeonManager]],
[[PlayerRoomTransitioner]], [[DoorController]].

## Code

`Assets/Scripts/Rollgeon/Dungeon/Components/DoorSlot.cs`

## External references

- TECHNICAL.md: §13.3 Room authoring, §13.6 Isaac topology.
