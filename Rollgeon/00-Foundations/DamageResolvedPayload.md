---
title: DamageResolvedPayload
type: payload
domain: 00-Foundations
status: done
tags: [foundation, events, payload, typed, combat]
---

# DamageResolvedPayload

> [[TypedEvent]] payload raised by [[DamagePipeline]] at the end of a
> damage resolution. Channeled exclusively via
> `TypedEvent<DamageResolvedPayload>` — no legacy [[EventName]] entry
> exists (single-channel rule).

## Shape

```csharp
public struct DamageResolvedPayload {
    public Guid SourceGuid;     // attacker
    public Guid TargetGuid;     // defender
    public int  FinalDamage;    // post-mitigation, post-multipliers
    public bool WeaknessHit;    // hit a weakness tag?
    public bool WasLethal;      // target Health hit 0?
    public int  ShieldAbsorbed; // damage absorbed by Shield
}
```

## Publishers / consumers

- **Published by:** [[DamagePipeline]] (stage 6, after Health is
  applied).
- **Consumed by:** [[FloatingDamageSpawner]], combat death watcher,
  passive hooks reacting to damage / lethal blows, weakness HUD.

## Dependencies

- **Uses:** [[TypedEvent]].
- **Used by:** [[DamagePipeline]] (publisher), HUD overlays, passives.

## Code

`Assets/Scripts/Rollgeon/Patterns/EventPayloads.cs`

## External references

- TECHNICAL.md: §1.2.1 TypedEvent, §12.2 Damage pipeline
