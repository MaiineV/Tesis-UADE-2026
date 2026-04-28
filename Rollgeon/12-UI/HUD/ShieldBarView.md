---
title: ShieldBarView
type: view
domain: 12-UI/HUD
status: done
tags: [ui, hud, view, shield, attributes]
---

# ShieldBarView

> Renders the player's current shield value as an `Image` fill plus a
> formatted label. Auto-hides the container when shield is 0.

## Overview

Subscribes to [[EventName]] `OnShieldChanged(entityGuid, current)` and
filters by `entityGuid == _playerGuid`. On `Bind` it queries the
[[AttributesManager]] (when registered) for the initial `Shield` value
to prime the view before any event fires.

The fill image is normalized as `current / 100f` (clamped) — visually
cosmetic, the real cap lives in the `Shield` attribute. Plan §4 shield.

## API / Shape

```csharp
public class ShieldBarView : MonoBehaviour {
    public void Bind(Guid playerGuid);
    public void Unbind();
    public void SetValue(int current);
}
```

Serialized: `_fillImage`, `_text`, `_textFormat`, `_container`.

## Dependencies

- **Uses:** [[AttributesManager]], `Shield`, [[ServiceLocator]],
  [[EventManager]], [[EventName]].
- **Used by:** [[CombatHUDView]], [[ExplorationHUDView]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/HUD/ShieldBarView.cs`
