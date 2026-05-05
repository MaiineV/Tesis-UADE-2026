---
title: DiceBagSO
type: so
domain: 08-Dice
status: done
tags: [dice, so, bag]
---

# DiceBagSO

> ScriptableObject que representa la bolsa de **5 dados** que lleva el
> heroe a una run. Producto final del flujo BuildSelectionScreen +
> [[DiceBagPoolSO]]. TECHNICAL.md §6.2.

## Overview

Lista plana de [[DiceType]] de tamano `RequiredSize = 5`. El orden
solo importa para mostrar el slot index en HUD — el [[DiceRoller]]
trata cada indice como independiente. `Validate` enforce-a el tamano
exacto y el `MaxPerBag` por tipo (via [[DiceTypeExt]]); `OnValidate`
emite warnings no-bloqueantes para permitir bolsas WIP en editor.
`Clone()` devuelve una copia independiente en memoria — sin asset
backing — usada por sistemas que mutan la bolsa durante la run.

## API / Shape

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Dice Bag")]
public class DiceBagSO : ScriptableObject {
    public const int RequiredSize = 5;
    public List<DiceType> Dice;

    public bool Validate(out string error);
    public DiceBagSO Clone();          // in-memory, sin asset
}
```

## Dependencies

- **Uses:** [[DiceType]], [[DiceTypeExt]].
- **Used by:** [[DiceBagPoolSO]] (target del flujo BuildSelectionScreen),
  [[DiceRoller]], [[IDiceRoller]], `BaseEntitySO`/`HeroDataSO` runtime
  state.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Dice/DiceBagSO.cs`
- Tests: `.../Tests/DiceBagSOTests.cs`

## External references

- TECHNICAL.md: §6.2 Dice bag
