---
title: FeedbackSequenceRuntime
type: class
domain: 22-Feedback
status: done
tags: [feedback, sequence, runtime]
---

# FeedbackSequenceRuntime

> Puntero estático al [[FeedbackEventBus]] activo. Lo usan componentes
> que no tienen referencia directa al bus (Animation Events, particle
> stop callbacks) para publicar eventos en el contexto de la secuencia
> en curso.

## Overview

`Current` lo setea [[FeedbackManager]] al arrancar `ExecuteLocalSequence`
y lo limpia al terminar. `ClearCurrent(expected)` solo libera si el
puntero coincide con `expected` — protege contra teardowns
fuera de orden entre secuencias anidadas o concurrentes.

## API / Shape

```csharp
public static class FeedbackSequenceRuntime {
    public static FeedbackEventBus Current { get; }
    public static void SetCurrent(FeedbackEventBus bus);
    public static void ClearCurrent(FeedbackEventBus expected);
    public static void Publish(string key);  // no-op si no hay bus activo
}
```

## Dependencies

**Uses:** [[FeedbackEventBus]].
**Used by:** [[FeedbackManager]] (set/clear), Animation Events
autorales y particle callbacks de prefabs (publish).

## Code

`Assets/Scripts/Rollgeon/Feedback/FeedbackSequenceRuntime.cs`

## External references

- TECHNICAL.md §10.8.2 — Runtime pointer.
