---
title: RoomSO
type: so
domain: 07-Dungeon
status: done
tags: [dungeon, so, room]
---

# RoomSO

> `ScriptableObject` describing a single dungeon room: id, type, enemy
> pool (for combat rooms), interaction blueprint, and display metadata.

## Shape (typical)

- `RoomId : string` — canonical id.
- `DisplayName : string` — tooltip / header text.
- `Type : RoomType` — `Combat | Shop | Potion | Boss | Rest`…
- `EnemyPool : EnemyPoolSO` — weighted pool sampled at combat start.
- `Interactions : List<ScriptableObject>` — optional interactables for
  exploration.

## Combat → handoff

When the player enters a combat room, [[ExplorationController]]
triggers [[EventName]] `OnCombatTriggered(roomInstanceId, roomId,
RoomType)`. [[CombatHandoffService]] listens and calls
[[DefaultEnemySpawnResolver]] with this room's `EnemyPool`.

## Dependencies

- **Uses:** [[RoomType]], [[EnemyPoolSO]].
- **Used by:** [[FloorLayoutSO]], [[DungeonManager]],
  [[CombatHandoffService]], interaction layer.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Dungeon/RoomSO.cs`

## External references

- Setup: `docs/setup/System#0011a_RoomAndFloorLayoutSO.md`
- TECHNICAL.md: §13.1 RoomSO
