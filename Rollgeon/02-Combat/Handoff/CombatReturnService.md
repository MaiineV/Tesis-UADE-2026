---
title: CombatReturnService
type: service
domain: 02-Combat/Handoff
status: done
tags: [combat, handoff, return]
---

# CombatReturnService

> Reverse counterpart of [[CombatHandoffService]]. Listens for
> [[CombatOutcome]] on [[EventName]] `OnCombatEnd` and routes the flow
> back to exploration (on win), the defeat screen (on loss), or the
> abort path.

## Flow

```
OnCombatEnd(playerId, roomInstanceId, outcome)
  └─ switch outcome:
       PlayerWon  → mark room cleared in DungeonManager; push Exploration screen.
       PlayerLost → push Defeat screen; RunBootstrapper.EndRun.
       Aborted    → no-op.
```

## Dependencies

- **Uses:** [[EventManager]], [[EventName]], [[DungeonManager]],
  [[ScreenManager]] (`IScreenManager`), [[RunBootstrapper]],
  [[CombatOutcome]].
- **Used by:** automatic subscriber registered by [[RunController]]
  during `OnRunStart` (`CombatReturnService.CreateAndRegister`).

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combat/Handoff/CombatReturnService.cs`
- Tests: `.../Tests/CombatReturnServiceTests.cs`

## External references

- Setup: `docs/setup/System#0012d_CombatEndToExploration.md`
- TECHNICAL.md: §12.9 Combat return
