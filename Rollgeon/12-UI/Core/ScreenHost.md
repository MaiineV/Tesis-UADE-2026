---
title: ScreenHost
type: system
domain: 12-UI/Core
status: done
tags: [ui, screen, host, monobehaviour]
---

# ScreenHost

> Scene-root `MonoBehaviour` that owns the screen stack's GameObject
> parent. Instantiated once in `01_MainMenu`; persists across
> reloads via `DontDestroyOnLoad` when configured.

## Role

- Provides the `Canvas` + `RectTransform` under which
  [[ScreenManager]] spawns screen prefabs.
- Routes `Escape` / back-button presses to
  `ScreenManager.Current?.OnBackPressed` (when implemented).
- Exposes editor slots for a default screen id to prefab mapping — the
  [[ScreenManager]] copies this table on startup.

## Dependencies

- **Uses:** [[ScreenManager]], Unity `Canvas`, `EventSystem`.
- **Used by:** `01_MainMenu.unity`, [[BaseScreen]] siblings.

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/ScreenHost.cs`

## External references

- TECHNICAL.md: §17.UI Screen host
