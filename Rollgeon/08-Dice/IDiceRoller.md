---
title: IDiceRoller
type: interface
domain: 08-Dice
status: done
tags: [dice, interface, rng]
---

# IDiceRoller

> Kernel de tirada de dados. Pure C#, sin `MonoBehaviour`, registrado
> en el `ServiceLocator`. TECHNICAL.md §6.3.

## API / Shape

```csharp
public interface IDiceRoller {
    int[] RollAll(DiceBagSO bag);
    int[] Reroll(DiceBagSO bag, int[] previousResult, bool[] keep);
}
```

- `RollAll` tira los 5 dados de la bolsa; el array devuelto tiene un
  indice por slot con la cara obtenida `[1..MaxFace]`.
- `Reroll` respeta `keep[i] == true` (mantiene `previousResult[i]`)
  y rerolla el resto. `keep` y `previousResult` pueden ser `null` /
  de largo distinto — los indices no cubiertos se tratan como `false`.

## Determinismo

Implementaciones encapsulan su propio RNG (ver [[DiceRoller]]). Dos
consumidores que comparten la misma instancia ven la misma secuencia
— intencional para reproducibilidad por run y tests deterministicos.

## Dependencies

- **Uses:** [[DiceType]], [[DiceBagSO]], [[DicePoolEntry]] (via bag).
- **Used by:** [[RerollBudgetService]] (via `IRerollBudget` adapter),
  `CombatHUD` reroll flow, action handlers que tiran dados.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Dice/IDiceRoller.cs`
- Default impl: [[DiceRoller]].

## External references

- TECHNICAL.md: §6.3 Dice roller contract
