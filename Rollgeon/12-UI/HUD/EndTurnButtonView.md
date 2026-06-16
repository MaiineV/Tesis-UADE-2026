---
title: EndTurnButtonView
type: view
domain: 12-UI/HUD
status: done
tags: [ui, hud, view, combat, buttons]
---

# EndTurnButtonView

> Standalone "End Turn" button widget. Listens to player-turn / dice
> events to gate its `interactable` flag.

## Overview

Distinct from the End-Turn button inside [[ActionButtonsView]] — this
is a separate prefab used when the layout splits End Turn out of the
main action panel. The view is enabled only when:

- the player's turn has started (`OnTurnStarted`), AND
- dice are not currently mid-roll (`OnDiceRolled` disables it,
  `OnRollResolved` re-enables it).

Clicking invokes the `OnEndTurnPressed` UnityEvent — wiring to the
controller is the parent screen's responsibility.

## API / Shape

```csharp
public class EndTurnButtonView : MonoBehaviour {
    public UnityEvent OnEndTurnPressed { get; }

    public void Bind(Guid playerGuid);
    public void Unbind();
    public void RefreshInteractable();
}
```

Serialized: `_endTurnButton`.

## Dependencies

- **Uses:** [[EventManager]], [[EventName]].
- **Used by:** [[CombatHUDView]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/HUD/EndTurnButtonView.cs`
- Tests: `Assets/Scripts/Rollgeon/UI/Tests/EndTurnButtonViewTests.cs`
