---
title: HealthChangedPayload
type: payload
domain: 00-Foundations
status: done
tags: [foundation, events, payload, typed, attributes]
---

# HealthChangedPayload

> [[TypedEvent]] payload raised whenever an entity's `Health` attribute
> changes value. Channeled exclusively via
> `TypedEvent<HealthChangedPayload>` — no legacy [[EventName]] entry
> exists (single-channel rule).

## Shape

```csharp
public struct HealthChangedPayload {
    public Guid EntityGuid;  // entity whose health changed
    public int  Current;     // new current HP
    public int  Max;         // current max HP (may shift via modifiers)
}
```

## Publishers / consumers

- **Published by:** [[AttributesManager]] / Health attribute setter
  pipeline (after [[DamagePipeline]] applies damage and after
  [[HealPipeline]] applies heal).
- **Consumed by:** [[HealthBarView]], enemy HP overlays, passives that
  gate on HP thresholds (e.g. low-HP procs).

## Dependencies

- **Uses:** [[TypedEvent]], [[Health]].
- **Used by:** HUD health views, AI conditions like
  [[AICond_HPBelow]].

## Code

`Assets/Scripts/Rollgeon/Patterns/EventPayloads.cs`

## External references

- TECHNICAL.md: §1.2.1 TypedEvent, §A.1 Health attribute
