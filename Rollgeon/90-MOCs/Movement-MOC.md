---
title: Movement-MOC
type: moc
domain: 90-MOCs
status: done
tags: [moc, movement]
---

# 18-Movement — Map of Content

> Pathfinding and movement execution over the active grid. Sits one
> layer above [[IGridManager]] — Grid handles "where things are",
> Movement handles "how things travel".

## Relationships

```
 ServiceLocator (Run scope)
       │
       ▼
 IMovementService
       │  ├─ GetReachableTiles(origin, range)   (BFS, 4-neighbourhood)
       │  ├─ FindPath(from, to)
       │  └─ Move(entity, destination)
       │         │
       │         ├─ writes through IGridManager.Move
       │         └─ emits OnEntityMoved(entity, from, to, path)
       │
       └─ consumed by AI move nodes, EffMove, EffApplyImpulse,
                        SelectionController, EntityPawn / VisualService
```

## Pages

### Core service
- [[IMovementService]] — public interface (run-scoped)
- [[MovementService]] — default impl, BFS over walkable + free tiles
- [[MovementServiceBootstrap]] — registers the service

## Cross-domain edges

- **Incoming** (consumers):
  - 02-Combat: AI nodes (`AINode_Move`, `TreeDrivenEnemyAI`) and
    `BasicEnemyAI` request reachable tiles + paths.
  - 04-Effects: `EffMove`, `EffApplyImpulse` execute moves through
    this service.
  - 14-UI: range / move-preview HUD views render reachable sets.
  - 25-Exploration: selection / preview flows on the exploration HUD.
  - 22-Feedback: [[FeedbackPositionResolver]] uses `OnEntityMoved`
    to resolve mid-flight positions.
  - 11-Player / Entities: `EntityPawn`, `EntityVisualService` listen
    on `OnEntityMoved` to animate the visual layer.
- **Outgoing** (dependencies):
  - 00-Foundations: [[ServiceLocator]], [[IPreloadableService]].
  - 17-Grid: [[IGridManager]], [[GridCoord]], [[NavGraph]] — reads
    walkability/occupancy and writes occupancy on `Move`.
