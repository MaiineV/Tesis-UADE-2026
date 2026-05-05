---
title: ComboMatchedPayload
type: payload
domain: 00-Foundations
status: done
tags: [foundation, events, payload, typed, combos]
---

# ComboMatchedPayload

> [[TypedEvent]] payload published when a player roll matches a combo.
> Channeled exclusively via `TypedEvent<ComboMatchedPayload>` — no
> legacy [[EventName]] entry exists (single-channel rule).

## Shape

```csharp
public struct ComboMatchedPayload {
    public Guid   SourceGuid;   // entity that produced the combo
    public string ComboId;      // catalog key
    public string DisplayName;  // UI label
    public int    BaseDamage;   // base damage before mitigation / multipliers
}
```

## Publishers / consumers

- **Published by:** combo detection / contract evaluation pipeline
  ([[ContractSheet]]`.EvaluateRoll` -> resolution layer).
- **Consumed by:** [[ComboCountersService]] (increments per-run
  counter), [[ComboIndicatorView]] (HUD flash), passive hooks listening
  for combo triggers.

## Dependencies

- **Uses:** [[TypedEvent]].
- **Used by:** [[ComboCountersService]], combat HUD views.

## Code

`Assets/Scripts/Rollgeon/Patterns/EventPayloads.cs`

## External references

- TECHNICAL.md: §1.2.1 TypedEvent — single-channel rule, §5.5 Combo
  counters
