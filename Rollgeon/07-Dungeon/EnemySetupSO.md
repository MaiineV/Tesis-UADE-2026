---
title: EnemySetupSO
type: so
domain: 07-Dungeon
status: done
tags: [dungeon, enemies, so]
---

# EnemySetupSO

> Hand-authored fixed enemy setup for a room: explicit
> `enemy → spawn point index` assignment. TECHNICAL §13.2 / §13.4.

## Overview

[[RoomSO]]`.PossibleSetups` lists multiple candidate setups; the
generator picks one at random when the room is entered. If the list
is empty, [[DefaultEnemySpawnResolver]] falls back to procedural
[[EnemyPoolSO]] rolls.

## API / Shape

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Dungeon/Enemy Setup")]
public sealed class EnemySetupSO : ScriptableObject {
    public string         SetupName;
    public List<SetupSlot> Slots;

    public bool TryResolve(int spawnPointCount, out List<(int, EnemyDataSO)> mapping);
}
```

## Dependencies
**Uses:** [[SetupSlot]], `EnemyDataSO`.
**Used by:** [[RoomSO]], [[DefaultEnemySpawnResolver]],
[[IEnemySpawnResolver]].

## Code
`Assets/Scripts/Rollgeon/Dungeon/EnemySetupSO.cs`
