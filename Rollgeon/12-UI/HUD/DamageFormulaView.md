---
title: DamageFormulaView
type: view
domain: 12-UI/HUD
status: done
tags: [ui, hud, view, combat, damage]
---

# DamageFormulaView

> HUD label that previews the outgoing damage formula for the player's
> currently-armed action — e.g. `"Par (10) × Estocada (1.5) = 15"`.

## Overview

Subscribes to the `TypedEvent<ComboMatchedPayload>` bus to track the
last matched combo for the local player. The currently-armed action is
pushed by the parent screen via `SetBehavior(HeroActionBehavior)`. On
update, finds the first `DealDamage` effect on the behavior and prints:

- `Constant` source → `"{ActionName} ({BaseAmount})"`.
- No combo yet → `"{ActionName} (sin combo)"`.
- Combo present → `"{ComboName} ({BaseDmg}) × {ActionName} (×{Mult}) = {Total}"`.

## API / Shape

```csharp
public class DamageFormulaView : MonoBehaviour {
    public void Bind(Guid playerGuid);
    public void Unbind();
    public void SetBehavior(HeroActionBehavior behavior);
    public void ClearBehavior();
}
```

Serialized: `_formulaLabel` (TMP).

## Dependencies

- **Uses:** `HeroActionBehavior`, `DealDamageEffect`, `DamageSource`,
  `ComboMatchedPayload`, `TypedEvent<>`.
- **Used by:** [[CombatHUDView]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/HUD/DamageFormulaView.cs`
