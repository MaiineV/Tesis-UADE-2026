---
title: Dungeon-MOC
type: moc
domain: 90-MOCs
status: done
tags: [moc, dungeon, exploration]
---

# 07-Dungeon — Map of Content

> Floor layouts, rooms, enemy pools, the dungeon manager, and the
> exploration controller that drives the loop between combats.

## Relationships

```
 FloorLayoutSO
    ├─ Rooms : List<RoomSO>
    │            ├─ Type (Combat / Boss / Shop / Potion / Rest)
    │            └─ EnemyPool : EnemyPoolSO  (WeightedEntry<EnemyDataSO>)
    └─ BossManager : BossFloorManagerSO

 DungeonManager (Run-scoped)
    ├─ CurrentRoom / CurrentRoomIndex
    ├─ AdvanceToNextRoom / MarkCurrentRoomCleared
    └─ uses FloorLayoutSO + seed RNG

 ExplorationController
    ├─ BeginExploration → push ExplorationHUDView
    ├─ EnterRoom(roomInstanceId) → fires OnCombatTriggered for combat rooms
    └─ RequestFloorExit → FloorExitInteractable → advance floor
```

## Notes

- **Layouts & rooms:** [[FloorLayoutSO]] · [[RoomSO]] · [[RoomType]] ·
  [[SampleFloorFactory]]
- **Pools:** [[EnemyPoolSO]] · [[WeightedEntry]]
- **Services & interactables:** [[DungeonManager]] ·
  [[ExplorationController]] · [[FloorExitInteractable]]

## Cross-domain edges

- Consumed by [[CombatHandoffService]] (see [[Combat-MOC]]) on combat
  triggers.
- [[MinimapView]] / [[RoomNavigationView]] render its state (see
  [[UI-MOC]]).
- [[BossFloorManagerSO]] in [[Entities-MOC]] packs the boss encounter.
