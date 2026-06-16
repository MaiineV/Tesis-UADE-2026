---
title: Combos-MOC
type: moc
domain: 90-MOCs
status: done
tags: [moc, combos]
---

# 03-Combos — Map of Content

> Generala-style combos, Balatro-style run counters, and the contract
> activation surface.

## Relationships

```
 BaseComboSO ← 8 concretes (Par, DoblePar, SumaX, Trio, Escalera,
                            FullHouse, Poker, Generala)
      │
      │ Priority (default BaseDamage; Generala = int.MaxValue)
      │
 ComboCatalogSO ─ lookup + dropdown source
      │
 ContractSheet (inside ClassHeroSO) ─ uses MatchBest → BaseComboSO
                                   ─ CrossCombo / IsCrossed
                                   ─ reads ComboBlockService

 TypedEvent<ComboMatchedPayload>
      │
 ComboCountersService (subscribes)
      │
 RunComboCounterState (Run-scoped, ISaveable)
      │
 ComboCountersConfig (in RulesetSO.Counters) → GetBonusMultiplier
```

## Notes

- **Catalog & base:** [[BaseComboSO]] · [[ComboCatalogSO]] · [[ComboId]]
  · [[ComboDetectionResult]]
- **Concretes:** [[Combo_Par]] · [[Combo_DoblePar]] · [[Combo_SumaX]] ·
  [[Combo_Trio]] · [[Combo_Escalera]] · [[Combo_FullHouse]] ·
  [[Combo_Poker]] · [[Combo_Generala]]
- **Counters:** [[ComboCountersService]] · [[IComboCountersService]] ·
  [[ComboCountersConfig]] · [[RunComboCounterState]]

## Cross-domain edges

- [[ContractSheet]] and [[ClassHeroSO]] in [[Heroes-MOC|Heroes]].
- Feeds damage via the future `AttackResolver` → [[DamagePipeline]].
- Blocked by [[ComboBlockService]] (see [[Combat-MOC]]).
- Read by [[ContractDisplayView]] and [[ComboIndicatorView]] in
  [[UI-MOC|UI]].
