---
title: PawnRegistryBootstrap
type: bootstrap
domain: 22-Feedback
status: done
tags: [feedback, bootstrap, so]
---

# PawnRegistryBootstrap

> `IPreloadableService` que registra una instancia de [[PawnRegistry]]
> como [[IPawnRegistry]] global. Priority 20 — mucho antes de
> [[FeedbackManagerBootstrap]] (55) para que el resolver de posición
> tenga el registry disponible.

## API / Shape

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Feedback/Pawn Registry Bootstrap")]
public sealed class PawnRegistryBootstrap : ScriptableObject, IPreloadableService {
    public int Priority => 20;
    public void Register();
}
```

## Dependencies

**Uses:** [[PawnRegistry]], [[IPawnRegistry]],
`Patterns.ServiceLocator`, `Rollgeon.Patterns.Bootstrap.IPreloadableService`.
**Used by:** `ServiceBootstrapSO.ExtraServices`.

## Code

`Assets/Scripts/Rollgeon/Feedback/PawnRegistryBootstrap.cs`
