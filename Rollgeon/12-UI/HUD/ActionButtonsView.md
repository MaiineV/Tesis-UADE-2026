---
title: ActionButtonsView
type: view
domain: 12-UI/HUD
status: done
tags: [ui, hud, view, combat, buttons]
---

# ActionButtonsView

> Sub-view that renders the three combat action buttons — Attack, Energy
> reroll, End Turn — and gates their interactable state by player turn,
> [[TurnManager]] `CanExecute`, and [[IRerollBudgetService]] availability.

## Overview

Owned by [[CombatHUDView]]. The buttons do **not** dispatch directly to
the [[TurnManager]] — they expose `UnityEvent` properties
(`OnAttackPressed`, `OnEnergyRerollPressed`, `OnEndTurnPressed`) that the
parent screen wires to delegates injected by `CombatController`. Plan
§3.5 / §4.5.

Subscribes to `OnTurnStarted`, `OnTurnFinished`, `OnEnergyChanged`, and
`OnRerollBudgetChanged` to re-evaluate `interactable` flags. Falls back
to a permissive enabled-state when [[TurnManager]] is absent (graceful
degradation).

## API / Shape

```csharp
public class ActionButtonsView : MonoBehaviour {
    public UnityEvent OnAttackPressed { get; }
    public UnityEvent OnEnergyRerollPressed { get; }
    public UnityEvent OnEndTurnPressed { get; }

    public void Bind(Guid playerGuid);
    public void Unbind();
    public void RefreshInteractable();
}
```

Serialized: `_attackButton`, `_energyRerollButton`, `_endTurnButton`,
`_attackAction` ([[ActionDefinitionSO]]).

## Dependencies

- **Uses:** [[TurnManager]], [[ActionDefinitionSO]],
  [[IRerollBudgetService]], [[ServiceLocator]], [[EventManager]],
  [[EventName]].
- **Used by:** [[CombatHUDView]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/HUD/ActionButtonsView.cs`
- Tests: `Assets/Scripts/Rollgeon/UI/Tests/ActionButtonsViewTests.cs`
