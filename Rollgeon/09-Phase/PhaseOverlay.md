---
title: PhaseOverlay
type: concept
domain: 09-Phase
status: done
tags: [phase, enum, overlay]
---

# PhaseOverlay

> Stackable overlay on top of the current [[GamePhase]]. Lets us pause
> combat, play a cutscene, or spin up a craps minigame without losing
> the base phase state.

## Shape

```csharp
public enum PhaseOverlay {
    None     = 0,
    Pause    = 1,
    Cutscene = 2,
    Craps    = 3,
}
```

## Stack semantics

[[IPhaseService]] exposes `PushOverlay` / `PopOverlay`. Overlays are
checked against the active base via
[[PhaseTransitionMatrixSO]]`.CanPushOverlay` — not every overlay is
allowed in every phase (e.g. `Craps` only during `Combat`).

## Dependencies

- **Used by:** [[IPhaseService]], [[PhaseService]],
  [[PhaseTransitionMatrixSO]], pause menu, cutscene system, craps
  minigame.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Phase/PhaseOverlay.cs`

## External references

- TECHNICAL.md: §17.CR Craps overlay / §17.CS Cutscenes / §17.UI Pause
