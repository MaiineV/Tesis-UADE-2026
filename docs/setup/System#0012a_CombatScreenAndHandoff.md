# System#0012a — CombatScreen and Handoff — Setup & Verification

## Overview

This task delivers the **combat handoff pipeline**: the bridge between
exploration and combat. When `ExplorationController` fires
`OnCombatTriggered`, the `CombatHandoffService` automatically:

1. Resolves enemy spawns from the room's `EnemyPoolSO`.
2. Registers enemies in the `IEntityRegistry`.
3. Pushes the `CombatHUD` screen via `IScreenManager`.
4. Starts the combat FSM via `ICombatStarter`.

## New Files

| Path | Purpose |
|------|---------|
| `Combat/Handoff/ICombatStarter.cs` | Testability seam wrapping `CombatController.StartCombat` |
| `Combat/Handoff/CombatControllerAdapter.cs` | Production adapter forwarding to real `CombatController` |
| `Combat/Handoff/IEnemySpawnResolver.cs` | Interface for enemy spawn resolution |
| `Combat/Handoff/DefaultEnemySpawnResolver.cs` | Rolls from `EnemyPoolSO`, creates stats, registers in registry |
| `Combat/Handoff/IEnemyAIHandler.cs` | Action slot for enemy turn AI |
| `Combat/Handoff/StubEnemyAIHandler.cs` | Placeholder (logs only) until S#0012b |
| `Combat/Handoff/ICombatHandoffService.cs` | Service interface |
| `Combat/Handoff/CombatHandoffService.cs` | Main orchestrator — listens to `OnCombatTriggered` |
| `Combat/Handoff/Tests/DefaultEnemySpawnResolverTests.cs` | 7 EditMode tests |
| `Combat/Handoff/Tests/CombatHandoffServiceTests.cs` | 10 EditMode tests |
| `Combat/Handoff/Tests/Rollgeon.Combat.Handoff.Tests.asmdef` | Test assembly definition |

All paths relative to `Assets/Scripts/Rollgeon/`.

## Prerequisites

- **ServiceBootstrapSO** must register at minimum:
  - `IDungeonService` (from DungeonManager)
  - `IPlayerService`
  - `IEntityRegistry` (InMemoryEntityRegistry or production)
  - `IScreenManager` (ScreenManager from UI)
  - `ICombatStarter` (CombatControllerAdapter wrapping scene CombatController)
  - `IEnemySpawnResolver` (DefaultEnemySpawnResolver)
  - `IEnemyAIHandler` (StubEnemyAIHandler for now)

## Verification Steps

### 1. Run EditMode Tests

In Unity: **Window > General > Test Runner > EditMode**

Run all tests under `Rollgeon.Combat.Handoff.Tests`. Expected: **17 green**.

| Test Class | Count |
|------------|-------|
| `DefaultEnemySpawnResolverTests` | 7 |
| `CombatHandoffServiceTests` | 10 |

### 2. Compilation Check

Open the Unity project and verify zero compilation errors in the Console.

### 3. Integration Smoke Test (manual)

1. Ensure `ServiceBootstrapSO` has all required services registered.
2. Start a run from MainMenu.
3. Navigate to a Combat room in exploration.
4. Verify:
   - Console logs `[StubEnemyAIHandler]` messages (enemy AI stub).
   - CombatHUD screen appears.
   - Combat FSM starts (turn queue visible).

## Dependencies

- **Upstream**: ExplorationController (fires `OnCombatTriggered`), DungeonManager,
  EnemyPoolSO, CombatController FSM, ScreenManager.
- **Downstream**: S#0012b (real enemy AI replaces `StubEnemyAIHandler`).
