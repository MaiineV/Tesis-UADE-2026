---
title: IFirstRollTracker
type: interface
domain: 02-Combat
status: done
tags: [combat, first-roll, interface]
---

# IFirstRollTracker

> Service that tracks whether a given entity is still inside its
> "first roll of combat". Consumed by `PCFirstRollOfCombat`
> (TECHNICAL §8.2 + §6 — Berserker passive).

## Overview

Resets on `OnCombatStart`. The flag for an entity is consumed when the
tracker observes the first `OnRollResolved` for that entity. Outside
combat the tracker returns `false` for everyone.

## API / Shape

```csharp
public interface IFirstRollTracker {
    bool IsFirstRoll(Guid entityGuid);
}
```

## Dependencies
**Used by:** `PCFirstRollOfCombat`, Berserker passive.
**Implemented by:** `FirstRollTrackerService`.

## Code
`Assets/Scripts/Rollgeon/Combat/FirstRoll/IFirstRollTracker.cs`
