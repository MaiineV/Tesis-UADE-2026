---
title: RerollBudget
type: system
domain: 08-Dice
status: done
tags: [dice, reroll, budget, pure]
---

# RerollBudget

> Pure in-memory state that tracks how many free rerolls remain for the
> current action. Owned by [[RerollBudgetService]].

## Shape

```csharp
public sealed class RerollBudget {
    public int Remaining { get; }
    public void Reset(int freeRolls);        // called on StartAction
    public bool TryConsumeFree();             // returns false at 0
}
```

Splitting the budget math out keeps [[RerollBudgetService]] focused on
event wiring / energy charging; unit tests for the budget live in
`RerollBudgetTests` without spinning up the service.

## Dependencies

- **Used by:** [[RerollBudgetService]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Dice/RerollBudget.cs`
- Tests: `.../Tests/RerollBudgetTests.cs`

## External references

- TECHNICAL.md: §6 Reroll budget internal state
