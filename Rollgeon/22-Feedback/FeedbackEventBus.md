---
title: FeedbackEventBus
type: class
domain: 22-Feedback
status: done
tags: [feedback, sequence, events]
---

# FeedbackEventBus

> Pub/sub **latched** por secuencia: un key publicado queda firado hasta
> que la secuencia termina. Subscribers tardíos que consulten
> `HasFired` reciben el estado acumulado.

## Overview

Storage = `HashSet<string>`. Vida ligada a una sola ejecución de
[[FeedbackManager]]`.RunSequence`: se crea con la secuencia y se limpia
al terminar. El [[FeedbackSequenceRuntime]] expone el bus activo a
componentes que no tienen referencia directa (Animation Events,
particle stop callbacks).

## API / Shape

```csharp
public sealed class FeedbackEventBus {
    public void Publish(string key);
    public bool HasFired(string key);
    public void Clear();
}
```

## Dependencies

**Used by:** [[FeedbackManager]] (`RunSequence`), [[FeedbackSequenceRuntime]],
[[FeedbackSequenceStep]] (`StartOnEventKey` / `EndOnEventKey`).

## Code

`Assets/Scripts/Rollgeon/Feedback/FeedbackEventBus.cs`

## External references

- TECHNICAL.md §10.8.1 — Latched event bus.
