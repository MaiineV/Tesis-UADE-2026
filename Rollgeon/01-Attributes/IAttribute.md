---
title: IAttribute
type: interface
domain: 01-Attributes
status: done
tags: [attributes, interface]
---

# IAttribute

> Minimal contract for a named, typed, duplicable attribute value. No
> modifier stack — that's [[IModifiable]]'s job.

## Shape

```csharp
public interface IAttribute {
    T GetValue<T>();             // throws InvalidCastException on mismatch
    void SetValue<T>(T value);
    Type GetValueType();         // typeof(int), typeof(float), ...
    string GetAttributeName();
    IAttribute Duplicate();      // independent copy
}

public interface IAttribute<TValue> : IAttribute {
    TValue Value { get; set; }   // typed accessor, no boxing
}
```

## Why `IAttribute<TValue>`

Concrete stats inherit the generic variant via [[IModifiable]]`<TValue>`
so they can expose a boxing-free `Value` accessor while still satisfying
the non-generic contract for polymorphic containers like
[[ModifiableAttributes]].

## Dependencies

- **Used by:** [[IModifiable]], [[BaseAttribute]], all concrete stats.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Attributes/IAttribute.cs`,
  `.../IAttributeT.cs`

## External references

- Setup: `docs/setup/Foundation#0003_AttributesAndModifiers.md`
- TECHNICAL.md: §2.1 Attributes — IAttribute
