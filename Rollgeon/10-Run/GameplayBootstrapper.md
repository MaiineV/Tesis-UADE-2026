---
title: GameplayBootstrapper
type: class
domain: 10-Run
status: done
tags: [run, bootstrap, scene, monobehaviour]
---

# GameplayBootstrapper

> Scene-scoped `MonoBehaviour` in `02_Gameplay.unity` that consumes a
> [[PendingRunRequest]], pushes the exploration HUD, fires
> [[RunBootstrapper]]`.StartRun`, applies built dice bag + starting
> items, and spawns the hero pawn in the first room.

## Overview

Bridges the class/build selection flow (in `01_MainMenu`) to the
event-driven run pipeline. Execution order `-500`, after `ScreenHost`
(`-1000`) and before default gameplay components.

## Behaviour

1. Validate `PendingRunRequest.HasRequest`.
2. Push `ExplorationHUD` so any later overlay (combat HUD, floor
   transition) lands on top.
3. Call [[RunBootstrapper]]`.StartRun(hero, ruleset, runId)`.
4. Apply `BuiltDiceBag` (Phase 2 build) via [[IPlayerService]]`.SetDiceBag`.
5. Apply `StartingItems` via `IInventoryService.AddItem`.
6. Spawn the hero pawn at the room's `PlayerSpawnPoint` via
   `IGridManager.Register` + `IEntityVisualService.SpawnHero`.
7. Set the camera follow target if available.
8. `PendingRunRequest.Clear()`.

## Dependencies

- **Uses:** [[PendingRunRequest]], [[RunBootstrapper]],
  [[IPlayerService]], `IScreenManager`, `IGridManager`,
  `IDungeonService`, `IEntityVisualService`, `ICameraService`,
  `IInventoryService`, [[ServiceLocator]], `RulesetSO`.
- **Used by:** scene wiring (no direct C# callers).

## Code

`Assets/Scripts/Rollgeon/Run/GameplayBootstrapper.cs`

## External references

- Setup: `docs/setup/Bootstrap#0011_GameplayBootstrapper.md`
- TECHNICAL.md: §1.1.3 Run lifecycle — Gameplay scene boot
