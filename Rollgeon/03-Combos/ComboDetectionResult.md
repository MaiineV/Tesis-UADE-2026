---
title: ComboDetectionResult
type: concept
domain: 03-Combos
status: done
tags: [combos, detection]
---

# ComboDetectionResult

> Immutable record returned by [[BaseComboSO]]`.Detect(diceValues)`.
> Carries `Matched`, `BaseDamage`, and `CountUsed`.

## Shape

```csharp
public readonly struct ComboDetectionResult {
    public readonly bool Matched;
    public readonly int  BaseDamage;
    public readonly int  CountUsed;

    public static ComboDetectionResult Match(int baseDamage, int countUsed);
    public static ComboDetectionResult NoMatch();
}
```

## Usage

Combos that need dynamic base damage (e.g. [[Combo_SumaX]], where the
damage equals the sum of matched dice) override `Detect` and build a
fresh `ComboDetectionResult` with a computed `baseDamage`. The default
`Detect` simply wraps `Matches` with `BaseDamage` and `GetCountUsed`.

## Dependencies

- **Used by:** [[BaseComboSO]], `AttackResolver` (future).

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combos/ComboDetectionResult.cs`

## External references

- TECHNICAL.md: §5.1 Detect API
