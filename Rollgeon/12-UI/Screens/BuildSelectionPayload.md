---
title: BuildSelectionPayload
type: payload
domain: 12-UI/Screens
status: done
tags: [ui, screen, payload, build]
---

# BuildSelectionPayload

> `IScreenPayload` passed from [[ClassSelectionScreen]] to
> [[BuildSelectionScreen]] carrying the selected hero, run id, and
> ruleset id. UI#0013a.

## Shape

```csharp
public sealed class BuildSelectionPayload : IScreenPayload {
    public ClassHeroSO SelectedHero;
    public Guid RunId;
    public string RulesetId;
}
```

## Overview

Plain DTO — no methods. The class-selection step picks a hero and
emits this payload; the build-selection screen reads it on
`OnPushed(payload)` to populate the dice/relic offering and to stamp
the eventual `RunStartedEvent`.

## Dependencies

- **Uses:** `ClassHeroSO`, [[IScreenPayload]].
- **Used by:** [[BuildSelectionScreen]] (consumer),
  [[ClassSelectionScreen]] (producer).

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/Screens/BuildSelectionPayload.cs`
