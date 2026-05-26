---
title: DamageContext
type: system
domain: 02-Combat/Pipelines
status: done
tags: [combat, pipeline, context]
---

# DamageContext

> Data object passed through [[DamagePipeline]]`.Resolve`. Callers fill
> input fields; the pipeline writes the output fields.

## Shape

```csharp
public class DamageContext {
    // Inputs
    public Guid   SourceId;
    public Guid   TargetId;
    public int    BaseDamage;
    public string ComboId;       // null/empty if not combo-based
    public bool   IsWeaknessHit;
    public AttackKind Kind;

    // Outputs
    public float WeaknessMultiplier;
    public int   FinalDamage;
    public bool  WasLethal;
}
```

## Dependencies

- **Uses:** [[AttackKind]].
- **Used by:** [[DamagePipeline]], [[BasicEnemyAI]], `EffDamage`,
  combat UI (floating damage renderer).

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combat/Pipelines/DamageContext.cs`

## External references

- TECHNICAL.md: §12.2 DamageContext
