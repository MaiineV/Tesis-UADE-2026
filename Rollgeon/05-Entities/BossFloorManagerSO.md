---
title: BossFloorManagerSO
type: so
domain: 05-Entities
status: done
tags: [entities, boss, floor, so]
---

# BossFloorManagerSO

> `ScriptableObject` that groups the boss room's layout, boss spawn, and
> post-victory rewards into a single asset. Referenced by the floor-1
> [[FloorLayoutSO]] at the end-of-floor slot.

## Fields (typical)

- `BossRoom : RoomSO` — the boss encounter room.
- `BossEnemy : EnemyDataSO` — the boss data asset.
- `IntroCutscene : CutsceneDataSO` (stub) — optional intro.
- `VictoryRewards : List<RewardEntrySO>` (TBD stub).

## Why a dedicated SO

Putting boss metadata outside of the ordinary room pool avoids
accidentally sampling a boss into a regular combat slot, and gives a
single hook for per-boss cutscene / reward config later.

## Dependencies

- **Uses:** [[RoomSO]], [[EnemyDataSO]].
- **Used by:** [[FloorLayoutSO]] (boss-floor entries), the boss spawn
  path inside [[CombatHandoffService]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Entities/Bosses/BossFloorManagerSO.cs`
- Tests: `.../Bosses/Tests/BossFloorManagerSOTests.cs`

## External references

- Setup: `docs/setup/Content#0103_BossFloorManager.md`
- TECHNICAL.md: §13.4 Boss floor manager
