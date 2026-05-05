---
title: ExplorationActionButtonsView
type: view
domain: 12-UI/HUD
status: done
tags: [ui, hud, view, exploration, buttons]
---

# ExplorationActionButtonsView

> Exploration-phase action panel. Each button maps by index to a
> `HeroActionBehavior` returned by
> `CurrentHero.GetBehaviorsForPhase(GamePhase.Exploration)`.

## Overview

Visible only while [[IPhaseService]]`.CurrentBase == Exploration`.
Subscribes to `OnPhaseEnter` / `OnPhaseExit` to show/hide itself, and
to `OnEnergyChanged` to refresh `interactable` flags based on each
behavior's `EnergyCost` vs. the player's current energy via
[[IEnergyService]].

Clicks call `IExplorationBehaviorService.OnBehaviorSelected(index)` —
the service decides what the index means in the active hero's
exploration behavior list.

## API / Shape

```csharp
public class ExplorationActionButtonsView : MonoBehaviour {
    public void Bind(Guid playerGuid);
    public void Unbind();
}
```

Serialized: `_buttons` (`List<Button>`).

## Dependencies

- **Uses:** [[IPhaseService]], [[IPlayerService]], [[IEnergyService]],
  `IExplorationBehaviorService`, `HeroActionBehavior`,
  [[ServiceLocator]], [[EventManager]], [[EventName]].
- **Used by:** [[ExplorationHUDView]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/HUD/ExplorationActionButtonsView.cs`
