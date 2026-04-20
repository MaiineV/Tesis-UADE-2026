---
title: RunBootstrapper
type: service
domain: 10-Run
status: done
tags: [run, bootstrap, lifecycle]
---

# RunBootstrapper

> Static entry point for starting and ending a run. Bridges the class
> selection flow into the event-based [[RunController]] pipeline.

## Shape

```csharp
public static class RunBootstrapper {
    public static void StartRun(ClassHeroSO selected, RulesetSO ruleset, Guid runId);
    public static void EndRun(Guid runId);
}
```

## `StartRun`

1. Creates [[RunContext]] and registers it as `IRunContextService` in
   `ServiceScope.Run`.
2. Resolves `IPlayerService` and calls `SetPlayer(selected, runId)`.
3. Fires [[EventName]] `OnRunStart(runId, rulesetId)`. The already-built
   [[RunController]] consumes this event and spawns run-scoped services.

## `EndRun`

1. Marks the current [[RunContext]] as inactive.
2. Clears the player on [[IPlayerService]].
3. Fires `OnRunEnd(runId, null)` so run-scoped modifiers auto-remove.
4. Calls [[ServiceLocator]]`.ClearScope(Run)` — every `Run`-scoped
   service (including [[DungeonManager]], damage/heal pipelines, AI) is
   disposed.

## Dependencies

- **Uses:** [[ServiceLocator]], [[ServiceScope]], [[EventManager]],
  [[EventName]], [[RunContext]], [[IPlayerService]], [[RulesetSO]].
- **Used by:** [[RunController]] (subscriber), class selection flow (UI).

## Code

- Runtime: `Assets/Scripts/Rollgeon/Run/RunBootstrapper.cs`
- Tests: `.../Tests/RunBootstrapperTests.cs`

## External references

- Setup: `docs/setup/Foundation#0010_RunContextAndScopedBootstrap.md`
- TECHNICAL.md: §1.1.3 Run lifecycle — RunBootstrapper
