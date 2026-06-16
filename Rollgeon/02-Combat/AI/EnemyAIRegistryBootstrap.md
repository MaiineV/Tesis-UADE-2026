---
title: EnemyAIRegistryBootstrap
type: so
domain: 02-Combat/AI
status: done
tags: [combat, ai, bootstrap, so]
---

# EnemyAIRegistryBootstrap

> `ScriptableObject` [[IPreloadableService]] that creates an
> [[EnemyAIRegistry]] and registers it as [[IEnemyAIRegistry]] in
> [[ServiceScope]] `Run`.

## Shape

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Combat/AI/Enemy AI Registry Bootstrap")]
public sealed class EnemyAIRegistryBootstrap : ScriptableObject, IPreloadableService {
    public int Priority => 77;
    public void Register();
}
```

## Behaviour

- `Priority = 77` — runs before `DefaultEnemySpawnResolver` (which
  writes into the registry) and before [[TreeDrivenEnemyAI]] (the
  reader).
- Run-scope: rebuilt every run via `ClearScope(Run)`.

## Dependencies

- **Uses:** [[EnemyAIRegistry]], [[IEnemyAIRegistry]],
  [[ServiceLocator]], [[IPreloadableService]].
- **Used by:** [[ServiceBootstrapSO]].

## Code

`Assets/Scripts/Rollgeon/Combat/AI/EnemyAIRegistryBootstrap.cs`

## External references

- TECHNICAL.md: §7.5 AI registry bootstrap
