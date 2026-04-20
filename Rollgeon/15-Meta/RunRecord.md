---
title: RunRecord
type: concept
domain: 15-Meta
status: tbd
tags: [meta, analytics, run, stub]
---

# RunRecord

> **[TBD]** — per-run summary artefact feeding [[UnlockSystem]] and
> future analytics (`TECHNICAL.md §14 / §25`).

## Shape (spec)

- `RunId : Guid`
- `HeroId : string`
- `Outcome : CombatOutcome` (won / lost / aborted)
- `FloorsCleared : int`
- `TimeElapsed : TimeSpan`
- `CombosMatched : Dictionary<string, int>`
- `Seed : int`
- `EndedAt : DateTime`

## Why it's worth scoping early

- Feeds unlock conditions — the simpler the record, the easier the
  unlock predicates.
- Enables analytics dashboards without piping detail per-event.
- Snapshot-friendly — fits into a single save slot's JSON under a
  history array.

## Dependencies

- **Would use:** [[CombatOutcome]], [[IRunContextService]].
- **Would be used by:** [[UnlockSystem]], future analytics service.

## External references

- TECHNICAL.md: §14 / §25 RunRecord
