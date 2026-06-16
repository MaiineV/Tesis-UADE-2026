---
title: Health
type: system
domain: 01-Attributes
status: done
tags: [attributes, stat, combat]
---

# Health

> Concrete HP stat (`int`). Visible in UI. Clamping is the caller's
> responsibility — [[BaseAttribute]] never clamps.

## Shape

```csharp
public sealed class Health : BaseAttribute<int> {
    public Health();
    public Health(int initial);
    public override string GetAttributeName() => "Health";
    protected override BaseAttribute<int> CreateDuplicate() => new Health(_rawValue);
}
```

## Clamp contract

- [[BaseAttribute]] stores raw values; negative or over-max are allowed.
- Clamping to `[0, Max]` happens at call sites:
  - [[DamagePipeline]] clamps to `0` after damage resolution.
  - `SupportHealBehavior` clamps to the entity's `BaseHP` when healing.
  - UI binding reads `GetAttributeModifiedValue<Health,int>()` for
    display and renders negatives as `0` defensively.

## Events

Mutations via [[AttributesManager]] fire `OnAttributeChanged(entityId,
typeof(Health))`; the HUD's `HealthBarView` subscribes to refresh.

## Dependencies

- **Uses:** [[BaseAttribute]].
- **Used by:** [[DamagePipeline]], [[HealPipeline]], `SupportHealBehavior`,
  `HealthBarView` (UI).

## Code

- Runtime: `Assets/Scripts/Rollgeon/Attributes/Stats/Health.cs`
- Tests: `.../Stats/Tests/HealthTests.cs`

## External references

- TECHNICAL.md: §4.2 / §D Stats
