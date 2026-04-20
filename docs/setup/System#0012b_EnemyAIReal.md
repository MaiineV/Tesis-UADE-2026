# System#0012b -- EnemyAIReal (BasicEnemyAI)

## Overview

Replaces `StubEnemyAIHandler` with a production `BasicEnemyAI` that reads
the enemy's Attack stat and deals damage to the player via the existing
`DamagePipeline`. Enemies with Attack <= 0 (support archetype) skip the
attack and immediately complete their turn.

## New files

| File | Purpose |
|------|---------|
| `Assets/Scripts/Rollgeon/Attributes/Stats/Attack.cs` | Attack stat (`BaseAttribute<int>`) |
| `Assets/Scripts/Rollgeon/Combat/AI/ICombatSignaller.cs` | Narrow interface to signal enemy turn done |
| `Assets/Scripts/Rollgeon/Combat/AI/BasicEnemyAI.cs` | Production `IEnemyAIHandler` implementation |
| `Assets/Scripts/Rollgeon/Combat/AI/Tests/Rollgeon.Combat.AI.Tests.asmdef` | Test assembly definition |
| `Assets/Scripts/Rollgeon/Combat/AI/Tests/BasicEnemyAITests.cs` | EditMode tests for BasicEnemyAI |

## Modified files

| File | Change |
|------|--------|
| `Assets/Scripts/Rollgeon/Entities/EnemyDataSO.cs` | `CreateRuntimeStats()` now initializes the `Attack` attribute alongside Health/Speed/Energy/HealStrength |
| `Assets/Scripts/Rollgeon/Combat/Handoff/CombatControllerAdapter.cs` | Implements `ICombatSignaller` with `SignalEnemyDone()` forwarding to `CombatController.SendEnemyDone()` |

## Prerequisites

Before using `BasicEnemyAI` at runtime, the following services must be
registered in `ServiceLocator`:

1. **`AttributesManager`** -- all combat entities (player + enemies) must
   have their attributes registered before combat starts (handled by
   `DefaultEnemySpawnResolver` and player bootstrap).
2. **`IPlayerService`** -- must be set via `SetPlayer()` before combat.
3. **`IDamagePipeline`** (or `DamagePipeline`) -- registered by the
   damage pipeline bootstrap.

## Registration

Replace the `StubEnemyAIHandler` with `BasicEnemyAI` in the combat
handoff wiring. The `BasicEnemyAI` constructor requires:

```csharp
var ai = new BasicEnemyAI(
    attributesManager,      // AttributesManager from ServiceLocator
    playerService,          // IPlayerService from ServiceLocator
    damagePipeline,         // IDamagePipeline from ServiceLocator
    () => signaller.SignalEnemyDone()  // ICombatSignaller callback
);
```

Then pass `ai.HandleEnemyTurn` as the `IEnemyAIHandler` to
`CombatHandoffService` (or wherever `StubEnemyAIHandler` was registered).

## Verification

1. **Tests pass**: open Unity > Window > General > Test Runner > EditMode.
   Run `Rollgeon.Combat.AI.Tests`. All 10 tests should pass.
2. **Attack stat on enemies**: existing `EnemyDataSO` assets now produce
   an `Attack` attribute in `CreateRuntimeStats()`. Enemies with
   `BaseAttack = 0` (support) will skip attack; enemies with
   `BaseAttack > 0` will deal damage.
3. **FSM integration**: `CombatControllerAdapter` now implements
   `ICombatSignaller`. The `SignalEnemyDone()` call forwards to
   `CombatController.SendEnemyDone()`, which sends `CombatInput.EnemyDone`
   to the FSM -- same as the stub path, but now after real damage.
