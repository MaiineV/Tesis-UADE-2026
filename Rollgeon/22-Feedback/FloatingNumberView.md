---
title: FloatingNumberView
type: class
domain: 22-Feedback
status: done
tags: [feedback, floating-number, monobehaviour, ui]
---

# FloatingNumberView

> View runtime del prefab `Resources/FloatingNumber`. El
> [[FeedbackManager]] lo instancia para mostrar damage / heal / shield
> / generic numbers; bob + fade y self-destroy.

## Overview

Soporta tres backends de texto sin hard-dependency en TextMeshPro:
`TextMesh` legacy, `UnityEngine.UI.Text`, o (si el prefab lo agrega)
TMP. La `NumberType` mapea a color (rojo damage, verde heal, celeste
shield, blanco generic). Llamar `Initialize(text, type, position)` al
spawnear.

## API / Shape

```csharp
public sealed class FloatingNumberView : MonoBehaviour {
    public enum NumberType { Damage, Heal, Shield, Generic }
    public void Initialize(string text, NumberType type, Vector3 position);
}
```

## Dependencies

**Uses:** `UnityEngine.UI.Text`, `TextMesh` (opcionales).
**Used by:** [[FeedbackManager]] (`DispatchFloatingNumber` /
`SpawnFloatingNumberDelayed` instancia el prefab cacheado de
`Resources/FloatingNumber`), [[FloatingDamageSpawner]] (12-UI — caller
alternativo), [[FloatingNumberType]] (12-UI — equivalente UI).

## Code

`Assets/Scripts/Rollgeon/Feedback/FloatingNumberView.cs`

## External references

- TECHNICAL.md §10.7 — Floating numbers.
