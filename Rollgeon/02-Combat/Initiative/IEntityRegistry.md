---
title: IEntityRegistry
type: interface
domain: 02-Combat
status: done
tags: [combat, initiative, registry, interface]
---

# IEntityRegistry

> Registry that maps entity `Guid` → `ModifiableAttributes`. Lets
> [[DefaultInitiativeProvider]] resolve an entity's `Speed` stat without
> touching the entity-spawn pipeline directly.

## Overview

Stub interface declared inside `Rollgeon.Combat.Initiative` so the
combat worktree can compile and test in isolation. When the canonical
entity registry lands in `Rollgeon.Entities`, the migration is a
namespace move + find/replace — no rewrite, because consumers depend
on this contract via DI (constructor injection), not via
`ServiceLocator` lookups.

## API / Shape

```csharp
public interface IEntityRegistry {
    bool TryGetAttributes(Guid entityId, out ModifiableAttributes attrs);
}
```

## Dependencies
**Uses:** `ModifiableAttributes`.
**Used by:** [[DefaultInitiativeProvider]].
**Implemented by:** [[InMemoryEntityRegistry]].

## Code
`Assets/Scripts/Rollgeon/Combat/Initiative/IEntityRegistry.cs`
