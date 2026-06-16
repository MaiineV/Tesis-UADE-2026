---
title: StateMachine
type: fsm
domain: 00-Foundations
status: done
tags: [foundation, fsm, patterns]
---

# StateMachine

> Generic, pure-C# FSM coordinator. Every domain FSM in the project
> (combat turns, run lifecycle, UI overlays) inherits from this shape.

## Purpose

Provide a reentrancy-safe, allocation-free finite state machine that the
owner ticks manually (no `MonoBehaviour` dependency, no `UnityEngine`
coupling in core). Supports both **imperative transitions**
(`BaseState.CheckInput`) and a **declarative fallback table** populated
by `StateMachineBuilder`.

## API

```csharp
public sealed class StateMachine<TContext, TInput> {
    public TContext Context { get; }
    public BaseState<TContext, TInput> Current { get; }
    public bool IsRunning { get; }

    public event Action<BaseState<TContext, TInput>> OnStateEntered;
    public event Action<BaseState<TContext, TInput>> OnStateExited;
    public event Action<..., ..., TInput> OnTransition;

    public void Start(TInput initialInput = default);
    public void Stop();
    public void SendInput(TInput input);          // reentrant-safe
    public void ForceState(BaseState<...> next, TInput input = default);
    public void Update(); void LateUpdate(); void FixedUpdate();
}
```

## Reentrancy

`SendInput` and `ForceState` use an internal dispatch flag + queue so
that transitions triggered from inside `Enter` / `Exit` are drained
after the current dispatch finishes. Prevents state-machine double-enter
bugs.

## Dependencies

- **Uses:** [[BaseState]], [[IState]]
- **Used by:** [[CombatTurnFSM]], combat states, UI overlay state, run
  lifecycle FSM.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Patterns/FSM/StateMachine.cs`
- Builder: `.../StateMachineBuilder.cs`, `.../TransitionGuard.cs`
- Tests: `.../Tests/FSMTests.cs`

## External references

- Setup: `docs/setup/Foundation#0002_FSM.md`
- TECHNICAL.md: §1.3 FSM framework
