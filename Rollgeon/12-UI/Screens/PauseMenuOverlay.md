---
title: PauseMenuOverlay
type: screen
domain: 12-UI/Screens
status: done
tags: [ui, screen, pause, overlay]
---

# PauseMenuOverlay

> Overlay pushed when the player hits `Escape`. Sits on top of the
> current base screen without replacing it, via
> [[IPhaseService]]`.PushOverlay(PhaseOverlay.Pause)`.

## Behaviour

- `OnShow`: calls `PushOverlay(Pause)`, freezes gameplay systems that
  check phase state.
- Presents "Resume", "Settings" (stub), "Return to main menu".
- `OnHide`: calls `PopOverlay`.
- Allowed phases come from [[PhaseTransitionMatrixSO]] —
  typically `Exploration` and `Combat`.

## Dependencies

- **Uses:** [[BaseScreen]], [[IPhaseService]], [[PhaseOverlay]],
  [[RunBootstrapper]].
- **Used by:** global input handler listening for the pause action.

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/Screens/PauseMenuOverlay.cs`
- Tests: `.../Tests/PauseMenuOverlayTests.cs`

## External references

- Setup: `docs/setup/UI#0014c_PauseMenuOverlay.md`
- TECHNICAL.md: §D Pause
