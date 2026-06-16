---
title: Grid-MOC
type: moc
domain: 90-MOCs
status: done
tags: [moc, grid]
---

# 17-Grid — Map of Content

> Single source of truth for the active room's grid: walkability,
> occupancy, world↔grid conversion, navmesh bake, tile rendering and
> input. Every gameplay system that asks "who is on which tile" goes
> through [[IGridManager]].

## Relationships

```
 ServiceLocator (Run scope)
       │
       ▼
 IGridManager  ── Graph: NavGraph (NavNode + NavEdge)
       │       └ occupancy: Guid ⇄ GridCoord
       │
   LoadRoom ◄── NavGraphBaker(NavGraphBakeSettings, RoomLayout)
       │
       ├─ TileRendererRegistrar → TileMarker (per-tile MeshRenderer)
       ├─ TileHighlightService  ← ITileHighlightService
       └─ TileClickHandler (input → WorldToGrid)
```

## Pages

### Core service
- [[IGridManager]] — public interface (run-scoped)
- [[GridManager]] — default impl
- [[GridManagerBootstrap]] — registers the service at priority 75

### Data
- [[GridCoord]] · [[GridSnapshot]]
- [[NavNode]] · [[NavEdge]] · [[NavGraph]]
- [[NavGraphBakeSettings]] · [[NavGraphBaker]]

### Tiles, render, input
- [[TileMarker]] · [[TileRendererRegistrar]] · [[TileRendererRegistrarBootstrap]]
- [[ITileHighlightService]] · [[TileHighlightService]] · [[TileHighlightServiceBootstrap]]
- [[TileClickHandler]]

## Cross-domain edges

- **Incoming** (consumers):
  - 02-Combat: [[BasicEnemyAI]], `AINode_Move`, `TreeDrivenEnemyAI`,
    `AIContext`, `CombatHandoffService`, `DefaultEnemySpawnResolver`,
    `CombatDeathWatcher` — read tile reachability and occupants.
  - 04-Effects: `EffMove`, `EffApplyImpulse`, `EffDealDamage`,
    `EffHeal`, `EffAddShield`, `EffPassDoor` consume [[IGridManager]].
  - 18-Movement: [[IMovementService]] is built on top of this domain.
  - 22-Feedback: [[FeedbackPositionResolver]] resolves world positions
    via `GridToWorld`.
  - 25-Exploration: [[ExplorationBehaviorService]] queries player
    position and valid tiles for selection.
  - 26-PreConditions: [[PCEntityInRange]], [[PCAdjacentToDoor]] read
    occupancy and adjacency.
  - 11-Player / Entities: `EntityVisualService`, `EntityPawn`,
    `PlayerRoomTransitioner`, `RoomGridLoader`.
- **Outgoing** (dependencies):
  - 00-Foundations: [[ServiceLocator]], [[IPreloadableService]],
    [[EventManager]], [[EventName]].
  - 07-Dungeon: [[RoomLayout]], [[RoomInstance]], `IDungeonService`
    feed the bake source via `LoadRoom`.
