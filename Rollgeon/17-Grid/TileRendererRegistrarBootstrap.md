---
title: TileRendererRegistrarBootstrap
type: so
domain: 17-Grid
status: done
tags: [grid, bootstrap, so]
---

# TileRendererRegistrarBootstrap

> ScriptableObject that owns the singleton [[TileRendererRegistrar]] and
> keeps it alive for the run.

## Overview
`IPreloadableService` with `Priority = 81` — runs after the grid (75),
highlight service (76), movement (78), and AI (80) so all dependencies
are resolvable when the registrar's `OnRoomEntered` handler fires.
Idempotent: only constructs the registrar on the first `Register` call.

## API / Shape

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Grid/Tile Renderer Registrar Bootstrap")]
public sealed class TileRendererRegistrarBootstrap : ScriptableObject, IPreloadableService {
    public int  Priority => 81;
    public void Register();
}
```

## Dependencies
**Uses:** [[TileRendererRegistrar]]
**Used by:** Service preload pipeline ([[Bootstrap]]).

## Code
`Assets/Scripts/Rollgeon/Grid/TileRendererRegistrarBootstrap.cs`

## External references
- TECHNICAL.md: §17.§I Grid coordinates
