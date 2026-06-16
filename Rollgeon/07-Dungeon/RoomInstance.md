---
title: RoomInstance
type: class
domain: 07-Dungeon
status: done
tags: [dungeon, runtime, graph]
---

# RoomInstance

> Runtime node of the floor graph — one concrete room with its world
> position, neighbour edges, lifecycle state and persistent per-object
> memory (TECHNICAL §13.6).

## Overview

Lives in `DungeonManager._instances` (Run scope, in-memory). Created
by [[DungeonManager.GenerateFloor]] from a [[RoomSO]] template and
referenced everywhere via [[IDungeonService]]. Acts as the bridge
between authoring data ([[RoomSO]] / [[FloorLayoutSO]]) and the live
state mutated during play.

### Persistence between visits

The `ObjectStates` collection is the load-bearing piece for Isaac-style
re-entry: when the player leaves a room, [[DungeonManager]] snapshots
enemy HP, door flags (forced / unlocked), chests, shop items, etc. into
this dict; when they come back, the snapshot is restored so the room
looks exactly as they left it. The container is
[[SerializableObjectStates]] backed by `[SerializeReference]`, so the
polymorphic [[RoomObjectState]] subtypes survive a JSON / YAML round
trip — a future SaveService can persist runs to disk without a data
migration.

## API / Shape

```csharp
[Serializable]
public sealed class RoomInstance {
    public Guid       InstanceId;
    public RoomSO     Template;
    public GameObject SpawnedPrefab; // null if Template has no RoomPrefab
    public Vector3    WorldPosition;
    public Vector2Int GridCell;

    [NonSerialized] public Dictionary<DoorDirection, Guid> Connections;
    [NonSerialized] public List<Guid> SpawnedEnemies; // cleared on ExitCurrentRoom

    public RoomState State = RoomState.Uncleared;
    public SerializableObjectStates ObjectStates = new();
}
```

`Connections` and `SpawnedEnemies` are `[NonSerialized]`: they're rebuilt
on floor generation / room entry and don't need disk persistence.

## Dependencies

**Uses:** [[RoomSO]], [[RoomState]], [[DoorDirection]],
[[SerializableObjectStates]], [[RoomObjectState]].

**Used by:** [[DungeonManager]], [[IDungeonService]],
[[PlayerRoomTransitioner]], [[RoomGridLoader]],
[[FloorShellVisibilityController]], [[CombatHandoffService]].

## Code

`Assets/Scripts/Rollgeon/Dungeon/RoomInstance.cs`

## External references

- TECHNICAL.md: §13.6 Isaac topology, §13.6.1 Per-object state.
