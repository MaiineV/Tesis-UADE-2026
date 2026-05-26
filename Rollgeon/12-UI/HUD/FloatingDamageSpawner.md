---
title: FloatingDamageSpawner
type: view
domain: 12-UI/HUD
status: done
tags: [ui, hud, feedback, damage]
---

# FloatingDamageSpawner

> Overlay component that pops floating damage / heal numbers at world
> positions whenever [[DamagePipeline]] or [[HealPipeline]] resolves a
> hit.

## Event binding

- [[TypedEvent]]`<DamageResolvedPayload>` → spawn a number.
- Future: `HealResolvedPayload` hook.
- Reads the position from the target entity's transform (mapped through
  `IEntityPositionResolver` in the future).

## Siblings

- `FloatingDamageInstance` — the instantiated text prefab that animates
  + auto-destroys.

## Dependencies

- **Uses:** [[TypedEvent]], `DamageResolvedPayload`,
  `FloatingDamageInstance` prefab, `IEntityPositionResolver` (stub).
- **Used by:** [[CombatHUDView]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/HUD/FloatingDamageSpawner.cs`,
  `.../FloatingDamageInstance.cs`
- Tests: `.../Tests/FloatingDamageSpawnerTests.cs`

## External references

- TECHNICAL.md: §10 Feedback — floating damage
