---
title: NavGraphBakeSettings
type: class
domain: 17-Grid
status: done
tags: [grid, nav, settings]
---

# NavGraphBakeSettings

> Authoring-time config for [[NavGraphBaker]] — how to walk a room
> hierarchy and decide which tiles connect.

## Overview
Plain serializable class (not an SO) so it can be embedded in editor
windows or other settings assets. Two knobs: tile size in world units,
and the maximum height delta between two adjacent tiles before the
baker refuses to connect them.

## API / Shape

```csharp
[Serializable]
public class NavGraphBakeSettings {
    [Min(0f)]    public float HeightThreshold = 0.5f;
    [Min(0.01f)] public float TileSize        = 1f;
}
```

## Dependencies
**Uses:** —
**Used by:** [[NavGraphBaker]]

## Code
`Assets/Scripts/Rollgeon/Grid/NavGraphBakeSettings.cs`

## External references
- TECHNICAL.md: §17.§I Grid coordinates
