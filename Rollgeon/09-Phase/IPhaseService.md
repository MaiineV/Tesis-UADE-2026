---
title: IPhaseService
type: interface
domain: 09-Phase
status: done
tags: [phase, service, interface]
---

# IPhaseService

> Global service that owns the current [[GamePhase]] and overlay stack.
> Every system that branches on phase reads state through this service.

## Shape

```csharp
public interface IPhaseService {
    GamePhase   CurrentBase    { get; }
    PhaseOverlay CurrentOverlay { get; }
    void ReplacePhase(GamePhase next);
    void PushOverlay(PhaseOverlay overlay);
    void PopOverlay();
}
```

## Contract

- `ReplacePhase` validates the transition against
  [[PhaseTransitionMatrixSO]]; invalid transitions throw
  `InvalidPhaseTransitionException`.
- `PushOverlay` / `PopOverlay` maintain a stack so nested overlays
  (e.g. `Pause` over `Cutscene` over `Combat`) are possible when the
  matrix allows it.

## Dependencies

- **Uses:** [[GamePhase]], [[PhaseOverlay]], [[PhaseTransitionMatrixSO]].
- **Used by:** combat screens, interaction gates, HUD visibility,
  [[ScreenManager]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Phase/IPhaseService.cs`

## External references

- Setup: `docs/setup/Foundation#0007_PhaseServiceReal.md`
- TECHNICAL.md: §17.PHA IPhaseService
