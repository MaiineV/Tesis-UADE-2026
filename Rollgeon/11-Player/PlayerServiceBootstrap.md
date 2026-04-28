---
title: PlayerServiceBootstrap
type: so
domain: 11-Player
status: done
tags: [player, bootstrap, so]
---

# PlayerServiceBootstrap

> `ScriptableObject` [[IPreloadableService]] wrapper that creates a
> [[PlayerService]] and registers it as [[IPlayerService]] in
> [[ServiceScope]] `Global`.

## Shape

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Bootstrap/Player Service")]
public sealed class PlayerServiceBootstrap : ScriptableObject, IPreloadableService {
    public int Priority => 30;
    public void Register();
}
```

## Behaviour

- `Priority = 30` — runs after foundational services
  ([[PhaseServiceBootstrap]] = 10) so the player service is ready
  before any UI/run-bound consumer.
- Idempotent: held instance prevents double-registration.

## Dependencies

- **Uses:** [[PlayerService]], [[IPlayerService]], [[ServiceLocator]],
  [[IPreloadableService]].
- **Used by:** [[ServiceBootstrapSO]].

## Code

`Assets/Scripts/Rollgeon/Player/PlayerServiceBootstrap.cs`

## External references

- TECHNICAL.md: §17.G Player service bootstrap
