---
title: IModifiable
type: interface
domain: 01-Attributes
status: done
tags: [attributes, interface, modifiers]
---

# IModifiable

> Runtime stat contract: [[IAttribute]] plus a stack of [[Modifier]]s
> and a callback hook for change notifications.

## Shape

```csharp
public interface IModifiable : IAttribute {
    T GetModifiedValue<T>();          // raw value with Intrinsic mods applied
    void SubscribeModifier();         // extension hook
    bool AddModifier<T>(IModifier<T> modifier);
    void RemoveModifier(Guid modifierId);
    void LinkAttribute(Action<Guid> callback); // notify on stack change
}

public interface IModifiable<TValue> : IModifiable, IAttribute<TValue> {
    TValue ModifiedValue { get; }     // boxing-free read of modified value
}
```

## Direction semantics

`GetModifiedValue` only applies modifiers with
`ModifierDirection.Intrinsic`. Directional modifiers
(`Outgoing` / `Incoming`) belong to [[DamagePipeline]] / [[HealPipeline]],
not to this accessor. See [[ModifierDirection]].

## Dependencies

- **Uses:** [[IAttribute]], [[Modifier]].
- **Used by:** [[BaseAttribute]], every concrete stat
  ([[Health]], [[Energy]], [[Attack]], [[Speed]]),
  [[ModifiableAttributes]], [[AttributesManager]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Attributes/IModifiable.cs`,
  `.../IModifiableT.cs`

## External references

- Setup: `docs/setup/Foundation#0003_AttributesAndModifiers.md`
- TECHNICAL.md: §2.1 IModifiable
