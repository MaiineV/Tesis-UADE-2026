---
title: FloatingDamageInstance
type: behavior
domain: 12-UI
status: done
tags: [ui, hud, feedback, mono-behaviour]
---

# FloatingDamageInstance

> Prefab-backed `MonoBehaviour` for a single floating number. Exposes
> `Play(text, tint, screenPos)` — fade-in, rise, fade-out,
> auto-destroy.

## Overview

Default implementation uses `IEnumerator` + `Lerp` so PrimeTween /
DOTween are not hard dependencies. If the project adopts a tweening
lib later, the lerps migrate without breaking the public API.
Configurable rise distance, total duration, and fade-out ratio.

## API / Shape

```csharp
public class FloatingDamageInstance : MonoBehaviour {
    public void Play(string text, Color tint, Vector3 screenPos);

    // Inspector
    private TextMeshProUGUI _text;
    private CanvasGroup _canvasGroup;
    private float _riseHeight = 50f;
    private float _durationSeconds = 1.2f;
    private float _fadeOutRatio = 0.6f;
}
```

## Dependencies
**Uses:** TextMeshPro, Unity UI.
**Used by:** [[FloatingDamageSpawner]] instantiates one per event.

## Code
`Assets/Scripts/Rollgeon/UI/HUD/FloatingDamageInstance.cs`
