---
title: SampleFloorFactory
type: system
domain: 07-Dungeon
status: done
tags: [dungeon, factory, sample, test]
---

# SampleFloorFactory

> Test-friendly factory that builds the canonical Sprint 03 sample
> [[FloorLayoutSO]] (five rooms — combat, combat, shop, potion, combat)
> used by smoke tests and the initial run scaffold.

## Why a factory

- Lets EditMode tests exercise [[DungeonManager]] without an actual
  asset on disk.
- Mirrors the shape that Round 3 manual setup will produce, so if the
  asset pipeline drifts the factory tests catch it.

## Dependencies

- **Uses:** [[FloorLayoutSO]], [[RoomSO]], [[RoomType]],
  [[EnemyPoolSO]], [[EnemyDataSO]] fakes for tests.
- **Used by:** `DungeonManagerTests`, `SampleFloorFactoryTests`, the
  initial run bootstrap if no asset is configured.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Dungeon/SampleFloorFactory.cs`
- Tests: `.../Tests/SampleFloorFactoryTests.cs`

## External references

- Setup: `docs/setup/Content#0011e_SampleFloorLayout.md`
- TECHNICAL.md: §13.1 Sample floor
