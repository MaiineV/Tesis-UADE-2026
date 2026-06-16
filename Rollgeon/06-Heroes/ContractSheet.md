---
title: ContractSheet
type: system
domain: 06-Heroes
status: done
tags: [heroes, contract, combos]
---

# ContractSheet

> Ordered list of [[BaseComboSO]]s the class can activate, plus a
> runtime set of crossed-out combos. Lives inside [[ClassHeroSO]].

## Shape

```csharp
[Serializable, HideReferenceObjectPicker]
public class ContractSheet {
    [OdinSerialize, Required] public List<BaseComboSO> Combos = new();

    public string DisplayLabel { get; }
    public bool   Validate(out string error);

    public BaseComboSO MatchBest(IReadOnlyList<int> dice);
    public BaseComboSO EvaluateRoll(int[] finalDice);

    public void CrossCombo(BaseComboSO combo);   // fires OnComboCrossed
    public bool IsCrossed(BaseComboSO combo);

    public ContractSheet Instantiate();          // clone per run
}
```

## Validation (§5.4, Warrior)

- Exactly 8 entries.
- No null entries, no duplicate `ComboId`.
- Last entry must be [[Combo_Generala]] (`Priority == int.MaxValue`).

## `MatchBest` algorithm

Iterates combos, skips crossed or blocked ones
([[ComboBlockService]]`.IsBlocked`), keeps the match with the highest
`BaseComboSO.Priority`. Returns `null` on no match.

## Runtime cloning

`Instantiate()` returns a copy with a fresh crossed-out set but shares
`BaseComboSO` references (catalog SOs are immutable at runtime).

## Dependencies

- **Uses:** [[BaseComboSO]], [[ComboBlockService]], [[EventManager]],
  [[EventName]].
- **Used by:** [[ClassHeroSO]], combat HUD combo list, combo executor.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Heroes/ContractSheet.cs`
- Tests: `.../Tests/ContractSheetTests.cs`,
  `ContractSheetBlockedComboTests.cs`

## External references

- Setup: `docs/setup/Content#0097b_WarriorContractAndWeakness.md`
- TECHNICAL.md: §5.3 ContractSheet
