---
title: IRunContextService
type: interface
domain: 10-Run
status: done
tags: [run, interface, context]
---

# IRunContextService

> Read-only service interface that exposes the current run state. The
> public surface that gameplay code consumes — the mutable
> [[RunContext]] is hidden behind it.

## Shape

```csharp
public interface IRunContextService {
    Guid        RunId        { get; }
    int         FloorIndex   { get; }
    ClassHeroSO SelectedHero { get; }
    bool        IsRunActive  { get; }

    void AdvanceFloor();   // increments FloorIndex, fires OnFloorChanged
}
```

## Lifecycle

Registered in [[ServiceScope]] `Run` by [[RunBootstrapper]]`.StartRun`
(implementation = [[RunContext]]). Removed automatically when
`ServiceLocator.ClearScope(Run)` runs in `EndRun`.

## Dependencies

- **Uses:** [[ClassHeroSO]].
- **Used by:** [[DungeonManager]], [[ExplorationController]], combat
  handoff, HUD floor counters.

## Code

- Interface: `Assets/Scripts/Rollgeon/Run/IRunContextService.cs`
- Implementation: [[RunContext]]

## External references

- TECHNICAL.md: §1.1.3 RunContext / IRunContextService
