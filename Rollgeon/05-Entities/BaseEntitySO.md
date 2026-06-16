---
title: BaseEntitySO
type: so
domain: 05-Entities
status: done
tags: [entities, so, abstract]
---

# BaseEntitySO

> Common parent of every entity data asset (enemies, props, NPCs).
> Minimal — identity plus the abstract stat builder `CreateRuntimeStats`.

## Shape

```csharp
public abstract class BaseEntitySO : SerializedScriptableObject {
    public string EntityId;      // "enemy.support.auditor"
    public string DisplayName;   // UI-visible name
    public string Description;   // codex / tooltip text

    public abstract ModifiableAttributes CreateRuntimeStats();
}
```

## Duplication invariant

`CreateRuntimeStats` always builds a **fresh**
[[ModifiableAttributes]] with base values — never mutates the asset.
Spawning an entity gives it its own runtime attribute bag (§2.2).

## Dependencies

- **Uses:** [[ModifiableAttributes]].
- **Used by:** [[EnemyDataSO]] (and future `PropEntitySO`, `NpcDataSO`).

## Code

- Runtime: `Assets/Scripts/Rollgeon/Entities/BaseEntitySO.cs`

## External references

- TECHNICAL.md: §7.0 BaseEntitySO
