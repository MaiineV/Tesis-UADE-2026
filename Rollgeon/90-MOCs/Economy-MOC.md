---
title: Economy-MOC
type: moc
domain: 90-MOCs
status: done
tags: [moc, economy]
---

# 19-Economy — Map of Content

> Single-currency run economy — gold tally, add/spend contract, and the
> enemy gold-drop pipeline that credits the wallet on death.

## Relationships

```
 ServiceLocator (Run scope)
       │
       ▼
 IEconomyService ── CurrentGold / Add / Spend / CanAfford
       │   └ emits OnGoldChanged [current, delta]
       │
 EnemyGoldDropService
       ├─ subscribes OnEntityDestroyed
       ├─ RegisterDrop(entityGuid, amount)  ← DefaultEnemySpawnResolver
       └─ on death → IEconomyService.Add + OnFloatingNumberRequested(Gold)

 ShopItemPedestalInteractable → IEconomyService.Spend(price)
 GoldCounterView                ← OnGoldChanged
```

## Pages

### Core service
- [[IEconomyService]] — public interface
- [[EconomyService]] — in-memory counter impl
- [[EconomyBootstrap]] — registers the service

### Drop pipeline
- [[EnemyGoldDropService]] — maps enemy Guid → drop amount, fires on
  `OnEntityDestroyed`

## Cross-domain edges

- **Incoming** (consumers):
  - 02-Combat: [[EnemyGoldDropService]] subscribes `OnEntityDestroyed`;
    [[DefaultEnemySpawnResolver]] rolls `Min/MaxGoldDrop` from
    [[EnemyDataSO]] and calls `RegisterDrop`.
  - 06-Run: [[RunController]] / [[RunContext]] tally gold across the run.
  - 14-UI: [[GoldCounterView]] subscribes to `OnGoldChanged`.
  - 20-Shop: [[ShopItemPedestalInteractable]] calls `Spend` on purchase.
  - 11-Player: future `Gold` attribute adapter (TECHNICAL.md §1.3).
- **Outgoing** (dependencies):
  - 00-Foundations: [[ServiceLocator]], [[EventManager]], [[EventName]]
    (`OnGoldChanged`, `OnEntityDestroyed`, `OnFloatingNumberRequested`).
