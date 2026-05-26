---
title: FeedbackCallbackListener
type: class
domain: 22-Feedback
status: done
tags: [feedback, completion, monobehaviour]
---

# FeedbackCallbackListener

> Componente que el [[FeedbackManager]] adjunta al GameObject del
> feedback para detectar completion natural — particle system stop o
> Animator state end.

## Overview

Single-fire (guard `_completed`). No se autodestruye — el dueño (el
manager o el GameObject host) decide cuándo limpiarlo. Para particles:
setea `main.stopAction = Callback` y reacciona a
`OnParticleSystemStopped`. Para animators: corre una coroutine que
espera fuera-de-transición + `normalizedTime >= 1`, con timeout de
seguridad. Hook adicional para Animation Events:
`OnFeedbackAnimationComplete()`.

## API / Shape

```csharp
public sealed class FeedbackCallbackListener : MonoBehaviour {
    public event Action OnCompleted;
    public bool IsCompleted { get; }

    public void ListenForParticleEnd();
    public void ListenForAnimatorStateEnd(Animator animator, string triggerName, float safetyTimeout);
    public void OnFeedbackAnimationComplete(); // hook para Animation Events
}
```

## Dependencies

**Used by:** [[FeedbackManager]] (instala el listener para
`CompletionMode.ParticleEnd` / `CompletionMode.AnimationEvent`),
Animation Events autorales en clips de feedback.

## Code

`Assets/Scripts/Rollgeon/Feedback/FeedbackCallbackListener.cs`

## External references

- TECHNICAL.md §10.11 — Natural completion listeners.
