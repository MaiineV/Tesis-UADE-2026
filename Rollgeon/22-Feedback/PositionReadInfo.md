---
title: PositionReadInfo
type: struct
domain: 22-Feedback
status: done
tags: [feedback, position]
---

# PositionReadInfo

> Info que el position resolver pasa al [[IPositionReader]]: solo el
> [[FeedbackPlayer]] que "posee" el request (para patrones como
> "centro del HUD del jugador X").

## Shape

```csharp
public struct PositionReadInfo {
    public FeedbackPlayer Player;
}
```

## Dependencies

**Uses:** [[FeedbackPlayer]].
**Used by:** [[IPositionReader]] (parámetro de `Read`),
[[FeedbackPositionResolver]] (constructor inline en `ResolveFromReader`).

## Code

`Assets/Scripts/Rollgeon/Feedback/IPositionReader.cs`
