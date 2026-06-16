---
title: ModifierOperation
type: concept
domain: 01-Attributes
status: done
tags: [attributes, modifier, enum]
---

# ModifierOperation

> Serializable enum replacing the `Func<T,T,T>` a modifier would
> conceptually hold. Save captures the integer; restore dispatches to
> the right lambda via [[OperationResolver]].

## Shape

```csharp
public enum ModifierOperation {
    Add, Subtract, Multiply, Override, Min, Max, Percent, // numeric
    Set, And, Or, Xor,                                     // bool
    Replace,                                               // ref / struct
}
```

- `Percent` applies `value + value * amount` — `amount` is a fraction
  (`0.2` = +20 %).
- `Override` / `Set` / `Replace` force the value; semantic overlap is
  kept for readability across `T`.

## Why an enum, not a delegate

Unity + Odin cannot serialize `Func<T,T,T>`. An enum round-trips
trivially and the lambda is resolved via reflection in
[[OperationResolver]] the first time it's needed.

## Dependencies

- **Used by:** [[Modifier]], [[OperationResolver]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Attributes/Modifiers/ModifierOperation.cs`

## External references

- TECHNICAL.md: §3.1 / §3.3 Modifier operations
