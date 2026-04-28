---
title: EffPlayFeedback
type: class
domain: 04-Effects
status: done
tags: [effects, concrete, feedback]
---

# EffPlayFeedback

> Bridge between the effects pipeline (§8) and the feedback system
> (§10). Builds a `FeedbackRequest` (snapshotted from the source
> behavior's value bag, §9.5) and asks `IFeedbackService` for a
> blocking play, gating [[TurnManager]] via
> `BeginFeedbackWait` / `OnFeedbackComplete`.

## Overview

Today the service is a stub (`FeedbackServiceStub`) that completes
immediately. When `FeedbackManager` + `FeedbackDBSO` land, this effect
doesn't change — only the service implementation does.

## API / Shape

```csharp
[Serializable, HideReferenceObjectPicker]
public class EffPlayFeedback : BaseEffect,
    IUsesFeedback, ICanBeAnimFeedback, ICanBeSFXFeedback, ICanBeVFXFeedback {
    private string _feedbackId;
}
```

## Dependencies
**Uses:** [[BaseEffect]], [[IUsesFeedback]], `IFeedbackService`,
[[TurnManager]], `FeedbackRequest`.
**Used by:** [[EffectData]] downstream of damage / heal / shield effects.

## Code
`Assets/Scripts/Rollgeon/Effects/Concretes/EffPlayFeedback.cs`
