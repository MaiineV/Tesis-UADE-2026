---
title: Attributes-MOC
type: moc
domain: 90-MOCs
status: done
tags: [moc, attributes]
---

# 01-Attributes — Map of Content

> Stat engine used by every entity: `IAttribute` → `IModifiable` →
> `BaseAttribute<T>` → concrete stats, all stored in a per-entity
> `ModifiableAttributes` bag mediated by `AttributesManager`.

## Relationships

```
            IAttribute
                │
          IAttribute<T>           (typed accessor)
                │
          IModifiable              (adds modifier stack)
                │
          IModifiable<T>           (typed modified value)
                │
          BaseAttribute<T>
           /      │       \
        Health  Energy  Attack  Speed  (Sprint 03)

   ModifiableAttributes   ─ contains many stats per entity
                │
        AttributesManager (Guid → ModifiableAttributes)

   Modifier<T> ─ direction / lifetime / operation + OperationResolver
```

## Notes

- **Interfaces:** [[IAttribute]] · [[IModifiable]]
- **Runtime containers:** [[BaseAttribute]] · [[ModifiableAttributes]] ·
  [[AttributesManager]]
- **Modifier system:** [[Modifier]] · [[ModifierDirection]] ·
  [[ModifierLifetime]] · [[ModifierOperation]] ·
  [[OperationResolver]]
- **Concrete stats (Sprint 03):** [[Health]] · [[Energy]] · [[Attack]] ·
  [[Speed]]

## Cross-domain edges

- [[DamagePipeline]] reads/writes [[Health]], consumes outgoing /
  incoming multipliers (placeholder).
- [[EnergyService]] owns the lifecycle of [[Energy]] on the player.
- [[TurnOrderService]] reads [[Speed]].
- [[EnemyDataSO]]`.CreateRuntimeStats` builds the stat bag on spawn.
