---
title: GamePhase
type: concept
domain: 09-Phase
status: done
tags: [phase, enum]
---

# GamePhase

> Global base phase of the game at any given moment. Gates which
> services expose functionality and which screens [[ScreenManager]] may
> present.

## Shape

```csharp
public enum GamePhase {
    None        = 0,
    Exploration = 1,
    Combat      = 2,
    Loading     = 3,
    GameOver    = 4,
}
```

## Complement

A `GamePhase` can be decorated with a [[PhaseOverlay]] (e.g. `Pause`,
`Cutscene`, `Craps`) that is pushed/popped without mutating the base.

## Dependencies

- **Used by:** [[IPhaseService]], [[PhaseService]],
  [[PhaseTransitionMatrixSO]], everything gated on phase (UI,
  interactions, service availability).

## Code

- Runtime: `Assets/Scripts/Rollgeon/Phase/GamePhase.cs`

## External references

- Setup: `docs/setup/Foundation#0007_PhaseServiceReal.md`
- TECHNICAL.md: §12 Combat / §17.PHA Phase service
