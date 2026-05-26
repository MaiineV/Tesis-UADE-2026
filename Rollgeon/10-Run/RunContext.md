---
title: RunContext
type: system
domain: 10-Run
status: done
tags: [run, state, context]
---

# RunContext

> Mutable container for the current run's state: `RunId`, `FloorIndex`,
> `SelectedHero`, `IsRunActive`. Exposed read-only via
> `IRunContextService`.

## Shape

```csharp
public sealed class RunContext : IRunContextService, IDisposable {
    public Guid RunId         { get; }
    public int  FloorIndex    { get; private set; }
    public ClassHeroSO SelectedHero { get; }
    public bool IsRunActive   { get; private set; }

    public RunContext(Guid runId, ClassHeroSO selectedHero);
    public void AdvanceFloor();  // fires OnFloorChanged
    internal void EndRun();      // called from RunBootstrapper.EndRun
}
```

The matching interface `IRunContextService` is the read-only API the
rest of the codebase consumes:

```csharp
public interface IRunContextService {
    Guid RunId { get; }
    int FloorIndex { get; }
    ClassHeroSO SelectedHero { get; }
    bool IsRunActive { get; }
    void AdvanceFloor();
}
```

## Lifecycle

- Created by [[RunBootstrapper]]`.StartRun`.
- Registered as `IRunContextService` in [[ServiceScope]] `Run`.
- `AdvanceFloor()` increments `FloorIndex` and publishes
  [[EventName]] `OnFloorChanged(RunId, FloorIndex)`.
- Disposed when [[ServiceLocator]] clears the `Run` scope.

## Dependencies

- **Uses:** [[ClassHeroSO]], [[EventManager]], [[EventName]].
- **Used by:** [[RunBootstrapper]], [[DungeonManager]],
  [[ExplorationController]], combat handoff.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Run/RunContext.cs`
- Interface: `.../IRunContextService.cs`
- Tests: `.../Tests/RunContextTests.cs`

## External references

- Setup: `docs/setup/Foundation#0010_RunContextAndScopedBootstrap.md`
- TECHNICAL.md: §1.1.3 RunContext
