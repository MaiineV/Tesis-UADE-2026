---
title: PawnRegistry
type: service
domain: 22-Feedback
status: done
tags: [feedback, registry]
---

# PawnRegistry

> Implementación default de [[IPawnRegistry]] sobre un `Dictionary<Guid,
> Transform>`. Tolera entradas stale: si el `Transform` fue destruido,
> remueve el slot al detectarlo en `TryGetTransform`.

## API / Shape

```csharp
public sealed class PawnRegistry : IPawnRegistry {
    public void Register(Guid entityGuid, Transform pawn);
    public void Unregister(Guid entityGuid);
    public bool TryGetTransform(Guid entityGuid, out Transform pawn);
}
```

## Dependencies

**Uses:** [[IPawnRegistry]].
**Used by:** [[PawnRegistryBootstrap]] (dueño), [[PawnRegistryBinding]]
(self-register), [[FeedbackPositionResolver]] (consumer).

## Code

`Assets/Scripts/Rollgeon/Feedback/PawnRegistry.cs`

## External references

- TECHNICAL.md §10.6 — Pawn registry default impl.
