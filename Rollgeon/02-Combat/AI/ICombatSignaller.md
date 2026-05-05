---
title: ICombatSignaller
type: interface
domain: 02-Combat
status: done
tags: [combat, ai, fsm, interface]
---

# ICombatSignaller

> Narrow contract that lets enemy AI signal turn completion / combat
> end to the combat FSM without taking a hard reference to
> [[CombatController]].

## Overview

Decouples AI from the FSM (Setup#0012b). The handoff service
implements it; AI invokes `SignalEnemyDone` after its tree finishes
executing, and `NotifyCombatEnded` when the AI itself decides combat
is over (e.g. all-enemies-dead detection).

## API / Shape

```csharp
public interface ICombatSignaller {
    void SignalEnemyDone();
    void NotifyCombatEnded(CombatOutcome outcome);
}
```

## Dependencies
**Uses:** [[CombatOutcome]].
**Used by:** `BasicEnemyAI`, `TreeDrivenEnemyAI`.
**Implemented by:** [[CombatHandoffService]] / [[CombatControllerAdapter]].

## Code
`Assets/Scripts/Rollgeon/Combat/AI/ICombatSignaller.cs`
