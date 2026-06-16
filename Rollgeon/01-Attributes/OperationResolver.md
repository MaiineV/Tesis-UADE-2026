---
title: OperationResolver
type: system
domain: 01-Attributes
status: done
tags: [attributes, modifier, resolver]
---

# OperationResolver

> Internal lookup that turns a [[ModifierOperation]] enum value into a
> concrete `Func<T,T,T>` lambda appropriate for `T ∈ {int, float, bool}`.

## Shape

```csharp
internal static class OperationResolver {
    public static Func<T, T, T> Resolve<T>(ModifierOperation op);
}
```

## Why a separate resolver

- Keeps serialization clean ([[Modifier]] stores the enum, not a
  delegate).
- Centralises type dispatch (`int` vs. `float` vs. `bool`) so concrete
  stats do not need to know about operation semantics.
- Invoked lazily: `Modifier<T>.ApplyModifier` rebuilds the lambda the
  first time it's called after deserialization.

## Dependencies

- **Uses:** [[ModifierOperation]].
- **Used by:** [[Modifier]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Attributes/Modifiers/OperationResolver.cs`

## External references

- TECHNICAL.md: §3.3 Operation resolver
