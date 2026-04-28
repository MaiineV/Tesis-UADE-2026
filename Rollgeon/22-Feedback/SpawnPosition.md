---
title: SpawnPosition
type: enum
domain: 22-Feedback
status: done
tags: [feedback, enum, position]
---

# SpawnPosition

> Estrategia de resolución de posición spawn. Switch consumido por
> [[FeedbackPositionResolver]].

## Shape

```csharp
public enum SpawnPosition {
    AtSource,
    AtTarget,
    AtSlot,
    BetweenSourceAndTarget,
    WorldPosition,
    FromReader,
}
```

## Notas

- `AtSource` / `AtTarget`: consulta [[IPawnRegistry]]; fallback a
  `IGridManager` y luego a `worldPositionHint`.
- `AtSlot`: directo a `IGridManager.GridToWorld`.
- `BetweenSourceAndTarget`: lerp 50/50 entre source y target.
- `WorldPosition`: usa el `WorldPosition` del [[FeedbackRequest]].
- `FromReader`: castea `entry.PositionReaderSO` a [[IPositionReader]] e
  invoca `Read(PositionReadInfo)`.

## Dependencies

**Used by:** [[FeedbackEntry]] (`Position`),
[[FeedbackPositionResolver]] (switch principal).

## Code

`Assets/Scripts/Rollgeon/Feedback/FeedbackType.cs`

## External references

- TECHNICAL.md §10.6 — Position resolution.
