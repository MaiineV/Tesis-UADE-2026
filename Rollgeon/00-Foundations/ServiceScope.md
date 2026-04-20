---
title: ServiceScope
type: concept
domain: 00-Foundations
status: done
tags: [foundation, service, enum]
---

# ServiceScope

> Lifetime label attached to each entry in [[ServiceLocator]]. Separates
> services that live the whole session from services scoped to a single
> run.

## Shape

```csharp
public enum ServiceScope {
    Global, // from bootstrap to shutdown
    Run,    // cleared by ServiceLocator.ClearScope(Run) at run end
}
```

## Why two scopes

- **Global**: infrastructure (catalogs, settings, pooled services). Must
  outlive runs — re-registering is expensive and state-invalidating.
- **Run**: run-only managers (e.g. [[RunController]], combat counters,
  reroll budget). Flushing on run end guarantees the next run starts fresh
  without bleeding state.

## Dependencies

- **Used by:** [[ServiceLocator]] (as registration key + clear filter),
  [[Bootstrap]] (always registers into `Global`), [[IPreloadableService]]
  (registers its own service).

## Code

- Runtime: `Assets/Scripts/Rollgeon/Patterns/ServiceScope.cs`

## External references

- TECHNICAL.md: §1.1 — ServiceLocator scopes
