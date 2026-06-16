---
title: SaveSystem
type: system
domain: 15-Meta
status: tbd
tags: [meta, save, persistence, stub]
---

# SaveSystem

> **[TBD]** — `TECHNICAL.md §15` specifies an `ISaveable` auto-registry,
> an in-memory cache, a JSON flush path, and save triggers
> (`RunStart`, `RoomEnd`, `Manual`, `Shutdown`). Sprint 03 ships only
> the [[ISaveable]] contract; the manager itself is not implemented.

## What exists today

- [[ISaveable]] interface (stub).
- [[RunComboCounterState]] implements `ISaveable` so it is ready for
  when the manager lands.

## What is missing

- An `ISaveManager` service that discovers `ISaveable`s and serializes
  their `CaptureState()` result.
- JSON schema / file layout under `%appdata%` (or Unity persistent data
  path).
- Conflict resolution between run-scoped and global saves.
- Save triggers wired to `OnRoomChanged`, `OnCombatEnd`, a manual
  `Save Run` button.

## Impact on the rest of the vault

Anything tagged `#tbd` with a save reference depends on this landing
before it can move to `done`. Keep those dependencies explicit —
flipping the tag later is cheap if the links are already in place.

## External references

- TECHNICAL.md: §15 Save / persistence
