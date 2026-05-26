---
title: FloatingNumberType
type: enum
domain: 12-UI
status: done
tags: [ui, hud, feedback, enum]
---

# FloatingNumberType

> Category enum for floating numbers. [[FloatingDamageSpawner]] reads
> it to choose tint and text format. Carried as `args[1]` of
> `EventName.OnFloatingNumberRequested`.

## Shape

```csharp
public enum FloatingNumberType {
    Damage = 0,
    Heal   = 1,
    Shield = 2,
    Gold   = 3,
    Status = 4,
}
```

## Dependencies
**Used by:** [[FloatingDamageSpawner]], [[FloatingDamageInstance]],
event publishers (damage / heal / shield / gold).

## Code
`Assets/Scripts/Rollgeon/UI/HUD/FloatingNumberType.cs`
