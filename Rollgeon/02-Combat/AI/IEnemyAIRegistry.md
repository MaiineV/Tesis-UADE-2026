---
title: IEnemyAIRegistry
type: interface
domain: 02-Combat
status: done
tags: [combat, ai, registry, interface]
---

# IEnemyAIRegistry

> Run-scoped registry that maps enemy `Guid` → AI metadata (decision
> tree root + reference `MaxHp`). TECHNICAL §7.5.

## Overview

Populated at enemy spawn by [[IEnemySpawnResolver]]; consumed by
`TreeDrivenEnemyAI` on each enemy turn. Decouples spawn-side data from
turn-side execution so tests can swap registries without touching the
spawn pipeline.

## API / Shape

```csharp
public interface IEnemyAIRegistry {
    void Register(Guid enemyId, AIDecisionNode root, int maxHp);
    void Unregister(Guid enemyId);
    bool TryGet(Guid enemyId, out AIDecisionNode root, out int maxHp);
    bool Has(Guid enemyId);
}
```

## Dependencies
**Uses:** `AIDecisionNode`.
**Used by:** `TreeDrivenEnemyAI`, [[IEnemySpawnResolver]].

## Code
`Assets/Scripts/Rollgeon/Combat/AI/IEnemyAIRegistry.cs`
