# F#0012d — CombatEnd to Exploration

## Overview

`CombatReturnService` listens for `OnCombatEnd` and routes back to
exploration on victory/aborted, or fires `OnPlayerDefeated` on defeat.

## New / Modified files

| File | Change |
|------|--------|
| `Patterns/EventName.cs` | Added `OnPlayerDefeated` |
| `Exploration/IExplorationController.cs` | Added `ResumeAfterCombat()` |
| `Exploration/ExplorationController.cs` | Implemented `ResumeAfterCombat()` |
| `Combat/Handoff/ICombatReturnService.cs` | New interface |
| `Combat/Handoff/CombatReturnService.cs` | New service |
| `Combat/Handoff/Tests/CombatReturnServiceTests.cs` | 8 tests |

## Prerequisites

- `IExplorationController` registered in `ServiceScope.Run`
- `IScreenManager` registered (global)
- `IPlayerService` registered (global or run)
- CombatHUD screen pushed before combat starts (handled by `CombatHandoffService`)

## Registration

In the run bootstrap (where `CombatHandoffService.CreateAndRegister()` is
called), add:

```csharp
CombatReturnService.CreateAndRegister();
```

This must run **after** `ExplorationController.CreateAndRegister()` so that
`IExplorationController` is available in the ServiceLocator.

## Verification

1. Start a run and enter a combat room.
2. Win the combat — the CombatHUD should pop, `OnRoomCleared` fires, and
   exploration resumes at the next room.
3. Lose the combat — the CombatHUD pops and `OnPlayerDefeated` fires with
   the current `RunId`.
4. Run EditMode tests: `Rollgeon.Combat.Handoff.Tests` — all 8 new tests
   should pass.
