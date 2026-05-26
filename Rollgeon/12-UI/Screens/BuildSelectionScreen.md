---
title: BuildSelectionScreen
type: screen
domain: 12-UI/Screens
status: done
tags: [ui, screen, build, selection]
---

# BuildSelectionScreen

> Post-class build picker. Today minimal (confirm and start). Scaffolded
> so the future perk / starting-item / seed override UI drops in without
> reworking the flow.

## Flow

1. Receive `BuildSelectionPayload(hero)`.
2. Show class summary + (future) build options.
3. On confirm → [[RunBootstrapper]]`.StartRun(hero, ruleset, runId)`.
4. The run's event chain pushes [[ExplorationHUDView]] via
   [[ExplorationController]]`.BeginExploration`.

## Dependencies

- **Uses:** [[ScreenManager]], [[BaseScreen]], [[ClassHeroSO]],
  [[RunBootstrapper]], [[RulesetSO]].
- **Used by:** [[ClassSelectionScreen]] (caller).

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/Screens/BuildSelectionScreen.cs`
- Payload: `.../BuildSelectionPayload.cs`
- Tests: `.../Tests/BuildSelectionScreenTests.cs`

## External references

- Setup: `docs/setup/UI#0013a_BuildSelectionScreen.md`
- TECHNICAL.md: §D Build selection
