---
title: EnemyGoldDropService
type: service
domain: 19-Economy
status: done
tags: [economy, service, drops]
---

# EnemyGoldDropService

> Maps enemy `Guid`s to the gold they drop on death; on `OnEntityDestroyed` it credits the [[IEconomyService]] and fires a floating `+XG` number.

## Overview

Run-scoped service. Subscribes to `OnEntityDestroyed` in its constructor; consumers register pending drops via `RegisterDrop(entityId, amount)`. The actual `Min/MaxGoldDrop` rolling lives in [[DefaultEnemySpawnResolver]] (which calls `RegisterDrop` after rolling against the [[EnemyDataSO]]). Implements `IDisposable` to unsubscribe and clear pending entries on run teardown.

If a destroyed entity has no registered drop, the handler is a no-op for that guid — non-enemy destructions never spuriously credit gold.

## API

```csharp
public sealed class EnemyGoldDropService : IDisposable {
    public EnemyGoldDropService(IEconomyService economy);
    public void RegisterDrop(Guid entityId, int amount); // amount<=0 or empty guid = no-op
    public void Dispose();
}
```

On destruction it triggers `OnFloatingNumberRequested` with `FloatingNumberType.Gold` so the HUD can render the `+XG` popup.

## Dependencies

**Uses:** [[IEconomyService]], [[EventManager]], [[EventName]] (`OnEntityDestroyed`, `OnFloatingNumberRequested`), [[FloatingNumberType]].
**Used by:** [[RunController]] (constructs and registers in `ServiceScope.Run`), [[DefaultEnemySpawnResolver]] (calls `RegisterDrop` after rolling), indirectly [[EnemyDataSO]] / [[EnemyCatalogSO]] (provide `Min/MaxGoldDrop` data).

## Code

`Assets/Scripts/Rollgeon/Economy/EnemyGoldDropService.cs`

## External references

- TECHNICAL.md §17.F (gold flow), enemy data §5.
