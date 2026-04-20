---
title: PlayerService
type: service
domain: 11-Player
status: done
tags: [player, service]
---

# PlayerService

> Singleton service that tracks the active player's identity, selected
> hero, and current run. Globally scoped so the identity persists across
> run boundaries until explicitly cleared.

## Shape

```csharp
public interface IPlayerService {
    Guid PlayerGuid   { get; }
    Guid RunId        { get; }
    ClassHeroSO CurrentHero { get; }

    void SetPlayer(ClassHeroSO hero, Guid runId); // assigns new PlayerGuid
    void ClearPlayer();                            // on run end / reset

    event Action<ClassHeroSO> OnPlayerSet;
    event Action              OnPlayerCleared;
}

public sealed class PlayerService : IPlayerService, IDisposable { ... }
```

## Behaviour

- `SetPlayer` mints a new `PlayerGuid` (`Guid.NewGuid()`) — prevents
  stale references after a run reset.
- Registered globally by `PlayerServiceBootstrap`
  ([[IPreloadableService]]) during [[Bootstrap]].
- [[RunBootstrapper]] calls `SetPlayer` in `StartRun` and `ClearPlayer`
  in `EndRun`.

## Dependencies

- **Uses:** [[ClassHeroSO]].
- **Used by:** [[RunBootstrapper]], [[RunController]], combat systems
  that need the player entity id, HUD player panels.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Player/PlayerService.cs`
- Interface: `.../IPlayerService.cs`
- Bootstrap: `.../PlayerServiceBootstrap.cs`
- Tests: `.../Tests/PlayerServiceTests.cs`

## External references

- Setup: `docs/setup/Foundation#0006_PlayerServiceReal.md`
- TECHNICAL.md: §17.G Player service
