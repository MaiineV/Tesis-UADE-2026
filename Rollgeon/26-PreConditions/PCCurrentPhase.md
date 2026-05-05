---
title: PCCurrentPhase
type: class
domain: 26-PreConditions
status: done
tags: [preconditions, gating, phase]
---

# PCCurrentPhase

> Passes when the global `IPhaseService.CurrentBase` matches
> `ExpectedPhase`, optionally also requiring the active overlay to match
> `ExpectedOverlay`.

## Overview

Lets effects only fire in a specific [[GamePhase]] (Exploration, Combat,
Loading, GameOver) and optionally only inside a specific
[[PhaseOverlay]] (Pause, Cutscene, Craps). Returns `false` if no
`IPhaseService` is registered. A turn-substate variant is deferred until
the Combat FSM exposes substates.

## Configuration

- `ExpectedPhase` ([[GamePhase]]) — base phase to match. Default
  `Combat`.
- `MatchOverlay` (`bool`) — when true, also enforces overlay equality.
- `ExpectedOverlay` ([[PhaseOverlay]]) — only shown/applied when
  `MatchOverlay` is true.

## Dependencies

**Uses:** [[BasePreCondition]], [[PreConditionContext]],
`IPhaseService`, [[GamePhase]], [[PhaseOverlay]]
**Used by:** [[EffectData]] groups that should only fire in a given
phase/overlay.

## Code

`Assets/Scripts/Rollgeon/PreConditions/Concretes/PCCurrentPhase.cs`
