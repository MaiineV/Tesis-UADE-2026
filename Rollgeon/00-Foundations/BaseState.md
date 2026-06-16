---
title: BaseState
type: fsm
domain: 00-Foundations
status: done
tags: [foundation, fsm, state]
---

# BaseState

> Abstract base for every state that plugs into [[StateMachine]].
> Captures an immutable `TContext` and exposes lifecycle + tick hooks.

## API

```csharp
public abstract class BaseState<TContext, TInput> : IState {
    protected TContext Context { get; }
    public virtual string Name => GetType().Name;

    public virtual void Enter(TInput input) { }
    public virtual void Exit(TInput input) { }
    public virtual void Update() { }
    public virtual void LateUpdate() { }
    public virtual void FixedUpdate() { }

    // Return true + non-null next to transition; false to fall back
    // to the declarative table (if registered).
    public virtual bool CheckInput(TInput input,
                                   out BaseState<TContext, TInput> next);
}
```

## Contract

- Overriding `CheckInput` is optional. States using
  `StateMachineBuilder`'s declarative table return `false` and let the
  FSM consult the table.
- `Exit(default)` runs on `StateMachine.Stop()` — treat it as shutdown,
  not a normal transition.

## Dependencies

- **Uses:** [[IState]]
- **Used by:** every concrete state in the codebase (combat states, run
  states, UI overlay states).

## Code

- Runtime: `Assets/Scripts/Rollgeon/Patterns/FSM/BaseState.cs`

## External references

- Setup: `docs/setup/Foundation#0002_FSM.md`
- TECHNICAL.md: §1.3 FSM framework — BaseState
