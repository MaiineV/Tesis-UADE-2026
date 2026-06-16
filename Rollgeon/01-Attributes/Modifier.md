---
title: Modifier
type: system
domain: 01-Attributes
status: done
tags: [attributes, modifier]
---

# Modifier

> Typed stat modifier with owner / source identity, direction, operation,
> and lifetime. Self-subscribes to [[EventManager]] for automatic tick /
> scope removal.

## Shape

```csharp
[Serializable]
public class Modifier<T> : IModifier<T> {
    public T Amount;
    public ModifierOperation Operation;
    public int Duration;
    public Guid ModifierId;
    public Guid CarrierId;            // entity that carries the mod
    public Guid SourceId;             // entity/effect that originated it
    public ModifierDirection Direction;
    public ModifierLifetime Lifetime;
    public EventName TickEvent;

    public void OnLoad();             // re-subscribe after deserialize
    public T ApplyModifier(T value);  // runs Operation
    public void RemoveAndNotify();    // fires OnModifierRemoved
}
```

## Lifecycle

- `ModifierLifetime.Turns` → subscribes to `TickEvent`; decrements
  `Duration`; auto-removes at 0.
- `ModifierLifetime.Run` → subscribes to `OnRunEnd`; auto-removes.
- `ModifierLifetime.Encounter` → subscribes to `OnCombatEnd`;
  auto-removes.
- `ModifierLifetime.Permanent` → no subscription; explicit
  `EffRemoveModifier` required.

## Save/restore

Event subscriptions do not persist. After load, the holder of the
modifier must call `OnLoad()` to re-hook. `_resolvedOp` is marked
`[NonSerialized]` and is lazy-rebuilt on first `ApplyModifier`.

## Dependencies

- **Uses:** [[ModifierDirection]], [[ModifierLifetime]],
  [[ModifierOperation]], [[OperationResolver]], [[EventManager]],
  [[EventName]].
- **Used by:** [[BaseAttribute]], [[AttributesManager]],
  [[DamagePipeline]] (direction-aware consumers),
  [[Effects-MOC|Effects]] that add/remove modifiers.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Attributes/Modifiers/Modifier.cs`
- Tests: `Assets/Scripts/Rollgeon/Attributes/Tests/ModifierTests.cs`

## External references

- Setup: `docs/setup/Foundation#0003_AttributesAndModifiers.md`
- TECHNICAL.md: §3.1 Modifier (with CarrierId/SourceId update 2026-04-18)
