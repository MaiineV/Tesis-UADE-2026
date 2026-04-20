# System#0013d — RunController Setup

## Overview

`RunController` is the convergence orchestrator that wires **all run-scoped
services** when a run starts. It listens to `OnRunStart` (fired by
`RunBootstrapper.StartRun`) and creates/registers every service needed for
dungeon exploration and combat in the correct dependency order.

## Service initialization order

| Step | Service                     | Registered as              | Scope |
|------|-----------------------------|----------------------------|-------|
| 1    | `InMemoryEntityRegistry`    | `InMemoryEntityRegistry`   | Run   |
| 2    | `DefaultEnemySpawnResolver` | `IEnemySpawnResolver`      | Run   |
| 3    | `DungeonManager`            | `IDungeonService`          | Run   |
| 4    | `DamagePipeline`            | `IDamagePipeline`          | Run   |
| 5    | `HealPipeline`              | `IHealPipeline`            | Run   |
| 6    | `BasicEnemyAI`              | `IEnemyAIHandler`          | Run   |
| 7    | `ExplorationController`     | `IExplorationController`   | Run   |
| 8    | `CombatHandoffService`      | `ICombatHandoffService`    | Run   |
| 9    | `CombatReturnService`       | `ICombatReturnService`     | Run   |

After registration, `ExplorationController.BeginExploration()` is called
to start room navigation.

## Prerequisites (Global services)

The following services must be registered in `ServiceScope.Global` **before**
`OnRunStart` fires:

- `IPlayerService`
- `AttributesManager`
- `IPhaseService`
- `IScreenManager`
- `ICombatStarter`
- `ICombatSignaller` (optional — logs warning and uses no-op if absent)

## Scene bootstrap wiring

1. Create a `MonoBehaviour` on a GameObject in the Bootstrap scene (or
   MainMenu scene, alongside the existing service bootstrap).
2. Add a serialized `FloorLayoutSO` field and assign the default floor
   layout asset.
3. In `Awake` (after `ServiceBootstrapSO` has registered global services),
   call:

```csharp
RunController.CreateAndRegister(defaultLayout);
```

This registers the `RunController` as `IRunController` in
`ServiceScope.Global`. It will then automatically react to run lifecycle
events.

## Verification

1. Enter Play mode from the Bootstrap scene.
2. Navigate to Build Selection and confirm a hero.
3. In the Console, verify:
   - No errors from `RunController`.
   - `[ExplorationController]` logs for room processing appear.
   - `ServiceLocator.HasService<IDungeonService>()` returns `true` during
     the run.
4. End the run — verify `IsRunActive` returns to `false` and run-scoped
   services are cleared.
