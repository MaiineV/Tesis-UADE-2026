---
title: AttackKind
type: concept
domain: 02-Combat/Pipelines
status: done
tags: [combat, pipeline, enum]
---

# AttackKind

> Enum tag on [[DamageContext]] that classifies the source of the
> damage. Lets downstream consumers (VFX, analytics, weakness rules)
> branch on origin without sniffing `ComboId`.

## Shape

```csharp
public enum AttackKind {
    BasicAttack,  // enemy AI auto-attack, player basic strike
    Combo,        // dice combo fired via ContractSheet
    Ability,      // item-granted or passive-triggered
    Dot,          // damage-over-time ticks (e.g. poison)
    Environment,  // room traps, hazards
}
```

## Dependencies

- **Used by:** [[DamageContext]], [[DamagePipeline]], VFX layer (future).

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combat/Pipelines/AttackKind.cs`

## External references

- TECHNICAL.md: §12.2 AttackKind
