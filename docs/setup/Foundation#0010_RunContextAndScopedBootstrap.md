# Foundation#0010 — RunContext and Scoped Bootstrap

## What this adds

- **`IRunContextService`** / **`RunContext`** — run-scoped service tracking run ID, selected hero, floor index, and active state.
- **`RunBootstrapper`** — static API to start and end runs (`StartRun` / `EndRun`).
- **`ServiceLocator.ClearScope`** now calls `IDisposable.Dispose()` on services before removing them.
- **`OnFloorChanged`** event added to `EventName`.
- **`BootstrapHooks`** updated: stubs replaced with documentation that `RunBootstrapper` now owns the lifecycle.

## Setup (engine side)

No Unity setup required. All classes are plain C# (no MonoBehaviours, no SOs to create).

## How to start a run (from gameplay code)

```csharp
using Rollgeon.Run;

var runId = Guid.NewGuid();
RunBootstrapper.StartRun(selectedHero, ruleset, runId);
```

## How to end a run

```csharp
RunBootstrapper.EndRun(runId);
```

This clears the player, fires `OnRunEnd`, and disposes all `ServiceScope.Run` services.

## Running tests

Open Unity Test Runner (Window > General > Test Runner), select **EditMode**, and run:

- `Rollgeon.Run.Tests.RunContextTests`
- `Rollgeon.Run.Tests.RunBootstrapperTests`
