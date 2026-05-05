---
title: ComboRow
type: struct
domain: 12-UI/HUD
status: done
tags: [ui, hud, combat, combo]
---

# ComboRow

> Inspector-configurable struct used by [[ComboIndicatorView]] to map a
> contract combo id to its label and "blocked" overlay.

## Shape

```csharp
[Serializable]
public struct ComboRow {
    public string ComboId;            // e.g. "combo.par", "combo.generala"
    public TextMeshProUGUI Label;     // optional name label
    public GameObject BlockedOverlay; // toggled on OnComboBlocked / OnComboUnblocked
}
```

## Overview

Designer cables one row per combo in the warrior's contract (eight
entries expected). [[ComboIndicatorView]] iterates `_rows`, matches the
`ComboId` from [[EventName]] `OnComboBlocked` / `OnComboUnblocked`, and
toggles `BlockedOverlay` on the matching row. Distinct from
[[ComboRowView]], which is a MonoBehaviour for the contract-table row
prefab.

## Dependencies

- **Used by:** [[ComboIndicatorView]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/HUD/ComboIndicatorView.cs`
  (declared as nested `public struct`).
