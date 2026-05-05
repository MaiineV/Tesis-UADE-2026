---
title: IInitiativeProvider
type: interface
domain: 02-Combat
status: done
tags: [combat, initiative, interface]
---

# IInitiativeProvider

> Replaceable strategy that returns the initiative value of an entity
> (TECHNICAL §12.7). Higher = earlier in the turn queue.

## Overview

The default implementation is [[DefaultInitiativeProvider]] — `Speed +
die(min, max)`. Game modes that want flat initiative, fixed order, or
custom rules implement this interface and register via
`ServiceLocator.AddService<IInitiativeProvider>(...)`.

## API / Shape

```csharp
public interface IInitiativeProvider {
    int RollInitiative(Guid entityGuid);
}
```

## Dependencies
**Uses:** [[IEntityRegistry]], [[IInitiativeRng]].
**Used by:** [[TurnOrderService]].
**Implemented by:** [[DefaultInitiativeProvider]].

## Code
`Assets/Scripts/Rollgeon/Combat/Initiative/IInitiativeProvider.cs`
