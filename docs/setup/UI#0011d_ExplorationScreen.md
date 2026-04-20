# UI#0011d — ExplorationScreen (Room Navigation)

## Summary

`RoomNavigationView` is a new HUD sub-view that displays the current room name,
progress (Room X/Y), room type, and provides Proceed/Pause buttons during
exploration. It integrates into `ExplorationHUDView` as the 6th sub-view.

## Prerequisites

| Dependency | Description |
|------------|-------------|
| S#0011c | ExplorationController (provides `IExplorationController`) |
| T95a | IDungeonService / DungeonManager |
| UI#0011b | ExplorationHUDView (the parent screen) |

## New Files

| File | Description |
|------|-------------|
| `Assets/Scripts/Rollgeon/UI/HUD/RoomNavigationView.cs` | Sub-view MonoBehaviour |
| `Assets/Scripts/Rollgeon/UI/Tests/RoomNavigationViewTests.cs` | 12 EditMode tests |

## Modified Files

| File | Change |
|------|--------|
| `Assets/Scripts/Rollgeon/UI/Screens/ExplorationHUDView.cs` | Added `_roomNavigation` field, wired in BindAll/UnbindAll |
| `Assets/Scripts/Rollgeon/UI/Tests/ExplorationHUDViewTests.cs` | Added 2 tests for RoomNavigation bind/unbind |

## Scene Wiring

1. **Open** the scene containing the ExplorationHUD prefab/hierarchy.
2. **Create** a child GameObject under the ExplorationHUD root named `RoomNavigation`.
3. **Add Component**: `Rollgeon > UI > HUD > Room Navigation View`.
4. **Create child UI elements** under `RoomNavigation`:
   - `RoomNameLabel` — TextMeshPro - Text (UI). Wire into `_roomNameLabel`.
   - `RoomProgressLabel` — TextMeshPro - Text (UI). Wire into `_roomProgressLabel`.
   - `RoomTypeLabel` — TextMeshPro - Text (UI). Wire into `_roomTypeLabel`.
   - `ProceedButton` — Button (UI) with a child TMP text. Wire into `_proceedButton`.
   - `PauseButton` — Button (UI) with a child TMP text. Wire into `_pauseButton`.
5. **Select** the ExplorationHUDView root GameObject.
6. **Drag** the `RoomNavigation` GameObject into the new `_roomNavigation` slot.
7. **Save** the scene.

## Verification

1. Open Unity and let scripts compile (no errors expected).
2. Run EditMode tests: **Window > General > Test Runner > EditMode**.
3. Filter by `RoomNavigationView` — all 12 tests should pass.
4. Filter by `ExplorationHUDView` — all 7 tests should pass (5 existing + 2 new).

### Test List — RoomNavigationViewTests

| # | Test | Validates |
|---|------|-----------|
| 1 | `Bind_ResolvesServicesAndRefreshesLabels` | Services resolved, labels populated |
| 2 | `Bind_WithNoDungeonService_DegradesGracefully` | Fallback text, no crash |
| 3 | `Bind_WithNoExplorationController_DegradesGracefully` | Proceed disabled, no crash |
| 4 | `RefreshRoomInfo_UpdatesLabelsFromDungeon` | Labels update on manual refresh |
| 5 | `RefreshRoomInfo_ProgressFormat` | "Room 1/8" format verified |
| 6 | `ProceedButton_CallsAdvanceRoom` | AdvanceRoom invoked on click |
| 7 | `ProceedButton_DisabledWhenNotExploring` | Interactable=false when not exploring |
| 8 | `OnRoomEntered_RefreshesUI` | Event triggers label update |
| 9 | `OnCombatTriggered_DisablesProceed` | Button disabled on combat |
| 10 | `Unbind_UnsubscribesEvents` | Events ignored after unbind |
| 11 | `Unbind_Idempotent` | Double unbind is safe |
| 12 | `PauseButton_DoesNotCrash` | Stub pause click is safe |

### Test List — ExplorationHUDViewTests (new)

| # | Test | Validates |
|---|------|-----------|
| 1 | `BindAll_BindsRoomNavigation` | RoomNavigationView._bound=true after BindAll |
| 2 | `UnbindAll_UnbindsRoomNavigation` | RoomNavigationView._bound=false after UnbindAll |

## Downstream

- **UI#0014c** — Pause overlay screen (currently stub in `OnPauseClicked`).
- **S#0012a** — Combat transition flow (Proceed button disables on `OnCombatTriggered`).
