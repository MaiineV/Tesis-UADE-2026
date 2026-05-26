---
title: ICombatDeathWatcher
type: interface
domain: 02-Combat
status: done
tags: [combat, death, interface]
---

# ICombatDeathWatcher

> Marker interface for the runtime service that watches combat death
> events (HP reaching zero) and routes them — removing the dead entity
> from the turn order, firing victory / defeat, etc.

## Overview

Disposable so it can unsubscribe from event bus channels on combat
end. The concrete implementation lives at
`Assets/Scripts/Rollgeon/Combat/CombatDeathWatcher.cs` and registers
through the bootstrap pipeline.

## API / Shape

```csharp
public interface ICombatDeathWatcher : IDisposable { }
```

## Dependencies
**Used by:** [[CombatTurnFSM]], combat-end flow.
**Implemented by:** `CombatDeathWatcher`.

## Code
`Assets/Scripts/Rollgeon/Combat/ICombatDeathWatcher.cs`
