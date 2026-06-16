---
title: IPlayerReader
type: interface
domain: 04-Effects
status: done
tags: [effects, readers, interface]
---

# IPlayerReader

> Reader contract specialized for reading from the player without
> resolving the entity explicitly — the reader consults `PlayerService`
> internally. TECHNICAL §8.6.

## API / Shape

```csharp
public interface IPlayerReader<T> {
    T Read();
}
```

## Dependencies
**Used by:** effects that always read from the player regardless of
the source/target context.

## Code
`Assets/Scripts/Rollgeon/Effects/Readers/IPlayerReader.cs`
