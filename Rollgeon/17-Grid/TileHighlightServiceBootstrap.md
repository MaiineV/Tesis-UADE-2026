---
title: TileHighlightServiceBootstrap
type: so
domain: 17-Grid
status: done
tags: [grid, highlight, bootstrap, so]
---

# TileHighlightServiceBootstrap

> ScriptableObject that registers [[TileHighlightService]] as
> [[ITileHighlightService]] in `ServiceLocator`.

## Overview
`IPreloadableService` with `Priority = 76` — right after
[[GridManagerBootstrap]] so the grid is available, before
[[TileRendererRegistrarBootstrap]] (81) which depends on this service.
Scope is `ServiceScope.Run`.

## API / Shape

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Grid/Tile Highlight Service Bootstrap")]
public sealed class TileHighlightServiceBootstrap : ScriptableObject, IPreloadableService {
    public int  Priority => 76;
    public void Register();
}
```

## Dependencies
**Uses:** [[TileHighlightService]], [[ITileHighlightService]],
`ServiceLocator`
**Used by:** Service preload pipeline ([[Bootstrap]]).

## Code
`Assets/Scripts/Rollgeon/Grid/TileHighlightServiceBootstrap.cs`

## External references
- TECHNICAL.md: §17.§I Grid coordinates
