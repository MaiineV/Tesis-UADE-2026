---
title: ICombatReturnService
type: interface
domain: 02-Combat
status: done
tags: [combat, handoff, interface]
---

# ICombatReturnService

> Marker interface for the orchestrator that transitions combat →
> exploration after combat ends.

## Overview

Symmetric to [[ICombatHandoffService]]. Implementation subscribes to
combat-end events, closes the combat HUD, and either resumes
exploration or shows a victory / defeat screen.

## API / Shape

```csharp
public interface ICombatReturnService : IDisposable { }
```

## Dependencies
**Uses:** [[IScreenManager]].
**Implemented by:** [[CombatReturnService]].

## Code
`Assets/Scripts/Rollgeon/Combat/Handoff/ICombatReturnService.cs`
