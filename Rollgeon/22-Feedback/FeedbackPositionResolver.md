---
title: FeedbackPositionResolver
type: class
domain: 22-Feedback
status: done
tags: [feedback, position]
---

# FeedbackPositionResolver

> Switch puro sobre [[SpawnPosition]] que convierte la intención
> autoral del [[FeedbackEntry]] en una posición mundial. Siempre suma
> `entry.PositionOffset` al final.

## Overview

Estática y sin estado. Fallbacks silenciosos — los callers nunca
deberían pegar `NullReferenceException` por este path. `AtSource` /
`AtTarget` consultan al [[IPawnRegistry]]; si falta o el guid no
está, cae a `IGridManager` (17-Grid) y luego al `worldPositionHint`.
`FromReader` invoca `IPositionReader.Read(PositionReadInfo)` con el
[[FeedbackPlayer]] del request.

## API / Shape

```csharp
public static class FeedbackPositionResolver {
    public static Vector3 Resolve(
        FeedbackEntry entry, Guid sourceGuid, Guid targetGuid,
        Vector3 worldPositionHint, FeedbackPlayer player = FeedbackPlayer.Player);

    public static Transform ResolvePawnTransform(Guid guid);
}
```

## Dependencies

**Uses:** [[FeedbackEntry]], [[SpawnPosition]], [[FeedbackPlayer]],
[[IPawnRegistry]], [[IPositionReader]], [[PositionReadInfo]],
`IGridManager` (17-Grid).
**Used by:** [[FeedbackManager]] (dispatch), [[FloatingNumberView]]
spawn (vía `ResolvePawnTransform`).

## Code

`Assets/Scripts/Rollgeon/Feedback/FeedbackPositionResolver.cs`

## External references

- TECHNICAL.md §10.6 — Position resolution.
