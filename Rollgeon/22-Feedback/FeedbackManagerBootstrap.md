---
title: FeedbackManagerBootstrap
type: bootstrap
domain: 22-Feedback
status: done
tags: [feedback, bootstrap, so]
---

# FeedbackManagerBootstrap

> `IPreloadableService` que crea el GameObject persistente del
> [[FeedbackManager]] y lo registra como [[IFeedbackService]] global.

## Overview

Asset autoral con la [[FeedbackDBSO]] asignada. Priority 55 — después
de [[AudioManagerBootstrap]] (50) para que el dispatch SFX encuentre
el [[IAudioService]] al primer request. Mutuamente excluyente con
[[FeedbackServiceStubBootstrap]] — sólo uno puede estar en
`ServiceBootstrapSO.ExtraServices`.

## API / Shape

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Feedback/Feedback Manager Bootstrap")]
public sealed class FeedbackManagerBootstrap : ScriptableObject, IPreloadableService {
    public int Priority => 55;
    public void Register();
}
```

## Dependencies

**Uses:** [[FeedbackManager]], [[FeedbackDBSO]], [[IFeedbackService]],
`Patterns.ServiceLocator`, `Rollgeon.Patterns.Bootstrap.IPreloadableService`.
**Used by:** `ServiceBootstrapSO.ExtraServices` (autoral).

## Code

`Assets/Scripts/Rollgeon/Feedback/FeedbackManagerBootstrap.cs`

## External references

- TECHNICAL.md §10.1 — Bootstrap.
