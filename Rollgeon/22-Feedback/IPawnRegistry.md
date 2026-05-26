---
title: IPawnRegistry
type: interface
domain: 22-Feedback
status: done
tags: [feedback, registry]
---

# IPawnRegistry

> Servicio opcional consultado por el pipeline §10.6 para resolver un
> `Guid` de entidad a un `Transform` de escena. Si no está registrado,
> el position resolver cae al `WorldPosition` del request o a
> `Vector3.zero`.

## API / Shape

```csharp
public interface IPawnRegistry {
    void Register(Guid entityGuid, Transform pawn);
    void Unregister(Guid entityGuid);
    bool TryGetTransform(Guid entityGuid, out Transform pawn);
}
```

`TryGetTransform` devuelve `false` si no hay registro **o** si el
transform fue destruido (referencia null) — limpia el slot stale al
detectar.

## Dependencies

**Used by:** [[PawnRegistry]] (impl default), [[PawnRegistryBinding]]
(self-register de pawn visuals), [[PawnRegistryBootstrap]],
[[FeedbackPositionResolver]], [[FeedbackManager]] (`ResolveAnimator`,
`ApplyImpulse`).

## Code

`Assets/Scripts/Rollgeon/Feedback/IPawnRegistry.cs`

## External references

- TECHNICAL.md §10.6 — Pawn registry contract.
