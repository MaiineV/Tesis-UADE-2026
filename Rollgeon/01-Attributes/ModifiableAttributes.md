---
title: ModifiableAttributes
type: system
domain: 01-Attributes
status: done
tags: [attributes, container]
---

# ModifiableAttributes

> Per-entity container mapping `Type → IModifiable`. Invariant: an entity
> has at most one instance of a given stat type.

## Shape

```csharp
[Serializable]
public class ModifiableAttributes {
    public void EnsureInitialized();
    public bool HasAttribute<T>() where T : class, IModifiable;
    public T GetAttribute<T>() where T : class, IModifiable; // throws if missing
    public void SetAttribute<T>(IModifiable attribute) ...;
    public V GetAttributeValue<T, V>() ...;
    public void SetAttributeValue<T, V>(V value) ...;
    public V GetAttributeModifiedValue<T, V>() ...;
    public ModifiableAttributes DuplicateAttributes(); // deep clone
}
```

## Odin serialization

The dictionary `Type → IModifiable` mixes type keys with polymorphic
values, which Unity's built-in `SerializeField` cannot handle. The field
uses `[OdinSerialize]` instead.

## Duplication semantics

`DuplicateAttributes()` deep-clones every entry via
[[IAttribute|IAttribute.Duplicate]] so a hero template
([[ClassHeroSO]]) can spawn a runtime container without mutating the
asset.

## Dependencies

- **Uses:** [[IAttribute]], [[IModifiable]], Odin serialization.
- **Used by:** [[AttributesManager]] (one per registered entity),
  [[BaseEntitySO]] on spawn, every concrete stat.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Attributes/ModifiableAttributes.cs`

## External references

- Setup: `docs/setup/Foundation#0003_AttributesAndModifiers.md`
- TECHNICAL.md: §2.2 ModifiableAttributes
