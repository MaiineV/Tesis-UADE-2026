---
title: RerollQueryResult
type: concept
domain: 08-Dice
status: done
tags: [dice, reroll, dto]
---

# RerollQueryResult

> Immutable DTO returned by [[RerollBudgetService]]`.Query()`. Carries
> enough state for the combat HUD to render the reroll button without
> re-querying mid-frame.

## Shape

```csharp
public readonly struct RerollQueryResult {
    public readonly bool Allowed;
    public readonly int  FreeRemaining;
    public readonly int  EnergyCost;      // 0 if free, otherwise the cost to pay
    public readonly string DisabledReason; // null when Allowed is true
}
```

## Consumers

- Reroll button — toggles on/off, label "Reroll (free)" /
  "Reroll (−1 energy)" / disabled with tooltip.
- Reroll tests — asserts on intermediate states across a multi-step
  flow.

## Dependencies

- **Used by:** [[RerollBudgetService]], combat HUD reroll button.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Dice/RerollQueryResult.cs`

## External references

- TECHNICAL.md: §6 Reroll DTO
