---
title: IGridManager
type: interface
domain: 17-Grid
status: done
tags: [grid, service, interface]
---

# IGridManager

> Run-scoped service contract for the active room's grid: walkability,
> occupancy, world↔grid conversion.

## Overview
Registered by [[GridManagerBootstrap]] at priority 75 with
`ServiceScope.Run`. Holds a [[NavGraph]] for the active room and a
two-way `Guid ⇄ GridCoord` occupancy map. Movement, AI, selection, and
the highlight services all read/write through this interface so a single
source of truth governs "who is on which tile".

## API / Shape

```csharp
public interface IGridManager {
    NavGraph  Graph      { get; }
    Vector3   GridOrigin { get; }
    float     TileSize   { get; }

    void LoadRoom(NavGraph graph, Vector3 origin = default, float tileSize = 1f);

    bool InBounds(GridCoord);
    bool IsWalkable(GridCoord);
    bool IsOccupied(GridCoord);
    bool IsFree(GridCoord);

    bool TryGetOccupant(GridCoord, out Guid);
    bool TryGetPosition(Guid,      out GridCoord);

    void Register(Guid, GridCoord);
    void Unregister(Guid);
    bool Move(Guid, GridCoord to);

    Vector3   GridToWorld(GridCoord);
    GridCoord WorldToGrid(Vector3);

    IEnumerable<KeyValuePair<Guid, GridCoord>> Occupants();
}
```

## Dependencies
**Uses:** [[GridCoord]], [[NavGraph]]
**Used by:** [[GridManager]], [[GridManagerBootstrap]],
[[MovementService]], [[TileRendererRegistrar]], [[TileClickHandler]],
`RoomGridLoader`, `PlayerRoomTransitioner`, `EntityVisualService`,
`EntityPawn`, `CombatHandoffService`, `DefaultEnemySpawnResolver`,
`CombatDeathWatcher`, `ExplorationBehaviorService`, AI nodes
(`AINode_Move`, `TreeDrivenEnemyAI`, `AIContext`), preconditions
(`PCEntityInRange`, `PCAdjacentToDoor`), effects (`EffMove`,
`EffApplyImpulse`, `EffDealDamage`, `EffHeal`, `EffAddShield`,
`EffPassDoor`), `FeedbackPositionResolver`, HUD views.

## Code
`Assets/Scripts/Rollgeon/Grid/IGridManager.cs`

## External references
- TECHNICAL.md: §17.§I Grid coordinates
