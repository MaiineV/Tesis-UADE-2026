---
title: BaseAttribute
type: system
domain: 01-Attributes
status: done
tags: [attributes, abstract, modifiers]
---

# BaseAttribute

> Abstract base class that implements 80 % of every runtime stat: raw
> value storage, typed modifier stack, intrinsic-direction pipeline,
> change callbacks. Concrete stats only override `CreateDuplicate`.

## Shape

```csharp
public abstract class BaseAttribute<TValue> : IModifiable<TValue> {
    protected TValue _rawValue;
    protected readonly List<Modifier<TValue>> _modifiers = new();

    public TValue Value { get; set; }
    public TValue ModifiedValue => ComputeModifiedValue(); // Intrinsic only
    public bool AddModifier<T>(IModifier<T>);
    public void RemoveModifier(Guid);
    public void LinkAttribute(Action<Guid> callback);

    public IReadOnlyList<Modifier<TValue>> GetRawModifiers();
    public bool RemoveModifierSilent(Guid modifierId);

    protected abstract BaseAttribute<TValue> CreateDuplicate();
}
```

## Duplicate contract

`Duplicate()` calls `CreateDuplicate()` then returns the clone **without
any modifiers** — an entity spawned fresh starts with no buffs. Applies
to runs, spawns, and encounter restarts. See
[[ModifiableAttributes|ModifiableAttributes.DuplicateAttributes]].

## Type safety

Generic accessors (`GetValue<T>`, `SetValue<T>`, `GetModifiedValue<T>`)
throw `InvalidCastException` when `T` mismatches `TValue` — prevents
silent boxing bugs.

## Dependencies

- **Uses:** [[IModifiable]], [[Modifier]], [[ModifierDirection]].
- **Used by:** [[Health]], [[Energy]], [[Attack]], [[Speed]], other stats.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Attributes/BaseAttribute.cs`

## External references

- Setup: `docs/setup/Foundation#0003_AttributesAndModifiers.md`
- TECHNICAL.md: §2.2 BaseAttribute
