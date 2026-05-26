---
title: EnemySpawnState
type: class
domain: 07-Dungeon
status: done
tags: [dungeon, state, enemies]
---

# EnemySpawnState

> [[RoomObjectState]] subtype that tracks the runtime status of a
> single enemy spawn point: which `EnemyDataSO` was rolled, current
> HP, alive/dead, and the spawn point index.

## Overview

Lets a room re-instantiate a previously-visited encounter with its
exact HP / alive state, picking from random free spawn points. Dead
enemies are not re-spawned.

## API / Shape

```csharp
[Serializable]
public class EnemySpawnState : RoomObjectState {
    public string EnemyDataSOId;
    public int    CurrentHP;
    public bool   IsDead;
    public int    SpawnPointIndex;
}
```

## Dependencies
**Uses:** [[RoomObjectState]], `BaseEntitySO.EntityId`.
**Used by:** [[DefaultEnemySpawnResolver]], [[RoomInstance]].

## Code
`Assets/Scripts/Rollgeon/Dungeon/State/RoomObjectState.cs`
