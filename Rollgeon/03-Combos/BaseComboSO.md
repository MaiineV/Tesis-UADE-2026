---
title: BaseComboSO
type: so
domain: 03-Combos
status: done
tags: [combos, so, abstract]
---

# BaseComboSO

> Abstract base for every dice combo the contract can activate. Carries
> display metadata, base damage, the §5.1.1 multiplier formula, optional
> extra effects, and the detection virtual.

## Shape

```csharp
public abstract class BaseComboSO : SerializedScriptableObject {
    public string ComboId        { get; }   // "combo.par", "combo.generala"...
    public string DisplayName    { get; }
    public string Description    { get; }
    public Sprite Icon           { get; }
    public int    BaseDamage     { get; }
    public IReadOnlyList<float> ValueMultipliers { get; } // 6 entries (pip 1..6)
    public float  GeneralMultiplier { get; }
    public IReadOnlyList<EffectData> ExtraEffects { get; }

    public abstract bool Matches(int[] finalDice);
    public virtual float ComputeCount(int[] finalDice); // Σ pip × mul × general
    public virtual int   Priority => BaseDamage;
    public virtual ComboDetectionResult Detect(IReadOnlyList<int> diceValues);
    protected virtual int GetCountUsed(int[] finalDice) => finalDice?.Length ?? 0;
}
```

## Damage formula (§5.1.1)

`damage = BaseDamage + ComputeCount(dice)` where
`ComputeCount = (Σ pip × ValueMultipliers[pip-1]) × GeneralMultiplier`.

## Priority

Default `Priority = BaseDamage` so combos that overlap (Par ⊆ Trio ⊆ Poker
⊆ Generala) break ties correctly. [[Combo_Generala]] overrides to
`int.MaxValue`.

## Dependencies

- **Uses:** `EffectData` ([[04-Effects]]), [[ComboDetectionResult]],
  [[ComboCatalogSO]] (dropdown source).
- **Used by:** the 8 concretes (see [[Combo_Par]] .. [[Combo_Generala]]),
  [[ContractSheet]], AttackResolver (future), [[ComboCountersService]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combos/BaseComboSO.cs`

## External references

- Setup: `docs/setup/Content#0097a_ComboBaseAndConcretes.md`
- TECHNICAL.md: §5.1 Combos
