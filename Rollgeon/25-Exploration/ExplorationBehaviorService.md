---
title: ExplorationBehaviorService
type: service
domain: 25-Exploration
status: done
tags: [exploration, behaviors, service]
---

# ExplorationBehaviorService

> Concrete [[IExplorationBehaviorService]] — the bridge between the exploration HUD's behavior bar and `HeroActionBehavior.Execute`, with phase-gated lifecycle, energy spend, and BeforeRoll target selection.

## Overview

Three internal states: `Inactive` (outside Exploration), `Idle` (Exploration entered, ready for input), `Selecting` (waiting on [[ISelectionController]]). The constructor subscribes to `OnPhaseEnter` / `OnPhaseExit` so the service auto-flips between Inactive ↔ Idle when [[GamePhase]]`.Exploration` enters/leaves; `CreateAndRegister` instantiates and registers under [[IExplorationBehaviorService]] in `ServiceScope.Run`.

`OnBehaviorSelected(int)`:

1. Resolves the current hero and the Exploration-phase behavior list via [[IPlayerService]].
2. Runs the behavior's `ShowConditions` against a [[PreConditionContext]] — fail = no-op.
3. Spends energy via [[IEnergyService]] if `EnergyCost > 0`.
4. If any effect requires `SelectionTiming.BeforeRoll`, hands off to `BeginSelection`; otherwise calls `ExecuteBehavior` immediately.

`BeginSelection` resolves the first selecting effect's `SelectionSettings`, looks up the player position via [[IGridManager]], and short-circuits for `SlotState.Self` or auto-resolve targets. Otherwise it fires `ISelectionController.BeginSelection` with the resolved valid tiles, transitions to `Selecting`, and waits for `OnSelectionCompleted` before running the behavior with the selection result.

`CancelSelection` and `Dispose` both unsubscribe the selection-controller event and tear down the phase event handlers.

## API / Shape

```csharp
public sealed class ExplorationBehaviorService : IExplorationBehaviorService, IDisposable {
    public static ExplorationBehaviorService CreateAndRegister();
    public bool IsActive { get; }
    public void OnBehaviorSelected(int index);
    public void CancelSelection();
    public void Dispose();
}
```

## Dependencies

**Uses:** [[GamePhase]], [[EventManager]], [[EventName]], [[ServiceLocator]], [[IPlayerService]], [[IEnergyService]], [[IGridManager]], `ISelectionController`, `HeroActionBehavior`, `HeroBehaviorContext`, `SelectionSettings`, `SelectionTiming`, `SlotState`, `TargetSelectionResult`, `TargetRef`, [[Entity]], [[PreConditionContext]].
**Used by:** Exploration HUD / behavior bar (calls `OnBehaviorSelected`), input handlers (cancel).

## Code

- Runtime: `Assets/Scripts/Rollgeon/Exploration/ExplorationBehaviorService.cs`
- Tests: `Assets/Scripts/Rollgeon/Exploration/Tests/ExplorationBehaviorServiceTests.cs`
