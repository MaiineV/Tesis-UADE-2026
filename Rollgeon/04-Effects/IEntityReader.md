---
title: IEntityReader
type: interface
domain: 04-Effects
status: done
tags: [effects, readers, interface]
---

# IEntityReader

> Reader contract for resolving a typed value `T` from a source
> [[Entity]]. TECHNICAL §8.6. Declared here as a placeholder so effects
> stay decoupled from `AttributesManager` until Foundation#0003 picks
> the canonical home for the type.

## API / Shape

```csharp
public interface IEntityReader<T> {
    T Read(Entity source);
}
```

## Dependencies
**Used by:** effects that read attribute values from a specific entity
(self / opponent / triggering).

## Code
`Assets/Scripts/Rollgeon/Effects/Readers/IEntityReader.cs`
