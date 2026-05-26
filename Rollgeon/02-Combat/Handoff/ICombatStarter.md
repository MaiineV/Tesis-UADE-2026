---
title: ICombatStarter
type: interface
domain: 02-Combat
status: done
tags: [combat, handoff, interface]
---

# ICombatStarter

> Test-friendly abstraction over [[CombatController]]`.StartCombat`.
> Production wires this through [[CombatControllerAdapter]]; tests
> inject a spy/stub so they can assert the participant list without a
> live FSM.

## API / Shape

```csharp
public interface ICombatStarter {
    void StartCombat(
        Guid playerId,
        IReadOnlyList<Guid> participants,
        Guid roomInstanceId,
        Action<Guid> enemyActionHandler);
}
```

## Dependencies
**Used by:** [[CombatHandoffService]].
**Implemented by:** [[CombatControllerAdapter]].

## Code
`Assets/Scripts/Rollgeon/Combat/Handoff/ICombatStarter.cs`
