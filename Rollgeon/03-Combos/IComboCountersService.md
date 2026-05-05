---
title: IComboCountersService
type: interface
domain: 03-Combos
status: done
tags: [combos, counters, interface]
---

# IComboCountersService

> Service interface for the Balatro-style combo counter system
> (TECHNICAL.md §5.5). Per-combo run-scoped counters with a derived
> bonus multiplier surfaced for the [[DamagePipeline]] downstream.

## Shape

```csharp
public interface IComboCountersService {
    int   GetCount(string comboId);
    void  IncrementCount(string comboId);   // fires OnComboCounterIncremented
    float GetBonusMultiplier(string comboId);
}
```

## Behaviour

- `GetCount` returns `0` if the combo never matched, or out of run.
- `IncrementCount` is a no-op out of run.
- `GetBonusMultiplier` formula:
  `1 + min(MaxBonus, Count * PerUseBonus)`. Returns `1.0f` if no
  ruleset, `Count == 0`, or out of run.
- Underlying state lives in [[RunComboCounterState]] (Run scope); the
  service itself is Global.

## Dependencies

- **Uses:** [[RunComboCounterState]], [[ComboCountersConfig]] (via
  `RulesetSO.Counters`).
- **Used by:** [[ComboCountersService]] (impl), `AttackResolver`
  (future damage-time consumer), HUD counter views.

## Code

- Interface: `Assets/Scripts/Rollgeon/Combos/Counters/IComboCountersService.cs`
- Implementation: [[ComboCountersService]]

## External references

- TECHNICAL.md: §5.5 Combo counters
