---
title: ExplorationController
type: service
domain: 07-Dungeon
status: done
tags: [exploration, controller, service]
---

# ExplorationController

> Run-scoped controller that drives the exploration loop: push the
> exploration screen, handle room navigation input, and dispatch
> [[EventName]] `OnCombatTriggered` when the player enters a combat
> room.

## API (typical)

```csharp
public sealed class ExplorationController : IExplorationController {
    public static ExplorationController CreateAndRegister();

    public void BeginExploration();       // pushes screen, activates input
    public void EnterRoom(Guid roomInstanceId);
    public void RequestFloorExit();
}
```

## Flow

1. `BeginExploration` pushes [[ExplorationHUD]] via [[ScreenManager]].
2. Player input (move between rooms) calls `EnterRoom(roomInstanceId)`.
3. If the room is a combat / boss room → fire `OnCombatTriggered` for
   [[CombatHandoffService]].
4. If the room is non-combat → push the room's specific screen (shop,
   potion).
5. On floor exit → advance via [[IRunContextService]].

## Dependencies

- **Uses:** [[DungeonManager]], [[IPhaseService]], [[ScreenManager]],
  [[EventManager]], [[EventName]], [[IRunContextService]].
- **Used by:** [[RunController]] (instantiates), [[CombatReturnService]]
  (re-enters on player win).

## Code

- Runtime: `Assets/Scripts/Rollgeon/Exploration/ExplorationController.cs`
- Interface: `.../IExplorationController.cs`
- Tests: `.../Tests/ExplorationControllerTests.cs`

## External references

- Setup: `docs/setup/System#0011c_ExplorationController.md`
- TECHNICAL.md: §13.5 Exploration controller
