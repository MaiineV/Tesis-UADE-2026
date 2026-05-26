---
title: ClassSelectionScreen
type: screen
domain: 12-UI/Screens
status: done
tags: [ui, screen, class, selection]
---

# ClassSelectionScreen

> Screen where the player picks a [[ClassHeroSO]] before starting a
> run. Sprint 03 ships with the Warrior only; the layout is built to
> scale to multiple classes.

## Flow

1. Load every `ClassHeroSO` asset from Addressables.
2. Render a card per class (portrait, name, short description, preview
   of [[ContractSheet]]).
3. On confirm → push [[BuildSelectionScreen]] with a
   `ClassSelectionPayload(hero)`.

## Dependencies

- **Uses:** [[ScreenManager]], [[BaseScreen]], [[ClassHeroSO]],
  [[ContractSheet]], `ClassSelectionPayload`.
- **Used by:** [[MainMenuScreen]] (caller).

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/Screens/ClassSelectionScreen.cs`
- Payload: `.../ClassSelectionPayload.cs`
- Tests: `.../Tests/ClassSelectionScreenTests.cs`

## External references

- Setup: `docs/setup/UI#0098_ClassSelectionScreen.md`
- TECHNICAL.md: §D Class selection
