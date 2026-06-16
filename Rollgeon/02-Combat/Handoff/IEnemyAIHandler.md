---
title: IEnemyAIHandler
type: interface
domain: 02-Combat
status: done
tags: [combat, handoff, ai, interface]
---

# IEnemyAIHandler

> Slot the handoff service binds to as `enemyActionHandler` when
> calling [[ICombatStarter]]`.StartCombat`. The FSM calls
> `HandleEnemyTurn(enemyId)` on each enemy turn; the handler delegates
> to the registered AI.

## API / Shape

```csharp
public interface IEnemyAIHandler {
    void HandleEnemyTurn(Guid enemyId);
}
```

## Dependencies
**Used by:** [[CombatHandoffService]], [[ICombatStarter]].
**Implemented by:** the AI dispatcher (BasicEnemyAI / TreeDrivenEnemyAI bridge).

## Code
`Assets/Scripts/Rollgeon/Combat/Handoff/IEnemyAIHandler.cs`
