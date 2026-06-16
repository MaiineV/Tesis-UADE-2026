---
title: FeedbackRequest
type: struct
domain: 22-Feedback
status: done
tags: [feedback, dto]
---

# FeedbackRequest

> DTO que un [[Effect]] (típicamente [[EffPlayFeedback]]) arma y pasa
> al [[IFeedbackService]]. Transporta id + actores + valores + posición
> + flag/lista de secuencia.

## Overview

`StoredValues` es un **snapshot** del bag del behavior al momento de
armar el request (`BaseEffect.GetFeedbackRequest`), no una referencia
viva — el behavior puede limpiar su bag sin afectar al request en
vuelo. `WorldPosition` se usa cuando la entry tiene
`SpawnPosition.WorldPosition` (o como fallback). `IsSequence` + 
`SequenceSteps` activan el path de [[FeedbackSequenceStep]] en lugar
del lookup por id.

## API / Shape

```csharp
public struct FeedbackRequest {
    public string FeedbackId;
    public Guid SourceGuid;
    public Guid TargetGuid;
    public IReadOnlyDictionary<BehaviorValueKey, List<BaseBehaviorStoredValue>> StoredValues;
    public Vector3 WorldPosition;
    public bool IsSequence;
    public List<FeedbackSequenceStep> SequenceSteps;
    public FeedbackPlayer Player;
}
```

## Dependencies

**Uses:** [[FeedbackSequenceStep]], [[FeedbackPlayer]],
`BehaviorValueKey` / `BaseBehaviorStoredValue` (05-Entities).
**Used by:** [[IFeedbackService]], [[FeedbackManager]],
[[FeedbackServiceStub]], [[EffPlayFeedback]] (04-Effects),
[[FeedbackPositionResolver]].

## Code

`Assets/Scripts/Rollgeon/Feedback/FeedbackRequest.cs`

## External references

- TECHNICAL.md §10.4 — Request DTO.
- TECHNICAL.md §9.5 — Behavior bag snapshot.
