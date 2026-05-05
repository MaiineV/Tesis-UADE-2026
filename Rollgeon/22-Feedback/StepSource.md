---
title: StepSource
type: enum
domain: 22-Feedback
status: done
tags: [feedback, enum, sequence]
---

# StepSource

> Origen de un [[FeedbackSequenceStep]]. Decide qué rama del
> `DispatchStep` corre en [[FeedbackManager]].

## Shape

```csharp
public enum StepSource {
    FeedbackRef,
    InlineWait,
    InlineAnimation,
    InlineBehaviorValue,
}
```

## Dependencies

**Used by:** [[FeedbackSequenceStep]] (`Source`), [[FeedbackManager]]
(`DispatchStep`).

## Code

`Assets/Scripts/Rollgeon/Feedback/FeedbackType.cs`

## External references

- TECHNICAL.md §10.8 — Sequence step sources.
