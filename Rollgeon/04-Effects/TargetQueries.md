---
title: TargetQueries
type: concept
domain: 04-Effects
status: done
tags: [effects, selection, target]
---

# Target Queries

> Index of the concrete [[BaseTargetQuery]] implementations shipped in
> Sprint 03.

## Shipped queries

- **`TQ_AllEnemies`** — resolves to every enemy currently in the combat
  roster. Used by AoE damage effects and the Auditor's buff targets.
- **`TQ_Self`** — resolves to the owner entity. Used by self-buff /
  self-heal effects.

## Extensibility

New queries subclass `BaseTargetQuery`, override `Query(ReadInfo)`, and
get referenced from [[SelectionSettings]] via Odin-serialized polymorphic
lists. Add the new query to the bootstrap's editor registry if you want
it to show up in inspector dropdowns.

## Dependencies

- **Uses:** [[BaseTargetQuery]], [[ReadInfo]], [[TargetRef]],
  [[EntityFilterMask]].
- **Used by:** [[SelectionSettings]], auto-selection in effects.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Effects/Selection/Queries/TQ_AllEnemies.cs`,
  `.../TQ_Self.cs`

## External references

- TECHNICAL.md: §11.2 Target queries
