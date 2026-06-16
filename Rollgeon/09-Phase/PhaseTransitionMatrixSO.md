---
title: PhaseTransitionMatrixSO
type: so
domain: 09-Phase
status: done
tags: [phase, so, config]
---

# PhaseTransitionMatrixSO

> `ScriptableObject` that encodes which [[GamePhase]] → [[GamePhase]]
> transitions are legal and which [[PhaseOverlay]]s are allowed on top
> of each base phase.

## Shape

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Phase/Transition Matrix")]
public class PhaseTransitionMatrixSO : SerializedScriptableObject {
    [OdinSerialize] bool[,] _allowedTransitions;          // 5 x 5
    [OdinSerialize] Dictionary<GamePhase, List<PhaseOverlay>> _allowedOverlays;

    public bool CanTransition(GamePhase from, GamePhase to);
    public bool CanPushOverlay(GamePhase currentBase, PhaseOverlay overlay);
}
```

## Design

- Row = from, column = to. Only ticked cells permit a transition.
- Separating overlay allowance keeps `Craps` exclusive to `Combat`,
  `Cutscene` allowed in `Exploration` and `Combat`, etc., without
  spreading `switch` statements across the codebase.

## Dependencies

- **Uses:** [[GamePhase]], [[PhaseOverlay]].
- **Used by:** [[PhaseService]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Phase/PhaseTransitionMatrixSO.cs`
- Tests: `.../Tests/PhaseTransitionMatrixTests.cs`

## External references

- Setup: `docs/setup/Foundation#0007_PhaseServiceReal.md`
- TECHNICAL.md: §17.PHA Transition matrix
