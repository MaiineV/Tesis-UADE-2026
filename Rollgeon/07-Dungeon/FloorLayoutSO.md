---
title: FloorLayoutSO
type: so
domain: 07-Dungeon
status: done
tags: [dungeon, so, layout, floor]
---

# FloorLayoutSO

> `ScriptableObject` describing a single floor's room sequence. Referenced
> by [[DungeonManager]] when the run enters the floor.

## Shape (typical)

- `FloorId : string`
- `DisplayName : string`
- `Rooms : List<RoomSO>` — ordered list (or a weighted / procedural
  template — see sub-entries).
- `BossManager : BossFloorManagerSO` — the end-of-floor boss.

## Relationship to rooms

Sprint 03 Sample floor ([[SampleFloorFactory]]) produces a simple
combat → shop → combat → boss layout. Later floors can carry richer
templates (branching, optional rooms, density).

## Dependencies

- **Uses:** [[RoomSO]], [[BossFloorManagerSO]].
- **Used by:** [[DungeonManager]], [[RunController]]'s default layout
  field.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Dungeon/FloorLayoutSO.cs`
- Sample factory: `.../SampleFloorFactory.cs`

## External references

- Setup: `docs/setup/System#0011a_RoomAndFloorLayoutSO.md`,
  `docs/setup/Content#0011e_SampleFloorLayout.md`
- TECHNICAL.md: §13.1 Floor layout
