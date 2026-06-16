---
title: IState
type: interface
domain: 00-Foundations
status: done
tags: [foundation, fsm, interface]
---

# IState

> Non-generic marker interface for [[BaseState]]. Enables heterogeneous
> collections and logs without dragging `TContext` / `TInput` everywhere.

## Shape

```csharp
public interface IState {
    string Name { get; } // default: GetType().Name in BaseState
}
```

## Why non-generic

State lists, logging, inspector debug views, transition history — all
want to hold "any state" without caring about its generic parameters.
Splitting the marker off [[BaseState]] keeps those call sites clean.

## Dependencies

- **Used by:** [[BaseState]], FSM logging, telemetry.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Patterns/FSM/IState.cs`

## External references

- TECHNICAL.md: §1.3 FSM framework — IState
