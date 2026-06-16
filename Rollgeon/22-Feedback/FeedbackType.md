---
title: FeedbackType
type: enum
domain: 22-Feedback
status: done
tags: [feedback, enum]
---

# FeedbackType

> Tipo de un [[FeedbackEntry]]. Determina qué campos autorales son
> relevantes (vía `[ShowIf]`) y qué rama del dispatch corre en
> [[FeedbackManager]].

## Shape

```csharp
public enum FeedbackType {
    VFX,
    SFX,
    Animation,
    Wait,
    BehaviorValue,
    FloatingNumber,
}
```

## Dispatch

| Type            | FeedbackManager rama |
|-----------------|----------------------|
| `VFX`           | `DispatchVFX`        |
| `SFX`           | `DispatchSFX` → [[IAudioService]] |
| `Animation`     | `DispatchAnimation`  |
| `Wait`          | no-op (timer-only)   |
| `BehaviorValue` | `DispatchBehaviorValue` |
| `FloatingNumber`| `DispatchFloatingNumber` → [[FloatingNumberView]] |

## Dependencies

**Used by:** [[FeedbackEntry]], [[FeedbackManager]], [[FeedbackDBSO]]
(`GetFilteredFeedbackIds`).

## Code

`Assets/Scripts/Rollgeon/Feedback/FeedbackType.cs`

## External references

- TECHNICAL.md §10.3 — Feedback types.
