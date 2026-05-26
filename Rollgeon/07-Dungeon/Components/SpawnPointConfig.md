---
title: SpawnPointConfig
type: class
domain: 07-Dungeon
status: done
tags: [dungeon, components, mono-behaviour, spawning]
---

# SpawnPointConfig

> Per-spawn-point enemy roster on a [[RoomLayout]]. Each list entry is
> one enemy "set"; all spawn points in a room use the same set index
> chosen at room start.

## Overview

Sits alongside [[SpawnPoint]] on enemy spawn transforms. The combat
handoff picks a single set index per encounter and queries every spawn
point with that index, so authoring three spawns × three sets gives
three distinct encounter compositions in the same room without
duplicating the prefab.

## API / Shape

```csharp
public sealed class SpawnPointConfig : MonoBehaviour {
    public List<EnemyDataSO> EnemySets;

    public int          SetCount { get; }
    public EnemyDataSO  GetEnemyForSet(int setIndex); // null on out-of-range
}
```

## Dependencies

**Uses:** [[EnemyDataSO]].

**Used by:** [[CombatHandoffService]], [[RoomLayout]], [[SpawnPoint]],
[[DefaultEnemySpawnResolver]].

## Code

`Assets/Scripts/Rollgeon/Dungeon/Components/SpawnPointConfig.cs`

## External references

- TECHNICAL.md: §13.3 Room authoring, §13.4 Combat handoff.
