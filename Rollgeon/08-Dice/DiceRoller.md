---
title: DiceRoller
type: class
domain: 08-Dice
status: done
tags: [dice, rng, default-impl]
---

# DiceRoller

> Implementacion default de [[IDiceRoller]]. Sealed pure-C# class —
> sin `MonoBehaviour` — wrappea un `System.Random` interno.
> TECHNICAL.md §6.3.

## Overview

El constructor parameterless siembra desde el reloj (uso normal); el
overload con `seed` existe para tests deterministicos (replicar la
misma secuencia que el ejemplo del spec). `RollAll` y `Reroll`
delegan en un helper privado `RollFace(type)` que llama
`_rng.Next(1, type.MaxFace() + 1)` — rango `[1, MaxFace]` inclusivo.

`Reroll` toler-a `previousResult` / `keep` `null` o de largo
inconsistente: los indices no cubiertos se rerolan. Bag `null` lanza
`ArgumentNullException`; bag con `Dice == null` devuelve array vacio
con un warning.

## API / Shape

```csharp
public sealed class DiceRoller : IDiceRoller {
    public DiceRoller();           // seed desde reloj
    public DiceRoller(int seed);   // seed explicito (tests)

    public int[] RollAll(DiceBagSO bag);
    public int[] Reroll(DiceBagSO bag, int[] previousResult, bool[] keep);
}
```

## Dependencies

- **Uses:** [[IDiceRoller]] (implementa), [[DiceType]],
  [[DiceTypeExt]] (`MaxFace`), [[DiceBagSO]].
- **Used by:** registrado en `ServiceLocator` por `DiceRollerBootstrap`;
  consumidores via `IDiceRoller`.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Dice/DiceRoller.cs`
- Bootstrap: `.../DiceRollerBootstrap.cs`
- Tests: `.../Tests/DiceRollerTests.cs`

## External references

- TECHNICAL.md: §6.3 Dice roller default impl
