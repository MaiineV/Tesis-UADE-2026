---
title: CombatExitState
type: state
domain: 02-Combat/FSM
status: done
tags: [combat, fsm, state, terminal]
---

# CombatExitState

> Terminal state of [[CombatTurnFSM]]. Emits `OnFinished(outcome)` so
> external consumers can route to victory / defeat / explore.

## Behaviour

- `Enter(input)`:
  1. Reads `Context.PendingOutcome` (falls back to `CombatOutcome.Aborted`).
  2. Fires [[EventName]] `OnCombatEnd(playerId, roomInstanceId, outcome)`.
  3. [[CombatTurnFSM]]'s `OnStateEntered` handler invokes
     `OnFinished(outcome)` for external subscribers.
- Rejects every input — the FSM is expected to be stopped after this
  state.

## Dependencies

- **Uses:** [[CombatContext]], [[CombatOutcome]], [[EventManager]],
  [[EventName]].
- **Used by:** [[CombatTurnFSM]], [[CombatReturnService]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combat/FSM/States/CombatExitState.cs`

## External references

- Setup: `docs/setup/System#0012d_CombatEndToExploration.md`
- TECHNICAL.md: §12.9 Combat exit
