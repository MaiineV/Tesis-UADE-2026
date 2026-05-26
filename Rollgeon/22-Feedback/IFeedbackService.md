---
title: IFeedbackService
type: interface
domain: 22-Feedback
status: done
tags: [feedback, service]
---

# IFeedbackService

> Contrato del servicio de feedback visual / sonoro / animación. Único
> entry point por el que un [[Effect]] o un [[Combat]] dispara
> feedback bloqueante.

## Overview

API mínima en Sprint 03 FP: `RequestFeedbackBlocking(request, onComplete)`.
El callback se invoca **exactamente una vez**, incluso si el id es
inválido (en cuyo caso se completa inmediatamente). Implementaciones:
[[FeedbackManager]] (real, con DB / dispatch / watchdog) y
[[FeedbackServiceStub]] (callback inmediato — fallback test/editor).

## API / Shape

```csharp
public interface IFeedbackService {
    void RequestFeedbackBlocking(FeedbackRequest request, Action onComplete);
}
```

## Dependencies

**Uses:** [[FeedbackRequest]].
**Used by:** [[FeedbackManager]], [[FeedbackServiceStub]],
[[EffPlayFeedback]] (04-Effects — caller principal), [[DamageContext]]
(02-Combat — vía effects).

## Code

`Assets/Scripts/Rollgeon/Feedback/IFeedbackService.cs`

## External references

- TECHNICAL.md §10.1 — Feedback contract.
