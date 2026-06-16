---
title: InvalidPhaseTransitionException
type: class
domain: 09-Phase
status: done
tags: [phase, exception]
---

# InvalidPhaseTransitionException

> `InvalidOperationException` thrown by [[PhaseService]] when a caller
> attempts a phase transition that the [[PhaseTransitionMatrixSO]] does
> not allow.

## Shape

```csharp
public class InvalidPhaseTransitionException : InvalidOperationException {
    public InvalidPhaseTransitionException(string message);
}
```

## When it fires

- `PhaseService.ReplacePhase(next)` when
  `matrix.CanTransition(CurrentBase, next)` is `false`.
- `PhaseService.PushOverlay(o)` when
  `matrix.CanPushOverlay(CurrentBase, o)` is `false`.

The message contains the offending current/target phase pair so test
output and runtime logs are debuggable.

## Dependencies

- **Used by:** [[PhaseService]] (throw site).

## Code

`Assets/Scripts/Rollgeon/Phase/InvalidPhaseTransitionException.cs`

## External references

- TECHNICAL.md: §17.PHA Phase transitions
