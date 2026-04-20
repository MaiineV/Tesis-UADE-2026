---
title: DungeonManager
type: service
domain: 07-Dungeon
status: done
tags: [dungeon, service, manager]
---

# DungeonManager

> Run-scoped service that owns the active [[FloorLayoutSO]], the current
> room pointer, and the RNG seed that drives room selection / enemy
> pool sampling.

## API (typical)

```csharp
public sealed class DungeonManager : IDungeonService, IDisposable {
    public RoomSO CurrentRoom { get; }
    public int    CurrentRoomIndex { get; }

    public static DungeonManager CreateAndRegister(FloorLayoutSO layout, int seed);
    public void AdvanceToNextRoom();    // triggers OnRoomChanged
    public void MarkCurrentRoomCleared();
}
```

## Lifecycle

- Registered in [[ServiceScope]] `Run` by [[RunController]] during
  `OnRunStart`.
- Seeds an internal `System.Random` from the run id + override so the
  same run is reproducible.
- On `OnFloorChanged`, advances to the next [[FloorLayoutSO]].

## Dependencies

- **Uses:** [[FloorLayoutSO]], [[RoomSO]], [[RoomType]],
  [[EnemyPoolSO]], [[EventManager]], [[EventName]].
- **Used by:** [[ExplorationController]], [[CombatHandoffService]],
  [[CombatReturnService]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Dungeon/DungeonManager.cs`
- Interface: `.../IDungeonService.cs`
- Tests: `.../Tests/DungeonManagerTests.cs`

## External references

- Setup: `docs/setup/System#0011b_DungeonManager.md`
- TECHNICAL.md: §13.2 DungeonManager
