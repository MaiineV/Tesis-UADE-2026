---
title: DiceSlotView
type: view
domain: 12-UI/HUD
status: done
tags: [ui, hud, view, dice]
---

# DiceSlotView

> Sub-view for a single die slot. Used in two contexts: build selection
> (shows die-type label) and combat (shows rolled face value, hold
> toggle). UI#0013a / T97c.

## Overview

In build selection, the parent calls `Bind(diceTypeName)` to label each
slot. In combat, [[DiceZoneView]] calls `ShowFace(int)` after a roll and
`SetHeld(bool)` when the die is held; `OnToggled` fires when the player
clicks the slot.

## Sprite roles

Each `DiceType` has a **set of 5 sprites** in [[DiceShapeCatalogSO]]
(`Front`, `SideA`, `SideB`, `Hover`, `Selected`) — the columns of
`Assets/Art/UI/Dices/Dices.png`. The slot picks one of them from its whole
state via `DiceShapeRoleResolver.Resolve`, with precedence
**spin > blocked > held > hovered > Front**.

This view is the **only writer of `Image.sprite`** for the slot: every
mutator sets state and funnels through one private `RefreshShape()`. That
is deliberate — with one writer per state, the sprite that ends up showing
depends on call order, which is exactly what leaves a die wearing the wrong
face (e.g. `DiceZoneView.RefreshDiceShapes` repaints all five slots on
every roll, held ones included).

Related:

- **Hover state** is owned by [[DiceSlotHoverJuice]] (same GameObject),
  which gates on `Button.interactable` and resets on disable; it calls
  `SetHovered`. uGUI does not fire `OnPointerExit` on a deactivating
  object, and `ClearAll` deactivates slots.
- **Spin cycling** is driven by [[DiceSlotAnimator]] via `SetSpinRole`,
  released to `null` on landing.
- **Blocked** keeps its gray tint + lock icon on top of the resolved
  sprite. Held no longer uses a blue tint — the `Selected` sprite carries
  it, and [[DiceSlotJuice]]'s glow pulse accompanies.

## API / Shape

```csharp
public class DiceSlotView : MonoBehaviour {
    public UnityEvent OnToggled;
    public int CurrentFace { get; }

    public void Bind(string diceTypeName);
    public void Bind(DiceType type);
    public void SetDiceType(DiceType type);
    public void SetHovered(bool hovered);
    public void SetSpinRole(DiceShapeRole? role);
    public void SetEnchantVisual(EnchantmentSO enchantment);
    public void ShowFace(int face);
    public void SetSpinPreviewFace(int face);
    public void SetHoldInteractable(bool interactable);
    public void SetHeld(bool held);
    public void SetBlocked(bool blocked);
    public void Clear();
}
```

Serialized: `_diceLabel`, `_button` (optional), `_background` (optional
`Image`), `_shapeCatalog` (optional — falls back to
`Resources/Dice/DiceShapeCatalog`), `_shapedOverlays`, `_enchantMaterial`,
`_lockIcon`.

The prefab's `Button` transition is **None** on purpose: its
`targetGraphic` is the same `Image` this view owns, so ColorTint fought the
view's writes (and dimmed the die to 50% alpha for the whole spin via
`DisabledColor`).

## Dependencies

- **Used by:** [[DiceZoneView]], [[BuildSelectionScreen]].
- **Reads:** [[DiceShapeCatalogSO]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/HUD/DiceSlotView.cs`
- Resolver: `Assets/Scripts/Rollgeon/UI/HUD/DiceShapeRoleResolver.cs`
- Tests: `Assets/Scripts/Rollgeon/UI/Tests/DiceShapeRoleResolverTests.cs`
