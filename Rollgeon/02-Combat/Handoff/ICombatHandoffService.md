---
title: ICombatHandoffService
type: interface
domain: 02-Combat
status: done
tags: [combat, handoff, interface]
---

# ICombatHandoffService

> Marker interface for the orchestrator that transitions exploration →
> combat: spawns enemies, pushes the combat HUD screen, and starts the
> combat FSM.

## Overview

The concrete implementation auto-subscribes to
`EventName.OnCombatTriggered` so callers don't dispatch combat starts
manually — they just trigger the event. `IsHandoffInProgress` is
inspected by tests / debug overlays to detect mid-handoff state.

## API / Shape

```csharp
public interface ICombatHandoffService : IDisposable {
    bool IsHandoffInProgress { get; }
}
```

## Dependencies
**Uses:** [[ICombatStarter]], [[IEnemySpawnResolver]], [[IScreenManager]].
**Implemented by:** [[CombatHandoffService]].

## Code
`Assets/Scripts/Rollgeon/Combat/Handoff/ICombatHandoffService.cs`
