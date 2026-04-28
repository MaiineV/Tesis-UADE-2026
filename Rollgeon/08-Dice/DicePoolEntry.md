---
title: DicePoolEntry
type: struct
domain: 08-Dice
status: done
tags: [dice, struct, pool]
---

# DicePoolEntry

> Una oferta dentro de un [[DiceBagPoolSO]]: que [[DiceType]] esta
> disponible y cuantas copias del jugador puede meter en su bolsa.

## Shape

```csharp
[Serializable]
public struct DicePoolEntry {
    public DiceType Type;
    public int      MaxInBag; // tope de copias; <= DiceTypeExt.MaxPerBag(Type)
}
```

`MaxInBag` es un override **hacia abajo** del hard cap global publicado
por [[DiceTypeExt]]. Un D6 con `MaxInBag = 2` significa "esta clase
puede tener a lo sumo 2 D6 en su bolsa", aun cuando el cap global son
5. La validacion de `DiceBagPoolSO` rechaza valores `> MaxPerBag`.

## Dependencies

- **Uses:** [[DiceType]], [[DiceTypeExt]] (cap global).
- **Used by:** [[DiceBagPoolSO]]`.Offerings`.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Dice/DiceBagPoolSO.cs` (al pie del
  archivo del pool).

## External references

- TECHNICAL.md: §6.2 Dice bag pool entries
