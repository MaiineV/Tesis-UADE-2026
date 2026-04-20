# Foundation#0009 -- HealPipeline

## Files created

| File | Purpose |
|------|---------|
| `Assets/Scripts/Rollgeon/Combat/Pipelines/IHealPipeline.cs` | Interface contract |
| `Assets/Scripts/Rollgeon/Combat/Pipelines/HealContext.cs` | Data object (input/output fields) |
| `Assets/Scripts/Rollgeon/Combat/Pipelines/HealPipeline.cs` | Pipeline implementation (7 stages) |
| `Assets/Scripts/Rollgeon/Combat/Pipelines/Tests/HealPipelineTests.cs` | 10 EditMode tests |

## Files modified

| File | Change |
|------|--------|
| `Assets/Scripts/Rollgeon/Patterns/EventPayloads.cs` | Added `HealResolvedPayload` struct |

## How to run tests

1. Open Unity > **Window > General > Test Runner**.
2. Under **EditMode**, expand **Rollgeon.Combat.Pipelines.Tests**.
3. Run all `HealPipelineTests`.

## How to consume

```csharp
// Option A: explicit dependencies (tests, manual wiring)
var pipeline = new HealPipeline(attributesManager, id => maxHpLookup(id));
var ctx = new HealContext
{
    SourceId = healerGuid,
    TargetId = targetGuid,
    BaseHeal = 30,
};
pipeline.Resolve(ctx);
// ctx.FinalHeal, ctx.WasClamped now populated

// Option B: ServiceLocator (bootstrap)
ServiceLocator.Register<IHealPipeline>(new HealPipeline());
var pipeline = ServiceLocator.GetService<IHealPipeline>();
```

## ServiceLocator registration

Add to your bootstrap sequence:

```csharp
ServiceLocator.Register<IHealPipeline>(new HealPipeline());
```

The parameterless constructor pulls `AttributesManager` from `ServiceLocator`
and defaults max HP to `int.MaxValue` (no clamp) until a MaxHealth stat is implemented.
