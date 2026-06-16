---
title: DiceTypeExt
type: class
domain: 08-Dice
status: done
tags: [dice, extension, static]
---

# DiceTypeExt

> Static extensions on [[DiceType]] that publish the GD's per-face
> range and per-bag copy cap. Single source of truth for die math —
> everything else (`DiceBagSO`, `DiceRoller`, balance tools) reads
> from here. TECHNICAL.md §6.1.

## Shape

```csharp
public static class DiceTypeExt {
    public static int MaxFace(this DiceType t);   // 1..MaxFace inclusive
    public static int MaxPerBag(this DiceType t); // hard cap por bolsa de 5
}
```

## Tablas

| Type | MaxFace | MaxPerBag |
|------|---------|-----------|
| D4   | 4       | 5         |
| D6   | 6       | 5         |
| D8   | 8       | 4         |
| D10  | 10      | 3         |
| D12  | 12      | 2         |
| D20  | 20      | 1         |

`MaxPerBag` se aplica como hard cap en [[DiceBagSO]]`.Validate` y como
techo del `MaxInBag` configurable en [[DicePoolEntry]] / [[DiceBagPoolSO]].

## Dependencies

- **Uses:** [[DiceType]].
- **Used by:** [[DiceBagSO]], [[DiceBagPoolSO]], [[DiceRoller]],
  balance audits, BuildSelectionScreen.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Dice/DiceType.cs`

## External references

- TECHNICAL.md: §6.1 Dice types tables
