---
title: IPlayerService
type: interface
domain: 11-Player
status: done
tags: [player, interface]
---

# IPlayerService

> Global service interface exposing the active player's identity
> ([[Guid]]), selected hero, run id, and runtime dice bag (§17.G).

## Shape

```csharp
public interface IPlayerService {
    Guid        PlayerGuid  { get; }
    Guid        RunId       { get; }
    ClassHeroSO CurrentHero { get; }
    DiceBagSO   DiceBag     { get; }

    void SetPlayer(ClassHeroSO hero, Guid runId);  // mints new PlayerGuid
    void SetDiceBag(DiceBagSO bag);                 // overrides default bag
    void ClearPlayer();

    event Action<ClassHeroSO> OnPlayerSet;
    event Action              OnPlayerCleared;
}
```

## Lifecycle

- Registered globally by [[PlayerServiceBootstrap]] during
  [[Bootstrap]].
- [[RunBootstrapper]]`.StartRun` calls `SetPlayer`; `EndRun` calls
  `ClearPlayer`.
- `SetDiceBag` is invoked by [[GameplayBootstrapper]] when a Phase 2
  built bag is present.

## Dependencies

- **Uses:** [[ClassHeroSO]], `DiceBagSO`.
- **Used by:** [[PlayerService]] (impl), [[RunBootstrapper]],
  [[RunController]], combat pipelines, HUD player views.

## Code

- Interface: `Assets/Scripts/Rollgeon/Player/IPlayerService.cs`
- Implementation: [[PlayerService]]

## External references

- TECHNICAL.md: §17.G Player service
