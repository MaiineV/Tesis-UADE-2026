---
title: RoomObjectState
type: class
domain: 07-Dungeon
status: done
tags: [dungeon, state]
---

# RoomObjectState

> Abstract base for serialisable per-object state inside a room
> (TECHNICAL §13.6.1). Concrete subtypes ([[DoorState]],
> [[EnemySpawnState]], [[ChestState]], [[PotionState]],
> [[ShopItemState]]) live inside `SerializableObjectStates` with
> `[SerializeReference]` to preserve subtype on round-trip.

## Overview

Each entry is keyed by a `SpawnPointId` (e.g. `door_N`,
`spawn_enemy_3`). The base also carries a `Consumed` flag so consumers
that don't need richer state can still mark an object as "used".
Concrete stub subtypes (`ChestState`, `PotionState`, `ShopItemState`)
exist to close the hierarchy and avoid data migrations when their
consumers ship.

## API / Shape

```csharp
[Serializable]
public abstract class RoomObjectState {
    public string SpawnPointId;
    public bool   Consumed;
}
```

## Dependencies
**Used by:** `SerializableObjectStates`, [[RoomInstance]],
[[DoorState]], [[EnemySpawnState]], [[ChestState]], [[PotionState]],
[[ShopItemState]].

## Code
`Assets/Scripts/Rollgeon/Dungeon/State/RoomObjectState.cs`
