---
title: PhaseService
type: service
domain: 09-Phase
status: done
tags: [phase, service]
---

# PhaseService

> Concrete implementation of [[IPhaseService]]. Registered globally by
> `PhaseServiceBootstrap` ([[IPreloadableService]]).

## Behaviour

- Holds `CurrentBase : GamePhase` and an internal stack of
  `PhaseStackEntry` for overlays.
- Delegates validation to [[PhaseTransitionMatrixSO]]:
  - `ReplacePhase(next)` → `matrix.CanTransition(CurrentBase, next)`.
  - `PushOverlay(o)` → `matrix.CanPushOverlay(CurrentBase, o)`.
- Emits events on change (see [[EventManager]] / [[TypedEvent]]).

## Bootstrap

`PhaseServiceBootstrap` is the [[IPreloadableService]] wrapper — listed
in [[ServiceBootstrapSO]]`.ExtraServices` so the service is available
before any phase-gated gameplay script runs.

## Dependencies

- **Uses:** [[GamePhase]], [[PhaseOverlay]], [[PhaseTransitionMatrixSO]],
  `InvalidPhaseTransitionException`, [[PhaseStackEntry]].
- **Used by:** same consumers as [[IPhaseService]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Phase/PhaseService.cs`
- Bootstrap: `.../PhaseServiceBootstrap.cs`
- Tests: `.../Tests/PhaseServiceTests.cs`

## External references

- Setup: `docs/setup/Foundation#0007_PhaseServiceReal.md`
- TECHNICAL.md: §17.PHA PhaseService
