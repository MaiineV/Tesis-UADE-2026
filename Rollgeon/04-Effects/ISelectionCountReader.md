---
title: ISelectionCountReader
type: interface
domain: 04-Effects
status: done
tags: [effects, selection, readers, interface]
---

# ISelectionCountReader

> Reader contract que resuelve en runtime la cantidad de targets de una
> selección (count dinámico de [[SelectionSettings]]). Mismo patrón que
> el `IReader<int>` de Bot-Game. Serializado polimórfico
> (`[OdinSerialize, SerializeReference]`, §13.6.1).

## API / Shape

```csharp
public interface ISelectionCountReader {
    int Read(ReadInfo info);   // ReadInfo: { Guid ownerGuid }
}
```

**Contrato defensivo:** las implementaciones deben devolver un mínimo
seguro (nunca lanzar) ante `ReadInfo` default o servicios sin registrar —
hay call sites sin owner disponible (`ActionDragPolicy`).

## Concretes

| Reader | Config | Semántica |
|---|---|---|
| `StatCountReader` | `Stat`, `UseModified`, `Min`, `Max` | count = stat del owner, clampeado |
| `AliveEnemiesCountReader` | `MaxCount` | count = enemigos vivos del owner, con tope |

## Dependencies

- **Uses:** `ReadInfo`, `AttributesManager` (stats), `IEntityQueryService`
  (enemigos vivos).
- **Used by:** [[SelectionSettings]] (`GetSelectionCount` con
  `IsConstantSelectionCount == false`).

## Code

- `Assets/Scripts/Rollgeon/Effects/Selection/ISelectionCountReader.cs`
- `Assets/Scripts/Rollgeon/Effects/Selection/Readers/StatCountReader.cs`
- `Assets/Scripts/Rollgeon/Effects/Selection/Readers/AliveEnemiesCountReader.cs`
- Tests: `SelectionCountReaderTests.cs`
