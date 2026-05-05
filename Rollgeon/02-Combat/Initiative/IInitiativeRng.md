---
title: IInitiativeRng
type: interface
domain: 02-Combat
status: done
tags: [combat, initiative, rng, interface]
---

# IInitiativeRng

> Minimal abstraction over `System.Random` so
> [[DefaultInitiativeProvider]] can be tested deterministically.

## Overview

Same range semantics as `System.Random.Next(int, int)`:
`minInclusive ≤ result < maxExclusive`. Callers wanting "die 1..6" pass
`rng.Next(1, 7)`. Production binding is `DefaultInitiativeRng` (wraps
`System.Random`); tests use `FixedInitiativeRng` from the test asmdef.

## API / Shape

```csharp
public interface IInitiativeRng {
    int Next(int minInclusive, int maxExclusive);
}
```

## Dependencies
**Used by:** [[DefaultInitiativeProvider]].
**Implemented by:** `DefaultInitiativeRng`.

## Code
`Assets/Scripts/Rollgeon/Combat/Random/IInitiativeRng.cs`
