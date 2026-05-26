---
title: DiceBagPoolSO
type: so
domain: 08-Dice
status: done
tags: [dice, so, pool, build-selection]
---

# DiceBagPoolSO

> ScriptableObject con la **oferta de dados por clase**. El jugador
> elige `RequiredBagSize` dados de este pool en `BuildSelectionScreen`
> antes de empezar la run; el resultado se materializa en un
> [[DiceBagSO]]. TECHNICAL.md §6.2.

## Overview

Cada clase tiene su propio pool — diferencia el feeling temprano de la
run sin tocar el roller. El pool define que tipos estan disponibles y
cuantas copias de cada uno se pueden meter en la bolsa (cap por
oferta, ademas del hard cap global de [[DiceTypeExt]]). `Validate`
chequea que la suma de `MaxInBag` alcance para llenar
`RequiredBagSize` y que ningun override exceda el cap global. `MaxFor`
expone el cap efectivo por tipo — `0` significa "no esta en el pool".

## API / Shape

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Dice Bag Pool")]
public class DiceBagPoolSO : ScriptableObject {
    public int RequiredBagSize = DiceBagSO.RequiredSize; // default 5
    public List<DicePoolEntry> Offerings;

    public bool Validate(out string error);
    public int  MaxFor(DiceType type);
}
```

## Dependencies

- **Uses:** [[DiceType]], [[DiceTypeExt]], [[DicePoolEntry]],
  [[DiceBagSO]] (lee la constante `RequiredSize`).
- **Used by:** `ClassHeroSO` (per-class pool), `BuildSelectionScreen`,
  balance auditors.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Dice/DiceBagPoolSO.cs`
- Tests: `.../Tests/DiceBagPoolSOTests.cs`

## External references

- TECHNICAL.md: §6.2 Dice bag pool
