---
title: RoomLayout
type: class
domain: 07-Dungeon
status: done
tags: [dungeon, components, mono-behaviour, authoring]
---

# RoomLayout

> `MonoBehaviour` on a room prefab that describes, in prefab-local
> space, the spawn points, the four door slots and the bounding box
> consumed by gameplay + camera systems (TECHNICAL §13.3).

## Overview

The authoring contract between content authors and runtime services.
[[DungeonManager]] and [[IDungeonService]] read this component off
[[RoomInstance.SpawnedPrefab]] when materialising a room, and
[[PlayerRoomTransitioner]] / [[RoomGridLoader]] / [[CameraService]] all
pull their per-room data from here.

Edit-only helper `AutoPopulateDoorSlots()` walks every child
[[DoorController]], infers its cardinal direction from local position,
and fills `DoorSlots` (with anchor + wall plug references). `OnValidate`
recomputes `LocalBounds` from child renderers so the field stays in
sync with the visuals.

## API / Shape

```csharp
public sealed class RoomLayout : MonoBehaviour {
    // Grid
    public Transform GridOrigin;             // tile (0,0) — defaults to transform.position
    public float     TileSize = 1f;
    public NavGraph  NavGraph;
    public NavGraphBakeSettings BakeSettings;

    // Spawn points
    public Transform        PlayerSpawnPoint;
    public List<Transform>  EnemySpawnPoints;
    public List<Transform>  RewardSpawnPoints;
    public List<Transform>  ObstacleSpawnPoints;

    // Doors
    public List<DoorSlotRef> DoorSlots; // 4 slots N/S/E/W

    // Bounds
    public Bounds LocalBounds;          // recomputed in OnValidate

    public Vector3      GetOrigin();                     // GridOrigin ?? transform.position
    public DoorSlotRef  GetDoorSlot(DoorDirection dir);  // null if not authored
#if UNITY_EDITOR
    public void         AutoPopulateDoorSlots();
#endif
}
```

## Dependencies

**Uses:** [[DoorSlotRef]], [[DoorDirection]], [[NavGraph]],
[[NavGraphBakeSettings]], [[DoorController]] (editor helper).

**Used by:** [[DungeonManager]], [[IDungeonService]],
[[RoomGridLoader]], [[PlayerRoomTransitioner]], [[CameraService]],
[[CombatHandoffService]].

## Code

`Assets/Scripts/Rollgeon/Dungeon/Components/RoomLayout.cs`

## External references

- TECHNICAL.md: §13.3 Room authoring, §17.E Camera floor view.
