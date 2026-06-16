---
title: DamagePipeline
type: pipeline
domain: 02-Combat/Pipelines
status: done
tags: [combat, pipeline, damage]
---

# DamagePipeline

> Central pipeline that resolves any damage application between two
> entities. Produces a `DamageContext` with the final damage, a
> `WasLethal` flag, and publishes `DamageResolvedPayload` on
> [[TypedEvent]].

## Stages

```
0. Zero / negative guard → return immediately.
1. Outgoing multiplier    (PLACEHOLDER — OutgoingDamageMultiplier stat TBD)
   → fire OnDamageOutgoing(source, target, damage)
2. Weakness multiplier    via IWeaknessChecker if IsWeaknessHit
3. Incoming multiplier    (PLACEHOLDER — IncomingDamageMultiplier stat TBD)
   → fire OnDamageIncoming(source, target, damage)
4. Shield absorption      (PLACEHOLDER — Shield stat TBD)
5. Apply to Health via AttributesManager.SetAttributeValue<Health,int>
   → clamp to 0; set WasLethal.
6. Raise TypedEvent<DamageResolvedPayload>.
```

## API

```csharp
public class DamagePipeline : IDamagePipeline {
    public DamagePipeline();                                   // resolves deps from ServiceLocator
    public DamagePipeline(AttributesManager attributes,
                          IWeaknessChecker weaknessChecker = null);
    public DamageContext Resolve(DamageContext ctx);
}
```

## Sprint 03 scope

Stages 0, 2, 5, 6 are live. Stages 1, 3, 4 are commented-out
placeholders waiting for `OutgoingDamageMultiplier`,
`IncomingDamageMultiplier`, and `Shield` stats to land — the public API
will not change when they do.

## Dependencies

- **Uses:** [[AttributesManager]], [[Health]], [[IWeaknessChecker]],
  [[DamageContext]], [[EventManager]], [[EventName]], [[TypedEvent]].
- **Used by:** [[BasicEnemyAI]], `EffDamage` effects, boss attack
  behaviors.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combat/Pipelines/DamagePipeline.cs`
- Interface: `.../IDamagePipeline.cs`
- Tests: `.../Tests/DamagePipelineTests.cs`

## External references

- Setup: `docs/setup/Foundation#0008_DamagePipeline.md`
- TECHNICAL.md: §12.2 Damage pipeline
