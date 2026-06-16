---
title: Run-MOC
type: moc
domain: 90-MOCs
status: done
tags: [moc, run, lifecycle]
---

# 10-Run — Map of Content

> Lifecycle of a single run: start → floors → combat → end.

## Relationships

```
 RunBootstrapper.StartRun(hero, ruleset, runId)
     ├─ creates RunContext, registers IRunContextService (Run scope)
     ├─ IPlayerService.SetPlayer(hero, runId)
     └─ EventManager.Trigger(OnRunStart, runId, rulesetId)
                 │
                 ↓
 RunController (listening) spawns run-scoped services:
   InMemoryEntityRegistry, DefaultEnemySpawnResolver, DungeonManager,
   DamagePipeline, HealPipeline, BasicEnemyAI, ExplorationController,
   CombatHandoffService, CombatReturnService
     → ExplorationController.BeginExploration

 RunBootstrapper.EndRun(runId)
     ├─ mark RunContext inactive
     ├─ IPlayerService.ClearPlayer
     ├─ EventManager.Trigger(OnRunEnd)
     └─ ServiceLocator.ClearScope(Run)   — disposes everything above
```

## Notes

- **Lifecycle:** [[RunBootstrapper]] · [[RunController]] ·
  [[IRunController]] · [[RunControllerBootstrapper]] ·
  [[GameplayBootstrapper]]
- **Context:** [[RunContext]] · [[IRunContextService]] ·
  [[PendingRunRequest]]

## Cross-domain edges

- Drives every Run-scoped service in [[Combat-MOC]] and
  [[Dungeon-MOC]].
- Writes [[IPlayerService]] (see [[Player-MOC]]).
- [[EventName]] `OnRunStart` / `OnRunEnd` are load-bearing events for
  [[Modifier]] lifetimes and [[ComboCountersService]].
