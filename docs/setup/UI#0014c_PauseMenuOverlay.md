# UI#0014c — PauseMenuOverlay

## Summary

`PauseMenuOverlay` is a screen overlay that pauses the game and provides
resume, settings (stub), and quit-run options. It pushes `PhaseOverlay.Pause`
on the `IPhaseService` while active.

## Prerequisites

- `BaseScreen`, `IScreenManager`, `ScreenHost` from UI#0010a.
- `IPhaseService` / `PhaseService` from Phase#0008.
- `RunBootstrapper` from Run#0006.
- `RoomNavigationView` from UI#0011d (provides the pause button).

## GameObject Hierarchy

Create the overlay as a child of the **Canvas** in the run scene:

```
Canvas
  +-- PauseMenuOverlay (deactivated)
        +-- Panel (Image — semi-transparent background)
        +-- ResumeButton (Button + TextMeshProUGUI "Resume")
        +-- SettingsButton (Button + TextMeshProUGUI "Settings")
        +-- QuitRunButton (Button + TextMeshProUGUI "Quit Run")
```

> The root `PauseMenuOverlay` GameObject must start **deactivated** (`SetActive(false)`).
> `ScreenHost` and the `ScreenManager` control visibility.

## Inspector Wiring

| Field | Type | Target |
|-------|------|--------|
| `_screenStringIdOverride` | `string` | Leave empty (defaults to code constant `"PauseMenu"`) |
| `_resumeButton` | `Button` | `ResumeButton` |
| `_settingsButton` | `Button` | `SettingsButton` |
| `_quitRunButton` | `Button` | `QuitRunButton` |

Add the `PauseMenuOverlay` component to the root GameObject. All three
button references are marked `[Required]` via Odin — the Inspector will
show validation errors if any are missing.

## ScreenHost Registration

Ensure the `ScreenHost` on the Canvas has `PauseMenuOverlay` in its
screen list so it gets registered with `IScreenManager` on `Awake`.

## PhaseTransitionMatrixSO Configuration

The `PhaseTransitionMatrixSO` asset must allow `PhaseOverlay.Pause` during
the relevant `GamePhase` values (at minimum `Exploration`). If not
configured, `PushOverlay` will throw `InvalidPhaseTransitionException`.

## RoomNavigationView

The pause button in `RoomNavigationView` now calls
`IScreenManager.PushOverlay<PauseMenuOverlay>()` instead of logging a stub.
No additional wiring is needed — the existing `_pauseButton` reference
is already connected.

## Smoke Test

1. Enter a run and reach the Exploration HUD.
2. Click the pause button in the room navigation bar.
3. Verify the pause overlay appears with three buttons.
4. Click **Resume** — overlay closes, exploration continues.
5. Re-open pause, click **Quit Run** — game returns to `01_MainMenu` scene.
6. Click **Settings** — check console for stub log message.

## Test Runner

Open **Window > General > Test Runner** in Unity, select **EditMode**, and
run the `PauseMenuOverlayTests` fixture:

| Test | Validates |
|------|-----------|
| `ScreenStringId_Returns_PauseMenu` | String id constant |
| `OnPushed_CallsPhaseServicePushOverlay` | Phase overlay pushed |
| `OnPopped_CallsPhaseServicePopOverlay` | Phase overlay popped on close |
| `OnPushed_WithoutPhaseService_DoesNotThrow` | Graceful degradation |
| `OnPopped_RemovesButtonListeners` | Listener cleanup |
| `ResumeButton_DoesNotThrow` | Resume click safety |
| `SettingsButton_DoesNotThrow` | Stub click safety |
