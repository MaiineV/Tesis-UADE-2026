---
title: UnlockSystem
type: system
domain: 15-Meta
status: tbd
tags: [meta, unlock, progression, stub]
---

# UnlockSystem

> **[TBD]** — meta-progression layer specified in `TECHNICAL.md §14`:
> `BaseUnlockSO` (conditions, rewards) + `UnlockStateSO` (per-account
> state) + `RunRecord` (per-run results feeding unlock conditions).
> Not implemented in Sprint 03.

## Shape (spec)

- `BaseUnlockSO` — Odin-polymorphic SOs declaring conditions
  (e.g. "beat floor 1 with Warrior") and rewards
  (new class, new item, cosmetic).
- `UnlockStateSO` — per-save-file state: which unlocks are satisfied,
  which rewards are already claimed.
- `RunRecord` — compact record of a finished run (hero, outcome,
  floors cleared, combos matched) consumed by unlock evaluators.

## Dependencies

- **Would use:** [[BaseCatalogSO]], [[SaveSystem]], `OnRunEnd` events.
- **Would be used by:** [[MainMenuScreen]] (grey out locked classes),
  [[ClassSelectionScreen]] (filter by unlocked).

## External references

- TECHNICAL.md: §14 Meta-progression
