---
title: FeedbackPlayer
type: enum
domain: 22-Feedback
status: done
tags: [feedback, enum]
---

# FeedbackPlayer

> Player target del modo `FromReader` ([[SpawnPosition]]). El
> [[IPositionReader]] lee este flag para decidir qué HUD/anchor usar.

## Shape

```csharp
public enum FeedbackPlayer {
    Player = 0,
    Enemy  = 1,
}
```

## Notas

Rollgeon es single-player (ver MEMORY user). El enum existe por
simetría con `FromReader` cuando el reader necesita resolver "el
adversario actual" (combate de boss, enemigos múltiples) sin acoplar
el reader a la entidad concreta.

## Dependencies

**Used by:** [[FeedbackEntry]] (`PlayerTarget`), [[FeedbackRequest]]
(`Player`), [[PositionReadInfo]] (`Player`),
[[FeedbackPositionResolver]] (`ResolveFromReader`).

## Code

`Assets/Scripts/Rollgeon/Feedback/FeedbackType.cs`
