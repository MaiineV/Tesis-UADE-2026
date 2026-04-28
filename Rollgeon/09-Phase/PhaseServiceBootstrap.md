---
title: PhaseServiceBootstrap
type: so
domain: 09-Phase
status: done
tags: [phase, bootstrap, so]
---

# PhaseServiceBootstrap

> `ScriptableObject` [[IPreloadableService]] wrapper that creates a
> [[PhaseService]] and registers it as `IPhaseService` in
> [[ServiceScope]] `Run`.

## Shape

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Bootstrap/Phase Service")]
public sealed class PhaseServiceBootstrap : ScriptableObject, IPreloadableService {
    public int Priority => 10;
    public void Register();
}
```

## Behaviour

- `Priority = 10` — runs early so phase-gated subscribers in later
  bootstraps can resolve `IPhaseService` immediately.
- Idempotent: holds the created instance internally, second `Register`
  is a no-op.
- Asset is dragged into `ServiceBootstrapSO.ExtraServices` in the
  inspector.

## Dependencies

- **Uses:** [[PhaseService]], [[ServiceLocator]],
  [[IPreloadableService]].
- **Used by:** [[ServiceBootstrapSO]].

## Code

`Assets/Scripts/Rollgeon/Phase/PhaseServiceBootstrap.cs`

## External references

- Setup: `docs/setup/Foundation#0007_PhaseServiceReal.md`
- TECHNICAL.md: §17.PHA PhaseService bootstrap
