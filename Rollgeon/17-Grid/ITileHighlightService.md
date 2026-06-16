---
title: ITileHighlightService
type: interface
domain: 17-Grid
status: done
tags: [grid, highlight, interface]
---

# ITileHighlightService

> Run-scoped service that paints tiles with named highlight styles
> (move, attack, selected) without authoring per-room materials.

## Overview
Decouples gameplay code (selection, AI previews, range visualisations)
from rendering. Callers register a `Renderer` per `GridCoord` once per
room load, then issue `Highlight` / `HighlightSingle` / `ClearAll`
during gameplay. The default impl uses `MaterialPropertyBlock` so it
costs no extra material instances.

## API / Shape

```csharp
public interface ITileHighlightService {
    void RegisterTile(GridCoord coord, Renderer renderer);
    void UnregisterAll();
    void Highlight(IEnumerable<GridCoord> tiles, string style);
    void HighlightSingle(GridCoord coord, string style);
    void ClearAll();
}
```

`style` keys are looked up against a `Dictionary<string, Color>`
configured in the impl; unknown keys fall back to yellow.

## Dependencies
**Uses:** [[GridCoord]]
**Used by:** [[TileHighlightService]], [[TileHighlightServiceBootstrap]],
[[TileRendererRegistrar]], `SelectionController`, AI / movement preview
HUD views.

## Code
`Assets/Scripts/Rollgeon/Grid/ITileHighlightService.cs`

## External references
- TECHNICAL.md: §17.§I Grid coordinates
