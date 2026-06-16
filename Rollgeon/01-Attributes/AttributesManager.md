---
title: AttributesManager
type: service
domain: 01-Attributes
status: done
tags: [attributes, service, manager]
---

# AttributesManager

> Centralised runtime service that maps `Guid entityId → ModifiableAttributes`.
> All systems read/write stats through this service, never by holding a
> direct reference to the container.

## Shape

```csharp
public class AttributesManager : IDisposable {
    public static bool LogMissingEntityAsWarning = true;

    public void Register(Guid entityId, ModifiableAttributes attrs);
    public void Unregister(Guid entityId);
    public bool IsRegistered(Guid entityId);

    public ModifiableAttributes GetAttributes(Guid entityId);
    public TAttr GetAttribute<TAttr>(Guid) where TAttr : class, IModifiable;
    public TValue GetAttributeValue<TAttr, TValue>(Guid id);
    public TValue GetAttributeModifiedValue<TAttr, TValue>(Guid id);
    public void SetAttributeValue<TAttr, TValue>(Guid id, TValue v);
    public void Modify<TAttr, TValue>(Guid id, Func<TValue, TValue>);

    public bool AddModifier<TAttr, TValue>(Guid id, Modifier<TValue>);
    public bool RemoveModifier<TAttr, TValue>(Guid id, Guid modifierId);
    public int RemoveModifierBySource<TAttr, TValue>(Guid id, Guid sourceId);
    public int RemoveAllModifiersBySource(Guid sourceId);
}
```

## Event integration

- Fires [[EventName]] `OnAttributeChanged`, `OnModifierAdded`,
  `OnModifierRemoved` on mutations.
- Subscribes to `OnModifierRemoved` to sweep the matching
  [[Modifier]] out of whichever [[BaseAttribute]] holds it — centralised
  so we don't multiply handlers per N stats × M entities.

## Thread-safety

Single-threaded, main-thread only. No locking.

## Dependencies

- **Uses:** [[ModifiableAttributes]], [[BaseAttribute]], [[Modifier]],
  [[EventManager]], [[EventName]].
- **Used by:** combat actions, [[DamagePipeline]], [[HealPipeline]],
  behaviors, UI views subscribed to `OnAttributeChanged`.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Attributes/AttributesManager.cs`
- Tests: `.../Tests/AttributesManagerTests.cs`

## External references

- Setup: `docs/setup/Foundation#0003_AttributesAndModifiers.md`
- TECHNICAL.md: §2.3 AttributesManager
