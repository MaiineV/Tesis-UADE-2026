---
title: InMemoryEntityRegistry
type: service
domain: 02-Combat/Initiative
status: done
tags: [combat, initiative, registry]
---

# InMemoryEntityRegistry

> Flat in-memory mapping `Guid → Entity metadata` used during a run to
> look up combat participants. Created per-run by [[RunController]]
> `.OnRunStart`.

## Role

- Satisfies [[IEntityRegistry]] without any persistence layer.
- Consumed by [[DefaultEnemySpawnResolver]] to register spawned enemies
  and by [[TurnOrderService]] to translate GUID lists into actor data.

## Scope

Registered in [[ServiceScope]] `Run` — cleared when the run ends.

## Dependencies

- **Uses:** `IEntityRegistry` interface.
- **Used by:** [[DefaultEnemySpawnResolver]], [[CombatHandoffService]],
  [[RunController]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combat/Initiative/InMemoryEntityRegistry.cs`
- Interface: `.../IEntityRegistry.cs`

## External references

- TECHNICAL.md: §12.8 Entity registry (run scope)
