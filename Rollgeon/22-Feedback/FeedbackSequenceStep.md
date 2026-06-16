---
title: FeedbackSequenceStep
type: class
domain: 22-Feedback
status: done
tags: [feedback, sequence, data]
---

# FeedbackSequenceStep

> Un step autoral dentro de una secuencia. Origen (`Source`), timing
> (`StartMode`/`EndMode`), y blocking (`BlockSequence`). Inspector con
> Odin `[ShowIf]` para mostrar sólo lo relevante.

## Overview

`Source` puede ser `FeedbackRef` (resolver via [[FeedbackDBSO]]),
`InlineWait` (pura duración), `InlineAnimation` (trigger directo), o
`InlineBehaviorValue` (consumir bag por key). El timing soporta
secuencial (`AfterPrevious` / `AfterStep`) y event-driven
(`OnEvent` con key del [[FeedbackEventBus]]). `BlockSequence == false`
permite steps "fire and forget" que no demoran el callback global.

## API / Shape

```csharp
[Serializable]
public class FeedbackSequenceStep {
    public StepSource Source;

    // Source-specific
    public string FeedbackRefId;
    public float WaitDuration;
    public string InlineAnimTrigger;
    public bool InlineAnimOnSource;
    public BehaviorValueKey InlineBehaviorValueKey;
    public BehaviorValueTarget InlineBehaviorValueTarget;

    // Timing
    public StepStartMode StartMode;
    public int StartDependsOnStepIndex;
    public string StartOnEventKey;
    public float StartDelay;

    public StepEndMode EndMode;
    public float DurationOverride;
    public string EndOnEventKey;

    // Blocking
    public bool BlockSequence;
}
```

## Dependencies

**Uses:** [[StepSource]], [[StepStartMode]], [[StepEndMode]],
[[BehaviorValueTarget]], `BehaviorValueKey` (05-Entities).
**Used by:** [[FeedbackRequest]] (`SequenceSteps`), [[FeedbackManager]]
(`RunSequence` / `DispatchStep` / `WaitStartTrigger` / `WaitEndTrigger`).

## Code

`Assets/Scripts/Rollgeon/Feedback/FeedbackSequenceStep.cs`

## External references

- TECHNICAL.md §10.8 — Sequences.
