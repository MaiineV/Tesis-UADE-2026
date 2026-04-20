---
title: Phase-MOC
type: moc
domain: 90-MOCs
status: done
tags: [moc, phase]
---

# 09-Phase — Map of Content

> Global game phase + stackable overlays, validated against a
> `ScriptableObject` matrix.

## Relationships

```
 PhaseService implements IPhaseService
   ├─ CurrentBase  (GamePhase enum)
   └─ Overlay stack (PhaseOverlay enum)
         │
         │ validation via PhaseTransitionMatrixSO
         │    ├─ CanTransition(from, to)
         │    └─ CanPushOverlay(currentBase, overlay)
         │
         └─ invalid transition → InvalidPhaseTransitionException
```

## Notes

- [[GamePhase]] · [[PhaseOverlay]] · [[IPhaseService]] ·
  [[PhaseService]] · [[PhaseTransitionMatrixSO]]

## Cross-domain edges

- [[GamePhaseMask]] (in [[Entities-MOC]]) gates behaviors by phase.
- [[CombatController]] + [[ExplorationController]] flip phase on
  transitions.
- [[PauseMenuOverlay]] / [[FloorTransitionScreen]] push overlays.
