---
title: Dungeon-MOC
type: moc
domain: 90-MOCs
status: done
tags: [moc, dungeon, exploration]
---

# 07-Dungeon — Map of Content

> Floor layouts, rooms, enemy pools, the dungeon manager, and the
> physical room shells / spawn points that exploration drives between
> combats.

## Relationships

```
 FloorLayoutSO
    ├─ Rooms : List<RoomSO>
    │            ├─ Type (Combat / Boss / Shop / Potion / Rest)
    │            └─ EnemyPool : EnemyPoolSO  (WeightedEntry<EnemyDataSO>)
    └─ BossManager : BossFloorManagerSO

 DungeonManager : IDungeonService (Run-scoped)
    ├─ CurrentRoom / CurrentRoomIndex
    ├─ AdvanceToNextRoom / MarkCurrentRoomCleared
    └─ uses FloorLayoutSO + seed RNG

 RoomInstance (runtime)
    ├─ RoomState ─ SerializableObjectStates of RoomObjectState:
    │                DoorState, EnemySpawnState, ChestState,
    │                PotionState, ShopItemState
    ├─ RoomLayout (authored)
    └─ FloorShell + FloorShellVisibilityController

 PlayerRoomTransitioner ─ orchestrates room → room movement
 RoomGridLoader         ─ instantiates a room into the grid

 EnemySetupSO + SetupSlot ─ per-room enemy placement spec

 Components: DoorController · DoorSlotRef · DoorDirection
             DoorDirectionExtensions · DoorVisualState
             SpawnPoint · SpawnPointConfig · SpawnKind
```

## Notes

- **Layouts & rooms:** [[FloorLayoutSO]] · [[RoomSO]] · [[RoomType]] ·
  [[RoomInstance]] · [[RoomState]] · [[RoomLayout]] ·
  [[SerializableObjectStates]] · [[SampleFloorFactory]]
- **Object states:** [[RoomObjectState]] · [[DoorState]] ·
  [[EnemySpawnState]] · [[ChestState]] · [[PotionState]] ·
  [[ShopItemState]]
- **Pools & setup:** [[EnemyPoolSO]] · [[WeightedEntry]] ·
  [[EnemySetupSO]] · [[SetupSlot]]
- **Floor shell:** [[FloorShell]] · [[FloorShellVisibilityController]] ·
  [[PlayerRoomTransitioner]] · [[RoomGridLoader]]
- **Services & interactables:** [[DungeonManager]] ·
  [[IDungeonService]] · [[FloorExitInteractable]]
- **Components:** [[DoorController]] · [[DoorSlotRef]] ·
  [[DoorDirection]] · [[DoorDirectionExtensions]] ·
  [[DoorVisualState]] · [[SpawnPoint]] · [[SpawnPointConfig]] ·
  [[SpawnKind]]

## Cross-domain edges

- Drives exploration via [[Exploration-MOC]] (the live
  [[ExplorationController]] now lives in `25-Exploration/`; the legacy
  `07-Dungeon/ExplorationController.md` is pending index reconciliation).
- Consumed by [[CombatHandoffService]] (see [[Combat-MOC]]) on combat
  triggers.
- Rooms instantiate onto [[Grid-MOC|Grid]] cells; transitions cue
  [[Camera-MOC|Camera]] follow.
- Shop rooms feed [[Shop-MOC|Shop]]; potion rooms feed [[Items-MOC]].
- [[MinimapView]] / [[RoomNavigationView]] render its state (see
  [[UI-MOC]]).
- [[BossFloorManagerSO]] in [[Entities-MOC]] packs the boss encounter.
