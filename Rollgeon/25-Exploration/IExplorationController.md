---
title: IExplorationController
type: interface
domain: 25-Exploration
status: done
tags: [exploration, controller, interface]
---

# IExplorationController

> Run-scoped contract that arbitrates the Combat ↔ Exploration phase transitions and the active-room handler — the entry point [[RunController]] calls when a floor begins or a combat finishes.

## Overview

Implementations expose `IsExploring` and two phase-driven methods. `BeginExploration` flips into [[GamePhase]]`.Exploration` and processes the current room; `ResumeAfterCombat` is the post-combat re-entry. Since the 2026-04-22 door-driven refactor (TECHNICAL.md §13.6) there is no `AdvanceRoom()` — room transitions are triggered by the player crossing a door via [[IDungeonService]]`.EnterRoomByDoor`.

## API / Shape

```csharp
public interface IExplorationController {
    bool IsExploring { get; }
    void BeginExploration();
    void ResumeAfterCombat();
}
```

## Dependencies

**Used by:** [[ExplorationController]] (impl), [[RunController]], CombatReturnService, [[DungeonManager]] consumers.

## Code

`Assets/Scripts/Rollgeon/Exploration/IExplorationController.cs`
