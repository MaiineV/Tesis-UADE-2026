---
title: FloorTransitionScreen
type: screen
domain: 12-UI/Screens
status: done
tags: [ui, screen, transition]
---

# FloorTransitionScreen

> Overlay shown between floors. Displays the next floor's number and
> kicks off [[IRunContextService]]`.AdvanceFloor` when dismissed.

## Behaviour

- `OnShow(FloorTransitionPayload)` reads the outgoing / incoming floor
  index.
- Runs an animation (fade in, text reveal).
- Calls `AdvanceFloor()` then pops itself, handing control back to
  [[ExplorationController]]`.BeginExploration` for the new layout.

## Dependencies

- **Uses:** [[BaseScreen]], [[IRunContextService]].
- **Used by:** [[FloorExitInteractable]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/Screens/FloorTransitionScreen.cs`
- Payload: `.../FloorTransitionPayload.cs`
- Tests: `.../Tests/FloorTransitionScreenTests.cs`

## External references

- Setup: `docs/setup/UI#0013b_FloorTransitionScreen.md`
- TECHNICAL.md: §D Floor transition
