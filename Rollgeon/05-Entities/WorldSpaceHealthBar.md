---
title: WorldSpaceHealthBar
type: class
domain: 05-Entities
status: done
tags: [entities, visuals, monobehaviour, hp, hud]
---

# WorldSpaceHealthBar

> `MonoBehaviour` that renders the world-space HP bar above an
> [[EntityPawn]]. Subscribes to the typed combat events
> (`DamageResolvedPayload`, `HealResolvedPayload`,
> `EventName.OnEntityDestroyed`) and re-reads HP from
> `AttributesManager` on every refresh.

## Overview

Initialized by the spawning code with `(entityGuid, currentHp, maxHp)`.
On every relevant event for its bound `Guid` it re-queries the live
`Health` attribute via `ServiceLocator.TryGetService<AttributesManager>()`
and updates `_fillImage.fillAmount` plus the optional `TextMeshProUGUI`
(format `"{0}/{1}"`). When the entity is destroyed the bar root hides.

`LateUpdate` faces the bar to `Camera.main.forward` (billboard) and
re-applies the configured local `_offset` so it always sits above the
pawn's head, decoupled from the pawn's animation.

## API / Shape

```csharp
[AddComponentMenu("Rollgeon/Entities/World Space Health Bar")]
public sealed class WorldSpaceHealthBar : MonoBehaviour {
    public void Initialize(Guid entityGuid, int currentHp, int maxHp);
    public void Teardown(); // unsubscribes; called on disable
}
```

Serialized fields: `Image _fillImage` (filled, horizontal),
`TextMeshProUGUI _hpText` (optional), `string _textFormat = "{0}/{1}"`,
`GameObject _barRoot` (hidden on death), `Vector3 _offset` (local,
default `(0,2,0)`).

## Dependencies

- **Uses:** `AttributesManager` (`GetAttributeValue<Health, int>`),
  `TypedEvent<DamageResolvedPayload>`, `TypedEvent<HealResolvedPayload>`,
  `EventManager` (`OnEntityDestroyed`), `Camera.main`.
- **Used by:** [[EntityPawn]]`.HealthBar` (serialized reference on
  enemy / boss prefabs).

## Code

- Runtime: `Assets/Scripts/Rollgeon/Entities/Visuals/WorldSpaceHealthBar.cs`
- Tests: `.../Tests/WorldSpaceHealthBarTests.cs`

## External references

- TECHNICAL.md: §17.I Visual entity layer / §10 Combat feedback
