---
title: PawnRegistryBinding
type: class
domain: 22-Feedback
status: done
tags: [feedback, registry, monobehaviour]
---

# PawnRegistryBinding

> `MonoBehaviour` que se pega al GameObject visual de un pawn para
> auto-registrarse en [[IPawnRegistry]] al `OnEnable` y desregistrarse
> en `OnDisable`.

## Overview

El `EntityGuid` lo setea normalmente una capa superior (combat spawner
o floor loader) justo después de instanciar el prefab via
`SetGuid(Guid)`. Como fallback dev, el inspector permite tipear un
guid string que se parsea en `OnEnable`. Si `SetGuid` se llama con un
guid distinto al actual, primero desregistra el viejo.

## API / Shape

```csharp
[DisallowMultipleComponent]
public sealed class PawnRegistryBinding : MonoBehaviour {
    public Guid EntityGuid { get; }
    public void SetGuid(Guid guid);
}
```

## Dependencies

**Uses:** [[IPawnRegistry]] (vía `ServiceLocator`).
**Used by:** Pawn prefabs autorales (combat, exploration), combat
spawner / floor loader (callers de `SetGuid`).

## Code

`Assets/Scripts/Rollgeon/Feedback/PawnRegistryBinding.cs`
