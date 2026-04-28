---
title: FeedbackServiceStubBootstrap
type: bootstrap
domain: 22-Feedback
status: done
tags: [feedback, bootstrap, so, stub]
---

# FeedbackServiceStubBootstrap

> `IPreloadableService` que registra un [[FeedbackServiceStub]] como
> [[IFeedbackService]] global. Sirve como fallback test/editor cuando
> no se quiere bootstrappear el [[FeedbackManager]] real.

## Overview

Priority 55 — mismo slot que [[FeedbackManagerBootstrap]] porque son
mutuamente excluyentes; el diseñador elige cuál pone en
`ServiceBootstrapSO.ExtraServices`.

## API / Shape

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Feedback/Feedback Service Stub Bootstrap")]
public sealed class FeedbackServiceStubBootstrap : ScriptableObject, IPreloadableService {
    public int Priority => 55;
    public void Register();
}
```

## Dependencies

**Uses:** [[FeedbackServiceStub]], [[IFeedbackService]],
`Patterns.ServiceLocator`, `Rollgeon.Patterns.Bootstrap.IPreloadableService`.
**Used by:** `ServiceBootstrapSO.ExtraServices` autoral en builds de test.

## Code

`Assets/Scripts/Rollgeon/Feedback/FeedbackServiceStubBootstrap.cs`
