---
title: CombatTurnFSM
type: fsm
domain: 02-Combat/FSM
status: done
tags: [combat, fsm]
---

# CombatTurnFSM

> Thin wrapper over [[StateMachine]]`<CombatContext, CombatInput>` that
> pre-composes the four combat states and exposes a combat-specific API.

## States

1. [[CombatEnterState]] — entry point; builds turn order and routes to
   the first actor.
2. [[PlayerTurnState]] — awaits player actions, re-enters itself on
   `PlayerActionDone`.
3. [[EnemyTurnState]] — invokes the injected enemy AI and awaits
   `EnemyDone`.
4. [[CombatExitState]] — terminal; publishes `OnFinished(outcome)`.

## API

```csharp
public sealed class CombatTurnFSM {
    public CombatEnterState Enter { get; }
    public PlayerTurnState  Player { get; }
    public EnemyTurnState   Enemy  { get; }
    public CombatExitState  ExitState { get; }
    public BaseState<CombatContext, CombatInput> Current { get; }
    public bool IsRunning { get; }
    public CombatContext Context { get; }

    public event Action<CombatInput> OnInputAccepted;
    public event Action<CombatOutcome> OnFinished;

    public CombatTurnFSM(CombatContext context);
    public void SetParticipants(IReadOnlyList<Guid> participants); // before Start
    public void Start(); public void Stop();
    public void SendInput(CombatInput input);
    public void Update(); public void LateUpdate(); public void FixedUpdate();
}
```

## Dependencies

- **Uses:** [[StateMachine]], [[BaseState]], [[CombatContext]],
  [[CombatInput]], [[CombatOutcome]], the 4 states.
- **Used by:** [[CombatController]], [[CombatControllerAdapter]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combat/FSM/CombatTurnFSM.cs`
- Tests: `.../Tests/CombatTurnFSMTests.cs`,
  `.../Tests/CombatControllerFreezeTests.cs`

## External references

- Setup: `docs/setup/System#0100d_CombatTurnFSM.md`
- TECHNICAL.md: §12 Combat FSM
