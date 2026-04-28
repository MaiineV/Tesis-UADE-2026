---
title: FeedbackCompletionMode
type: enum
domain: 22-Feedback
status: done
tags: [feedback, enum, completion]
---

# FeedbackCompletionMode

> Cómo sabe el [[FeedbackManager]] que un feedback terminó. `Timer` es
> el fallback universal — siempre seguro pero puede cortar early/late.

## Shape

```csharp
public enum FeedbackCompletionMode {
    Timer,
    AnimationEvent,
    ParticleEnd,
}
```

## Notas

- `Timer`: espera `entry.Duration`. Default y catch-all.
- `AnimationEvent`: el clip dispara
  [[FeedbackCallbackListener]]`.OnFeedbackAnimationComplete()` (o el
  watchdog de coroutine se acaba en `entry.Duration`).
- `ParticleEnd`: el manager pone `stopAction = Callback` y espera el
  `OnParticleSystemStopped` del listener; si tarda más que
  `Duration + WatchdogSafetySeconds`, el watchdog lo fuerza.

## Dependencies

**Used by:** [[FeedbackEntry]] (`CompletionMode`), [[FeedbackManager]]
(`DispatchVFX` / `DispatchAnimation` / `ExecuteLocalFeedback`),
[[FeedbackCallbackListener]].

## Code

`Assets/Scripts/Rollgeon/Feedback/FeedbackType.cs`

## External references

- TECHNICAL.md §10.5 — Completion strategies.
