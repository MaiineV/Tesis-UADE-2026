---
title: RunController
type: service
domain: 10-Run
status: done
tags: [run, service, orchestrator]
---

# RunController

> Orchestrator that stands up every run-scoped service the moment a run
> starts. Subscribes to `OnRunStart` / `OnRunEnd` on [[EventManager]] and
> flips the `IsRunActive` flag.

## Shape

```csharp
public sealed class RunController : IRunController {
    public bool IsRunActive { get; private set; }

    public RunController(FloorLayoutSO defaultLayout, int? seedOverride = null);
    public static RunController CreateAndRegister(FloorLayoutSO layout, int? seed = null);

    public void Dispose();
}
```

## What `OnRunStart` sets up (in order)

1. `InmemoryEntityRegistry` (Run scope).
2. `DefaultEnemySpawnResolver` (Run scope).
3. [[DungeonManager]] via `CreateAndRegister(layout, seed)`.
4. [[DamagePipeline]] — resolves its deps from [[ServiceLocator]].
5. [[HealPipeline]].
6. [[BasicEnemyAI]] — wired with `AttributesManager`, `IPlayerService`,
   damage pipeline and an `onTurnComplete` callback (from
   `ICombatSignaller` if available).
7. [[ExplorationController]].
8. [[CombatHandoffService]].
9. `CombatReturnService`.
10. Calls `IExplorationController.BeginExploration()`.

`OnRunEnd` flips `IsRunActive` to false — the actual teardown
(`ClearScope(Run)`) is done by [[RunBootstrapper]].

## Dependencies

- **Uses:** [[EventManager]], [[EventName]], [[ServiceLocator]],
  [[FloorLayoutSO]], [[DungeonManager]], [[DamagePipeline]],
  [[HealPipeline]], [[BasicEnemyAI]], [[ExplorationController]],
  [[CombatHandoffService]], [[IPlayerService]], [[AttributesManager]].
- **Used by:** [[RunBootstrapper]] (indirect via events).

## Code

- Runtime: `Assets/Scripts/Rollgeon/Run/RunController.cs`
- Interface: `.../IRunController.cs`
- Tests: `.../Tests/RunControllerTests.cs`

## External references

- Setup: `docs/setup/System#0013d_RunController.md`
- TECHNICAL.md: §1.1.3 Run lifecycle
