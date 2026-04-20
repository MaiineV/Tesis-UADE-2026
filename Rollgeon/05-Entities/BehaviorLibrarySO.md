---
title: BehaviorLibrarySO
type: catalog
domain: 05-Entities
status: done
tags: [entities, behavior, library, so]
---

# BehaviorLibrarySO

> Catalog of named [[BaseBehavior]] templates. [[EnemyDataSO]] can
> inline behaviors or reference a template from this library by name
> (designed as a cheap editor-reuse pattern).

## Responsibilities

- Hold a list of `(name, BaseBehavior)` template entries.
- Return a cloned instance by name so each spawn gets its own
  `StoredValues` bag.
- Validation: unique names, non-null behaviors.

## When to use the library vs. inline

- **Inline** when the behavior is one-of-a-kind (most boss attacks).
- **Library** when several enemies share the exact same behavior, so
  you can tune it in one place.

## Dependencies

- **Uses:** [[BaseBehavior]], Odin `SerializationUtility` for deep
  copies.
- **Used by:** [[EnemyDataSO]] (via `BehaviorSlot` references in
  TECHNICAL.md — currently inline only in Sprint 03).

## Code

- Runtime: `Assets/Scripts/Rollgeon/Entities/Behaviors/BehaviorLibrarySO.cs`
- Tests: `.../Tests/BehaviorLibrarySOTests.cs`

## External references

- TECHNICAL.md: §7.2 Behavior library
