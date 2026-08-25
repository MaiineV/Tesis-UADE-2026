---
title: MovementDieSO
type: so
domain: 08-Dice
status: done
tags: [dice, movement, so]
---

# MovementDieSO

> Dado de Movimiento de una clase (TECHNICAL.md §6.6). Entidad **separada** del
> [[DiceBagSO]] de combate: no ocupa slot de la build, no recibe encantamientos ni
> bloqueos, y la build no lo modifica.

## Overview
Un solo `DiceType` (default D4). Cada clase lo referencia en
`ClassHeroSO.StartingMovementDie`; null ⇒ D4 (fallback de
[[MovementDieService]]). Su cara, al resolver Movimiento en combate, reemplaza
al `Range` fijo del `EffMove` (`SelectionSettings.RangeFromMovementDie`).

## API / Shape

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Dice/Movement Die", fileName = "AD_MovementDie")]
public class MovementDieSO : ScriptableObject {
    public const DiceType DefaultType = DiceType.D4;
    public DiceType Type = DefaultType;
    public int MaxFace => Type.MaxFace();
}
```

## Dependencies
**Uses:** [[DiceType]], [[DiceTypeExt]]
**Used by:** [[MovementDieService]] (vía `ClassHeroSO`), `MovementDieView`.

## Code
`Assets/Scripts/Rollgeon/Movement/Die/MovementDieSO.cs`
Asset autorado: `Assets/Resources/Dice/AD_Warrior_MovementDie.asset`.

## External references
- TECHNICAL.md: §6.6 Dado de Movimiento
- `docs/setup/movement-die.md`
