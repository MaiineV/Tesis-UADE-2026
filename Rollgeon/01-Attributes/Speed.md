---
title: Speed
type: system
domain: 01-Attributes
status: done
tags: [attributes, stat, combat, hidden]
---

# Speed

> Concrete Speed stat (`int`). Sets turn order before the speed-die
> tiebreak. **Hidden from the HUD** — the player only observes the
> resulting turn queue, never the numeric value.

## Shape

```csharp
[HiddenFromUI]
public sealed class Speed : BaseAttribute<int> {
    public Speed();
    public Speed(int initial);
    protected override BaseAttribute<int> CreateDuplicate() => new Speed(_rawValue);
}
```

## Hidden-from-UI marker

`[HiddenFromUI]` (see `HiddenFromUIAttribute.cs`) is read by HUD code
when iterating stats for display. Speed is the canonical example of
hidden design ("Hidden Speed"): intentionally opaque so the player has
to learn turn-order behaviour empirically.

## Turn-order role

[[DefaultInitiativeProvider]] reads
`AttributesManager.GetAttributeModifiedValue<Speed,int>(entityId)` per
entity, then folds in the speed-die roll governed by
[[TurnOrderConfig]].

## Dependencies

- **Uses:** [[BaseAttribute]], `HiddenFromUIAttribute`.
- **Used by:** [[DefaultInitiativeProvider]], [[TurnOrderConfig]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Attributes/Stats/Speed.cs`

## External references

- Setup: `docs/setup/System#0100c_TurnOrderHiddenSpeed.md`
- TECHNICAL.md: §4.2 / §12.7 Turn order — Hidden Speed
