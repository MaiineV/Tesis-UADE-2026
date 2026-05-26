---
title: ComboRowView
type: view
domain: 12-UI/HUD
status: done
tags: [ui, hud, view, combo]
---

# ComboRowView

> Sub-view representing one row of the contract combo table: name +
> base damage + optional description + optional icon. Plan §4.4.

## Overview

Instantiated from [[ContractDisplayView]]`.Bind`. Pure display — no
event subscriptions. Falls back to `BaseComboSO.ComboId` if
`DisplayName` is empty; hides the icon `Image` when `combo.Icon` is
null.

## API / Shape

```csharp
public class ComboRowView : MonoBehaviour {
    public void Bind(BaseComboSO combo);
}
```

Serialized: `_nameLabel`, `_damageLabel`, `_descriptionLabel`
(optional), `_iconImage` (optional).

## Dependencies

- **Uses:** `BaseComboSO`.
- **Used by:** [[ContractDisplayView]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/HUD/ComboRowView.cs`
- Prefab: `Assets/Rollgeon/Prefabs/UI/ComboRow.prefab` (designer setup,
  instructive §8.5).
