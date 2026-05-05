---
title: DiceType
type: enum
domain: 08-Dice
status: done
tags: [dice, enum]
---

# DiceType

> Enum of die shapes available to the Dice Builder (D4..D20).
> Foundation for the bag, pool and roller — every other dice type
> indexes into it. TECHNICAL.md §6.1.

## Shape

```csharp
public enum DiceType {
    D4, D6, D8, D10, D12, D20,
}
```

- `D4` — 4 faces, weakest, plentiful in bag.
- `D6` — 6 faces, generic workhorse.
- `D8` — 8 faces.
- `D10` — 10 faces.
- `D12` — 12 faces.
- `D20` — 20 faces, capped at 1 per bag.

Per-face range and per-bag cap live in [[DiceTypeExt]] to keep the
enum stable. Adding a die means appending a value here and extending
the two switches in `DiceTypeExt`.

## Dependencies

- **Used by:** [[DiceBagSO]], [[DiceBagPoolSO]], [[DicePoolEntry]],
  [[DiceRoller]], [[IDiceRoller]], [[DiceTypeExt]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Dice/DiceType.cs`
- Tests: `.../Tests/DiceTypeTests.cs`

## External references

- TECHNICAL.md: §6.1 Dice types
