---
title: TileHighlightService
type: service
domain: 17-Grid
status: done
tags: [grid, highlight, service]
---

# TileHighlightService

> Default [[ITileHighlightService]] — paints tile renderers via shared
> `MaterialPropertyBlock` and a style→color table.

## Overview
Holds three small structures: `_tileRenderers` (per-coord renderer
registry), `_styleColors` (style name → color), and `_active` (the set
of currently highlighted coords so `ClearAll` can reset only what was
painted). Default styles ship with the impl: `move`
(cyan), `attack` (red), `selected` (yellow). Custom palettes can be
injected via the constructor.

## API / Shape

Implements [[ITileHighlightService]]. Internally:

- Uses `Shader.PropertyToID("_BaseColor")` (URP lit shader convention).
- `Highlight` logs `matched/total` so missing tiles are easy to debug.
- `ClearAll` calls `SetPropertyBlock(null)` to revert to the renderer's
  shared material values.

## Dependencies
**Uses:** [[GridCoord]], [[ITileHighlightService]]
**Used by:** [[TileHighlightServiceBootstrap]], [[TileRendererRegistrar]]

## Code
`Assets/Scripts/Rollgeon/Grid/TileHighlightService.cs`

## External references
- TECHNICAL.md: §17.§I Grid coordinates
