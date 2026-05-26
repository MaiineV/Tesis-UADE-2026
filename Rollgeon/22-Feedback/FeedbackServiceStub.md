---
title: FeedbackServiceStub
type: service
domain: 22-Feedback
status: done
tags: [feedback, stub]
---

# FeedbackServiceStub

> Implementación stub de [[IFeedbackService]]. Loguea el id (si hay)
> e invoca el `onComplete` inmediatamente — no toca audio, VFX ni
> animator.

## Overview

Fallback para EditMode tests, scenes sin arte y tooling que no quiere
pagar el costo de instanciar el [[FeedbackManager]] real. Para combate
jugable usar el [[FeedbackManagerBootstrap]]; los dos stubs son
mutuamente excluyentes — sólo uno puede registrar `IFeedbackService` a
la vez.

## API / Shape

```csharp
public sealed class FeedbackServiceStub : IFeedbackService {
    public void RequestFeedbackBlocking(FeedbackRequest request, Action onComplete);
}
```

## Dependencies

**Uses:** [[IFeedbackService]], [[FeedbackRequest]].
**Used by:** [[FeedbackServiceStubBootstrap]] (dueño), tests EditMode
de [[Effect]] / [[Combat]].

## Code

`Assets/Scripts/Rollgeon/Feedback/FeedbackServiceStub.cs`

## External references

- TECHNICAL.md §10.1 — Stub fallback.
