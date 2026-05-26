---
title: ActionType
type: concept
domain: 02-Combat/Actions
status: done
tags: [combat, actions, enum]
---

# ActionType

> Classifies an [[ActionDefinitionSO]] into one of the canonical action
> buckets. Used by the HUD action-bar layout and by catalogs that want
> to filter by type.

## Shape

```csharp
public enum ActionType {
    Attack,
    Combo,
    Ability,
    Move,
    UseItem,
    SkillCheck,
    EndTurn,
}
```

## Dependencies

- **Used by:** [[ActionDefinitionSO]], [[ActionCatalogSO]], combat HUD
  filters.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combat/Actions/ActionType.cs`

## External references

- TECHNICAL.md: §12.6.0 ActionType
