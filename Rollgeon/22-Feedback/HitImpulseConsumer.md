---
title: HitImpulseConsumer
type: class
domain: 22-Feedback
status: done
tags: [feedback, impulse, monobehaviour]
---

# HitImpulseConsumer

> Componente visual que consume `ImpulseBehaviorValue`s aplicándolos
> como knockback temporal del transform. Lo busca el [[FeedbackManager]]
> via `GetComponent` / `GetComponentInChildren` en el transform
> registrado en [[IPawnRegistry]].

## Overview

Animación default = "knockback breve": desplaza el transform hacia el
vector y vuelve a la posición original en `_returnSeconds` (push +
return en dos halves). Si el pawn ya está corriendo un knockback, el
nuevo impulso reemplaza al anterior. `_localSpace = true` por default
— respeta la escala del parent.

## API / Shape

```csharp
[DisallowMultipleComponent]
public sealed class HitImpulseConsumer : MonoBehaviour {
    public void ApplyImpulse(Vector3 impulse);
}
```

## Dependencies

**Uses:** `ImpulseBehaviorValue` (05-Entities).
**Used by:** [[FeedbackManager]] (`DispatchBehaviorValue` →
`ApplyImpulse`), [[PawnRegistry]] (consumer encontrado por el manager
via guid → transform → component).

## Code

`Assets/Scripts/Rollgeon/Feedback/HitImpulseConsumer.cs`

## External references

- TECHNICAL.md §9.2 — HitImpulse behavior value.
