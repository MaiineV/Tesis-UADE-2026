---
title: Energy
type: system
domain: 01-Attributes
status: done
tags: [attributes, stat, combat, energy]
---

# Energy

> Concrete Energy stat (`int`). Gates every combat action via
> [[ActionDefinitionSO]]`.EnergyCost` and pays for dice rerolls via
> [[RerollBudgetService]].

## Shape

```csharp
public sealed class Energy : BaseAttribute<int> {
    public Energy();
    public Energy(int initial);
    public override string GetAttributeName() => "Energy";
    protected override BaseAttribute<int> CreateDuplicate() => new Energy(_rawValue);
}
```

## Clamp contract

- The raw `Value` is allowed to go negative — [[BaseAttribute]] does not
  clamp deliberately.
- Canonical clamp lives in [[EnergyService]]`.SpendEnergy` (returns
  `false` on insufficient energy instead of allowing negatives).
- Regeneration happens between turns via [[EnergyRegenPolicy]].

## Dependencies

- **Uses:** [[BaseAttribute]].
- **Used by:** [[EnergyService]], [[ActionDefinitionSO]],
  [[RerollBudgetService]], `EnergyBarView` (UI).

## Code

- Runtime: `Assets/Scripts/Rollgeon/Attributes/Stats/Energy.cs`
- Tests: `.../Stats/Tests/EnergyTests.cs`

## External references

- Setup: `docs/setup/System#0100a_EnergyAttributeAndRegen.md`
- TECHNICAL.md: §12.6 Combat — Energy
