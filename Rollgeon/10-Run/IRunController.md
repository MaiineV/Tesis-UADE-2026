---
title: IRunController
type: interface
domain: 10-Run
status: done
tags: [run, interface, lifecycle]
---

# IRunController

> Service interface exposed by the run orchestrator. Currently a thin
> contract — `IsRunActive` for callers that need to gate UI / queries,
> plus `IDisposable` for scope teardown.

## Shape

```csharp
public interface IRunController : IDisposable {
    bool IsRunActive { get; }
}
```

## Methods

- **`IsRunActive`** — `true` between `OnRunStart` and `OnRunEnd` on
  [[EventManager]]. Flipped by [[RunController]] inside its event
  handlers.
- **`Dispose()`** — unsubscribes from `OnRunStart` / `OnRunEnd` and
  flips `IsRunActive` to `false`. Called when [[ServiceScope]] `Global`
  tears down.

## Note

The actual `StartRun` / `EndRun` entry points are static methods on
[[RunBootstrapper]] — they fire the bus events that this controller
subscribes to. There is no direct `StartRun` method on the interface.

## Dependencies

- **Used by:** [[RunController]] (impl), [[RunControllerBootstrapper]]
  (registers the impl), gameplay code that needs `IsRunActive`.

## Code

- Interface: `Assets/Scripts/Rollgeon/Run/IRunController.cs`
- Implementation: [[RunController]]

## External references

- TECHNICAL.md: §1.1.3 Run lifecycle
