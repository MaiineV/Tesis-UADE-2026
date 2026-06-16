---
title: BehaviorValueTarget
type: enum
domain: 22-Feedback
status: done
tags: [feedback, enum]
---

# BehaviorValueTarget

> Indica si el handler de `BehaviorValue` debe leer el valor del pawn
> source o target.

## Shape

```csharp
public enum BehaviorValueTarget {
    Source,
    Target,
}
```

## Dependencies

**Used by:** [[FeedbackEntry]] (`ValueTarget`),
[[FeedbackSequenceStep]] (`InlineBehaviorValueTarget`),
[[FeedbackManager]] (`DispatchBehaviorValue` → elige source/target guid).

## Code

`Assets/Scripts/Rollgeon/Feedback/FeedbackType.cs`
