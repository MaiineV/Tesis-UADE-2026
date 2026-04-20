# System#0011c — ExplorationController

## Prerequisites

| Dependency | Task |
|---|---|
| `IDungeonService` | System#0011b — DungeonManager must be registered in `ServiceScope.Run` |
| `IPhaseService` | Foundation#0012 — PhaseManager must be registered |
| `EventManager` | Foundation#0001 — bus must be initialized |
| `ServiceLocator` | Foundation#0001 — locator must be initialized |

## Overview

`ExplorationController` drives room-to-room exploration within a dungeon
floor. After `BeginExploration()` is called it listens for
`OnRoomEntered` events and routes each room to the appropriate game phase
based on `RoomType`.

## Registration

```csharp
// After DungeonManager and PhaseManager are registered:
var exploration = ExplorationController.CreateAndRegister();
exploration.BeginExploration();
```

Or manually via constructor (useful in tests):

```csharp
var controller = new ExplorationController(dungeonService, phaseService);
```

## Room Routing Table

| RoomType | Action |
|---|---|
| `Start` | No-op |
| `Combat` | Set `IsExploring = false`, fire `OnCombatTriggered`, transition to `GamePhase.Combat` |
| `Boss` | Same as Combat |
| `Shop` | Debug.Log stub (exploration continues) |
| `Potion` | Debug.Log stub (exploration continues) |

## Events

### Published

| Event | Args | When |
|---|---|---|
| `OnExplorationStarted` | `[Guid runId]` | `BeginExploration()` called |
| `OnCombatTriggered` | `[Guid roomInstanceId, string roomId, RoomType roomType]` | Combat or Boss room entered |

### Subscribed

| Event | Handler |
|---|---|
| `OnRoomEntered` | Routes current room through `ProcessRoom` |

## Verification

1. Confirm `ExplorationController.CreateAndRegister()` resolves from
   `ServiceLocator` after registration.
2. Call `BeginExploration()` with a Start room — `IsExploring` should be
   `true`, phase should be `Exploration`.
3. Call `AdvanceRoom()` into a Combat room — `OnCombatTriggered` fires,
   phase transitions to `Combat`, `IsExploring` becomes `false`.
4. Run EditMode tests:
   **Window > General > Test Runner > EditMode > Rollgeon.Exploration.Tests**
