---
title: Heroes-MOC
type: moc
domain: 90-MOCs
status: done
tags: [moc, heroes]
---

# 06-Heroes — Map of Content

> Playable class data (Warrior in Sprint 03) and its Generala contract.

## Relationships

```
 ClassHeroSO
   ├─ Identity (EntityId, DisplayName, Description)
   ├─ ContractSheet
   │     ├─ Combos : List<BaseComboSO>  (8 entries, Generala last)
   │     ├─ MatchBest(dice) → skip crossed / blocked → highest priority
   │     └─ CrossCombo / IsCrossed (runtime-only, fires OnComboCrossed)
   └─ [STUB] BaseMaxHp / BaseSpeed / Portrait / StartingDiceBagRef /
             PassiveRef  — elevated by future Hero Template task

 ContractWarriorFactory ─ builds the canonical 8-entry sheet for tests
```

## Notes

- [[ClassHeroSO]] · [[ContractSheet]] · [[ContractWarriorFactory]]

## Cross-domain edges

- Referenced by [[IPlayerService]]`.CurrentHero` (see
  [[Player-MOC]]).
- Dictates which [[Combos-MOC|combos]] are available in combat.
- Drives the [[ClassSelectionScreen]] and [[BuildSelectionScreen]] in
  [[UI-MOC]].
