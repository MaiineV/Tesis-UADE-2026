# UI#0013c — Victory / Defeat Screens

## Summary

`VictoryScreen` and `DefeatScreen` are end-of-run screens that auto-push
themselves when the corresponding event fires (`OnFloorCleared` /
`OnPlayerDefeated`). Both provide a return-to-menu button that calls
`RunBootstrapper.EndRun` and loads `01_MainMenu`.

## Prerequisites

- `BaseScreen`, `IScreenManager`, `ScreenHost` from UI#0010a.
- `RunBootstrapper` / `IRunContextService` from Foundation#0010.
- `EventManager` from Foundation#0001.
- Feature#0012d (CombatEnd to Exploration) for `OnFloorCleared` trigger.

## GameObject Hierarchy

Create both screens as children of the **Canvas** in the run scene:

```
Canvas
  +-- VictoryScreen (ACTIVE at start)
  |     +-- Panel (Image -- background)
  |     +-- TitleLabel (TextMeshProUGUI -- "Victory!")
  |     +-- ReturnToMenuButton (Button + TextMeshProUGUI "Return to Menu")
  |
  +-- DefeatScreen (ACTIVE at start)
        +-- Panel (Image -- background)
        +-- TitleLabel (TextMeshProUGUI -- "Defeat")
        +-- ReturnToMenuButton (Button + TextMeshProUGUI "Return to Menu")
```

> **IMPORTANT:** Both root GameObjects must start **ACTIVE** so that
> `Awake()` fires and subscribes to the event bus. `ScreenHost` will
> deactivate them immediately after registration. If they start
> deactivated, `Awake` never runs and the screens will not respond to
> events.

## Inspector Wiring

### VictoryScreen

| Field | Type | Target |
|-------|------|--------|
| `_screenStringIdOverride` | `string` | Leave empty (defaults to `"VictoryScreen"`) |
| `_returnToMenuButton` | `Button` | `ReturnToMenuButton` |
| `_titleLabel` | `TextMeshProUGUI` | `TitleLabel` |

### DefeatScreen

| Field | Type | Target |
|-------|------|--------|
| `_screenStringIdOverride` | `string` | Leave empty (defaults to `"DefeatScreen"`) |
| `_returnToMenuButton` | `Button` | `ReturnToMenuButton` |
| `_titleLabel` | `TextMeshProUGUI` | `TitleLabel` |

Both button and label references are marked `[Required]` via Odin --
the Inspector will show validation errors if any are missing.

## ScreenHost Registration

Ensure the `ScreenHost` on the Canvas includes both `VictoryScreen` and
`DefeatScreen` in its screen list so they get registered with
`IScreenManager` on `Awake`.

## Verification

### Victory flow

1. Enter a run with a single-floor layout (or clear all rooms on the last floor).
2. Clear the final room to trigger `OnFloorCleared`.
3. Verify the Victory screen appears with title "Victory!" and a return button.
4. Click **Return to Menu** -- game returns to `01_MainMenu` scene.

### Defeat flow

1. Enter a run and engage in combat.
2. Let the player die (HP reaches 0) to trigger `OnPlayerDefeated`.
3. Verify the Defeat screen appears with title "Defeat" and a return button.
4. Click **Return to Menu** -- game returns to `01_MainMenu` scene.

## Test Runner

Open **Window > General > Test Runner** in Unity, select **EditMode**, and
run the `VictoryScreenTests` and `DefeatScreenTests` fixtures:

### VictoryScreenTests

| Test | Validates |
|------|-----------|
| `ScreenStringId_ReturnsVictoryScreen` | String id constant |
| `OnFloorCleared_PushesScreenViaScreenManager` | Event triggers push |
| `OnPushed_WiresReturnButton` | Button click safety |
| `OnPopped_RemovesButtonListener` | Listener cleanup |
| `OnPushed_SetsTitleText` | Title set to "Victory!" |
| `OnDestroy_UnsubscribesFromEvent` | Cleanup on destroy |

### DefeatScreenTests

| Test | Validates |
|------|-----------|
| `ScreenStringId_ReturnsDefeatScreen` | String id constant |
| `OnPlayerDefeated_PushesScreenViaScreenManager` | Event triggers push |
| `OnPushed_WiresReturnButton` | Button click safety |
| `OnPopped_RemovesButtonListener` | Listener cleanup |
| `OnPushed_SetsTitleText` | Title set to "Defeat" |
| `OnDestroy_UnsubscribesFromEvent` | Cleanup on destroy |
