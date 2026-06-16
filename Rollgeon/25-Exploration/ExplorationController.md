---
title: ExplorationController
type: service
domain: 25-Exploration
status: done
tags: [exploration, controller, service]
---

# ExplorationController

> Run-scoped service driving room-to-room exploration on a floor: listens to [[EventName]] `OnRoomEntered`, processes the active [[RoomInstance]], and routes Combat / Boss rooms into the Combat phase via [[IPhaseService]] + `OnCombatTriggered`.

## Overview

Built by `CreateAndRegister`, which resolves [[IDungeonService]] and [[IPhaseService]] from [[ServiceLocator]] and registers the instance as [[IExplorationController]] in `ServiceScope.Run`. Subscribes to `OnRoomEntered` in its constructor and unsubscribes on `Dispose`.

`BeginExploration` flips the phase to [[GamePhase]]`.Exploration`, fires `OnExplorationStarted`, and invokes `ProcessRoom` on the current instance. `ResumeAfterCombat` re-enters the Exploration phase but does not advance — under the door system the player stays in place until the next `EnterRoomByDoor`.

`ProcessRoom` short-circuits on already-cleared rooms (except Shop / Potion, which are re-enterable). Combat / Boss rooms fire `OnCombatTriggered(InstanceId, RoomId, Type)` and replace the phase with `Combat`. Shop / Potion rooms currently log a stub. Start rooms are no-ops.

## API / Shape

```csharp
public sealed class ExplorationController : IExplorationController, IDisposable {
    public static ExplorationController CreateAndRegister();
    public bool   IsExploring { get; }
    public void   BeginExploration();
    public void   ResumeAfterCombat();
    public void   Dispose();
}
```

## Dependencies

**Uses:** [[IDungeonService]], [[IPhaseService]], [[GamePhase]], [[EventManager]], [[EventName]], [[RoomInstance]], [[RoomState]], `RoomType`, [[ServiceLocator]].
**Used by:** [[RunController]] (instantiates via `CreateAndRegister`), CombatReturnService (calls `ResumeAfterCombat`), [[DungeonManager]] (publisher of `OnRoomEntered`).

## Code

- Runtime: `Assets/Scripts/Rollgeon/Exploration/ExplorationController.cs`
- Tests: `Assets/Scripts/Rollgeon/Exploration/Tests/ExplorationControllerTests.cs`

## External references

- TECHNICAL.md §13.5 Exploration controller, §13.6 door-driven room transitions.
