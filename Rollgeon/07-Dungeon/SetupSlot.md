---
title: SetupSlot
type: struct
domain: 07-Dungeon
status: done
tags: [dungeon, enemies, struct]
---

# SetupSlot

> One assignment inside an [[EnemySetupSO]]: a specific
> `EnemyDataSO` at a specific `SpawnPointIndex` of the room layout.

## API / Shape

```csharp
[Serializable]
public struct SetupSlot {
    public int          SpawnPointIndex; // index into RoomLayout.EnemySpawnPoints
    public EnemyDataSO  Enemy;
}
```

## Dependencies
**Uses:** `EnemyDataSO`.
**Used by:** [[EnemySetupSO]].

## Code
`Assets/Scripts/Rollgeon/Dungeon/EnemySetupSO.cs`
