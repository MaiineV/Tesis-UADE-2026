---
title: HealPipeline
type: pipeline
domain: 02-Combat/Pipelines
status: done
tags: [combat, pipeline, heal]
---

# HealPipeline

> Heal counterpart of [[DamagePipeline]]. Writes directly to
> [[Health]] via [[AttributesManager]] with `[0, maxHp]` clamping.

## API

```csharp
public class HealPipeline : IHealPipeline {
    public HealPipeline();                               // resolves deps from ServiceLocator
    public HealPipeline(AttributesManager attributes);
    public HealContext Resolve(HealContext ctx);
}
```

`HealContext` carries `SourceId`, `TargetId`, `BaseAmount`,
`FinalAmount`, and a `WasOverheal` flag.

## Stages (current)

1. Zero / negative guard.
2. Apply to `Health`, clamping to the target's `BaseHP` (read from
   `EnemyDataSO` or class template — caller-provided cap).
3. Fire `OnHealResolved` (untyped today, typed in the future).

Outgoing / incoming heal multipliers are specified in `TECHNICAL.md §12.3`
but not yet stat-wired — placeholder identical to [[DamagePipeline]].

## Dependencies

- **Uses:** [[AttributesManager]], [[Health]], [[EventManager]].
- **Used by:** `EffHeal`, `SupportHealBehavior`, potion interactions.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combat/Pipelines/HealPipeline.cs`
- Context: `.../HealContext.cs`
- Interface: `.../IHealPipeline.cs`
- Tests: `.../Tests/HealPipelineTests.cs`

## External references

- Setup: `docs/setup/Foundation#0009_HealPipeline.md`
- TECHNICAL.md: §12.3 Heal pipeline
