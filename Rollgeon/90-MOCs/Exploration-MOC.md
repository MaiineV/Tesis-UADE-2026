---
title: Exploration-MOC
type: moc
domain: 90-MOCs
status: done
tags: [moc, exploration]
---

# 25-Exploration — Map of Content

> Phase-driven exploration loop: arbitrates Combat ↔ Exploration
> transitions, processes the active room, and runs the exploration
> behavior bar (target selection, energy spend, hero behavior execution).

## Relationships

```
 RunController ──► IExplorationController.BeginExploration()
                          │
                          ▼
              ExplorationController (Run scope)
                ├─ subscribes OnRoomEntered (DungeonManager)
                ├─ ProcessRoom(RoomInstance)
                │     ├─ Combat / Boss → fire OnCombatTriggered, push GamePhase.Combat
                │     └─ Shop / Potion / Start → re-enterable / no-op
                ├─ BeginExploration → IPhaseService.SetPhase(Exploration)
                └─ ResumeAfterCombat (post-combat re-entry)

 ExplorationBehaviorService (Run scope)
   ├─ states: Inactive (auto on phase exit) / Idle / Selecting
   ├─ OnBehaviorSelected(idx)
   │     ├─ ShowConditions (BasePreCondition)
   │     ├─ IEnergyService.Spend
   │     └─ SelectionTiming.BeforeRoll → ISelectionController.BeginSelection
   └─ ExecuteBehavior(HeroBehaviorContext)
```

## Pages

### Controllers
- [[IExplorationController]] · [[ExplorationController]]
- [[IExplorationBehaviorService]] · [[ExplorationBehaviorService]]

## Cross-domain edges

- **Incoming** (consumers):
  - 06-Run: [[RunController]] calls `BeginExploration` on floor start
    and `ResumeAfterCombat` after a combat returns.
  - 14-UI: [[ExplorationHUDView]] / behavior bar invokes
    `OnBehaviorSelected` / `CancelSelection`.
  - 02-Combat: `CombatReturnService` calls `ResumeAfterCombat`.
- **Outgoing** (dependencies):
  - 00-Foundations: [[ServiceLocator]], [[EventManager]], [[EventName]]
    (`OnRoomEntered`, `OnCombatTriggered`, `OnPhaseEnter`/`Exit`).
  - 07-Dungeon: [[IDungeonService]], [[RoomInstance]], `RoomState`,
    `RoomType` (and door-driven `EnterRoomByDoor`).
  - 06-Run / Phase: [[IPhaseService]], [[GamePhase]].
  - 17-Grid: [[IGridManager]] for player position + valid-tile resolve.
  - 11-Player: [[IPlayerService]] for the hero + behavior list.
  - Energy: [[IEnergyService]] for behavior cost.
  - 26-PreConditions: [[BasePreCondition]] / [[PreConditionContext]]
    drive `ShowConditions` gating; selection types (`SelectionSettings`,
    `SelectionTiming`, `SlotState`, `TargetSelectionResult`).
