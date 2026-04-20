# Foundation#0007 — PhaseServiceReal Setup

## 1. Create PhaseTransitionMatrixSO asset

- **Right-click** in Project window → `Create > Rollgeon > Phase > Transition Matrix`.
- Name it `PhaseTransitionMatrix`.
- In the inspector, configure the `_allowedTransitions` 5×5 grid:
  - Row = from-phase, Column = to-phase (None=0, Exploration=1, Combat=2, Loading=3, GameOver=4).
  - Tick the pairs that are valid (e.g. None→Exploration, Exploration→Combat, Combat→Exploration, etc.).
- Configure `_allowedOverlays` dictionary:
  - Add entries for each base phase that supports overlays.
  - Example: `Exploration → [Pause]`, `Combat → [Pause, Craps]`.

## 2. Register the matrix in ServiceBootstrapSO

- Open the existing `ServiceBootstrapSO` asset.
- Drag the `PhaseTransitionMatrix` asset into the `SettingsAssets` list so it gets registered via `RegisterByRuntimeType`.

## 3. Create PhaseServiceBootstrap asset

- **Right-click** → `Create > Rollgeon > Bootstrap > Phase Service`.
- Name it `PhaseServiceBootstrap`.
- Drag it into the `ExtraServices` list of `ServiceBootstrapSO`.
- Priority is 10 (runs early, before combat services).

## 4. Verify in Play Mode

- Enter Play Mode with the Bootstrap scene loaded.
- Open the Console — no errors about missing `IPhaseService`.
- Optionally add a test script that calls:
  ```csharp
  var ps = ServiceLocator.GetService<IPhaseService>();
  ps.ReplacePhase(GamePhase.Exploration);
  Debug.Log($"Phase: {ps.CurrentBase}"); // → Exploration
  ```
