---
title: StepEndMode
type: enum
domain: 22-Feedback
status: done
tags: [feedback, enum, sequence]
---

# StepEndMode

> Cuándo se considera terminado un step. El [[FeedbackManager]] espera
> el trigger correspondiente en `WaitEndTrigger` antes de marcar el
> step como `Done` y publicar `$step.{i}.end` al [[FeedbackEventBus]].

## Shape

```csharp
public enum StepEndMode {
    OnDuration,    // WaitForSeconds(DurationOverride or guess)
    OnNaturalEnd,  // listener (particle/animator); fallback a duration
    OnEvent,       // FeedbackEventBus.HasFired(EndOnEventKey)
    Immediate,     // termina al dispatchear
}
```

## Dependencies

**Used by:** [[FeedbackSequenceStep]] (`EndMode`), [[FeedbackManager]]
(`WaitEndTrigger`), [[FeedbackEventBus]] (lookup de keys).

## Code

`Assets/Scripts/Rollgeon/Feedback/FeedbackType.cs`

## External references

- TECHNICAL.md §10.8 — Step end triggers.
