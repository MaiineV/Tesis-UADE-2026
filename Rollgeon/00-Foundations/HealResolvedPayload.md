---
title: HealResolvedPayload
type: payload
domain: 00-Foundations
status: done
tags: [foundation, events, payload, typed, combat]
---

# HealResolvedPayload

> [[TypedEvent]] payload raised by [[HealPipeline]] when a heal is
> successfully applied. Channeled exclusively via
> `TypedEvent<HealResolvedPayload>` — no legacy [[EventName]] entry
> exists (single-channel rule).

## Shape

```csharp
public struct HealResolvedPayload {
    public Guid SourceGuid;       // entity that provided the heal
    public Guid TargetGuid;       // entity that received the heal
    public int  FinalHeal;        // post-clamp, post-multipliers
    public bool WasPercentBased;  // heal based on % of max HP?
}
```

## Publishers / consumers

- **Published by:** [[HealPipeline]] after Health is bumped and clamped
  against max HP.
- **Consumed by:** [[FloatingDamageSpawner]] (green pop), HUD heal
  overlays, passives that react to heals (`OnHealReceived`).

## Dependencies

- **Uses:** [[TypedEvent]].
- **Used by:** [[HealPipeline]] (publisher), HUD overlays.

## Code

`Assets/Scripts/Rollgeon/Patterns/EventPayloads.cs`

## External references

- TECHNICAL.md: §1.2.1 TypedEvent, §12.3 Heal pipeline
