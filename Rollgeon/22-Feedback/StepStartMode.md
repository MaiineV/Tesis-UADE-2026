---
title: StepStartMode
type: enum
domain: 22-Feedback
status: done
tags: [feedback, enum, sequence]
---

# StepStartMode

> Cuándo arranca un step de una secuencia. El [[FeedbackManager]]
> espera el trigger correspondiente en `WaitStartTrigger` antes de
> aplicar `StartDelay` y dispatchear el step.

## Shape

```csharp
public enum StepStartMode {
    Immediate,       // arranca al iniciar la secuencia
    AfterPrevious,   // espera a step[i-1]
    AfterStep,       // espera al step indicado en StartDependsOnStepIndex
    OnEvent,         // espera a FeedbackEventBus.HasFired(StartOnEventKey)
}
```

## Dependencies

**Used by:** [[FeedbackSequenceStep]] (`StartMode`), [[FeedbackManager]]
(`WaitStartTrigger`), [[FeedbackEventBus]] (lookup de keys).

## Code

`Assets/Scripts/Rollgeon/Feedback/FeedbackType.cs`

## External references

- TECHNICAL.md §10.8 — Step start triggers.
