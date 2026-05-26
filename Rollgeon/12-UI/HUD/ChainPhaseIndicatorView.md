---
title: ChainPhaseIndicatorView
type: view
domain: 12-UI/HUD
status: done
tags: [ui, hud, view, chain]
---

# ChainPhaseIndicatorView

> HUD widget that announces the current phase of a multi-phase combo
> chain (e.g. `Phase 2/3`) and hides itself when no chain is active.

## Overview

Subscribes to [[EventName]] `OnChainPhaseStarted(sourceGuid, phaseIndex,
totalPhases)` and `OnChainCompleted(sourceGuid)`. Filters by
`sourceGuid == _playerGuid` and updates a `_text` TMP label with the
configured format (default `Phase {0}/{1}`, 1-based).

## API / Shape

```csharp
public class ChainPhaseIndicatorView : MonoBehaviour {
    public void Bind(Guid playerGuid);
    public void Unbind();
    public void Show(int phaseIndex, int totalPhases);
    public void Hide();
}
```

Serialized: `_text`, `_textFormat`, `_container`.

## Dependencies

- **Uses:** [[EventManager]], [[EventName]].
- **Used by:** [[CombatHUDView]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/HUD/ChainPhaseIndicatorView.cs`
